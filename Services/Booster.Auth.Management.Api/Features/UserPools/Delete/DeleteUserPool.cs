using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.Database.Repositories;
using FastEndpoints;

namespace Booster.Auth.Management.Api.Features.UserPools.Delete;

public class DeleteUserPoolEndpoint : EndpointWithoutRequest<DeleteUserPoolResponse>
{
    public override void Configure()
    {
        Delete("{id:guid}");
        Group<UserPoolsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Kullanıcı havuzunu siler";
            s.Description = "Havuzu soft-delete ile kaldırır. Uygulamalar, roller ve scope tanımları cascade ile düşer.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new DeleteUserPoolCommand(Route<Guid>("id")).ExecuteAsync(ct), ct);
}

public record DeleteUserPoolCommand(Guid Id) : ICommand<DeleteUserPoolResponse>;

public readonly record struct DeleteUserPoolResponse(Guid Id);

public class DeleteUserPoolCommandHandler(
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IClientDataCacheStore clientDataCacheStore)
    : ICommandHandler<DeleteUserPoolCommand, DeleteUserPoolResponse>
{
    public async Task<DeleteUserPoolResponse> ExecuteAsync(DeleteUserPoolCommand command, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(p => p.Id == command.Id, cancellationToken: ct);
        pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

        userPoolRepository.Delete(pool!);
        await userPoolRepository.SaveChangesAsync(ct);

        await clientDataCacheStore.InvalidatePoolAsync(pool!.Id, ct);

        return new DeleteUserPoolResponse(pool!.Id);
    }
}
