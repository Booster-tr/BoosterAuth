using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Grpc;
using Booster.Database.Repositories;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Management.Api.Features.Clients.Grpc;

[AllowAnonymous]
public sealed class ClientDataGrpcService(
    IAsyncRepository<Client, AuthNDbContext> clientRepository,
    IAsyncRepository<Role, AuthNDbContext> roleRepository)
    : ClientDataService.ClientDataServiceBase
{
    public override async Task<GetClientDataResponse> GetByClientKey(
        GetClientDataRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ClientKey))
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "client_key boş olamaz."));

        var ct = context.CancellationToken;

        var client = await clientRepository.Query()
            .AsNoTracking()
            .Include(c => c.UserPool)
            .Include(c => c.AllowedScopes)
            .FirstOrDefaultAsync(c => c.ClientKey == request.ClientKey, ct);

        if (client is null)
            return new GetClientDataResponse { Found = false };

        var defaultRoles = await ResolveDefaultRolesAsync(client.UserPool, ct);

        return new GetClientDataResponse
        {
            Found = true,
            Client = ClientDataMapper.ToMessage(BuildClientData(client, defaultRoles))
        };
    }

    private async Task<List<DefaultRoleData>> ResolveDefaultRolesAsync(UserPool pool, CancellationToken ct)
    {
        if (pool.DefaultRoleIds.Count == 0)
            return [];

        return await roleRepository.Query()
            .AsNoTracking()
            .Where(r => pool.DefaultRoleIds.Contains(r.Id))
            .Select(r => new DefaultRoleData { RoleId = r.Id, RoleName = r.Key })
            .ToListAsync(ct);
    }

    private static ClientData BuildClientData(Client client, List<DefaultRoleData> defaultRoles)
    {
        var pool = client.UserPool;
        var policy = pool.Policy;

        return new ClientData
        {
            ClientId = client.Id,
            ClientKey = client.ClientKey,
            AllowedGrantTypes = client.AllowedGrantTypes,
            AllowedScopes = [.. client.AllowedScopes.Select(s => s.Scope)],

            IsolationId = pool.Id,

            IsActive = client.Status == EntityStatus.Active && pool.Status == EntityStatus.Active,

            RedirectUris = [.. client.RedirectUris],
            PostLogoutRedirectUris = [.. client.PostLogoutRedirectUris],
            AllowedAudiences = [.. client.AllowedAudiences],
            RequireConsent = client.RequireConsent,

            Login = new LoginPolicyData
            {
                AllowedCredentialProviders = policy.Login.AllowedCredentialProviders,
                AllowedIdentifiers = [.. policy.Login.AllowedIdentifiers],
                MaxFailedAttempts = policy.Login.MaxFailedAttempts,
                LockoutSeconds = policy.Login.LockoutSeconds
            },
            Registration = new RegistrationPolicyData
            {
                Mode = policy.Registration.Mode,
                RequireEmailVerification = policy.Registration.RequireEmailVerification,
                RequirePhoneVerification = policy.Registration.RequirePhoneVerification,
                AllowFederatedJit = policy.Registration.AllowFederatedJit
            },
            Password = new PasswordPolicyData
            {
                MinLength = policy.Password.MinLength,
                RequireUppercase = policy.Password.RequireUppercase,
                RequireLowercase = policy.Password.RequireLowercase,
                RequireDigit = policy.Password.RequireDigit,
                RequireNonAlphanumeric = policy.Password.RequireNonAlphanumeric
            },
            Session = new SessionPolicyData
            {
                AccessTokenTtlSeconds = policy.Session.AccessTokenTtlSeconds,
                RefreshTokenTtlSeconds = policy.Session.RefreshTokenTtlSeconds,
                IdleTimeoutSeconds = policy.Session.IdleTimeoutSeconds
            },
            DefaultScopes = [.. pool.DefaultScopes],
            DefaultRoles = defaultRoles
        };
    }
}
