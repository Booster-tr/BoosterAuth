using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Fga;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.Stores.CreateStore;

public class CreateStoreEndpoint : Endpoint<CreateStoreCommand, CreateStoreResponse>
{
    public override void Configure()
    {
        Post("authz/stores");
        Summary(s =>
        {
            s.Summary = "Pool için OpenFGA store'u oluşturur ve eşlemesini kaydeder";
            s.Description = "Store schema'sız açılır; kullanılabilir olması için ardından " +
                            "POST authz/schema ile ilk model yazılmalıdır. Bu iki adımı " +
                            "birleştiren olay tabanlı pool oluşturma ayrı bir fazdır.";
        });
    }

    public override async Task HandleAsync(CreateStoreCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record CreateStoreCommand(Guid IsolationId, string Name) : ICommand<CreateStoreResponse>;

public readonly record struct CreateStoreResponse(Guid PoolId, string StoreId);

public class CreateStoreCommandHandler(
    IPoolStoreProvider poolStoreProvider,
    IAsyncRepository<PoolStore, AuthZDbContext> poolStoreRepository,
    FgaSchemaWriter schemaWriter)
    : ICommandHandler<CreateStoreCommand, CreateStoreResponse>
{
    public async Task<CreateStoreResponse> ExecuteAsync(CreateStoreCommand command, CancellationToken ct)
    {
        var existing = await poolStoreProvider.GetByPoolIdAsync(command.IsolationId, ct);
        if (existing is not null)
            throw new BusinessException(AuthZMessageKeys.PoolStoreAlreadyExists);

        var storeId = await schemaWriter.CreateStoreAsync(command.Name, ct);

        var mapping = new PoolStore
        {
            PoolId = command.IsolationId,
            StoreId = storeId,
            ActiveModelId = string.Empty
        };
        mapping.SetId();

        await poolStoreRepository.AddAsync(mapping, ct);
        await poolStoreRepository.SaveChangesAsync(ct);

        await poolStoreProvider.RemoveCacheAsync(command.IsolationId, ct);

        return new CreateStoreResponse(command.IsolationId, storeId);
    }
}

public class CreateStoreValidator : Validator<CreateStoreCommand>
{
    public CreateStoreValidator()
    {
        RuleFor(x => x.IsolationId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(64).WithMessage(ValidationErrors.MaxLengthError);
    }
}
