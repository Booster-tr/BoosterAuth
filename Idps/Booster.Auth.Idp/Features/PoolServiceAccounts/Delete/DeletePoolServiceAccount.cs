using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Features.PoolServiceAccounts._base;
using Booster.Auth.Idp.Stores.PrincipalStores;
using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Grpc;
using Booster.Authorization.Fga.Attributes;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;

namespace Booster.Auth.Idp.Features.PoolServiceAccounts.Delete;

public class DeletePoolServiceAccountEndpoint : EndpointWithoutRequest<DeletePoolServiceAccountResponse>
{
    public override void Configure()
    {
        Delete("pools/service-accounts");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, IdpPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s => s.Summary = "Servis hesabını ve havuz üzerindeki yetkisini kaldırır");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var command = new DeletePoolServiceAccountCommand(Query<Guid>("UserPoolId"), Query<Guid>("ServiceAccountId"));

        await Send.OkAsync(await command.ExecuteAsync(ct), ct);
    }
}

public record DeletePoolServiceAccountCommand(Guid UserPoolId, Guid ServiceAccountId)
    : ICommand<DeletePoolServiceAccountResponse>;

public readonly record struct DeletePoolServiceAccountResponse(Guid Id);

public class DeletePoolServiceAccountCommandHandler(
    IAsyncRepository<ServiceAccount, IdpDbContext> serviceAccountRepository,
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    AuthorizationService.AuthorizationServiceClient authorizationClient,
    ICacheService cacheService,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<DeletePoolServiceAccountCommand, DeletePoolServiceAccountResponse>
{
    public async Task<DeletePoolServiceAccountResponse> ExecuteAsync(
        DeletePoolServiceAccountCommand command, CancellationToken ct)
    {
        using var _ = requestContextAccessor.BeginSystemContext(AuthConstants.ManagementPoolId);

        var account = await serviceAccountRepository.GetAsync(
            s => s.Id == command.ServiceAccountId && s.OwnerPoolId == command.UserPoolId, cancellationToken: ct)
            ?? throw new BusinessException(IdpMessageKeys.ServiceAccountNotFound);

        var principal = await principalRepository.GetAsync(p => p.Id == account.PrincipalId, cancellationToken: ct);

        var relations = await authorizationClient.ReadRelationsAsync(
            new ReadRelationsRequest
            {
                IsolationId = ServiceAccountRules.Isolation,
                User = AuthConstants.User(account.PrincipalId),
                Object = AuthConstants.Pool(command.UserPoolId)
            },
            cancellationToken: ct);

        if (relations.Results.Count > 0)
        {
            var delete = new WriteRelationsRequest { IsolationId = ServiceAccountRules.Isolation };
            delete.Tuples.AddRange(relations.Results.Select(r => new RelationTuple
            {
                User = r.User,
                Relation = r.Relation,
                Object = r.Object
            }));
            await authorizationClient.DeleteRelationsAsync(delete, cancellationToken: ct);
        }

        serviceAccountRepository.Delete(account);
        if (principal is not null)
            principalRepository.Delete(principal);

        await serviceAccountRepository.SaveChangesAsync(ct);
        await cacheService.RemoveByTagAsync(LocalPrincipalStore.Tag(account.PrincipalId), ct);

        return new DeletePoolServiceAccountResponse(account.Id);
    }
}
