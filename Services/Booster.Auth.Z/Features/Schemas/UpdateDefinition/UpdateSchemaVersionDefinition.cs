using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Z.Constants;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Auth.Z.Features.Schemas._base;
using Booster.Authorization.Fga.Attributes;
using Booster.Authorization.Relational.Options;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.Schemas.UpdateDefinition;

public class UpdateSchemaVersionDefinitionEndpoint
    : Endpoint<UpdateSchemaVersionDefinitionCommand, UpdateSchemaVersionDefinitionResponse>
{
    public override void Configure()
    {
        Put("authz/schema/versions/definition");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthZPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, AuthZPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Sürümün tanımını kaydeder";
            s.Description = "Yayındaki sürüm düzenlenemez. Tanım bütün olarak değişir ve yarım hâliyle kaydedilebilir; " +
                            "tip ve yetki sınırları aşılamaz. Revision eşleşmezse başka biri araya girmiştir.";
        });
    }

    public override async Task HandleAsync(UpdateSchemaVersionDefinitionCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateSchemaVersionDefinitionCommand(Guid UserPoolId, Guid VersionId, int Revision, SchemaDefinition Definition)
    : ICommand<UpdateSchemaVersionDefinitionResponse>;

public readonly record struct UpdateSchemaVersionDefinitionResponse(Guid Id, int Revision);

public class UpdateSchemaVersionDefinitionCommandHandler(
    IAsyncRepository<PoolSchema, AuthZDbContext> schemaRepository,
    FgaOptions options)
    : ICommandHandler<UpdateSchemaVersionDefinitionCommand, UpdateSchemaVersionDefinitionResponse>
{
    public async Task<UpdateSchemaVersionDefinitionResponse> ExecuteAsync(
        UpdateSchemaVersionDefinitionCommand command, CancellationToken ct)
    {
        var definition = command.Definition;

        if (definition.Types.Count > options.MaxSchemaTypes ||
            definition.Types.Any(t => t.Relations.Count(r => !r.IsLink(SchemaValidator.UserType)) > options.MaxPermissionsPerType))
            throw new BusinessException(AuthZMessageKeys.SchemaLimitExceeded);

        var schema = await schemaRepository.GetAsync(
            s => s.Id == command.VersionId && s.PoolId == command.UserPoolId, cancellationToken: ct);
        schema.ThrowCustomExceptionIfDataNull(AuthZMessageKeys.SchemaVersionNotFound);

        if (schema!.Status == SchemaVersionStatus.Active)
            throw new BusinessException(AuthZMessageKeys.SchemaVersionActiveReadOnly);

        if (schema.Revision != command.Revision)
            throw new BusinessException(AuthZMessageKeys.SchemaConflict);

        // Yalnızca görünüm (ad, açıklama, konum) değiştiyse mevcut model geçerli kalır.
        if (schema.Definition.ModelFingerprint() != definition.ModelFingerprint())
            schema.ModelId = null;

        schema.Definition = definition;
        schema.Revision++;

        await schemaRepository.SaveChangesAsync(ct);

        return new UpdateSchemaVersionDefinitionResponse(schema.Id, schema.Revision);
    }
}

public class UpdateSchemaVersionDefinitionValidator : Validator<UpdateSchemaVersionDefinitionCommand>
{
    public UpdateSchemaVersionDefinitionValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.VersionId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Definition).NotNull().WithMessage(ValidationErrors.NotNullOrEmptyError);

        RuleForEach(x => x.Definition.Types).ChildRules(type =>
        {
            type.RuleFor(t => t.Name).MaximumLength(40).WithMessage(ValidationErrors.MaxLengthError);
            type.RuleFor(t => t.DisplayName).MaximumLength(100).WithMessage(ValidationErrors.MaxLengthError);
            type.RuleFor(t => t.Description).MaximumLength(500).WithMessage(ValidationErrors.MaxLengthError);

            type.RuleForEach(t => t.Relations).ChildRules(relation =>
            {
                relation.RuleFor(r => r.Name).MaximumLength(40).WithMessage(ValidationErrors.MaxLengthError);
                relation.RuleFor(r => r.DisplayName).MaximumLength(100).WithMessage(ValidationErrors.MaxLengthError);
                relation.RuleFor(r => r.Description).MaximumLength(500).WithMessage(ValidationErrors.MaxLengthError);
            });
        }).When(x => x.Definition is not null);
    }
}
