using Booster.Auth.Z.Managers.QueryAccess;
using Booster.Auth.Common.Internal.Licensing;
using Booster.Auth.Z.Features.Lookup.LookupByUser;
using Booster.Auth.Z.Managers.LookupManagers;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.Auth.Z.Features._base;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.Lookup.LookupByObject;

/// <summary>Salt okuma: lisans kisitliyken de calisir (panel ekrani cizebilsin).</summary>
[AllowWhenLicenseRestricted]
public class LookupByObjectEndpoint : Endpoint<LookupByObjectCommand, LookupResponse>
{
    public override void Configure()
    {
        Post("authz/lookup/by-object");
        Summary(s =>
        {
            s.Summary = "Bir nesne üzerinde kimlerin hangi yetkilere sahip olduğunu listeler";
            s.Description = "relation boş bırakılırsa nesnenin tipindeki tüm ilişkiler taranır. " +
                            "Sonuç, tuple yazılmamış ama modelden türeyen yetkileri de içerir.";
        });
    }

    public override async Task HandleAsync(LookupByObjectCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record LookupByObjectCommand(
    Guid IsolationId,
    string Object,
    string? Relation) : ICommand<LookupResponse>;

public class LookupByObjectCommandHandler(
    QueryCallerGuard guard,
    ILookupManager lookupManager)
    : ICommandHandler<LookupByObjectCommand, LookupResponse>
{
    public async Task<LookupResponse> ExecuteAsync(LookupByObjectCommand command, CancellationToken ct)
    {
        // Govdedeki havuzu sorgulama yetkisi (bkz. QueryCallerGuard).
        await guard.EnsureAsync(command.IsolationId, ct);

        var results = await lookupManager.LookupByObjectAsync(
            command.IsolationId, command.Object, command.Relation, ct);

        return new LookupResponse(
            results.Select(r => new LookupTupleResponse(r.User, r.Relation, r.Object)).ToList());
    }
}

public class LookupByObjectValidator : Validator<LookupByObjectCommand>
{
    public LookupByObjectValidator()
    {
        RuleFor(x => x.IsolationId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Object).ObjectFormat();
    }
}
