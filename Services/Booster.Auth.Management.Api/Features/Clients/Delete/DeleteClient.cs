using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;

namespace Booster.Auth.Management.Api.Features.Clients.Delete;

public class DeleteClientEndpoint : EndpointWithoutRequest<DeleteClientResponse>
{
    public override void Configure()
    {
        Delete("{id:guid}");
        Group<ClientsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Client siler";
            s.Description = "Uygulamayı soft-delete ile kaldırır. İzin verilen scope kayıtları cascade ile düşer.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new DeleteClientCommand(Route<Guid>("id")).ExecuteAsync(ct), ct);
}

public record DeleteClientCommand(Guid Id) : ICommand<DeleteClientResponse>;

public readonly record struct DeleteClientResponse(Guid Id);

public class DeleteClientCommandHandler(
    IAsyncRepository<Client, AuthNDbContext> clientRepository)
    : ICommandHandler<DeleteClientCommand, DeleteClientResponse>
{
    public async Task<DeleteClientResponse> ExecuteAsync(DeleteClientCommand command, CancellationToken ct)
    {
        var client = await clientRepository.GetAsync(c => c.Id == command.Id, cancellationToken: ct);
        client.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.ClientNotFound);

        clientRepository.Delete(client!);
        await clientRepository.SaveChangesAsync(ct);

        return new DeleteClientResponse(client!.Id);
    }
}
