using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.PoolStores.UpdateActiveModel;

public class UpdateActiveModelEndpoint : Endpoint<UpdateActiveModelCommand, UpdateActiveModelResponse>
{
    public override void Configure()
    {
        Put("pool-stores/{PoolId:guid}/active-model");
        Summary(s =>
        {
            s.Summary = "Pool'un aktif authorization model'ini değiştirir";
            s.Description = "Store'a dokunmaz; yalnızca yetki sorgularında kullanılacak model id'sini değiştirir (model switch).";
        });
    }

    public override async Task HandleAsync(UpdateActiveModelCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateActiveModelCommand(
    Guid PoolId,
    string ActiveModelId) : ICommand<UpdateActiveModelResponse>;

public readonly record struct UpdateActiveModelResponse(Guid PoolId, string ActiveModelId);

public class UpdateActiveModelCommandHandler(
    IAsyncRepository<PoolStore, AuthZDbContext> poolStoreRepository,
    IPoolStoreProvider poolStoreProvider)
    : ICommandHandler<UpdateActiveModelCommand, UpdateActiveModelResponse>
{
    public async Task<UpdateActiveModelResponse> ExecuteAsync(UpdateActiveModelCommand command, CancellationToken ct)
    {
        var entity = await poolStoreRepository.GetAsync(
            x => x.PoolId == command.PoolId, cancellationToken: ct);

        entity.ThrowExceptionIfDataNull(new BusinessException(AuthZMessageKeys.PoolStoreNotFound));

        if (entity!.ActiveModelId != command.ActiveModelId)
        {
            entity.ActiveModelId = command.ActiveModelId;
            entity.UpdatedTime = DateTimeOffset.UtcNow;

            await poolStoreRepository.SaveChangesAsync(ct);
        }

        await poolStoreProvider.RemoveCacheAsync(command.PoolId, ct);

        return new UpdateActiveModelResponse(entity.PoolId, entity.ActiveModelId);
    }
}

public class UpdateActiveModelValidator : Validator<UpdateActiveModelCommand>
{
    public UpdateActiveModelValidator()
    {
        RuleFor(x => x.PoolId)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.ActiveModelId)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
