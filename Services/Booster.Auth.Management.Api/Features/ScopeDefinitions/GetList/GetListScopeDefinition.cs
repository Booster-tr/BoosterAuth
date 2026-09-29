using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.DynamicFilter.Models;
using Booster.Database.Helpers;
using Booster.Database.NameValueCollectionHelpers;
using Booster.Database.Repositories;
using FastEndpoints;

namespace Booster.Auth.Management.Api.Features.ScopeDefinitions.GetList;

public class GetListScopeDefinitionEndpoint : EndpointWithoutRequest<ListModel<GetListScopeDefinitionResponse>>
{
    public override void Configure()
    {
        Get("");
        Group<ScopeDefinitionsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.ViewerRelation)));

        Summary(s =>
        {
            s.Summary = "Havuzun kapsam tanımlarını listeler";
            s.Description = "Dinamik filtre ve sayfalama ile döner.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var nvc = HttpContext.Request.ToNvc();
        var query = new GetListScopeDefinitionQuery(Query<Guid>("UserPoolId"))
        {
            DynamicQuery = nvc.ToDynamicFilter<GetListScopeDefinitionResponse>(),
            PageRequest = nvc.ToPageRequest()
        };

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetListScopeDefinitionQuery(Guid UserPoolId)
    : BaseDynamicRequest, ICommand<ListModel<GetListScopeDefinitionResponse>>;

public record GetListScopeDefinitionResponse(
    Guid Id,
    string Scope,
    string DisplayName,
    string? Description,
    DateTimeOffset CreatedTime,
    DateTimeOffset? UpdatedTime);

public class GetListScopeDefinitionQueryHandler(
    IAsyncRepository<ScopeDefinition, AuthNDbContext> scopeRepository)
    : ICommandHandler<GetListScopeDefinitionQuery, ListModel<GetListScopeDefinitionResponse>>
{
    public async Task<ListModel<GetListScopeDefinitionResponse>> ExecuteAsync(
        GetListScopeDefinitionQuery command, CancellationToken ct)
    {
        var data = await scopeRepository.ListDynamicAsync(
            command.DynamicQuery,
            predicate: s => s.UserPoolId == command.UserPoolId,
            index: command.PageRequest.PageIndex,
            size: command.PageRequest.PageSize,
            enableTracking: false,
            cancellationToken: ct);

        return data.MapListModel<ScopeDefinition, GetListScopeDefinitionResponse>(command);
    }
}
