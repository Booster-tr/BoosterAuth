using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Database.Repositories;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using Booster.Auth.Management.Api.Constants;

namespace Booster.Auth.Management.Api.Features.Clients.Delete;

public class DeleteClientEndpoint : EndpointWithoutRequest<DeleteClientResponse>
{
    public override void Configure()
    {
        Delete("");
        Group<ClientsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.ClientType, ManagementPolicies.ClientTarget, AuthConstants.ManagerRelation)));
        Summary(s =>
        {
            s.Summary = "Client siler";
            s.Description = "Uygulamayı soft-delete ile kaldırır. İzin verilen scope kayıtları cascade ile düşer.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new DeleteClientCommand(Query<Guid>("ClientId")).ExecuteAsync(ct), ct);
}

public record DeleteClientCommand(Guid ClientId) : ICommand<DeleteClientResponse>;

public readonly record struct DeleteClientResponse(Guid Id);

public class DeleteClientCommandHandler(
    IAsyncRepository<Client, AuthNDbContext> clientRepository,
    IClientDataCacheStore clientDataCacheStore,
    IAccessManager accessManager)
    : ICommandHandler<DeleteClientCommand, DeleteClientResponse>
{
    public async Task<DeleteClientResponse> ExecuteAsync(DeleteClientCommand command, CancellationToken ct)
    {
        var client = await clientRepository.GetAsync(c => c.Id == command.ClientId, cancellationToken: ct);
        client.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.ClientNotFound);

        clientRepository.Delete(client!);
        await clientRepository.SaveChangesAsync(ct);

        await clientDataCacheStore.InvalidateClientAsync(client!.ClientKey, ct);

        accessManager.RevokeAllInBackground(AuthConstants.ClientType, client.Id);

        return new DeleteClientResponse(client!.Id);
    }
}
