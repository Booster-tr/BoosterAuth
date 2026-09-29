using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Features.IdentityProviders._base;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.Hashing.TwoWays.Aes;
using Booster.Auth.Common.Internal.Constants;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using FluentValidation;
using Booster.Auth.Management.Api.Constants;

namespace Booster.Auth.Management.Api.Features.IdentityProviders.Update;

public class UpdateIdentityProviderEndpoint : Endpoint<UpdateIdentityProviderCommand>
{
    public override void Configure()
    {
        Put("");
        Group<IdentityProvidersEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.ManagerRelation)));
        Summary(s =>
        {
            s.Summary = "Sağlayıcı ayarlarını güncelller";
            s.Description = "ClientSecret boş bırakılırsa mevcut sır KORUNUR. " +
                            "Key ve ProviderType değiştirilemez: ikisi de mevcut bağlantıların " +
                            "anlamını taşır.";
        });
    }

    public override async Task HandleAsync(UpdateIdentityProviderCommand req, CancellationToken ct)
    {
        await req.ExecuteAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

public record UpdateIdentityProviderCommand(
    Guid UserPoolId,
    Guid IdentityProviderId,
    string DisplayName,
    string? ClientSecret,
    Dictionary<string, string> Settings) : ICommand;

public class UpdateIdentityProviderCommandHandler(
    IAsyncRepository<IdentityProvider, AuthNDbContext> providerRepository,
    IClientDataCacheStore clientDataCacheStore,
    ISecretProtector secretProtector) : ICommandHandler<UpdateIdentityProviderCommand>
{
    public async Task ExecuteAsync(UpdateIdentityProviderCommand command, CancellationToken ct)
    {
        var provider = await providerRepository.GetAsync(
            p => p.Id == command.IdentityProviderId && p.UserPoolId == command.UserPoolId, cancellationToken: ct);
        provider.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.IdentityProviderNotFound);

        provider!.DisplayName = command.DisplayName.Trim();
        provider.Settings = IdentityProviderSettings.Normalize(command.Settings, provider.ProviderType);

        // Tur bagimli zorunluluklar BURADA da kontrol edilir. Yalnizca olusturmada
        // kontrol edilseydi, ayarlari duzenleyen bir guncelleme client_id gibi
        // zorunlu bir alani kaldirip girisi sessizce bozabilirdi.
        IdentityProviderSettings.EnsureRequired(provider.ProviderType, provider.Settings);

        // Sır YALNIZCA yenisi gönderildiğinde değişir.
        //
        // Boş gelen değeri yazsaydık, ayarları düzeltmek için yapılan her
        // güncelleme sırrı silip girişi bozardı — yönetici de sebebini
        // göremezdi çünkü sır hiçbir okuma ucunda görünmüyor.
        if (!string.IsNullOrWhiteSpace(command.ClientSecret))
            provider.ClientSecretEncrypted = secretProtector.Protect(command.ClientSecret);

        providerRepository.Update(provider);
        await providerRepository.SaveChangesAsync(ct);

        await clientDataCacheStore.InvalidatePoolAsync(provider.UserPoolId, ct);
    }
}

public class UpdateIdentityProviderValidator : Validator<UpdateIdentityProviderCommand>
{
    public UpdateIdentityProviderValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.IdentityProviderId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);

        // Tur bagimli zorunluluklar handler'da: tur ancak kayit okunduktan
        // sonra biliniyor.
        RuleFor(x => x.Settings)
            .NotNull().WithMessage(ValidationErrors.NotEmptyError);
    }
}
