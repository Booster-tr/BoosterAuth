using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Common.Internal.Options;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Manager.RequestContexts;
using Booster.Database.Repositories;
using Booster.Hashing.OneWays.Argon;

namespace Booster.Auth.Idp.Bootstrappers;


public sealed class AdminBootstrapper(
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IAsyncRepository<LocalCredential, IdpDbContext> credentialRepository,
    IPasswordHasher passwordHasher,
    IRequestContextAccessor requestContextAccessor,
    BootstrapOptions bootstrapOptions)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        var exists = await principalRepository.AnyAsync(
            p => p.Id == AuthConstants.AdminUserId,
            withoutGlobalFilter: true,
            cancellationToken: ct);

        if (exists)
            return;

        using var scope = requestContextAccessor.BeginSystemContext(AuthConstants.ManagementPoolId);

        var now = DateTimeOffset.UtcNow;

        var admin = new Principal
        {
            Id = AuthConstants.AdminUserId,
            IsolationId = AuthConstants.ManagementPoolId,

            Type = PrincipalType.User,
            Status = PrincipalStatus.Active,
            DisplayName = AuthConstants.AdminDisplayName,
            UserName = AuthConstants.AdminUserName,
            Email = AuthConstants.AdminEmail,
            EmailVerified = true,
            CreatedTime = now
        };

        var credential = new LocalCredential
        {
            Id = EntityId.New(),
            PrincipalId = admin.Id,
            PasswordHash = passwordHasher.Hash(bootstrapOptions.AdminPassword),
            PasswordChangedAt = now,
            MustChangePassword = true,
            CreatedTime = now
        };

        await principalRepository.AddAsync(admin, ct);
        await credentialRepository.AddAsync(credential, ct);

        await principalRepository.SaveChangesAsync(ct);
    }
}
