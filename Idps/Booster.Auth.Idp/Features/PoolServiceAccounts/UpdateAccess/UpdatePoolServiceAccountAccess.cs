using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Features.PoolServiceAccounts._base;
using Booster.Auth.Idp.Features.PoolUsers.UpdateAccess;
using Booster.Auth.Idp.Stores.PrincipalStores;
using Booster.Auth.Management.Grpc;
using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Grpc;
using Booster.Authorization.Fga.Attributes;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Idp.Features.PoolServiceAccounts.UpdateAccess;

public class UpdatePoolServiceAccountAccessEndpoint
    : Endpoint<UpdatePoolServiceAccountAccessCommand, UpdatePoolServiceAccountAccessResponse>
{
    public override void Configure()
    {
        Put("pools/service-accounts/access");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, IdpPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Servis hesabının havuz yetkisini, rol ve kapsamlarını değiştirir";
            s.Description = "Roller ve kapsamlar yönetimin tanımlarından ve çağıranın kendisinde olanlardan seçilebilir.";
        });
    }

    public override async Task HandleAsync(UpdatePoolServiceAccountAccessCommand req, CancellationToken ct)
    {
        var command = req with
        {
            CallerRoles = User.FindAll("role").Select(c => c.Value).ToList(),
            CallerScopes = (User.FindFirst("scope")?.Value ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        };

        await Send.OkAsync(await command.ExecuteAsync(ct), ct);
    }
}

public record UpdatePoolServiceAccountAccessCommand(
    Guid UserPoolId,
    Guid ServiceAccountId,
    string Relation,
    List<Guid> RoleIds,
    List<string> Scopes) : ICommand<UpdatePoolServiceAccountAccessResponse>
{
    public IReadOnlyList<string> CallerRoles { get; init; } = [];
    public IReadOnlyList<string> CallerScopes { get; init; } = [];
}

public readonly record struct UpdatePoolServiceAccountAccessResponse(Guid Id);

public class UpdatePoolServiceAccountAccessCommandHandler(
    IAsyncRepository<ServiceAccount, IdpDbContext> serviceAccountRepository,
    IAsyncRepository<PrincipalRole, IdpDbContext> principalRoleRepository,
    IAsyncRepository<PrincipalScope, IdpDbContext> principalScopeRepository,
    AuthorizationService.AuthorizationServiceClient authorizationClient,
    ClientDataService.ClientDataServiceClient clientDataClient,
    ICacheService cacheService,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<UpdatePoolServiceAccountAccessCommand, UpdatePoolServiceAccountAccessResponse>
{
    public async Task<UpdatePoolServiceAccountAccessResponse> ExecuteAsync(
        UpdatePoolServiceAccountAccessCommand command, CancellationToken ct)
    {
        var definitions = await clientDataClient.GetPoolDefinitionsAsync(
            new GetPoolDefinitionsRequest { UserPoolId = AuthConstants.ManagementPoolId.ToString() },
            cancellationToken: ct);

        var callerRoles = command.CallerRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var roles = definitions.Roles.ToDictionary(r => Guid.Parse(r.Id), r => r.Key);
        var roleIds = command.RoleIds.Distinct().ToList();

        if (roleIds.Any(id => !roles.ContainsKey(id)))
            throw new BusinessException(IdpMessageKeys.RoleNotInPool);

        if (roleIds.Any(id => !callerRoles.Contains(roles[id])))
            throw new ForbiddenException(IdpMessageKeys.RoleNotHeldByCaller);

        var scopes = command.Scopes.Select(s => s.Trim().ToLowerInvariant()).Distinct().ToList();

        if (scopes.Any(s => !definitions.Scopes.Contains(s)))
            throw new BusinessException(IdpMessageKeys.ScopeNotInPool);

        if (scopes.Any(s => !command.CallerScopes.Contains(s)))
            throw new ForbiddenException(IdpMessageKeys.ScopeNotHeldByCaller);

        using var _ = requestContextAccessor.BeginSystemContext(AuthConstants.ManagementPoolId);

        var account = await serviceAccountRepository.GetAsync(
            s => s.Id == command.ServiceAccountId && s.OwnerPoolId == command.UserPoolId,
            enableTracking: false, cancellationToken: ct)
            ?? throw new BusinessException(IdpMessageKeys.ServiceAccountNotFound);

        await ReplaceRelationAsync(account.PrincipalId, command.UserPoolId, command.Relation.ToLowerInvariant(), ct);

        await PrincipalAccessWriter.ReplaceAsync(
            principalRoleRepository, principalScopeRepository, account.PrincipalId,
            roleIds.Select(id => (id, roles[id])).ToList(), scopes, ct);

        await principalRoleRepository.SaveChangesAsync(ct);
        await cacheService.RemoveByTagAsync(LocalPrincipalStore.Tag(account.PrincipalId), ct);

        return new UpdatePoolServiceAccountAccessResponse(account.Id);
    }

    private async Task ReplaceRelationAsync(Guid principalId, Guid poolId, string relation, CancellationToken ct)
    {
        var user = AuthConstants.User(principalId);
        var pool = AuthConstants.Pool(poolId);

        var current = await authorizationClient.ReadRelationsAsync(
            new ReadRelationsRequest { IsolationId = ServiceAccountRules.Isolation, User = user, Object = pool },
            cancellationToken: ct);

        var stale = current.Results.Where(r => r.Relation != relation).ToList();

        if (stale.Count > 0)
        {
            var delete = new WriteRelationsRequest { IsolationId = ServiceAccountRules.Isolation };
            delete.Tuples.AddRange(stale.Select(r => new RelationTuple { User = r.User, Relation = r.Relation, Object = r.Object }));
            await authorizationClient.DeleteRelationsAsync(delete, cancellationToken: ct);
        }

        if (current.Results.All(r => r.Relation != relation))
        {
            var write = new WriteRelationsRequest { IsolationId = ServiceAccountRules.Isolation };
            write.Tuples.Add(new RelationTuple { User = user, Relation = relation, Object = pool });
            await authorizationClient.WriteRelationsAsync(write, cancellationToken: ct);
        }
    }
}

public class UpdatePoolServiceAccountAccessValidator : Validator<UpdatePoolServiceAccountAccessCommand>
{
    public UpdatePoolServiceAccountAccessValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.ServiceAccountId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Relation)
            .Must(r => ServiceAccountRules.Assignable.Contains(r, StringComparer.OrdinalIgnoreCase))
            .WithMessage(IdpMessageKeys.InvalidServiceAccountRelation);

        RuleFor(x => x.RoleIds).NotNull().WithMessage(ValidationErrors.NotNullOrEmptyError);
        RuleFor(x => x.Scopes).NotNull().WithMessage(ValidationErrors.NotNullOrEmptyError);
    }
}
