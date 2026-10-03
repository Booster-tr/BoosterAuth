using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Constants;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Fga.Attributes;
using Booster.Authorization.Relational.Fga;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.Schemas.Init;

public class InitSchemaEndpoint : Endpoint<InitSchemaCommand, InitSchemaResponse>
{
    public override void Configure()
    {
        Post("authz/schema/init");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthZPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, AuthZPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Havuzun yetkilendirme şemasını oluşturur";
            s.Description = "Havuz için OpenFGA store'u açar ve yalnızca user tipini içeren ilk sürümü yayına alır. " +
                            "Şema zaten varsa yayındaki sürümü döner.";
        });
    }

    public override async Task HandleAsync(InitSchemaCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record InitSchemaCommand(Guid UserPoolId, string Name) : ICommand<InitSchemaResponse>;

public readonly record struct InitSchemaResponse(Guid Id);

public class InitSchemaCommandHandler(
    IAsyncRepository<PoolSchema, AuthZDbContext> schemaRepository,
    IPoolStoreProvider poolStoreProvider,
    FgaSchemaWriter schemaWriter,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<InitSchemaCommand, InitSchemaResponse>
{
    public async Task<InitSchemaResponse> ExecuteAsync(InitSchemaCommand command, CancellationToken ct)
    {
        var existing = await schemaRepository.GetAsync(
            s => s.PoolId == command.UserPoolId && s.Status == SchemaVersionStatus.Active,
            enableTracking: false, cancellationToken: ct);

        if (existing is not null)
            return new InitSchemaResponse(existing.Id);

        var definition = new SchemaDefinition();
        var storeId = await schemaWriter.CreateStoreAsync($"pool-{command.UserPoolId}", ct);

        var schema = new PoolSchema
        {
            PoolId = command.UserPoolId,
            StoreId = storeId,
            Name = command.Name.Trim(),
            Status = SchemaVersionStatus.Active,
            Definition = definition,
            ModelId = await schemaWriter.WriteModelAsync(storeId, definition.ToFgaModel(), ct),
            Revision = 1,
            ActivatedTime = DateTimeOffset.UtcNow,
            ActivatedBy = requestContextAccessor.Context().PrincipalId
        };
        schema.SetId();

        await schemaRepository.AddAsync(schema, ct);
        await schemaRepository.SaveChangesAsync(ct);

        await poolStoreProvider.RemoveCacheAsync(command.UserPoolId, ct);

        return new InitSchemaResponse(schema.Id);
    }
}

public class InitSchemaValidator : Validator<InitSchemaCommand>
{
    public InitSchemaValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(100).WithMessage(ValidationErrors.MaxLengthError);
    }
}
