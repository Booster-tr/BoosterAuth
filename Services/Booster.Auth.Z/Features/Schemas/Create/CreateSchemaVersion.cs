using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Z.Constants;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Authorization.Fga.Attributes;
using Booster.Authorization.Relational.Options;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using Mapster;

namespace Booster.Auth.Z.Features.Schemas.Create;

public class CreateSchemaVersionEndpoint : Endpoint<CreateSchemaVersionCommand, CreateSchemaVersionResponse>
{
    public override void Configure()
    {
        Post("authz/schema/versions");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthZPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, AuthZPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Yeni taslak sürüm oluşturur";
            s.Description = "Kaynak sürüm verilirse tanımı kopyalanır; verilmezse boş başlar. Sürüm sayısı sınırlıdır.";
        });
    }

    public override async Task HandleAsync(CreateSchemaVersionCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record CreateSchemaVersionCommand(Guid UserPoolId, string Name, Guid? SourceVersionId)
    : ICommand<CreateSchemaVersionResponse>;

public readonly record struct CreateSchemaVersionResponse(Guid Id);

public class CreateSchemaVersionCommandHandler(
    IAsyncRepository<PoolSchema, AuthZDbContext> schemaRepository,
    FgaOptions options)
    : ICommandHandler<CreateSchemaVersionCommand, CreateSchemaVersionResponse>
{
    public async Task<CreateSchemaVersionResponse> ExecuteAsync(CreateSchemaVersionCommand command, CancellationToken ct)
    {
        var count = await schemaRepository.CountAsync(s => s.PoolId == command.UserPoolId, cancellationToken: ct);

        if (count >= options.MaxSchemaVersions)
            throw new BusinessException(AuthZMessageKeys.SchemaVersionLimitReached);

        var name = command.Name.Trim();

        if (await schemaRepository.AnyAsync(s => s.PoolId == command.UserPoolId && s.Name == name, cancellationToken: ct))
            throw new BusinessException(AuthZMessageKeys.SchemaVersionNameTaken);

        // Store havuz başına tektir; kaynak verilmezse herhangi bir sürümden alınır.
        var source = await schemaRepository.GetAsync(
            s => s.PoolId == command.UserPoolId && (command.SourceVersionId == null || s.Id == command.SourceVersionId),
            enableTracking: false, cancellationToken: ct);
        source.ThrowCustomExceptionIfDataNull(AuthZMessageKeys.SchemaVersionNotFound);

        var copy = command.SourceVersionId is not null;

        var schema = new PoolSchema
        {
            PoolId = command.UserPoolId,
            StoreId = source!.StoreId,
            Name = name,
            Status = SchemaVersionStatus.Draft,
            Definition = copy ? source.Definition.Adapt<SchemaDefinition>() : new SchemaDefinition(),
            // Kopyanın modeli kaynağınkiyle aynıdır; düzenlenmedikçe yayına alınırken yeniden yazılmaz.
            ModelId = copy ? source.ModelId : null,
            Revision = 1
        };
        schema.SetId();

        await schemaRepository.AddAsync(schema, ct);
        await schemaRepository.SaveChangesAsync(ct);

        return new CreateSchemaVersionResponse(schema.Id);
    }
}

public class CreateSchemaVersionValidator : Validator<CreateSchemaVersionCommand>
{
    public CreateSchemaVersionValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(100).WithMessage(ValidationErrors.MaxLengthError);
    }
}
