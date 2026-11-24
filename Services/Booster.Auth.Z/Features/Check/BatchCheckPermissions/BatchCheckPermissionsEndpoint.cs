using Booster.Auth.Z.Managers.QueryAccess;
using Booster.Auth.Common.Internal.Licensing;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Fga;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.Auth.Z.Features._base;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.Check.BatchCheckPermissions;

/// <summary>Salt okuma: lisans kisitliyken de calisir (panel ekrani cizebilsin).</summary>
[AllowWhenLicenseRestricted]
public class BatchCheckPermissionsEndpoint : Endpoint<BatchCheckPermissionsCommand, BatchCheckPermissionsResponse>
{
    public override void Configure()
    {
        Post("authz/batch-check");
        Summary(s =>
        {
            s.Summary = "Birden çok yetki sorgusunu tek istekte çalıştırır";
            s.Description = "Sonuçlar correlationId ile eşleştirilir; istek sırası garanti edilmez.";
        });
    }

    public override async Task HandleAsync(BatchCheckPermissionsCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record BatchCheckItemRequest(string User, string Relation, string Object, string? CorrelationId);

public record BatchCheckPermissionsCommand(
    Guid IsolationId,
    List<BatchCheckItemRequest> Checks) : ICommand<BatchCheckPermissionsResponse>;

public record BatchCheckPermissionsResponse(IReadOnlyList<BatchCheckResultResponse> Results);

public readonly record struct BatchCheckResultResponse(string CorrelationId, bool Allowed);

public class BatchCheckPermissionsCommandHandler(
    QueryCallerGuard guard,
    IPoolStoreProvider poolStoreProvider,
    FgaRelationManager relationManager)
    : ICommandHandler<BatchCheckPermissionsCommand, BatchCheckPermissionsResponse>
{
    public async Task<BatchCheckPermissionsResponse> ExecuteAsync(
        BatchCheckPermissionsCommand command, CancellationToken ct)
    {
        // Govdedeki havuzu sorgulama yetkisi (bkz. QueryCallerGuard).
        await guard.EnsureAsync(command.IsolationId, ct);

        var mapping = await poolStoreProvider.GetRequiredByPoolIdAsync(command.IsolationId, ct);

        var checks = command.Checks
            .Select(c => new CheckInput(c.User, c.Relation, c.Object, c.CorrelationId))
            .ToList();

        var results = await relationManager.BatchCheckAsync(
            mapping.StoreId, checks, mapping.ActiveModelId, ct);

        return new BatchCheckPermissionsResponse(
            results.Select(r => new BatchCheckResultResponse(r.CorrelationId, r.Allowed)).ToList());
    }
}

public class BatchCheckPermissionsValidator : Validator<BatchCheckPermissionsCommand>
{
    public BatchCheckPermissionsValidator()
    {
        RuleFor(x => x.IsolationId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Checks).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleForEach(x => x.Checks).ChildRules(check =>
        {
            check.RuleFor(c => c.User).UserFormat();
            check.RuleFor(c => c.Relation).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
            check.RuleFor(c => c.Object).ObjectFormat();
            check.RuleFor(c => c.CorrelationId).CorrelationIdFormat();
        });

        RuleFor(x => x.Checks)
            .Must(checks =>
            {
                var provided = checks
                    .Select(c => c.CorrelationId)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToList();

                return provided.Count == provided.Distinct().Count();
            })
            .WithMessage(AuthZMessageKeys.DuplicateCorrelationId);
    }
}
