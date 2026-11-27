using Booster.Auth.Idp.Providers.Abstractions.Stores;
using Booster.Auth.Common.Internal.Notifications;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.InteractionKeyStores.Models;
using Booster.Auth.Idp.Stores.PasswordResetStores;
using Booster.Auth.Idp.Stores.PasswordResetStores.Models;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Booster.Hashing.OneWays.Simple;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features.Password.ForgotPassword;

public sealed class ForgotPasswordHandler(
    ISecureCacheStore<InteractionStateData> interactionStore,
    ISecureCacheStore<PasswordResetData> resetStore,
    IClientDataStore clientDataStore,
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IRequestContextAccessor requestContextAccessor,
    ISimpleHasher simpleHasher,
    NotificationClient notifications,
    Booster.Auth.Idp.Managers.LdapManagers.LdapLoginService ldapLogin) : ICommandHandler<ForgotPasswordCommand>
{
    private static readonly TimeSpan ResetCodeTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ExpiredCodeGrace = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan SendWindow = TimeSpan.FromMinutes(15);
    private const int MaxSendsPerWindow = 5;

    public async Task ExecuteAsync(ForgotPasswordCommand cmd, CancellationToken ct)
    {
        var interaction = await interactionStore.GetAsync(cmd.InteractionKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.InvalidInteraction);

        var client = await clientDataStore.GetByClientKeyAsync(interaction.ClientKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.ClientNotFound);

        var identifier = cmd.Identifier.Trim();

        using var scope = requestContextAccessor.BeginSystemContext(client.IsolationId);

        var principal = await FindPrincipalAsync(identifier, client.Login.AllowedIdentifiers, client.Login.DefaultCountryCode, ct);

        if (principal is null || principal.Status != PrincipalStatus.Active)
            return;

        if (await ldapLogin.IsDirectoryManagedAsync(client, principal.Id, ct))
            return;

        var target = ResolveTarget(principal, out var channel);
        if (target is null)
            return;

        // Kullanici basina gonderim siniri (15 dakikada en fazla 5). Once kullanici basina TEK bekleyen kod vardi:
        // baska sekme / cihazdaki sifirlama kod alamiyor, adresi bilen biri de kurbanin sifirlamasini engelleyebiliyordu.
        var now = DateTimeOffset.UtcNow;
        var pendingKey = PasswordResetKeys.Pending(client.IsolationId, principal.Id);
        var window = await resetStore.GetAsync(pendingKey, ct);
        if (window is null || window.ExpiresAt <= now)
            window = new PasswordResetData
            {
                PrincipalId = principal.Id, IsolationId = client.IsolationId, ClientKey = client.ClientKey,
                CodeHash = string.Empty, ExpiresAt = now.Add(SendWindow)
            };
        if (window.AttemptCount >= MaxSendsPerWindow)
            return;

        var code = GenerateCode();

        var reset = new PasswordResetData
        {
            PrincipalId = principal.Id,
            IsolationId = client.IsolationId,
            ClientKey = client.ClientKey,
            CodeHash = simpleHasher.Hash(code),
            ExpiresAt = DateTimeOffset.UtcNow.Add(ResetCodeTtl)
        };

        // Onbellek kod suresinden uzun: suresi dolan kod "hatali" degil "suresi doldu" desin.
        await resetStore.StoreAsync(PasswordResetKeys.ForInteraction(cmd.InteractionKey), reset, ResetCodeTtl + ExpiredCodeGrace, ct);

        // Sayac: AttemptCount = penceredeki gonderim sayisi.
        await resetStore.StoreAsync(pendingKey, window with { AttemptCount = window.AttemptCount + 1 }, window.ExpiresAt - now, ct);

        notifications.Send(new NotificationRequest
        {
            TenantId = Guid.Empty,
            UserPoolId = client.IsolationId,
            PrincipalId = principal.Id,
            TemplateKey = NotificationTemplateKeys.PasswordReset,
            Channel = channel,
            Target = target,
            LanguageCode = requestContextAccessor.Context().AcceptLanguage,
            Data = new Dictionary<string, string>
            {
                ["Code"] = code,
                ["DisplayName"] = principal.DisplayName,
                ["ExpiresInMinutes"] = ((int)ResetCodeTtl.TotalMinutes).ToString()
            }
        });
    }

    private async Task<Principal?> FindPrincipalAsync(
        string identifier, IReadOnlyList<IdentifierType> allowed, string? defaultCountryCode, CancellationToken ct)
    {
        var normalized = IdentifierNormalizer.Email(identifier);
        var phone = IdentifierNormalizer.Phone(identifier, defaultCountryCode) ?? identifier;

        var query = principalRepository.Query().AsNoTracking();

        if (allowed.Contains(IdentifierType.Email))
        {
            var byEmail = await query.FirstOrDefaultAsync(p => p.Email == normalized, ct);
            if (byEmail is not null) return byEmail;
        }

        if (allowed.Contains(IdentifierType.Username))
        {
            var byUserName = await query.FirstOrDefaultAsync(p => p.UserName == normalized, ct);
            if (byUserName is not null) return byUserName;
        }

        if (allowed.Contains(IdentifierType.Phone))
        {
            var byPhone = await query.FirstOrDefaultAsync(p => p.PhoneNumber == phone, ct);
            if (byPhone is not null) return byPhone;
        }

        return null;
    }

    private static string? ResolveTarget(Principal principal, out NotificationChannel channel)
    {
        if (!string.IsNullOrWhiteSpace(principal.Email))
        {
            channel = NotificationChannel.Email;
            return principal.Email;
        }

        if (!string.IsNullOrWhiteSpace(principal.PhoneNumber))
        {
            channel = NotificationChannel.Sms;
            return principal.PhoneNumber;
        }

        channel = NotificationChannel.Email;
        return null;
    }

    private static string GenerateCode()
        => System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}

public sealed class ForgotPasswordCommand : ICommand
{
    public string InteractionKey { get; set; } = default!;

    public string Identifier { get; set; } = default!;
}

public sealed class ForgotPasswordValidator : Validator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.InteractionKey).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Identifier).NotEmpty().WithMessage(ValidationErrors.NotEmptyError).MaximumLength(320).WithMessage(ValidationErrors.MaxLengthError);
    }
}
