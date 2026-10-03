using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Constants;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Auth.Z.Features.Schemas._base;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Fga.Attributes;
using Booster.Authorization.Relational.Fga;
using Booster.Authorization.Relational.Options;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using OpenFga.Sdk.Exceptions;

namespace Booster.Auth.Z.Features.Schemas.Activate;

public class ActivateSchemaVersionEndpoint : Endpoint<ActivateSchemaVersionCommand, ActivateSchemaVersionResponse>
{
    public override void Configure()
    {
        Post("authz/schema/versions/activate");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthZPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, AuthZPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Sürümü yayına alır";
            s.Description = "Yalnızca durumlar değişir: seçilen sürüm aktif, önceki aktif pasif olur. " +
                            "OpenFGA'ya model yalnızca sürümün modeli yoksa yazılır. Sorunlu sürüm yayına alınamaz.";
        });
    }

    public override async Task HandleAsync(ActivateSchemaVersionCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record ActivateSchemaVersionCommand(Guid UserPoolId, Guid VersionId) : ICommand<ActivateSchemaVersionResponse>;

public readonly record struct ActivateSchemaVersionResponse(Guid Id);

public class ActivateSchemaVersionCommandHandler(
    IAsyncRepository<PoolSchema, AuthZDbContext> schemaRepository,
    AuthZDbContext dbContext,
    IPoolStoreProvider poolStoreProvider,
    FgaSchemaWriter schemaWriter,
    FgaOptions options,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<ActivateSchemaVersionCommand, ActivateSchemaVersionResponse>
{
    public async Task<ActivateSchemaVersionResponse> ExecuteAsync(ActivateSchemaVersionCommand command, CancellationToken ct)
    {
        var schema = await schemaRepository.GetAsync(
            s => s.Id == command.VersionId && s.PoolId == command.UserPoolId, cancellationToken: ct);
        schema.ThrowCustomExceptionIfDataNull(AuthZMessageKeys.SchemaVersionNotFound);

        if (schema!.Status == SchemaVersionStatus.Active)
            return new ActivateSchemaVersionResponse(schema.Id);

        if (SchemaValidator.Validate(schema.Definition, command.UserPoolId, options).Count > 0)
            throw new BusinessException(AuthZMessageKeys.SchemaHasIssues);

        if (schema.ModelId is null)
        {
            try
            {
                schema.ModelId = await schemaWriter.WriteModelAsync(schema.StoreId, schema.Definition.ToFgaModel(), ct);
            }
            catch (FgaApiValidationError)
            {
                throw new BusinessException(AuthZMessageKeys.SchemaRejected);
            }
        }

        var current = await schemaRepository.GetAsync(
            s => s.PoolId == command.UserPoolId && s.Status == SchemaVersionStatus.Active, cancellationToken: ct);

        // "Tek aktif" indeksi satır satır denetlenir: önce eski aktif bırakılır, ikisi tek işlemde yazılır.
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(ct))
        {
            if (current is not null)
            {
                current.Status = SchemaVersionStatus.Inactive;
                await schemaRepository.SaveChangesAsync(ct);
            }

            schema.Status = SchemaVersionStatus.Active;
            schema.ActivatedTime = DateTimeOffset.UtcNow;
            schema.ActivatedBy = requestContextAccessor.Context().PrincipalId;

            await schemaRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        await poolStoreProvider.RemoveCacheAsync(command.UserPoolId, ct);

        return new ActivateSchemaVersionResponse(schema.Id);
    }
}

public class ActivateSchemaVersionValidator : Validator<ActivateSchemaVersionCommand>
{
    public ActivateSchemaVersionValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.VersionId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
