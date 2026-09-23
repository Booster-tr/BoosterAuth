using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.Clients.ChangeStatus;

public class ChangeClientStatusEndpoint : Endpoint<ChangeClientStatusCommand, ChangeClientStatusResponse>
{
    public override void Configure()
    {
        Put("{Id:guid}/status");
        Group<ClientsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Client durumunu değiştirir";
            s.Description = "Uygulamayı aktif/pasif yapar. Aktife alma, havuzu pasifken reddedilir.";
        });
    }

    public override async Task HandleAsync(ChangeClientStatusCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record ChangeClientStatusCommand(Guid Id, EntityStatus Status) : ICommand<ChangeClientStatusResponse>;

public readonly record struct ChangeClientStatusResponse(Guid Id);

public class ChangeClientStatusCommandHandler(
    IAsyncRepository<Client, AuthNDbContext> clientRepository,
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IClientDataCacheStore clientDataCacheStore)
    : ICommandHandler<ChangeClientStatusCommand, ChangeClientStatusResponse>
{
    public async Task<ChangeClientStatusResponse> ExecuteAsync(
        ChangeClientStatusCommand command, CancellationToken ct)
    {
        var client = await clientRepository.GetAsync(c => c.Id == command.Id, cancellationToken: ct);
        client.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.ClientNotFound);

        if (command.Status == EntityStatus.Active)
        {
            var pool = await userPoolRepository.GetAsync(
                p => p.Id == client!.UserPoolId, enableTracking: false, cancellationToken: ct);
            pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

            if (pool!.Status != EntityStatus.Active)
                throw new BusinessException(AuthNMessageKeys.ParentNotActive);
        }

        client!.Status = command.Status;

        await clientRepository.SaveChangesAsync(ct);

        await clientDataCacheStore.InvalidateClientAsync(client.ClientKey, ct);

        return new ChangeClientStatusResponse(client.Id);
    }
}

public class ChangeClientStatusValidator : Validator<ChangeClientStatusCommand>
{
    public ChangeClientStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Status).IsInEnum();
    }
}
