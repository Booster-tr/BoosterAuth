using Booster.Auth.Z.Managers.LookupManagers;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.Auth.Z.Features._base;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.Lookup.LookupByUser;

public class LookupByUserEndpoint : Endpoint<LookupByUserCommand, LookupResponse>
{
    public override void Configure()
    {
        Post("authz/lookup/by-user");
        Summary(s =>
        {
            s.Summary = "Bir öznenin hangi nesneler üzerinde hangi yetkilere sahip olduğunu listeler";
            s.Description = "relation boş bırakılırsa tipin kullanıcıya ulaşabilen tüm ilişkileri taranır. " +
                            "Yalnızca yazılmış tuple'ları değil, model üzerinden türeyen etkin yetkiyi de kapsar.";
        });
    }

    public override async Task HandleAsync(LookupByUserCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record LookupByUserCommand(
    Guid IsolationId,
    string User,
    string ObjectType,
    string? Relation) : ICommand<LookupResponse>;

public record LookupResponse(IReadOnlyList<LookupTupleResponse> Results);

public readonly record struct LookupTupleResponse(string User, string Relation, string Object);

public class LookupByUserCommandHandler(ILookupManager lookupManager)
    : ICommandHandler<LookupByUserCommand, LookupResponse>
{
    public async Task<LookupResponse> ExecuteAsync(LookupByUserCommand command, CancellationToken ct)
    {
        var results = await lookupManager.LookupByUserAsync(
            command.IsolationId, command.User, command.ObjectType, command.Relation, ct);

        return new LookupResponse(
            results.Select(r => new LookupTupleResponse(r.User, r.Relation, r.Object)).ToList());
    }
}

public class LookupByUserValidator : Validator<LookupByUserCommand>
{
    public LookupByUserValidator()
    {
        RuleFor(x => x.IsolationId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.User).UserFormat();
        RuleFor(x => x.ObjectType).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
