using Booster.Auth.Z.Features.Relations.WriteRelations;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Fga;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.Relations.DeleteRelations;

public class DeleteRelationsEndpoint : Endpoint<DeleteRelationsCommand, DeleteRelationsResponse>
{
    public override void Configure()
    {
        Delete("authz/relations");
        Summary(s =>
        {
            s.Summary = "Bir veya daha fazla ilişki tuple'ını siler";
            s.Description = "Silinecek tuple'lar istek gövdesinde gelir. Araya giren bazı vekil " +
                            "sunucular DELETE gövdesini düşürebilir; sorun yaşanırsa POST'a taşınmalıdır.";
        });
    }

    public override async Task HandleAsync(DeleteRelationsCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record DeleteRelationsCommand(
    Guid IsolationId,
    List<RelationTupleRequest> Tuples) : ICommand<DeleteRelationsResponse>;

public readonly record struct DeleteRelationsResponse(int DeletedCount);

public class DeleteRelationsCommandHandler(
    IPoolStoreProvider poolStoreProvider,
    FgaRelationManager relationManager)
    : ICommandHandler<DeleteRelationsCommand, DeleteRelationsResponse>
{
    public async Task<DeleteRelationsResponse> ExecuteAsync(DeleteRelationsCommand command, CancellationToken ct)
    {
        var mapping = await poolStoreProvider.GetRequiredByPoolIdAsync(command.IsolationId, ct);

        var tuples = command.Tuples
            .Select(t => new TupleInput(t.User, t.Relation, t.Object))
            .ToList();

        await relationManager.DeleteBatchAsync(mapping.StoreId, tuples, ct);

        return new DeleteRelationsResponse(tuples.Count);
    }
}

public class DeleteRelationsValidator : Validator<DeleteRelationsCommand>
{
    public DeleteRelationsValidator()
    {
        RuleFor(x => x.IsolationId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Tuples).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleForEach(x => x.Tuples).ChildRules(tuple =>
        {
            tuple.RuleFor(t => t.User).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
            tuple.RuleFor(t => t.Relation).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
            tuple.RuleFor(t => t.Object).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        });
    }
}
