using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Z.Grpc;
using Booster.Database.Repositories;

namespace Booster.Auth.Idp.Managers.UserManagers;

/// <summary>
/// Son kullanici olusturmanin TEK noktasi (panelden ekleme, CSV ice aktarma).
/// Principal + havuzun varsayilan rol/kapsamlari + FGA end_user iliskisi.
/// <para>
/// Faz 1.5'te lisans limit kontrolu buraya eklenir ve self-servis kayit ile federasyon ilk girisi de bu sinifa
/// tasinir; boylece kullanici olusturan HER yol tek kontrolden gecer.
/// </para>
/// Kaydetmez: cagiran, davet olayini da ayni islemde (outbox) yazabilsin diye SaveChanges'i kendisi cagirir.
/// Cagiran, havuzun sistem baglaminda (<c>BeginSystemContext(poolId)</c>) olmalidir.
/// </summary>
public sealed class EndUserFactory(
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IAsyncRepository<PrincipalScope, IdpDbContext> principalScopeRepository,
    IAsyncRepository<PrincipalRole, IdpDbContext> principalRoleRepository,
    AuthorizationService.AuthorizationServiceClient authorizationClient)
{
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct) =>
        principalRepository.AnyAsync(p => p.Email == normalizedEmail, cancellationToken: ct);

    /// <summary>Parolasiz kullanici: parolayi davet baglantisindan kendisi belirler.</summary>
    public async Task<Principal> CreateInvitedAsync(ClientData pool, string displayName, string normalizedEmail, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var principal = new Principal
        {
            Id = EntityId.New(),
            DisplayName = displayName.Trim(),
            Email = normalizedEmail,
            // Davet kabul edilince (baglantiya tiklandiginda) dogrulanmis sayilir.
            EmailVerified = false,
            Type = PrincipalType.User,
            Status = PrincipalStatus.Active,
            CreatedTime = now
        };

        await principalRepository.AddAsync(principal, ct);

        foreach (var role in pool.DefaultRoles)
            await principalRoleRepository.AddAsync(new PrincipalRole
            {
                Id = EntityId.New(),
                PrincipalId = principal.Id,
                RoleId = role.RoleId,
                RoleName = role.RoleName,
                CreatedTime = now
            }, ct);

        foreach (var scope in pool.DefaultScopes)
            await principalScopeRepository.AddAsync(new PrincipalScope
            {
                Id = EntityId.New(),
                PrincipalId = principal.Id,
                Scope = scope,
                CreatedTime = now
            }, ct);

        var endUser = new WriteRelationsRequest { IsolationId = AuthConstants.ManagementPoolId.ToString() };
        endUser.Tuples.Add(new RelationTuple
        {
            User = AuthConstants.User(principal.Id),
            Relation = AuthConstants.EndUserRelation,
            Object = AuthConstants.Pool(pool.IsolationId)
        });
        await authorizationClient.WriteRelationsAsync(endUser, cancellationToken: ct);

        return principal;
    }
}
