using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Z.Constants;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;

namespace Booster.Auth.Z.Features.Schemas.Delete;

public class DeleteSchemaVersionEndpoint : EndpointWithoutRequest<DeleteSchemaVersionResponse>
{
    public override void Configure()
    {
        Delete("authz/schema/versions");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthZPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, AuthZPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Sürümü siler";
            s.Description = "Yayındaki sürüm silinemez.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(
            await new DeleteSchemaVersionCommand(Query<Guid>("UserPoolId"), Query<Guid>("VersionId")).ExecuteAsync(ct), ct);
}

public record DeleteSchemaVersionCommand(Guid UserPoolId, Guid VersionId) : ICommand<DeleteSchemaVersionResponse>;

public readonly record struct DeleteSchemaVersionResponse(Guid Id);

public class DeleteSchemaVersionCommandHandler(IAsyncRepository<PoolSchema, AuthZDbContext> schemaRepository)
    : ICommandHandler<DeleteSchemaVersionCommand, DeleteSchemaVersionResponse>
{
    public async Task<DeleteSchemaVersionResponse> ExecuteAsync(DeleteSchemaVersionCommand command, CancellationToken ct)
    {
        var schema = await schemaRepository.GetAsync(
            s => s.Id == command.VersionId && s.PoolId == command.UserPoolId, cancellationToken: ct);
        schema.ThrowCustomExceptionIfDataNull(AuthZMessageKeys.SchemaVersionNotFound);

        if (schema!.Status == SchemaVersionStatus.Active)
            throw new BusinessException(AuthZMessageKeys.SchemaVersionActiveCannotDelete);

        schemaRepository.Delete(schema);
        await schemaRepository.SaveChangesAsync(ct);

        return new DeleteSchemaVersionResponse(schema.Id);
    }
}
