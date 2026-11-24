using Booster.Auth.Common.Internal.Auditing;
using Booster.Auth.Common.Internal.Amqp.Extensions;
using Booster.Auth.Common.Internal.Amqp.Events;
using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Features._base.Enums;
using Booster.Auth.Idp.Managers.LdapManagers;
using Booster.Auth.Idp.Managers.PasswordManagers;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.PrincipalStores;
using Booster.Auth.Idp.Stores.SessionStores;
using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Grpc;
using Booster.Caching.Abstraction;
using Booster.Hashing.OneWays.Argon;
using Booster.Hashing.OneWays.Simple;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using IEventBus = Booster.Amqp.Bus.IEventBus;

namespace Booster.Auth.Idp.Managers.AccountManagers;

/// <summary>
/// Son kullanicinin kendi hesabi ("Hesabim", /ui/account): profil, e-posta / telefon degisikligi (kodla), parola,
/// oturumlar ve hesap silme. Kimlik IdP oturum cerezinden gelir; her islem cerezdeki oturumun havuzunda calisir.
/// <para>
/// Kurum dizini (LDAP) kullanicilarinda ad / e-posta dizinden gelir ve parola dizindedir: bu alanlar degistirilemez.
/// </para>
/// </summary>
public sealed class AccountService(
    IClientDataStore clientDataStore,
    ISessionStore sessionStore,
    IdpDbContext db,
    IPasswordPolicyManager passwordPolicyManager,
    IPasswordHasher passwordHasher,
    ISimpleHasher simpleHasher,
    ISecureCacheStore<ContactChangeData> changeStore,
    ICacheService cacheService,
    IRequestContextAccessor requestContextAccessor,
    IEventBus eventBus,
    LdapLoginService ldapLogin,
    AuthorizationService.AuthorizationServiceClient authorizationClient,
    ILogger<AccountService> logger,
    AuditLog audit)
{
    private static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(15);
    private const int MaxCodeAttempts = 5;

    // ---------------------------------------------------------------------------------------------------------
    // Baglam
    // ---------------------------------------------------------------------------------------------------------

    public async Task<ClientData?> FindClientAsync(string? clientKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientKey))
            return null;
        var client = await clientDataStore.GetByClientKeyAsync(clientKey, ct);
        return client is { IsActive: true } ? client : null;
    }

    /// <summary>
    /// Donus adresi yalnizca uygulamanin kayitli yonlendirme / cikis adresleriyle AYNI kokene (scheme + host + port)
    /// sahipse kabul edilir; boylece sayfa acik yonlendirici (open redirect) olarak kullanilamaz.
    /// </summary>
    public static bool IsAllowedReturnUrl(ClientData client, string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl))
            return true;
        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var target))
            return false;

        return client.RedirectUris.Concat(client.PostLogoutRedirectUris)
            .Select(u => Uri.TryCreate(u, UriKind.Absolute, out var r) ? r : null)
            .Any(r => r is not null
                      && string.Equals(r.Scheme, target.Scheme, StringComparison.OrdinalIgnoreCase)
                      && string.Equals(r.Host, target.Host, StringComparison.OrdinalIgnoreCase)
                      && r.Port == target.Port);
    }

    /// <summary>Cerezdeki oturum bu uygulamanin havuzunda gecerliyse hesap baglami; degilse null (giris gerekir).</summary>
    public async Task<AccountContext?> ResolveAsync(ClientData client, string? sessionToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sessionToken))
            return null;

        var session = await sessionStore.ResolveAsync(sessionToken, ct);
        if (session is null || session.IsolationId != client.IsolationId)
            return null;

        using var scope = requestContextAccessor.BeginSystemContext(client.IsolationId);

        var principal = await db.Principals.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == session.PrincipalId && p.Type == PrincipalType.User, ct);
        if (principal is null || principal.Status != PrincipalStatus.Active)
            return null;

        var hasPassword = await db.LocalCredentials.AnyAsync(c => c.PrincipalId == principal.Id, ct);
        var directoryManaged = await ldapLogin.IsDirectoryManagedAsync(client, principal.Id, ct);

        return new AccountContext(client, session, principal, hasPassword, directoryManaged);
    }

    // ---------------------------------------------------------------------------------------------------------
    // Profil
    // ---------------------------------------------------------------------------------------------------------

    public async Task UpdateDisplayNameAsync(AccountContext ctx, string? displayName, CancellationToken ct)
    {
        if (ctx.DirectoryManaged)
            throw Invalid(string.Empty, IdpMessageKeys.AccountDirectoryManaged);

        var name = displayName?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 300)
            throw Invalid("DisplayName", IdpMessageKeys.AccountDisplayNameInvalid);

        using var scope = requestContextAccessor.BeginSystemContext(ctx.Client.IsolationId);
        var principal = await db.Principals.FirstAsync(p => p.Id == ctx.Principal.Id, ct);
        principal.DisplayName = name;
        principal.UpdatedTime = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await cacheService.RemoveByTagAsync(LocalPrincipalStore.Tag(principal.Id), ct);
    }

    // ---------------------------------------------------------------------------------------------------------
    // E-posta / telefon degisikligi: yeni adrese kod gonderilir, kod girilince kaydedilir.
    // ---------------------------------------------------------------------------------------------------------

    public async Task StartContactChangeAsync(AccountContext ctx, ContactKind kind, string? value, string? currentPassword,
        CancellationToken ct)
    {
        if (kind == ContactKind.Email && ctx.DirectoryManaged)
            throw Invalid(string.Empty, IdpMessageKeys.AccountDirectoryManaged);

        // Hassas islem: parolasi olan kullanici parolasini yeniden girer (acik birakilmis bir oturum ele gecirilse de
        // e-posta degistirilip hesap devralinamasin).
        await VerifyCurrentPasswordAsync(ctx, currentPassword, ct);

        var normalized = kind == ContactKind.Email ? NormalizeEmail(value) : NormalizePhone(value, ctx.Client.Login.DefaultCountryCode);
        if (normalized is null)
            throw Invalid("Value", kind == ContactKind.Email ? IdpMessageKeys.EmailInvalid : IdpMessageKeys.AccountPhoneInvalid);

        var current = kind == ContactKind.Email ? ctx.Principal.Email : ctx.Principal.PhoneNumber;
        if (string.Equals(current, normalized, StringComparison.OrdinalIgnoreCase))
            throw Invalid("Value", IdpMessageKeys.AccountContactUnchanged);

        using var scope = requestContextAccessor.BeginSystemContext(ctx.Client.IsolationId);
        if (await IsTakenAsync(kind, normalized, ctx.Principal.Id, ct))
            throw Invalid("Value", IdpMessageKeys.IdentifierAlreadyExists);

        var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        await changeStore.StoreAsync(ChangeKey(ctx), new ContactChangeData
        {
            PrincipalId = ctx.Principal.Id,
            Kind = kind,
            Value = normalized,
            CodeHash = simpleHasher.Hash(code),
            ExpiresAt = DateTimeOffset.UtcNow.Add(CodeTtl)
        }, CodeTtl, ct);

        await eventBus.PublishAsync(new NotificationRequestedEvent
        {
            TenantId = Guid.Empty,
            UserPoolId = ctx.Client.IsolationId,
            PrincipalId = ctx.Principal.Id,
            TemplateKey = kind == ContactKind.Email ? NotificationTemplateKeys.EmailChange : NotificationTemplateKeys.PhoneVerification,
            Channel = kind == ContactKind.Email ? NotificationChannel.Email : NotificationChannel.Sms,
            Target = normalized,
            LanguageCode = requestContextAccessor.Context().AcceptLanguage,
            Data = new Dictionary<string, string>
            {
                ["Code"] = code,
                ["DisplayName"] = ctx.Principal.DisplayName,
                ["ExpiresInMinutes"] = ((int)CodeTtl.TotalMinutes).ToString()
            }
        }, ct);
    }

    /// <summary>Bekleyen degisiklik (kod ekrani icin); yoksa null.</summary>
    public async Task<ContactChangeData?> GetPendingChangeAsync(AccountContext ctx, ContactKind kind, CancellationToken ct)
    {
        var pending = await changeStore.GetAsync(ChangeKey(ctx), ct);
        return pending is not null && pending.Kind == kind && pending.PrincipalId == ctx.Principal.Id
                                   && pending.ExpiresAt > DateTimeOffset.UtcNow
            ? pending
            : null;
    }

    public async Task CompleteContactChangeAsync(AccountContext ctx, ContactKind kind, string? code, CancellationToken ct)
    {
        var pending = await GetPendingChangeAsync(ctx, kind, ct)
            ?? throw Invalid("Code", IdpMessageKeys.AccountCodeInvalid);

        if (string.IsNullOrWhiteSpace(code) || !simpleHasher.Verify(code.Trim(), pending.CodeHash))
        {
            if (pending.AttemptCount + 1 >= MaxCodeAttempts)
                await changeStore.RemoveAsync(ChangeKey(ctx), ct);
            else
                await changeStore.StoreAsync(ChangeKey(ctx), pending with { AttemptCount = pending.AttemptCount + 1 },
                    pending.ExpiresAt - DateTimeOffset.UtcNow, ct);
            throw Invalid("Code", IdpMessageKeys.AccountCodeInvalid);
        }

        using var scope = requestContextAccessor.BeginSystemContext(ctx.Client.IsolationId);

        // Kod beklerken ayni adresi baska biri almis olabilir.
        if (await IsTakenAsync(kind, pending.Value, ctx.Principal.Id, ct))
        {
            await changeStore.RemoveAsync(ChangeKey(ctx), ct);
            throw Invalid("Code", IdpMessageKeys.IdentifierAlreadyExists);
        }

        var principal = await db.Principals.FirstAsync(p => p.Id == ctx.Principal.Id, ct);
        if (kind == ContactKind.Email)
        {
            principal.Email = pending.Value;
            principal.EmailVerified = true;
        }
        else
        {
            principal.PhoneNumber = pending.Value;
            principal.PhoneVerified = true;
        }
        principal.UpdatedTime = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        await changeStore.RemoveAsync(ChangeKey(ctx), ct);
        await cacheService.RemoveByTagAsync(LocalPrincipalStore.Tag(principal.Id), ct);
        audit.Success(kind == ContactKind.Email ? "account.email_changed" : "account.phone_changed",
            new AuditEntry(PrincipalId: principal.Id, PoolId: ctx.Client.IsolationId, ClientId: ctx.Client.ClientId));
    }

    // ---------------------------------------------------------------------------------------------------------
    // Parola
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>Parolayi degistirir; bu tarayici disindaki tum oturumlari kapatir.</summary>
    public async Task ChangePasswordAsync(AccountContext ctx, string? currentPassword, string? newPassword,
        string? newPasswordConfirm, CancellationToken ct)
    {
        if (ctx.DirectoryManaged)
            throw Invalid(string.Empty, IdpMessageKeys.AccountDirectoryManaged);
        if (!ctx.HasPassword)
            throw Invalid(string.Empty, IdpMessageKeys.AccountNoPassword);

        await VerifyCurrentPasswordAsync(ctx, currentPassword, ct);

        if (string.IsNullOrEmpty(newPassword) || newPassword.Any(char.IsWhiteSpace))
            throw Invalid("NewPassword", string.IsNullOrEmpty(newPassword)
                ? Booster.ExceptionManager.Abstraction.Constants.ValidationErrors.NotEmptyError
                : IdpMessageKeys.PasswordContainsWhitespace);
        if (!string.Equals(newPassword, newPasswordConfirm, StringComparison.Ordinal))
            throw Invalid("NewPasswordConfirm", IdpMessageKeys.PasswordsDoNotMatch);

        passwordPolicyManager.Validate(ctx.Client.Password, newPassword, "NewPassword");

        using var scope = requestContextAccessor.BeginSystemContext(ctx.Client.IsolationId);

        await passwordPolicyManager.EnsureNotReusedAsync(ctx.Client.Password, ctx.Principal.Id, newPassword, "NewPassword", ct);

        var credential = await db.LocalCredentials.FirstAsync(c => c.PrincipalId == ctx.Principal.Id, ct);
        var newHash = passwordHasher.Hash(newPassword);
        credential.PasswordHash = newHash;
        credential.PasswordChangedAt = DateTimeOffset.UtcNow;
        credential.MustChangePassword = false;
        credential.FailedAttemptCount = 0;
        credential.LockedUntil = null;

        await passwordPolicyManager.RecordAsync(ctx.Principal.Id, newHash, ctx.Client.Password, ct);
        await RevokeSessionsAsync(ctx.Principal.Id, exceptSessionId: ctx.Session.Id, ct);
        await db.SaveChangesAsync(ct);
        await cacheService.RemoveByTagAsync(LocalPrincipalStore.Tag(ctx.Principal.Id), ct);
        audit.Success("account.password_changed",
            new AuditEntry(PrincipalId: ctx.Principal.Id, PoolId: ctx.Client.IsolationId, ClientId: ctx.Client.ClientId));
    }

    // ---------------------------------------------------------------------------------------------------------
    // Oturumlar
    // ---------------------------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<AccountSessionItem>> ListSessionsAsync(AccountContext ctx, CancellationToken ct)
    {
        using var scope = requestContextAccessor.BeginSystemContext(ctx.Client.IsolationId);
        var now = DateTimeOffset.UtcNow;
        var sessions = await db.Sessions.AsNoTracking()
            .Where(s => s.PrincipalId == ctx.Principal.Id && s.Status == SessionStatus.Active && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastActivityAt)
            .Take(50)
            .ToListAsync(ct);

        return sessions
            .Select(s => new AccountSessionItem(s.Id == ctx.Session.Id, s.UserAgent, s.IpAddress, s.CreatedTime, s.LastActivityAt))
            .OrderByDescending(s => s.IsCurrent)
            .ToList();
    }

    /// <summary>Bu tarayici disindaki tum oturumlari (ve yenileme token'larini) kapatir. Kapatilan sayi doner.</summary>
    public async Task<int> SignOutOtherSessionsAsync(AccountContext ctx, CancellationToken ct)
    {
        using var scope = requestContextAccessor.BeginSystemContext(ctx.Client.IsolationId);
        var count = await RevokeSessionsAsync(ctx.Principal.Id, exceptSessionId: ctx.Session.Id, ct);
        await db.SaveChangesAsync(ct);
        audit.Success("account.sessions_revoked", new AuditEntry(PrincipalId: ctx.Principal.Id, PoolId: ctx.Client.IsolationId, Reason: $"{count} oturum"));
        return count;
    }

    // ---------------------------------------------------------------------------------------------------------
    // Hesap silme
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>Havuz politikasi izin veriyorsa ve yonetim havuzu (panel yoneticileri) degilse.</summary>
    public static bool CanDelete(AccountContext ctx) =>
        ctx.Client.Registration.AllowSelfDeletion && ctx.Client.IsolationId != AuthConstants.ManagementPoolId;

    /// <summary>
    /// Hesabi kalici olarak siler: oturumlar kapatilir; parola, dis kimlik baglantilari, izinler, roller ve yetki
    /// iliskileri silinir; kullanici kaydi anonimlestirilip silinmis isaretlenir (e-posta / kullanici adi / telefon
    /// bosaltilir, ayni bilgilerle yeniden kayit olunabilir). Lisans kullanici sayisindan duser.
    /// </summary>
    public async Task DeleteAccountAsync(AccountContext ctx, string? currentPassword, bool confirmed, CancellationToken ct)
    {
        if (!CanDelete(ctx))
            throw Invalid(string.Empty, IdpMessageKeys.AccountDeletionDisabled);
        if (!confirmed)
            throw Invalid("Confirm", IdpMessageKeys.AccountDeletionConfirmRequired);

        // Kurum dizini kullanicisinin parolasi dizinde; burada yalnizca onay istenir.
        if (!ctx.DirectoryManaged)
            await VerifyCurrentPasswordAsync(ctx, currentPassword, ct);

        await DeletePrincipalAsync(ctx.Principal.Id, ctx.Client.IsolationId, ct);
        await changeStore.RemoveAsync(ChangeKey(ctx), ct);
    }

    /// <summary>
    /// Silmenin cekirdegi; son kullanicinin kendi hesap silmesi ve panelden kullanici silme ortak kullanir.
    /// Cagiran yetkiyi ve kullanicinin havuzda oldugunu dogrulamis olmalidir.
    /// </summary>
    /// <summary>
    /// Panelden askiya alma / etkinlestirme. Askiya alinan kullanici giris yapamaz, kod alamaz, token yenileyemez; tum
    /// oturumlari ve yenileme token'lari hemen kapatilir. Kayit, roller ve iliskiler korunur (geri acilabilir).
    /// </summary>
    public async Task SetStatusAsync(Guid principalId, Guid poolId, PrincipalStatus status, CancellationToken ct)
    {
        using (requestContextAccessor.BeginSystemContext(poolId))
        {
            var principal = await db.Principals.FirstAsync(p => p.Id == principalId, ct);
            if (principal.Status == status)
                return;

            principal.Status = status;
            principal.UpdatedTime = DateTimeOffset.UtcNow;
            if (status != PrincipalStatus.Active)
                await RevokeSessionsAsync(principalId, exceptSessionId: null, ct);

            await db.SaveChangesAsync(ct);
        }

        audit.Success(status == PrincipalStatus.Active ? "account.activated" : "account.suspended", new AuditEntry(PrincipalId: principalId, PoolId: poolId));

        await cacheService.RemoveByTagAsync(LocalPrincipalStore.Tag(principalId), ct);
    }

    public async Task DeletePrincipalAsync(Guid principalId, Guid poolId, CancellationToken ct)
    {
        using (requestContextAccessor.BeginSystemContext(poolId))
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            await RevokeSessionsAsync(principalId, exceptSessionId: null, ct);

            await db.LocalCredentials.Where(x => x.PrincipalId == principalId).ExecuteDeleteAsync(ct);
            await db.PasswordHistories.Where(x => x.PrincipalId == principalId).ExecuteDeleteAsync(ct);
            await db.PrincipalIdentities.Where(x => x.PrincipalId == principalId).ExecuteDeleteAsync(ct);
            await db.Consents.Where(x => x.PrincipalId == principalId).ExecuteDeleteAsync(ct);
            await db.PrincipalRoles.Where(x => x.PrincipalId == principalId).ExecuteDeleteAsync(ct);
            await db.PrincipalScopes.Where(x => x.PrincipalId == principalId).ExecuteDeleteAsync(ct);

            var principal = await db.Principals.FirstAsync(p => p.Id == principalId, ct);
            var now = DateTimeOffset.UtcNow;
            principal.DisplayName = "-";
            principal.Email = null;
            principal.EmailVerified = false;
            principal.UserName = null;
            principal.PhoneNumber = null;
            principal.PhoneVerified = false;
            principal.UpdatedTime = now;
            principal.DeletedTime = now;

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        // Panelden ya da kullanicinin kendi istegiyle silme (kimin yaptigi istegin token / oturum sahibinden).
        audit.Success("account.deleted", new AuditEntry(PrincipalId: principalId, PoolId: poolId));
        await cacheService.RemoveByTagAsync(LocalPrincipalStore.Tag(principalId), ct);
        await DeleteRelationsAsync(principalId, poolId, ct);

        // Kisisel veri tutan diger servisler (Notification: gonderilen e-posta / SMS kayitlari) temizlesin. Commit'ten sonra;
        // Kafka hatasi silmeyi bozmaz, loglanir.
        await eventBus.PublishOrLogAsync(new PrincipalDeletedEvent { UserPoolId = poolId, PrincipalId = principalId }, logger, ct);
    }

    /// <summary>
    /// Yetki iliskileri: yonetim deposundaki "havuzun son kullanicisi" ve havuzun kendi yetki semasindaki tum
    /// iliskiler. En iyi caba: AuthZ'ye ulasilamazsa hesap yine silinmis sayilir (kalan iliskiler silinmis bir
    /// kullaniciyi gosterir, kimseye yetki vermez) ve durum loglanir.
    /// </summary>
    private async Task DeleteRelationsAsync(Guid principalId, Guid poolId, CancellationToken ct)
    {
        var user = AuthConstants.User(principalId);
        try
        {
            var endUser = new WriteRelationsRequest { IsolationId = AuthConstants.ManagementPoolId.ToString() };
            endUser.Tuples.Add(new RelationTuple { User = user, Relation = AuthConstants.EndUserRelation, Object = AuthConstants.Pool(poolId) });
            await authorizationClient.DeleteRelationsAsync(endUser, cancellationToken: ct);

            string? continuation = null;
            do
            {
                var page = await authorizationClient.ReadRelationsAsync(new ReadRelationsRequest
                {
                    IsolationId = poolId.ToString(),
                    User = user,
                    PageSize = 100,
                    ContinuationToken = continuation ?? string.Empty
                }, cancellationToken: ct);

                if (page.Results.Count > 0)
                {
                    var delete = new WriteRelationsRequest { IsolationId = poolId.ToString() };
                    delete.Tuples.AddRange(page.Results.Select(r => new RelationTuple { User = r.User, Relation = r.Relation, Object = r.Object }));
                    await authorizationClient.DeleteRelationsAsync(delete, cancellationToken: ct);
                }

                continuation = string.IsNullOrEmpty(page.ContinuationToken) ? null : page.ContinuationToken;
            } while (continuation is not null);
        }
        catch (global::Grpc.Core.RpcException ex) when (ex.StatusCode == global::Grpc.Core.StatusCode.FailedPrecondition)
        {
            // Havuzun yetki semasi (store) hic olusturulmamis: silinecek iliski de yok.
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Silinen hesabin yetki iliskileri temizlenemedi. Principal={PrincipalId} Pool={PoolId}", principalId, poolId);
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // Yardimcilar
    // ---------------------------------------------------------------------------------------------------------

    private async Task VerifyCurrentPasswordAsync(AccountContext ctx, string? currentPassword, CancellationToken ct)
    {
        if (!ctx.HasPassword || ctx.DirectoryManaged)
            return;

        using var scope = requestContextAccessor.BeginSystemContext(ctx.Client.IsolationId);
        var credential = await db.LocalCredentials
            .FirstOrDefaultAsync(c => c.PrincipalId == ctx.Principal.Id, ct);

        // Giris ekraniyla ayni kilit: oturum cerezini ele geciren biri bu alanda parolayi deneme yanilmayla
        // bulamasin. Sayac ve kilit girisle ortaktir (havuzun MaxFailedAttempts / LockoutSeconds politikasi).
        if (credential?.LockedUntil is { } lockedUntil && lockedUntil > DateTimeOffset.UtcNow)
            throw Invalid("CurrentPassword", IdpMessageKeys.AccountLocked);

        if (credential is not null && !string.IsNullOrEmpty(currentPassword)
            && passwordHasher.Verify(currentPassword, credential.PasswordHash))
        {
            if (credential.FailedAttemptCount != 0 || credential.LockedUntil is not null)
                await SaveLockoutAsync(credential, 0, null, ct);
            return;
        }

        if (credential is not null)
        {
            var failed = credential.FailedAttemptCount + 1;
            var locked = failed >= ctx.Client.Login.MaxFailedAttempts;
            await SaveLockoutAsync(credential, failed,
                locked ? DateTimeOffset.UtcNow.AddSeconds(ctx.Client.Login.LockoutSeconds) : null, ct);

            if (locked)
                throw Invalid("CurrentPassword", IdpMessageKeys.AccountLocked);
        }

        throw Invalid("CurrentPassword", IdpMessageKeys.CurrentPasswordInvalid);
    }

    private async Task SaveLockoutAsync(LocalCredential credential, int failedCount, DateTimeOffset? lockedUntil, CancellationToken ct)
    {
        credential.FailedAttemptCount = failedCount;
        credential.LockedUntil = lockedUntil;
        await db.SaveChangesAsync(ct);
        await cacheService.RemoveByTagAsync(LocalPrincipalStore.Tag(credential.PrincipalId), ct);
    }

    /// <summary>Oturumlari ve yenileme token'larini iptal eder (kaydetmez). Cagiran sistem baglaminda olmalidir.</summary>
    private async Task<int> RevokeSessionsAsync(Guid principalId, Guid? exceptSessionId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var sessions = await db.Sessions
            .Where(s => s.PrincipalId == principalId && s.Status == SessionStatus.Active
                        && (exceptSessionId == null || s.Id != exceptSessionId))
            .ToListAsync(ct);
        if (sessions.Count == 0)
            return 0;

        var ids = sessions.Select(s => s.Id).ToList();
        foreach (var s in sessions)
        {
            s.Status = SessionStatus.Revoked;
            s.RevokedAt = now;
        }

        var tokens = await db.RefreshTokens.Where(r => ids.Contains(r.SessionId) && r.RevokedAt == null).ToListAsync(ct);
        foreach (var t in tokens)
            t.RevokedAt = now;

        foreach (var id in ids)
            await cacheService.RemoveByTagAsync(SessionStore.Tag(id), ct);

        return sessions.Count;
    }

    private Task<bool> IsTakenAsync(ContactKind kind, string value, Guid selfId, CancellationToken ct) =>
        kind == ContactKind.Email
            ? db.Principals.AnyAsync(p => p.Email == value && p.Id != selfId, ct)
            : db.Principals.AnyAsync(p => p.PhoneNumber == value && p.Id != selfId, ct);

    private static string ChangeKey(AccountContext ctx) => $"account-contact:{ctx.Session.Id}";

    private static string? NormalizeEmail(string? value)
    {
        var v = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(v) || v.Length > 320)
            return null;
        var at = v.IndexOf('@');
        return at > 0 && at == v.LastIndexOf('@') && v.IndexOf('.', at) > at + 1 && !v.EndsWith('.') && !v.Any(char.IsWhiteSpace)
            ? v
            : null;
    }

    /// <summary>"+90 532 000 00 00" -> "+905320000000". Ulke kodsuz numaraya havuzun varsayilan ulke kodu eklenir.</summary>
    private static string? NormalizePhone(string? value, string? defaultCountryCode)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = value.Trim();
        var digits = new string(v.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            return null;

        string full;
        if (v.StartsWith('+'))
            full = digits;
        else if (v.StartsWith("00"))
            full = digits[2..];
        else
        {
            var cc = new string((defaultCountryCode ?? string.Empty).Where(char.IsDigit).ToArray());
            if (cc.Length == 0)
                return null;
            full = cc + digits.TrimStart('0');
        }

        return full.Length is >= 8 and <= 15 ? "+" + full : null;
    }

    private static ValidationException Invalid(string property, string messageKey)
        => new([new ValidationFailure(property, messageKey)]);
}

public enum ContactKind { Email = 1, Phone = 2 }

public sealed record AccountContext(
    ClientData Client,
    Session Session,
    Principal Principal,
    bool HasPassword,
    bool DirectoryManaged);

public sealed record AccountSessionItem(
    bool IsCurrent,
    string? UserAgent,
    string? IpAddress,
    DateTimeOffset CreatedTime,
    DateTimeOffset LastActivityAt);

public sealed record ContactChangeData
{
    public required Guid PrincipalId { get; init; }
    public required ContactKind Kind { get; init; }
    public required string Value { get; init; }
    public required string CodeHash { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public int AttemptCount { get; init; }
}
