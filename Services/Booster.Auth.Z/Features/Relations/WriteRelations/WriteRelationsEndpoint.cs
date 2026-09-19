using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Fga;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.Relations.WriteRelations;

public class WriteRelationsEndpoint : Endpoint<WriteRelationsCommand, WriteRelationsResponse>
{
    public override void Configure()
    {
        Post("authz/relations");
        Summary(s =>
        {
            s.Summary = "Bir veya daha fazla ilişki tuple'ı yazar";
            s.Description = "Tek tuple da liste olarak gönderilir; tek ve toplu yazım için ayrı uç yoktur.";
        });
    }

    public override async Task HandleAsync(WriteRelationsCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record RelationTupleRequest(string User, string Relation, string Object);

public record WriteRelationsCommand(
    Guid IsolationId,
    List<RelationTupleRequest> Tuples) : ICommand<WriteRelationsResponse>;

public readonly record struct WriteRelationsResponse(int WrittenCount);

public class WriteRelationsCommandHandler(
    IPoolStoreProvider poolStoreProvider,
    FgaRelationManager relationManager)
    : ICommandHandler<WriteRelationsCommand, WriteRelationsResponse>
{
    public async Task<WriteRelationsResponse> ExecuteAsync(WriteRelationsCommand command, CancellationToken ct)
    {
        var mapping = await poolStoreProvider.GetRequiredByPoolIdAsync(command.IsolationId, ct);

        var tuples = command.Tuples
            .Select(t => new TupleInput(t.User, t.Relation, t.Object))
            .ToList();

        await relationManager.WriteBatchAsync(mapping.StoreId, tuples, ct);

        return new WriteRelationsResponse(tuples.Count);
    }
}

public class WriteRelationsValidator : Validator<WriteRelationsCommand>
{
    public WriteRelationsValidator()
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
