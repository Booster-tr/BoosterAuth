using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Features.IdentityProviders._base;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Hashing.TwoWays.Aes;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.IdentityProviders.Create;

public class CreateIdentityProviderEndpoint : Endpoint<CreateIdentityProviderCommand, CreateIdentityProviderResponse>
{
    public override void Configure()
    {
        Post("");
        Group<IdentityProvidersEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Havuza dış kimlik sağlayıcısı ekler";
            s.Description = "Key havuz içinde benzersizdir ve giriş adresinde görünür. " +
                            "ClientSecret şifrelenerek saklanır ve hiçbir okuma ucunda geri dönmez.";
        });
    }

    public override async Task HandleAsync(CreateIdentityProviderCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record CreateIdentityProviderCommand(
    Guid UserPoolId,
    string Key,
    string DisplayName,
    string ProviderType,
    string? ClientSecret,
    Dictionary<string, string> Settings) : ICommand<CreateIdentityProviderResponse>;

public readonly record struct CreateIdentityProviderResponse(Guid Id, string Key);

public class CreateIdentityProviderCommandHandler(
    IAsyncRepository<IdentityProvider, AuthNDbContext> providerRepository,
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IClientDataCacheStore clientDataCacheStore,
    ISecretProtector secretProtector)
    : ICommandHandler<CreateIdentityProviderCommand, CreateIdentityProviderResponse>
{
    public async Task<CreateIdentityProviderResponse> ExecuteAsync(
        CreateIdentityProviderCommand command, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(
            p => p.Id == command.UserPoolId, enableTracking: false, cancellationToken: ct);
        pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

        if (pool!.Status != EntityStatus.Active)
            throw new BusinessException(AuthNMessageKeys.ParentNotActive);

        var key = command.Key.Trim().ToLowerInvariant();
        var providerType = command.ProviderType.Trim().ToLowerInvariant();

        var keyExists = await providerRepository.AnyAsync(
            p => p.UserPoolId == command.UserPoolId && p.Key == key, cancellationToken: ct);

        if (keyExists)
            throw new BusinessException(AuthNMessageKeys.IdentityProviderKeyAlreadyExists);

        var settings = IdentityProviderSettings.Normalize(command.Settings, providerType);
        IdentityProviderSettings.EnsureRequired(providerType, settings);

        var provider = new IdentityProvider
        {
            UserPoolId = command.UserPoolId,
            Key = key,
            DisplayName = command.DisplayName.Trim(),
            ProviderType = providerType,
            Status = EntityStatus.Active,
            Settings = settings,

            // Sır ŞİFRELİ yazılır. Düz metin yazılsaydı bir veritabanı yedeği ya
            // da salt okuma yetkisi, bütün müşterilerin dış sağlayıcı sırlarını
            // birlikte ele verirdi.
            ClientSecretEncrypted = string.IsNullOrWhiteSpace(command.ClientSecret)
                ? null
                : secretProtector.Protect(command.ClientSecret)
        };

        provider.SetId();

        await providerRepository.AddAsync(provider, ct);
        await providerRepository.SaveChangesAsync(ct);

        // Sağlayıcı listesi ClientData ile taşındığı için havuzun bütün
        // istemcilerinin önbelleği düşürülür; düşmezse yeni sağlayıcı önbellek
        // ömrü boyunca giriş ekranında görünmezdi.
        await clientDataCacheStore.InvalidatePoolAsync(command.UserPoolId, ct);

        return new CreateIdentityProviderResponse(provider.Id, provider.Key);
    }
}

public class CreateIdentityProviderValidator : Validator<CreateIdentityProviderCommand>
{
    public CreateIdentityProviderValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Key)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(100).WithMessage(ValidationErrors.MaxLengthError)
            .Matches("^[a-z0-9-]+$").WithMessage(AuthNMessageKeys.IdentityProviderKeyInvalidFormat);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);

        RuleFor(x => x.ProviderType)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .Must(FederationProviderCatalog.IsKnown)
            .WithMessage(AuthNMessageKeys.IdentityProviderTypeUnknown);

        // Tür bağımlı zorunlu ayarlar burada DEĞİL handler'da doğrulanır:
        // aynı kontrolün güncellemede de çalışması gerekiyor ve orada tür
        // istekte gelmiyor. Tek yerde tutulması iki akışın ayrışmasını önler.
    }
}
