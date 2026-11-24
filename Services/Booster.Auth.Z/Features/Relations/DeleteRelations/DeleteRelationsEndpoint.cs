using Booster.Auth.Z.Features.Relations.WriteRelations;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Fga;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Z.Constants;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.Relations.DeleteRelations;

public class DeleteRelationsEndpoint : Endpoint<DeleteRelationsCommand, DeleteRelationsResponse>
{
    public override void Configure()
    {
        Delete("authz/relations");

        // Hedef havuz QUERY'den (UserPoolId) gelir ve FGA ayni degeri kontrol eder.
        // Govdeden alinsaydi query'ye yetkili olunan havuz, govdeye baska havuz yazilip
        // yetki kontrolu atlatilabilirdi. Servisler bu isi gRPC'den yapar (yalniz ic ag).
        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthZPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, AuthZPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));
        Summary(s =>
        {
            s.Summary = "Bir veya daha fazla ilişki tuple'ını siler";
            s.Description = "Silinecek tuple'lar istek gövdesinde gelir. Araya giren bazı vekil " +
                            "sunucular DELETE gövdesini düşürebilir; sorun yaşanırsa POST'a taşınmalıdır.";
        });
    }

    // FGA hangi havuzu kontrol ettiyse handler da ONU kullanir; govdede farkli havuz gelse de yok sayilir.
    public override async Task HandleAsync(DeleteRelationsCommand req, CancellationToken ct)
        => await Send.OkAsync(await (req with { UserPoolId = Query<Guid>("UserPoolId") }).ExecuteAsync(ct), ct);
}

public record DeleteRelationsCommand(
    Guid UserPoolId,
    List<RelationTupleRequest> Tuples) : ICommand<DeleteRelationsResponse>;

public readonly record struct DeleteRelationsResponse(int DeletedCount);

public class DeleteRelationsCommandHandler(
    IPoolStoreProvider poolStoreProvider,
    FgaRelationManager relationManager)
    : ICommandHandler<DeleteRelationsCommand, DeleteRelationsResponse>
{
    public async Task<DeleteRelationsResponse> ExecuteAsync(DeleteRelationsCommand command, CancellationToken ct)
    {
        var mapping = await poolStoreProvider.GetRequiredByPoolIdAsync(command.UserPoolId, ct);

        var tuples = command.Tuples
            .Select(t => new TupleInput(t.User, t.Relation, t.Object))
            .ToList();

        await relationManager.DeleteBatchAsync(mapping.StoreId, tuples, mapping.ActiveModelId, ct);

        return new DeleteRelationsResponse(tuples.Count);
    }
}

public class DeleteRelationsValidator : Validator<DeleteRelationsCommand>
{
    public DeleteRelationsValidator()
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
