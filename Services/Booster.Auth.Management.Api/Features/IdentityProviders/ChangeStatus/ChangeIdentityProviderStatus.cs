using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.IdentityProviders.ChangeStatus;

public class ChangeIdentityProviderStatusEndpoint : Endpoint<ChangeIdentityProviderStatusCommand>
{
    public override void Configure()
    {
        Patch("{id}/status");
        Group<IdentityProvidersEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Sağlayıcıyı açar veya kapatır";
            s.Description = "Kapatılan sağlayıcı giriş ekranında görünmez ve devam eden bir " +
                            "akış dönüşünde de reddedilir. Mevcut bağlantılar SİLİNMEZ; " +
                            "sağlayıcı yeniden açıldığında kullanıcılar eski hesaplarıyla girer.";
        });
    }

    public override async Task HandleAsync(ChangeIdentityProviderStatusCommand req, CancellationToken ct)
    {
        await req.ExecuteAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

public record ChangeIdentityProviderStatusCommand(Guid Id, EntityStatus Status) : ICommand;

public class ChangeIdentityProviderStatusCommandHandler(
    IAsyncRepository<IdentityProvider, AuthNDbContext> providerRepository,
    IClientDataCacheStore clientDataCacheStore) : ICommandHandler<ChangeIdentityProviderStatusCommand>
{
    public async Task ExecuteAsync(ChangeIdentityProviderStatusCommand command, CancellationToken ct)
    {
        var provider = await providerRepository.GetAsync(p => p.Id == command.Id, cancellationToken: ct);
        provider.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.IdentityProviderNotFound);

        provider!.Status = command.Status;

        providerRepository.Update(provider);
        await providerRepository.SaveChangesAsync(ct);

        await clientDataCacheStore.InvalidatePoolAsync(provider.UserPoolId, ct);
    }
}

public class ChangeIdentityProviderStatusValidator : Validator<ChangeIdentityProviderStatusCommand>
{
    public ChangeIdentityProviderStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Status).IsInEnum().WithMessage(ValidationErrors.NotEmptyError);
    }
}
