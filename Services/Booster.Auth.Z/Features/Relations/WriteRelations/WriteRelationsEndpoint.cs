using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Fga;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Z.Constants;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.Relations.WriteRelations;

public class WriteRelationsEndpoint : Endpoint<WriteRelationsCommand, WriteRelationsResponse>
{
    public override void Configure()
    {
        Post("authz/relations");

        // Hedef havuz QUERY'den (UserPoolId) gelir ve FGA ayni degeri kontrol eder.
        // Govdeden alinsaydi query'ye yetkili olunan havuz, govdeye baska havuz yazilip
        // yetki kontrolu atlatilabilirdi. Servisler bu isi gRPC'den yapar (yalniz ic ag).
        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthZPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, AuthZPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));
        Summary(s =>
        {
            s.Summary = "Bir veya daha fazla ilişki tuple'ı yazar";
            s.Description = "Tek tuple da liste olarak gönderilir; tek ve toplu yazım için ayrı uç yoktur.";
        });
    }

    // FGA hangi havuzu kontrol ettiyse handler da ONU kullanir; govdede farkli havuz gelse de yok sayilir.
    public override async Task HandleAsync(WriteRelationsCommand req, CancellationToken ct)
        => await Send.OkAsync(await (req with { UserPoolId = Query<Guid>("UserPoolId") }).ExecuteAsync(ct), ct);
}

public record RelationTupleRequest(string User, string Relation, string Object);

public record WriteRelationsCommand(
    Guid UserPoolId,
    List<RelationTupleRequest> Tuples) : ICommand<WriteRelationsResponse>;

public readonly record struct WriteRelationsResponse(int WrittenCount);

public class WriteRelationsCommandHandler(
    IPoolStoreProvider poolStoreProvider,
    FgaRelationManager relationManager)
    : ICommandHandler<WriteRelationsCommand, WriteRelationsResponse>
{
    public async Task<WriteRelationsResponse> ExecuteAsync(WriteRelationsCommand command, CancellationToken ct)
    {
        var mapping = await poolStoreProvider.GetRequiredByPoolIdAsync(command.UserPoolId, ct);

        var tuples = command.Tuples
            .Select(t => new TupleInput(t.User, t.Relation, t.Object))
            .ToList();

        await relationManager.WriteBatchAsync(mapping.StoreId, tuples, mapping.ActiveModelId, ct);

        return new WriteRelationsResponse(tuples.Count);
    }
}

public class WriteRelationsValidator : Validator<WriteRelationsCommand>
{
    public WriteRelationsValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Tuples).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleForEach(x => x.Tuples).ChildRules(tuple =>
        {
            tuple.RuleFor(t => t.User).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
            tuple.RuleFor(t => t.Relation).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
            tuple.RuleFor(t => t.Object).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        });
    }
}
