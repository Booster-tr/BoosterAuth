using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Idp.Apis._base;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Grpc;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using Grpc.Core;

namespace Booster.Auth.Idp.Apis.Relations;

// Iliskiler servis hesabinin havuzunun KENDI AuthZ store'una yazilir. Tip / iliski havuzun yayindaki
// semasina uymali; uymazsa OpenFGA reddeder ve PoolApiRelationRejected doner.

public record RelationTupleDto(string User, string Relation, string Object);

public record RelationsRequest(List<RelationTupleDto> Tuples);

public record RelationsResponse(int AffectedCount);

public class WriteRelationsEndpoint(AuthorizationService.AuthorizationServiceClient authorizationClient, IdpDbContext db, IRequestContextAccessor requestContextAccessor)
    : Endpoint<RelationsRequest, RelationsResponse>
{
    public override void Configure()
    {
        Post(PoolApiRoutes.Relations);
        Summary(s =>
        {
            s.Summary = "Havuzun yetki şemasına ilişki yazar (havuz API'si)";
            s.Description = "Örnek: { user: \"user:<id>\", relation: \"editor\", object: \"document:42\" }. En fazla 100 tuple.";
        });
    }

    public override async Task HandleAsync(RelationsRequest req, CancellationToken ct)
    {
        var poolId = await PoolApiCaller.RequirePoolAsync(requestContextAccessor, db, authorizationClient, AuthConstants.GrantManagerRelation, ct);
        var request = PoolRelationRules.ToGrpc(poolId, req.Tuples);
        var result = await PoolRelationRules.CallAsync(() => authorizationClient.WriteRelationsAsync(request, cancellationToken: ct).ResponseAsync);
        await Send.OkAsync(new RelationsResponse(result.AffectedCount), ct);
    }
}

public class DeleteRelationsEndpoint(AuthorizationService.AuthorizationServiceClient authorizationClient, IdpDbContext db, IRequestContextAccessor requestContextAccessor)
    : Endpoint<RelationsRequest, RelationsResponse>
{
    public override void Configure()
    {
        Post($"{PoolApiRoutes.Relations}/delete");
        Summary(s => s.Summary = "Havuzun yetki şemasından ilişki siler (havuz API'si)");
    }

    public override async Task HandleAsync(RelationsRequest req, CancellationToken ct)
    {
        var poolId = await PoolApiCaller.RequirePoolAsync(requestContextAccessor, db, authorizationClient, AuthConstants.GrantManagerRelation, ct);
        var request = PoolRelationRules.ToGrpc(poolId, req.Tuples);
        var result = await PoolRelationRules.CallAsync(() => authorizationClient.DeleteRelationsAsync(request, cancellationToken: ct).ResponseAsync);
        await Send.OkAsync(new RelationsResponse(result.AffectedCount), ct);
    }
}

public record CheckRelationRequest(string User, string Relation, string Object);

public record CheckRelationResponse(bool Allowed);

public class CheckRelationEndpoint(AuthorizationService.AuthorizationServiceClient authorizationClient, IdpDbContext db, IRequestContextAccessor requestContextAccessor)
    : Endpoint<CheckRelationRequest, CheckRelationResponse>
{
    public override void Configure()
    {
        Post($"{PoolApiRoutes.Relations}/check");
        Summary(s =>
        {
            s.Summary = "Bir öznenin bir nesne üzerindeki yetkisini sorgular (havuz API'si)";
            s.Description = "Doğrudan ilişki ve şemadaki miras birlikte değerlendirilir.";
        });
    }

    public override async Task HandleAsync(CheckRelationRequest req, CancellationToken ct)
    {
        var poolId = await PoolApiCaller.RequirePoolAsync(requestContextAccessor, db, authorizationClient, AuthConstants.ViewerRelation, ct);
        var request = new CheckRequest
        {
            IsolationId = poolId.ToString(),
            User = req.User,
            Relation = req.Relation,
            Object = req.Object
        };
        var result = await PoolRelationRules.CallAsync(() => authorizationClient.CheckAsync(request, cancellationToken: ct).ResponseAsync);
        await Send.OkAsync(new CheckRelationResponse(result.Allowed), ct);
    }
}

public class RelationsRequestValidator : Validator<RelationsRequest>
{
    public RelationsRequestValidator()
    {
        RuleFor(x => x.Tuples).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Tuples.Count).LessThanOrEqualTo(100).When(x => x.Tuples is not null).WithMessage(ValidationErrors.MaxLengthError);
        RuleForEach(x => x.Tuples).ChildRules(t =>
        {
            t.RuleFor(x => x.User).Matches(PoolRelationRules.SubjectPattern).WithMessage(IdpMessageKeys.PoolApiRelationRejected);
            t.RuleFor(x => x.Relation).Matches(PoolRelationRules.RelationPattern).WithMessage(IdpMessageKeys.PoolApiRelationRejected);
            t.RuleFor(x => x.Object).Matches(PoolRelationRules.ObjectPattern).WithMessage(IdpMessageKeys.PoolApiRelationRejected);
        });
    }
}

public class CheckRelationRequestValidator : Validator<CheckRelationRequest>
{
    public CheckRelationRequestValidator()
    {
        RuleFor(x => x.User).Matches(PoolRelationRules.SubjectPattern).WithMessage(IdpMessageKeys.PoolApiRelationRejected);
        RuleFor(x => x.Relation).Matches(PoolRelationRules.RelationPattern).WithMessage(IdpMessageKeys.PoolApiRelationRejected);
        RuleFor(x => x.Object).Matches(PoolRelationRules.ObjectPattern).WithMessage(IdpMessageKeys.PoolApiRelationRejected);
    }
}

internal static class PoolRelationRules
{
    /// <summary>"tip:id" ya da kullanici kumesi "tip:id#iliski".</summary>
    public const string SubjectPattern = @"^[a-z][a-z0-9_]{0,63}:[^\s:#]{1,128}(#[a-z][a-z0-9_]{0,63})?$";
    public const string ObjectPattern = @"^[a-z][a-z0-9_]{0,63}:[^\s:#]{1,128}$";
    public const string RelationPattern = @"^[a-z][a-z0-9_]{0,63}$";

    public static WriteRelationsRequest ToGrpc(Guid poolId, IEnumerable<RelationTupleDto> tuples)
    {
        var request = new WriteRelationsRequest { IsolationId = poolId.ToString() };
        request.Tuples.AddRange(tuples.Select(t => new RelationTuple { User = t.User, Relation = t.Relation, Object = t.Object }));
        return request;
    }

    /// <summary>Semaya uymayan tuple ya da semasi olmayan havuz: 500 yerine anlasilir ret.</summary>
    public static async Task<T> CallAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.InvalidArgument or StatusCode.FailedPrecondition or StatusCode.NotFound)
        {
            throw new BusinessException(IdpMessageKeys.PoolApiRelationRejected);
        }
    }
}
