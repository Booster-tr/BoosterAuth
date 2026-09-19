using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;
using Mapster;

namespace Booster.Auth.Z.Features.PoolStores.UpsertPoolStore;

public class UpsertPoolStoreEndpoint : Endpoint<UpsertPoolStoreCommand, UpsertPoolStoreResponse>
{
    public override void Configure()
    {
        Post("pool-stores/{PoolId:guid}");
        Summary(s =>
        {
            s.Summary = "Pool → OpenFGA store eşlemesini oluşturur ya da günceller";
            s.Description = "Eşleme yoksa açılır, varsa store ve aktif model üzerine yazılır. " +
                            "PoolId istekten alınır: bu tablo sistem seviyesidir, istek bağlamının izolasyonuna bağlı değildir.";
        });
    }

    public override async Task HandleAsync(UpsertPoolStoreCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpsertPoolStoreCommand(
    Guid PoolId,
    string StoreId,
    string ActiveModelId) : ICommand<UpsertPoolStoreResponse>;

public readonly record struct UpsertPoolStoreResponse(Guid Id, bool Created);

public class UpsertPoolStoreCommandHandler(
    IAsyncRepository<PoolStore, AuthZDbContext> poolStoreRepository,
    IPoolStoreProvider poolStoreProvider)
    : ICommandHandler<UpsertPoolStoreCommand, UpsertPoolStoreResponse>
{
    public async Task<UpsertPoolStoreResponse> ExecuteAsync(UpsertPoolStoreCommand command, CancellationToken ct)
    {
        var entity = await poolStoreRepository.GetAsync(
            x => x.PoolId == command.PoolId, cancellationToken: ct);

        var created = entity is null;

        entity = command.Adapt<PoolStore>();
        if (created)
        {
            entity.SetId();
            await poolStoreRepository.AddAsync(entity, ct);
        }
        
        await poolStoreRepository.SaveChangesAsync(ct);
        await poolStoreProvider.RemoveCacheAsync(command.PoolId, ct);

        return new UpsertPoolStoreResponse(entity.Id, created);
    }
}

public class UpsertPoolStoreValidator : Validator<UpsertPoolStoreCommand>
{
    public UpsertPoolStoreValidator()
    {
        RuleFor(x => x.PoolId)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.StoreId)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.ActiveModelId)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
