using Booster.Auth.Common.Internal.Amqp.Events;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.InteractionKeyStores.Models;
using Booster.Auth.Idp.Stores.PasswordResetStores.Models;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Booster.Hashing.OneWays.Simple;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using IEventBus = Booster.Amqp.Bus.IEventBus;

namespace Booster.Auth.Idp.Features.Password.ForgotPassword;

public sealed class ForgotPasswordHandler(
    ISecureCacheStore<InteractionStateData> interactionStore,
    ISecureCacheStore<PasswordResetData> resetStore,
    IClientDataStore clientDataStore,
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IRequestContextAccessor requestContextAccessor,
    ISimpleHasher simpleHasher,
    IEventBus eventBus) : ICommandHandler<ForgotPasswordCommand>
{
    private static readonly TimeSpan ResetCodeTtl = TimeSpan.FromMinutes(15);

    public async Task ExecuteAsync(ForgotPasswordCommand cmd, CancellationToken ct)
    {
        var interaction = await interactionStore.GetAsync(cmd.InteractionKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.InvalidInteraction);

        var client = await clientDataStore.GetByClientKeyAsync(interaction.ClientKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.ClientNotFound);

        var identifier = cmd.Identifier.Trim();

        using var scope = requestContextAccessor.BeginSystemContext(client.IsolationId);

        var principal = await FindPrincipalAsync(identifier, client.Login.AllowedIdentifiers, ct);

        if (principal is null || principal.Status != PrincipalStatus.Active)
            return;

        var target = ResolveTarget(principal, out var channel);
        if (target is null)
            return;

        var code = GenerateCode();

        await resetStore.StoreAsync(
            cmd.InteractionKey,
            new PasswordResetData
            {
                PrincipalId = principal.Id,
                IsolationId = client.IsolationId,
                ClientKey = client.ClientKey,
                CodeHash = simpleHasher.Hash(code)
            },
            ResetCodeTtl,
            ct);

        await eventBus.PublishAsync(new NotificationRequestedEvent
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
        }, ct);
    }

    private async Task<Principal?> FindPrincipalAsync(
        string identifier, IReadOnlyList<IdentifierType> allowed, CancellationToken ct)
    {
        var normalized = identifier.ToLowerInvariant();

        var query = principalRepository.Query().AsNoTracking();

        if (allowed.Contains(IdentifierType.Email))
        {
            var byEmail = await query.FirstOrDefaultAsync(p => p.Email == normalized, ct);
            if (byEmail is not null) return byEmail;
        }

        if (allowed.Contains(IdentifierType.Username))
        {
            var byUserName = await query.FirstOrDefaultAsync(p => p.UserName == identifier, ct);
            if (byUserName is not null) return byUserName;
        }

        if (allowed.Contains(IdentifierType.Phone))
        {
            var byPhone = await query.FirstOrDefaultAsync(p => p.PhoneNumber == identifier, ct);
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
        RuleFor(x => x.Identifier).NotEmpty().MaximumLength(320).WithMessage(ValidationErrors.NotEmptyError);
    }
}
