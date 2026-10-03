using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Z.Constants;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Fga.Attributes;
using Booster.Authorization.Relational.Fga;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.PoolRelations.GetList;

public class GetListPoolRelationEndpoint : Endpoint<GetListPoolRelationQuery, GetListPoolRelationResponse>
{
    public override void Configure()
    {
        Get("authz/pool-relations");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthZPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, AuthZPolicies.PoolTarget, AuthConstants.ViewerRelation)));

        Summary(s =>
        {
            s.Summary = "Havuzun yazılmış ilişkilerini listeler";
            s.Description = "Kaynak tipi zorunludur; kayıt kimliği ya da kullanıcı da verilmelidir. OpenFGA bir tipin " +
                            "tüm kayıtlarını filtresiz okumaz. Sayfalama devam belirteciyle yapılır.";
        });
    }

    public override async Task HandleAsync(GetListPoolRelationQuery req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record GetListPoolRelationQuery(
    Guid UserPoolId,
    string ObjectType,
    string? ObjectId,
    Guid? UserId,
    string? ContinuationToken,
    int PageSize = 50) : ICommand<GetListPoolRelationResponse>;

public record GetListPoolRelationResponse(IReadOnlyList<PoolRelationItem> Items, string? ContinuationToken);

/// <param name="Subject">Atanan taraf: user:kimlik ya da tip:kimlik.</param>
public record PoolRelationItem(string ObjectType, string ObjectId, string Relation, string SubjectType, string SubjectId);

public class GetListPoolRelationQueryHandler(IPoolStoreProvider poolStoreProvider, FgaRelationManager relationManager)
    : ICommandHandler<GetListPoolRelationQuery, GetListPoolRelationResponse>
{
    public async Task<GetListPoolRelationResponse> ExecuteAsync(GetListPoolRelationQuery command, CancellationToken ct)
    {
        var store = await poolStoreProvider.GetRequiredByPoolIdAsync(command.UserPoolId, ct);

        var page = await relationManager.ReadPageAsync(
            store.StoreId,
            command.UserId is { } userId ? AuthConstants.User(userId) : null,
            relation: null,
            @object: $"{command.ObjectType}:{command.ObjectId}",
            command.PageSize,
            command.ContinuationToken,
            ct);

        var items = page.Tuples.Select(t =>
        {
            var (objectType, objectId) = Split(t.Object);
            var (subjectType, subjectId) = Split(t.User);
            return new PoolRelationItem(objectType, objectId, t.Relation, subjectType, subjectId);
        }).ToList();

        return new GetListPoolRelationResponse(items, page.ContinuationToken);
    }

    private static (string Type, string Id) Split(string value)
    {
        var index = value.IndexOf(':');
        return (value[..index], value[(index + 1)..]);
    }
}

public class GetListPoolRelationValidator : Validator<GetListPoolRelationQuery>
{
    public GetListPoolRelationValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.ObjectType).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.ObjectId)
            .NotEmpty().When(x => x.UserId is null).WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage(ValidationErrors.MaxLengthError);
    }
}
