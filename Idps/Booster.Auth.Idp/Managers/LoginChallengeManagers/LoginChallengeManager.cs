using System.Security.Cryptography;
using Booster.Auth.Common.Internal.Amqp.Events;
using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Models;
using Booster.Auth.Idp.Providers.Abstractions.Stores;
using Booster.Auth.Idp.Providers.Abstractions.Stores.Models;
using Booster.Auth.Idp.Stores.SessionStores;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;
using Booster.Hashing.OneWays.Simple;
using FluentValidation;
using FluentValidation.Results;

using IEventBus = Booster.Amqp.Bus.IEventBus;

namespace Booster.Auth.Idp.Managers.LoginChallengeManagers;

public interface ILoginChallengeManager
{
    /// <summary>
    /// Birincil kimlik doğrulaması geçmiş kullanıcı için oturum açar ya da —
    /// havuz e-posta/telefon doğrulaması veya MFA istiyorsa — ilk adımın kodunu
    /// gönderip yarım girişi saklar.
    /// </summary>
    /// <param name="passwordLogin">
    /// Parola (yerel / LDAP) ve parolasız kodla girişte havuzun MFA politikası uygulanır. Kayıt sonrası
    /// doğrulamada ve federasyonda uygulanmaz (federasyonda ikinci adım dış sağlayıcının sorumluluğundadır).
    /// </param>
    /// <param name="provenChannel">
    /// Parolasız girişte kodun gittiği kanal. Birinci adımı bu kanal kanıtladığı için MFA'da kullanılmaz; ikinci
    /// adım diğer kanaldan istenir, kanal kalmazsa giriş reddedilir. Aksi halde MFA zorunluyken tek kodla oturum
    /// açılıyordu.
    /// </param>
    Task<LoginOutcome> BeginAsync(
        string interactionKey, ClientData client, LocalUser user, string authMethod,
        bool passwordLogin, string? userAgent, string? ipAddress, CancellationToken ct,
        ChallengeChannel? provenChannel = null);

    Task<LoginChallengeView?> GetAsync(string interactionKey, ClientData client, CancellationToken ct);

    Task<LoginOutcome> VerifyAsync(string interactionKey, ClientData client, string code, CancellationToken ct);

    /// <summary>Kodu yeniden gönderir. Bekleme süresi dolmadıysa sessizce hiçbir şey yapmaz.</summary>
    Task ResendAsync(string interactionKey, ClientData client, CancellationToken ct);

    /// <summary>MFA adımında kanal değiştirir (e-posta ↔ SMS) ve yeni kanala kod gönderir.</summary>
    Task SelectChannelAsync(string interactionKey, ClientData client, ChallengeChannel channel, CancellationToken ct);
}

public sealed class LoginChallengeManager(
    ISecureCacheStore<LoginChallengeData> challengeStore,
    ISecureCacheStore<PasswordlessCode> codeStore,
    ISimpleHasher codeHasher,
    ILocalUserStore userStore,
    ISessionStore sessionStore,
    IRequestContextAccessor requestContextAccessor,
    IEventBus eventBus) : ILoginChallengeManager
{
    /// <summary>Yarım girişin ömrü: süresi dolarsa kullanıcı parolayı yeniden girer.</summary>
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);

    /// <summary>"Kodu yeniden gönder" en erken bu kadar sonra yeni kod üretir.</summary>
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    private const int MaxAttempts = 5;

    public async Task<LoginOutcome> BeginAsync(
        string interactionKey, ClientData client, LocalUser user, string authMethod,
        bool passwordLogin, string? userAgent, string? ipAddress, CancellationToken ct,
        ChallengeChannel? provenChannel = null)
    {
        var steps = new List<LoginStep>();

        if (client.Registration.RequireEmailVerification && !string.IsNullOrEmpty(user.Email) && !user.EmailVerified)
            steps.Add(LoginStep.VerifyEmail);

        if (client.Registration.RequirePhoneVerification && !string.IsNullOrEmpty(user.PhoneNumber) && !user.PhoneVerified)
            steps.Add(LoginStep.VerifyPhone);

        var mfaChannels = passwordLogin && client.Mfa.Required
            ? ResolveMfaChannels(client.Mfa.AllowedMethods, user, provenChannel)
            : [];

        // MFA, doğrulama adımı aynı girişte yapılmış olsa bile AYRI bir adımdır:
        // politika "her parolalı girişte ikinci adım" der; doğrulama yapılan
        // ilk girişte atlanması, MFA açıkken MFA'sız oturum açılması demekti.
        if (mfaChannels.Count > 0)
            steps.Add(LoginStep.Mfa);

        var challenge = new LoginChallengeData
        {
            PrincipalId = user.Id,
            ClientId = client.ClientId,
            AuthMethod = authMethod,
            UserAgent = userAgent,
            IpAddress = ipAddress,
            Steps = steps,
            MfaChannels = mfaChannels
        };

        if (steps.Count == 0)
            return await OpenSessionAsync(challenge, client, ct);

        await challengeStore.StoreAsync(interactionKey, challenge, ChallengeLifetime, ct);
        await SendCodeAsync(interactionKey, client, challenge, ct);

        return LoginOutcome.Challenge();
    }

    public async Task<LoginChallengeView?> GetAsync(string interactionKey, ClientData client, CancellationToken ct)
    {
        var challenge = await challengeStore.GetAsync(interactionKey, ct);
        if (challenge is null || challenge.ClientId != client.ClientId)
            return null;

        var user = await GetUserAsync(challenge, client, ct);
        if (user is null)
            return null;

        return new LoginChallengeView(
            challenge.CurrentStep,
            challenge.CurrentChannel,
            Mask(TargetOf(user, challenge.CurrentChannel) ?? string.Empty, challenge.CurrentChannel),
            challenge.CurrentStep == LoginStep.Mfa ? challenge.MfaChannels : []);
    }

    public async Task<LoginOutcome> VerifyAsync(string interactionKey, ClientData client, string code, CancellationToken ct)
    {
        var challenge = await RequireAsync(interactionKey, client, ct);
        var codeKey = CodeKey(interactionKey, challenge);

        var record = await codeStore.GetAsync(codeKey, ct);
        if (record is null || record.PrincipalId != challenge.PrincipalId)
            throw InvalidCode(IdpMessageKeys.LoginCodeInvalid);

        if (record.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            await codeStore.RemoveAsync(codeKey, ct);
            throw InvalidCode(IdpMessageKeys.LoginCodeExpired);
        }

        if (!codeHasher.Verify(code.Trim(), record.CodeHash))
        {
            // Deneme sınırı dolunca kod yakılır: 6 haneli kodu kaba kuvvetle
            // bulmak 5 denemede pratik olarak imkânsızdır.
            if (record.AttemptCount + 1 >= MaxAttempts)
                await codeStore.RemoveAsync(codeKey, ct);
            else
                await codeStore.StoreAsync(codeKey, record with { AttemptCount = record.AttemptCount + 1 },
                    record.ExpiresAt - DateTimeOffset.UtcNow, ct);

            throw InvalidCode(IdpMessageKeys.LoginCodeInvalid);
        }

        await codeStore.RemoveAsync(codeKey, ct);

        var completed = challenge.CurrentStep;
        if (completed is LoginStep.VerifyEmail or LoginStep.VerifyPhone)
        {
            using var _ = requestContextAccessor.BeginSystemContext(client.IsolationId);
            await userStore.MarkIdentifierVerifiedAsync(
                challenge.PrincipalId,
                completed == LoginStep.VerifyEmail ? IdentifierType.Email : IdentifierType.Phone,
                ct);
        }

        var next = challenge with
        {
            Steps = [.. challenge.Steps.Skip(1)],
            MfaCompleted = challenge.MfaCompleted || completed == LoginStep.Mfa
        };

        if (next.Steps.Count == 0)
        {
            await challengeStore.RemoveAsync(interactionKey, ct);
            return await OpenSessionAsync(next, client, ct);
        }

        await challengeStore.StoreAsync(interactionKey, next, ChallengeLifetime, ct);
        await SendCodeAsync(interactionKey, client, next, ct);

        return LoginOutcome.Challenge();
    }

    public async Task ResendAsync(string interactionKey, ClientData client, CancellationToken ct)
    {
        var challenge = await RequireAsync(interactionKey, client, ct);

        var existing = await codeStore.GetAsync(CodeKey(interactionKey, challenge), ct);
        if (existing is not null && existing.ExpiresAt - CodeLifetime + ResendCooldown > DateTimeOffset.UtcNow)
            return;

        await SendCodeAsync(interactionKey, client, challenge, ct);
    }

    public async Task SelectChannelAsync(
        string interactionKey, ClientData client, ChallengeChannel channel, CancellationToken ct)
    {
        var challenge = await RequireAsync(interactionKey, client, ct);

        if (challenge.CurrentStep != LoginStep.Mfa || !challenge.MfaChannels.Contains(channel)
            || challenge.CurrentChannel == channel)
            return;

        var next = challenge with { MfaChannel = channel };
        await challengeStore.StoreAsync(interactionKey, next, ChallengeLifetime, ct);
        await SendCodeAsync(interactionKey, client, next, ct);
    }

    // ------------------------------------------------------------------ iç

    private async Task<LoginOutcome> OpenSessionAsync(LoginChallengeData challenge, ClientData client, CancellationToken ct)
    {
        // Oturum havuzun izolasyonunda yazılır; doğrulama ekranından gelen
        // istekte (giriş işleyicisinin dışında) bu bağlam henüz açık değildir.
        using var _ = requestContextAccessor.BeginSystemContext(client.IsolationId);

        var (session, token) = await sessionStore.CreateAsync(
            principalId: challenge.PrincipalId,
            clientId: client.ClientId,
            ttl: TimeSpan.FromSeconds(client.Session.RefreshTokenTtlSeconds),
            deviceName: null,
            userAgent: challenge.UserAgent,
            ipAddress: challenge.IpAddress,
            authMethod: challenge.MfaCompleted ? $"{challenge.AuthMethod}+mfa" : challenge.AuthMethod,
            ct: ct);

        return LoginOutcome.Session(token, session.ExpiresAt);
    }

    private async Task SendCodeAsync(
        string interactionKey, ClientData client, LoginChallengeData challenge, CancellationToken ct)
    {
        var user = await GetUserAsync(challenge, client, ct)
            ?? throw Invalid(IdpMessageKeys.InvalidInteraction);

        var channel = challenge.CurrentChannel;
        var target = TargetOf(user, channel) ?? throw Invalid(IdpMessageKeys.MfaMethodUnavailable);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var expiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime);

        await codeStore.StoreAsync(CodeKey(interactionKey, challenge), new PasswordlessCode
        {
            PrincipalId = challenge.PrincipalId,
            Target = target,
            CodeHash = codeHasher.Hash(code),
            ExpiresAt = expiresAt
        }, CodeLifetime, ct);

        await eventBus.PublishAsync(new NotificationRequestedEvent
        {
            TenantId = Guid.Empty,
            UserPoolId = client.IsolationId,
            PrincipalId = challenge.PrincipalId,
            TemplateKey = challenge.CurrentStep switch
            {
                LoginStep.VerifyEmail => NotificationTemplateKeys.EmailVerification,
                LoginStep.VerifyPhone => NotificationTemplateKeys.PhoneVerification,
                _ => NotificationTemplateKeys.MfaCode
            },
            Channel = channel == ChallengeChannel.Sms ? NotificationChannel.Sms : NotificationChannel.Email,
            Target = target,
            LanguageCode = requestContextAccessor.Context().AcceptLanguage,
            Data = new Dictionary<string, string>
            {
                ["Code"] = code,
                ["DisplayName"] = user.DisplayName,
                ["ExpiresInMinutes"] = ((int)CodeLifetime.TotalMinutes).ToString()
            }
        }, ct);
    }

    private async Task<LoginChallengeData> RequireAsync(string interactionKey, ClientData client, CancellationToken ct)
    {
        var challenge = await challengeStore.GetAsync(interactionKey, ct);
        if (challenge is null || challenge.ClientId != client.ClientId)
            throw Invalid(IdpMessageKeys.LoginChallengeExpired);

        return challenge;
    }

    private async Task<LocalUser?> GetUserAsync(LoginChallengeData challenge, ClientData client, CancellationToken ct)
    {
        using var _ = requestContextAccessor.BeginSystemContext(client.IsolationId);
        var user = await userStore.GetByIdAsync(challenge.PrincipalId, ct);
        return user is { IsActive: true } ? user : null;
    }

    /// <summary>
    /// Havuzun izin verdiği ve kullanıcının hedefi olan kanallar (e-posta, SMS).
    /// </summary>
    private static List<ChallengeChannel> ResolveMfaChannels(MfaMethod allowed, LocalUser user, ChallengeChannel? provenChannel)
    {
        var channels = new List<ChallengeChannel>(2);

        if (allowed.HasFlag(MfaMethod.Email) && !string.IsNullOrEmpty(user.Email) && provenChannel != ChallengeChannel.Email)
            channels.Add(ChallengeChannel.Email);

        if (allowed.HasFlag(MfaMethod.Sms) && !string.IsNullOrEmpty(user.PhoneNumber) && provenChannel != ChallengeChannel.Sms)
            channels.Add(ChallengeChannel.Sms);

        // Parolasız girişte kodun gittiği kanal dışında kanal kalmadı: ikinci adım yapılamaz, kullanıcı parolayla girmeli.
        if (channels.Count == 0 && provenChannel is not null && (allowed & (MfaMethod.Email | MfaMethod.Sms)) != 0)
            throw Invalid(IdpMessageKeys.MfaSecondChannelUnavailable);

        // Havuz e-posta/SMS ile MFA istiyor ama kullanıcının o kanalda adresi yok:
        // MFA'yı atlamak politikayı delmek olurdu; giriş reddedilir.
        if (channels.Count == 0 && (allowed & (MfaMethod.Email | MfaMethod.Sms)) != 0)
            throw Invalid(IdpMessageKeys.MfaMethodUnavailable);

        return channels;
    }

    private static string? TargetOf(LocalUser user, ChallengeChannel channel)
        => channel == ChallengeChannel.Sms ? user.PhoneNumber : user.Email;

    /// <summary>Her adım/kanal kendi kodunu tutar; e-posta kodu SMS adımında geçmez.</summary>
    private static string CodeKey(string interactionKey, LoginChallengeData challenge)
        => $"login-challenge:{challenge.CurrentStep}:{challenge.CurrentChannel}:{interactionKey}";

    /// <summary>"alper@example.com" → "a***r@example.com", "+905551234567" → "+90*******4567".</summary>
    public static string Mask(string target, ChallengeChannel channel)
    {
        if (string.IsNullOrEmpty(target))
            return string.Empty;

        if (channel == ChallengeChannel.Sms)
            return target.Length <= 7
                ? new string('*', target.Length)
                : string.Concat(target.AsSpan(0, 3), new string('*', target.Length - 7), target.AsSpan(target.Length - 4));

        var at = target.IndexOf('@');
        if (at <= 0)
            return target;

        var local = target[..at];
        var masked = local.Length <= 2
            ? string.Concat(local.AsSpan(0, 1), "***")
            : string.Concat(local.AsSpan(0, 1), "***", local.AsSpan(local.Length - 1));

        return masked + target[at..];
    }

    private static ValidationException Invalid(string messageKey)
        => new([new ValidationFailure(string.Empty, messageKey)]);

    /// <summary>Kod alanının altında gösterilen hata.</summary>
    private static ValidationException InvalidCode(string messageKey)
        => new([new ValidationFailure("Code", messageKey)]);
}
