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
using FastEndpoints;
using Mapster;

namespace Booster.Auth.Z.Features.Schemas.GetById;

public class GetByIdSchemaVersionEndpoint : EndpointWithoutRequest<GetByIdSchemaVersionResponse>
{
    public override void Configure()
    {
        Get("authz/schema/versions/detail");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthZPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, AuthZPolicies.PoolTarget, AuthConstants.ViewerRelation)));

        Summary(s =>
        {
            s.Summary = "Bir şema sürümünün tanımını ve sorunlarını döner";
            s.Description = "Sorunlar her okumada yeniden hesaplanır. Protected, kodu değiştirilemeyen sistem öğeleridir.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(
            await new GetByIdSchemaVersionQuery(Query<Guid>("UserPoolId"), Query<Guid>("VersionId")).ExecuteAsync(ct), ct);
}

public record GetByIdSchemaVersionQuery(Guid UserPoolId, Guid VersionId) : ICommand<GetByIdSchemaVersionResponse>;

public record GetByIdSchemaVersionResponse(
    Guid Id,
    string Name,
    SchemaVersionStatus Status,
    int Revision,
    SchemaDefinition Definition)
{
    public IReadOnlyList<SchemaIssue> Issues { get; init; } = [];

    public IReadOnlyList<string> Protected { get; init; } = [];
}

public class GetByIdSchemaVersionQueryHandler(
    IAsyncRepository<PoolSchema, AuthZDbContext> schemaRepository,
    FgaOptions options)
    : ICommandHandler<GetByIdSchemaVersionQuery, GetByIdSchemaVersionResponse>
{
    public async Task<GetByIdSchemaVersionResponse> ExecuteAsync(GetByIdSchemaVersionQuery command, CancellationToken ct)
    {
        var schema = await schemaRepository.GetAsync(
            s => s.Id == command.VersionId && s.PoolId == command.UserPoolId, enableTracking: false, cancellationToken: ct);
        schema.ThrowCustomExceptionIfDataNull(AuthZMessageKeys.SchemaVersionNotFound);

        return schema!.Adapt<GetByIdSchemaVersionResponse>() with
        {
            Issues = SchemaValidator.Validate(schema!.Definition, command.UserPoolId, options),
            Protected = SystemSchema.ProtectedFor(command.UserPoolId)
        };
    }
}
