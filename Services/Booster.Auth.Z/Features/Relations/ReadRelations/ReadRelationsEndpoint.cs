using Booster.Auth.Common.Internal.Licensing;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Fga;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Z.Constants;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.Relations.ReadRelations;

/// <summary>Salt okuma: lisans kisitliyken de calisir (panel ekrani cizebilsin).</summary>
[AllowWhenLicenseRestricted]
public class ReadRelationsEndpoint : Endpoint<ReadRelationsCommand, ReadRelationsResponse>
{
    public override void Configure()
    {
        Post("authz/relations/read");

        // Hedef havuz QUERY'den (UserPoolId) gelir ve FGA ayni degeri kontrol eder.
        // Govdeden alinsaydi query'ye yetkili olunan havuz, govdeye baska havuz yazilip
        // yetki kontrolu atlatilabilirdi. Servisler bu isi gRPC'den yapar (yalniz ic ag).
        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthZPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, AuthZPolicies.PoolTarget, AuthConstants.ViewerRelation)));
        Summary(s =>
        {
            s.Summary = "İlişki tuple'larını filtreleyerek listeler";
            s.Description = "user / relation / object alanları opsiyoneldir; verilmeyenler filtreye girmez. " +
                            "Okuma olmasına rağmen POST'tur: filtreler gövdede taşınır.";
        });
    }

    // FGA hangi havuzu kontrol ettiyse handler da ONU kullanir; govdede farkli havuz gelse de yok sayilir.
    public override async Task HandleAsync(ReadRelationsCommand req, CancellationToken ct)
        => await Send.OkAsync(await (req with { UserPoolId = Query<Guid>("UserPoolId") }).ExecuteAsync(ct), ct);
}

public record ReadRelationsCommand(
    Guid UserPoolId,
    string? User,
    string? Relation,
    string? Object) : ICommand<ReadRelationsResponse>;

public record ReadRelationsResponse(IReadOnlyList<RelationTupleResponse> Tuples);

public readonly record struct RelationTupleResponse(string User, string Relation, string Object);

public class ReadRelationsCommandHandler(
    IPoolStoreProvider poolStoreProvider,
    FgaRelationManager relationManager)
    : ICommandHandler<ReadRelationsCommand, ReadRelationsResponse>
{
    public async Task<ReadRelationsResponse> ExecuteAsync(ReadRelationsCommand command, CancellationToken ct)
    {
        var mapping = await poolStoreProvider.GetRequiredByPoolIdAsync(command.UserPoolId, ct);

        var tuples = await relationManager.ReadAsync(
            mapping.StoreId,
            command.User,
            command.Relation,
            command.Object,
            ct);

        return new ReadRelationsResponse(
            tuples.Select(t => new RelationTupleResponse(t.User, t.Relation, t.Object)).ToList());
    }
}

public class ReadRelationsValidator : Validator<ReadRelationsCommand>
{
    public ReadRelationsValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
