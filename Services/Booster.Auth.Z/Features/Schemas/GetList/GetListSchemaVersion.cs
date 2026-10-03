using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Z.Constants;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Authorization.Fga.Attributes;
using Booster.Authorization.Relational.Options;
using Booster.Database.Repositories;
using FastEndpoints;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Z.Features.Schemas.GetList;

public class GetListSchemaVersionEndpoint : EndpointWithoutRequest<GetListSchemaVersionResponse>
{
    public override void Configure()
    {
        Get("authz/schema/versions");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthZPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, AuthZPolicies.PoolTarget, AuthConstants.ViewerRelation)));

        Summary(s =>
        {
            s.Summary = "Havuzun şema sürümlerini listeler";
            s.Description = "Liste boşsa şema henüz oluşturulmamıştır. Aktif sürüm başta gelir; arayüzün sınırları da döner.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new GetListSchemaVersionQuery(Query<Guid>("UserPoolId")).ExecuteAsync(ct), ct);
}

public record GetListSchemaVersionQuery(Guid UserPoolId) : ICommand<GetListSchemaVersionResponse>;

public record GetListSchemaVersionResponse(IReadOnlyList<SchemaVersionListItem> Items, SchemaLimits Limits);

public record SchemaVersionListItem(
    Guid Id,
    string Name,
    SchemaVersionStatus Status,
    DateTimeOffset CreatedTime,
    DateTimeOffset? UpdatedTime,
    DateTimeOffset? ActivatedTime);

public record SchemaLimits(int MaxSchemaTypes, int MaxPermissionsPerType, int MaxSchemaVersions);

public class GetListSchemaVersionQueryHandler(
    IAsyncRepository<PoolSchema, AuthZDbContext> schemaRepository,
    FgaOptions options)
    : ICommandHandler<GetListSchemaVersionQuery, GetListSchemaVersionResponse>
{
    public async Task<GetListSchemaVersionResponse> ExecuteAsync(GetListSchemaVersionQuery command, CancellationToken ct)
    {
        // Tanım (JSON) okunmaz; liste yalnızca özet sütunları taşır.
        var items = await schemaRepository.Query()
            .AsNoTracking()
            .Where(s => s.PoolId == command.UserPoolId)
            .OrderByDescending(s => s.Status == SchemaVersionStatus.Active)
            .ThenByDescending(s => s.UpdatedTime ?? s.CreatedTime)
            .ProjectToType<SchemaVersionListItem>()
            .ToListAsync(ct);

        return new GetListSchemaVersionResponse(items, options.Adapt<SchemaLimits>());
    }
}
