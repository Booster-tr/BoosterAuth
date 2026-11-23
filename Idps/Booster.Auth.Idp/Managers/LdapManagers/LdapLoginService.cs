using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Providers.Abstractions.Stores;
using Booster.Auth.Idp.Providers.Abstractions.Stores.Models;
using Booster.Auth.Idp.Stores.FederationStores;
using Booster.Auth.Idp.Stores.PrincipalStores;
using Booster.Auth.Ldap;
using Booster.Auth.Z.Grpc;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Managers.LdapManagers;

/// <summary>
/// LDAP / Active Directory ile parola girisi. Havuzda "ldap" turunde aktif bir kimlik saglayici ve giris politikasinda
/// LDAP yontemi varsa, YEREL PAROLASI OLMAYAN kullanicilar parola formunda dizinle dogrulanir.
/// <list type="bullet">
/// <item>Eslesme <see cref="PrincipalIdentity"/> (saglayici + dizindeki degismez kimlik, or. objectGUID) ile yapilir.</item>
/// <item>Ilk giriste hesap acilir (havuzun "ilk dis giriste hesap ac" ayari ve lisans kullanici limiti gecerli); ad ve
/// e-posta her giriste dizinden guncellenir.</item>
/// <item>Ayni e-posta/kullanici adiyla YEREL PAROLASI OLAN bir hesap varsa otomatik baglanmaz (hesap ele gecirme riski).</item>
/// <item>Hatali denemeler (havuz + kullanici adi) sayilir; havuzun kilitleme politikasi uygulanir. Dizinin kendi kilitleme
/// politikasi ayrica gecerlidir.</item>
/// </list>
/// Cagiran, havuzun sistem baglamini (BeginSystemContext) acmis olmalidir.
/// </summary>
public sealed class LdapLoginService(
    IFederationProviderConfigStore configStore,
    LdapDirectory directory,
    ICacheService cache,
    ILocalUserStore userStore,
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IAsyncRepository<PrincipalIdentity, IdpDbContext> identityRepository,
    IAsyncRepository<LocalCredential, IdpDbContext> credentialRepository,
    IAsyncRepository<PrincipalScope, IdpDbContext> principalScopeRepository,
    IAsyncRepository<PrincipalRole, IdpDbContext> principalRoleRepository,
    AuthorizationService.AuthorizationServiceClient authorizationClient,
    UserManagers.EndUserFactory endUserFactory,
    ILogger<LdapLoginService> logger)
{
    /// <summary>Havuzun etkin LDAP saglayicisi (giris politikasinda LDAP acik degilse null).</summary>
    public static FederationProviderData? FindProvider(ClientData client) =>
        client.Login.AllowedLoginProviders.HasFlag(LoginProviderType.Ldap)
            ? client.FederationProviders.FirstOrDefault(p => string.Equals(p.ProviderType, LdapSettings.ProviderType, StringComparison.OrdinalIgnoreCase))
            : null;

    public async Task<LocalUser> SignInAsync(ClientData client, FederationProviderData meta, string identifier, string password, CancellationToken ct)
    {
        var failKey = $"ldap-fail:{client.IsolationId}:{identifier.Trim().ToLowerInvariant()}";
        var failures = await cache.GetAsync<int?>(failKey, ct) ?? 0;
        if (client.Login.LockoutSeconds > 0 && failures >= client.Login.MaxFailedAttempts)
            throw Invalid(IdpMessageKeys.AccountLocked);

        var config = await configStore.GetAsync(client.IsolationId, meta.Key, ct);
        var settings = config is { IsActive: true } ? LdapSettings.From(config.Settings) : null;
        if (settings is null)
        {
            logger.LogWarning("LDAP saglayicisi {Key} eksik ya da pasif (havuz {Pool}).", meta.Key, client.IsolationId);
            throw Invalid(IdpMessageKeys.LdapUnavailable);
        }

        var result = await directory.AuthenticateAsync(settings, identifier, password, ct);
        if (!result.Succeeded)
        {
            if (result.Failure is LdapFailure.Unreachable or LdapFailure.Misconfigured)
                throw Invalid(IdpMessageKeys.LdapUnavailable);

            if (client.Login.LockoutSeconds > 0)
                await cache.SetAsync(failKey, failures + 1, TimeSpan.FromSeconds(client.Login.LockoutSeconds), ct: ct);
            throw Invalid(IdpMessageKeys.InvalidCredentials);
        }

        await cache.RemoveAsync(failKey, ct);
        var ldapUser = result.User!;

        var identity = await identityRepository.Query()
            .FirstOrDefaultAsync(i => i.IdentityProviderId == meta.Id && i.ExternalSubject == ldapUser.Subject, ct);

        Guid principalId;
        if (identity is not null)
        {
            principalId = identity.PrincipalId;
            await SyncAsync(principalId, ldapUser, ct);
            identity.LastLoginAt = DateTimeOffset.UtcNow;
            identityRepository.Update(identity);
            await identityRepository.SaveChangesAsync(ct);
        }
        else
        {
            var existing = await FindExistingAsync(ldapUser, ct);
            if (existing is not null)
            {
                // Yerel parolasi olan hesaba dizin kaydi otomatik baglanmaz (dizinde ayni e-postayla acilan bir kayit o
                // hesabi ele gecirebilirdi).
                if (await credentialRepository.AnyAsync(c => c.PrincipalId == existing.Id, cancellationToken: ct))
                    throw Invalid(IdpMessageKeys.LdapLocalAccountExists);

                principalId = existing.Id;
                await identityRepository.AddAsync(NewIdentity(principalId, meta, client, ldapUser), ct);
                await identityRepository.SaveChangesAsync(ct);
                await SyncAsync(principalId, ldapUser, ct);
            }
            else
            {
                if (!client.Registration.AllowFederatedJit)
                    throw Invalid(IdpMessageKeys.LdapJitDisabled);
                principalId = await ProvisionAsync(client, meta, ldapUser, ct);
            }
        }

        var user = await userStore.GetByIdAsync(principalId, ct);
        if (user is null || !user.IsActive)
            throw Invalid(IdpMessageKeys.InvalidCredentials);
        return user;
    }

    /// <summary>Yerel parolasi olmayan ve havuzun LDAP saglayicisina bagli kullanici: parolasi dizindedir.</summary>
    public async Task<bool> IsDirectoryManagedAsync(ClientData client, Guid principalId, CancellationToken ct)
    {
        var meta = client.FederationProviders.FirstOrDefault(p => string.Equals(p.ProviderType, LdapSettings.ProviderType, StringComparison.OrdinalIgnoreCase));
        return meta is not null && await identityRepository.AnyAsync(i => i.PrincipalId == principalId && i.IdentityProviderId == meta.Id, cancellationToken: ct);
    }

    private async Task<Principal?> FindExistingAsync(LdapUser u, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(u.Email))
        {
            var byEmail = await principalRepository.Query().AsNoTracking().FirstOrDefaultAsync(p => p.Email == u.Email, ct);
            if (byEmail is not null) return byEmail;
        }
        var username = u.Username?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(username)
            ? null
            : await principalRepository.Query().AsNoTracking().FirstOrDefaultAsync(p => p.UserName == username, ct);
    }

    /// <summary>Ad ve e-posta dizinden; baska bir hesapta kullanilan e-posta / kullanici adi yazilmaz (tekillik).</summary>
    private async Task SyncAsync(Guid principalId, LdapUser u, CancellationToken ct)
    {
        var principal = await principalRepository.GetAsync(p => p.Id == principalId, cancellationToken: ct);
        if (principal is null) return;

        var changed = false;
        if (!string.IsNullOrWhiteSpace(u.DisplayName) && principal.DisplayName != u.DisplayName)
        {
            principal.DisplayName = u.DisplayName.Trim();
            changed = true;
        }
        if (!string.IsNullOrEmpty(u.Email) && principal.Email != u.Email
            && !await principalRepository.AnyAsync(p => p.Email == u.Email && p.Id != principalId, cancellationToken: ct))
        {
            principal.Email = u.Email;
            principal.EmailVerified = true;
            changed = true;
        }
        var username = u.Username?.Trim().ToLowerInvariant();
        if (principal.UserName is null && !string.IsNullOrEmpty(username)
            && !await principalRepository.AnyAsync(p => p.UserName == username && p.Id != principalId, cancellationToken: ct))
        {
            principal.UserName = username;
            changed = true;
        }
        if (!changed) return;

        principalRepository.Update(principal);
        await principalRepository.SaveChangesAsync(ct);
        await cache.RemoveByTagAsync(LocalPrincipalStore.Tag(principalId), ct);
    }

    private async Task<Guid> ProvisionAsync(ClientData client, FederationProviderData meta, LdapUser u, CancellationToken ct)
    {
        var username = u.Username?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(username) && await principalRepository.AnyAsync(p => p.UserName == username, cancellationToken: ct))
            username = null;

        var principal = new Principal
        {
            Id = EntityId.New(),
            DisplayName = u.DisplayName ?? username ?? u.Email ?? u.Subject,
            UserName = string.IsNullOrEmpty(username) ? null : username,
            Email = u.Email,
            EmailVerified = u.Email is not null,
            Type = PrincipalType.User,
            Status = PrincipalStatus.Active,
            CreatedTime = DateTimeOffset.UtcNow,
        };

        try
        {
            // Lisans kullanici limiti (transaction + kilit). Doluysa "kayit su an kapali".
            await endUserFactory.RunWithinUserLimitAsync(async _ =>
            {
                await principalRepository.AddAsync(principal, ct);
                await identityRepository.AddAsync(NewIdentity(principal.Id, meta, client, u), ct);

                foreach (var role in client.DefaultRoles)
                    await principalRoleRepository.AddAsync(new PrincipalRole
                    {
                        Id = EntityId.New(), PrincipalId = principal.Id, RoleId = role.RoleId, RoleName = role.RoleName, CreatedTime = DateTimeOffset.UtcNow,
                    }, ct);
                foreach (var scopeName in client.DefaultScopes)
                    await principalScopeRepository.AddAsync(new PrincipalScope
                    {
                        Id = EntityId.New(), PrincipalId = principal.Id, Scope = scopeName, CreatedTime = DateTimeOffset.UtcNow,
                    }, ct);

                var endUser = new WriteRelationsRequest { IsolationId = AuthConstants.ManagementPoolId.ToString() };
                endUser.Tuples.Add(new RelationTuple
                {
                    User = AuthConstants.User(principal.Id),
                    Relation = AuthConstants.EndUserRelation,
                    Object = AuthConstants.Pool(client.IsolationId),
                });
                await authorizationClient.WriteRelationsAsync(endUser, cancellationToken: ct);

                await principalRepository.SaveChangesAsync(ct);
            }, ct, LicenseMessageKeys.RegistrationClosed);
        }
        catch (Booster.ExceptionManager.Abstraction.Exceptions.BusinessException e) when (e.Message == LicenseMessageKeys.RegistrationClosed)
        {
            throw Invalid(LicenseMessageKeys.RegistrationClosed);
        }

        logger.LogInformation("LDAP ile ilk giris: kullanici olusturuldu {PrincipalId} (havuz {Pool}).", principal.Id, client.IsolationId);
        return principal.Id;
    }

    private static PrincipalIdentity NewIdentity(Guid principalId, FederationProviderData meta, ClientData client, LdapUser u) => new()
    {
        Id = EntityId.New(),
        PrincipalId = principalId,
        IdentityProviderId = meta.Id,
        IsolationId = client.IsolationId,
        ExternalSubject = u.Subject,
        LastLoginAt = DateTimeOffset.UtcNow,
        CreatedTime = DateTimeOffset.UtcNow,
    };

    private static ValidationException Invalid(string messageKey) => new([new ValidationFailure(string.Empty, messageKey)]);
}
