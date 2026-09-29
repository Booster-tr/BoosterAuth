using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Managers.LookupManagers;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.Lookup.MyAccess;

public class MyAccessEndpoint : Endpoint<MyAccessCommand, MyAccessResponse>
{
    public override void Configure()
    {
        Post("authz/lookup/my-access");
        Summary(s =>
        {
            s.Summary = "Bir öznenin verilen tiplerdeki tüm nesneler üzerindeki yetkilerini döner";
            s.Description = "Nesne başına tek satır döner; ilişkiler o satırda toplanır. " +
                            "Oturum açılışında bağlam listesini tek istekte doldurmak içindir.";
        });
    }

    public override async Task HandleAsync(MyAccessCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record MyAccessCommand(
    Guid IsolationId,
    string User,
    List<string> ObjectTypes) : ICommand<MyAccessResponse>;

public record MyAccessResponse(IReadOnlyList<ObjectAccessResponse> Items);

public readonly record struct ObjectAccessResponse(
    string ObjectType,
    string ObjectId,
    IReadOnlyList<string> Relations);

public class MyAccessCommandHandler(ILookupManager lookupManager)
    : ICommandHandler<MyAccessCommand, MyAccessResponse>
{
    public async Task<MyAccessResponse> ExecuteAsync(MyAccessCommand command, CancellationToken ct)
    {
        var results = await lookupManager.LookupMyAccessAsync(
            command.IsolationId, command.User, command.ObjectTypes, ct);

        return new MyAccessResponse(
            results.Select(r => new ObjectAccessResponse(r.ObjectType, r.ObjectId, r.Relations)).ToList());
    }
}

public class MyAccessValidator : Validator<MyAccessCommand>
{
    public MyAccessValidator()
    {
        RuleFor(x => x.IsolationId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.User).UserFormat();

        RuleFor(x => x.ObjectTypes).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleForEach(x => x.ObjectTypes).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
