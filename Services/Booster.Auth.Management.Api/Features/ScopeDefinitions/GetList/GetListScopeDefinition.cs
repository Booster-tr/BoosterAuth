using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Helpers;
using Booster.Database.DynamicFilter.Models;
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
        Summary(s =>
        {
            s.Summary = "Scope tanımlarını listeler";
            s.Description = "Dinamik filtre ve sayfalama ile döner. " +
                            "Havuz kapsamı için query string içine userPoolId filtresi verilir.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var nvc = HttpContext.Request.ToNvc();
        var query = new GetListScopeDefinitionQuery
        {
            DynamicQuery = nvc.ToDynamicFilter<GetListScopeDefinitionResponse>(),
            PageRequest = nvc.ToPageRequest()
        };

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetListScopeDefinitionQuery : BaseDynamicRequest, ICommand<ListModel<GetListScopeDefinitionResponse>>;

public record GetListScopeDefinitionResponse(
    Guid Id,
    Guid UserPoolId,
    string Scope,
    string DisplayName,
    string? Description);

public class GetListScopeDefinitionQueryHandler(
    IAsyncRepository<ScopeDefinition, AuthNDbContext> scopeRepository)
    : ICommandHandler<GetListScopeDefinitionQuery, ListModel<GetListScopeDefinitionResponse>>
{
    public async Task<ListModel<GetListScopeDefinitionResponse>> ExecuteAsync(
        GetListScopeDefinitionQuery command, CancellationToken ct)
    {
        var data = await scopeRepository.ListDynamicAsync(
            command.DynamicQuery,
            index: command.PageRequest.PageIndex,
            size: command.PageRequest.PageSize,
            cancellationToken: ct);

        return data.MapListModel<ScopeDefinition, GetListScopeDefinitionResponse>(command);
    }
}
