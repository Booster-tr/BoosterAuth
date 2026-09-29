using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.Auth.Common.Internal.Constants;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using Booster.Auth.Management.Api.Constants;

namespace Booster.Auth.Management.Api.Features.IdentityProviders.Delete;

public class DeleteIdentityProviderEndpoint : EndpointWithoutRequest<DeleteIdentityProviderResponse>
{
    public override void Configure()
    {
        Delete("");
        Group<IdentityProvidersEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.ManagerRelation)));
        Summary(s =>
        {
            s.Summary = "Sağlayıcıyı siler";
            s.Description = "Soft-delete. Kullanıcıların bu sağlayıcıyla kurduğu bağlantılar " +
                            "IdP tarafında kalır; silmek yerine kapatmak (status) çoğu durumda " +
                            "daha doğrudur çünkü silinen bir sağlayıcının anahtarı yeniden " +
                            "kullanılabilir hale gelir.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new DeleteIdentityProviderCommand(Query<Guid>("UserPoolId"), Query<Guid>("IdentityProviderId")).ExecuteAsync(ct), ct);
}

public record DeleteIdentityProviderCommand(Guid UserPoolId, Guid IdentityProviderId) : ICommand<DeleteIdentityProviderResponse>;

public readonly record struct DeleteIdentityProviderResponse(Guid Id);

public class DeleteIdentityProviderCommandHandler(
    IAsyncRepository<IdentityProvider, AuthNDbContext> providerRepository,
    IClientDataCacheStore clientDataCacheStore)
    : ICommandHandler<DeleteIdentityProviderCommand, DeleteIdentityProviderResponse>
{
    public async Task<DeleteIdentityProviderResponse> ExecuteAsync(
        DeleteIdentityProviderCommand command, CancellationToken ct)
    {
        var provider = await providerRepository.GetAsync(
            p => p.Id == command.IdentityProviderId && p.UserPoolId == command.UserPoolId, cancellationToken: ct);
        provider.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.IdentityProviderNotFound);

        providerRepository.Delete(provider!);
        await providerRepository.SaveChangesAsync(ct);

        await clientDataCacheStore.InvalidatePoolAsync(provider!.UserPoolId, ct);

        return new DeleteIdentityProviderResponse(provider.Id);
    }
}
