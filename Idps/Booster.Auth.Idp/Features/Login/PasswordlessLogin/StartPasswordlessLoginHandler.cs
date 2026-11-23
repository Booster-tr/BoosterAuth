using Booster.Auth.Idp.Features._base;
using Booster.Auth.Common.Internal.Amqp.Events;
using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;
using Booster.Auth.Idp.Providers.Abstractions;
using Booster.Auth.Idp.Providers.PhoneCode;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Models;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.InteractionKeyStores.Models;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;

using IEventBus = Booster.Amqp.Bus.IEventBus;

namespace Booster.Auth.Idp.Features.Login.PasswordlessLogin;

public sealed class StartPasswordlessLoginHandler(
    ISecureCacheStore<InteractionStateData> interactionStore,
    IClientDataStore clientDataStore,
    IProviderFactory providerFactory,
    IRequestContextAccessor requestContextAccessor,
    IEventBus eventBus,
    IdpDbContext db) : ICommandHandler<StartPasswordlessLoginCommand>
{
    public async Task ExecuteAsync(StartPasswordlessLoginCommand cmd, CancellationToken ct)
    {
        var interaction = await interactionStore.GetAsync(cmd.InteractionKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.InvalidInteraction);

        var client = await clientDataStore.GetByClientKeyAsync(interaction.ClientKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.ClientNotFound);

        var provider = providerFactory.GetPasswordlessProvider(cmd.ProviderType)
            ?? throw new AuthorizeException(IdpMessageKeys.ProviderNotAvailable);

        if (!client.Login.AllowedLoginProviders.HasFlag(provider.LoginProvider))
            throw new AuthorizeException(IdpMessageKeys.ProviderNotAllowed);

        if (!client.Login.AllowedIdentifiers.Contains(provider.IdentifierType))
            throw new AuthorizeException(IdpMessageKeys.ProviderNotAllowed);

        using var scope = requestContextAccessor.BeginSystemContext(client.IsolationId);

        var result = await provider.StartAsync(
            new PasswordlessStartRequest
            {
                Identifier = cmd.Identifier,
                ChallengeKey = cmd.InteractionKey
            },
            BuildConfig(provider.ProviderType, client),
            ct);

        if (!result.Succeeded)
            return;
        if (result.Code is null)
            return;

        await eventBus.PublishAsync(new NotificationRequestedEvent
        {
            TenantId = Guid.Empty,
            UserPoolId = client.IsolationId,
            PrincipalId = result.PrincipalId,
            TemplateKey = ResolveTemplateKey(provider.IdentifierType),
            Channel = ResolveChannel(provider.IdentifierType),
            Target = result.Target!,
            LanguageCode = requestContextAccessor.Context().AcceptLanguage,
            Data = new Dictionary<string, string>
            {
                ["Code"] = result.Code!,
                ["DisplayName"] = result.Target!,
                ["ExpiresInMinutes"] =
                    ((int)Math.Ceiling((result.ExpiresAt!.Value - DateTimeOffset.UtcNow).TotalMinutes)).ToString()
            }
        }, ct);
        // Outbox: olay ancak SaveChanges ile kaydedilir (bu akista baska DB degisikligi yok).
        await db.SaveChangesAsync(ct);
    }

    private static ProviderConfig BuildConfig(string providerType, ClientData client)
    {
        var settings = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(client.Login.DefaultCountryCode))
            settings[PhoneCodeProvider.DefaultCountryCodeSetting] = client.Login.DefaultCountryCode;

        return new ProviderConfig
        {
            ProviderKey = providerType,
            ProviderType = providerType,
            IsolationId = client.IsolationId,
            IsActive = true,
            Settings = settings
        };
    }

    private static string ResolveTemplateKey(IdentifierType identifierType) => identifierType switch
    {
        IdentifierType.Phone => NotificationTemplateKeys.PhoneLoginCode,
        _ => NotificationTemplateKeys.EmailLoginCode
    };

    private static NotificationChannel ResolveChannel(IdentifierType identifierType) => identifierType switch
    {
        IdentifierType.Phone => NotificationChannel.Sms,
        _ => NotificationChannel.Email
    };
}

public sealed class StartPasswordlessLoginCommand : ICommand
{
    public string InteractionKey { get; set; } = default!;

    public string ProviderType { get; set; } = default!;

    public string Identifier { get; set; } = default!;
}

public sealed class StartPasswordlessLoginValidator : Validator<StartPasswordlessLoginCommand>
{
    public StartPasswordlessLoginValidator()
    {
        RuleFor(x => x.InteractionKey).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.ProviderType).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Identifier).NotEmpty().MaximumLength(320).WithMessage(ValidationErrors.NotEmptyError);
    }
}
