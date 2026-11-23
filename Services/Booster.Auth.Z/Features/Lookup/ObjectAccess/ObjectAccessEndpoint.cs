using Booster.Auth.Common.Internal.Licensing;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features.Lookup.MyAccess;
using Booster.Auth.Z.Managers.LookupManagers;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.Lookup.ObjectAccess;

/// <summary>Salt okuma: lisans kisitliyken de calisir (panel ekrani cizebilsin).</summary>
[AllowWhenLicenseRestricted]
public class ObjectAccessEndpoint : Endpoint<ObjectAccessCommand, ObjectAccessResponse>
{
    public override void Configure()
    {
        Post("authz/lookup/object-access");
        Summary(s =>
        {
            s.Summary = "Bir öznenin tek bir nesne üzerindeki yetkilerini döner";
            s.Description = "Elde tutulan bağlamın hâlâ geçerli olup olmadığını sınamak içindir. " +
                            "Yetki kalmamışsa relations boş döner; bu 'artık yetkili değil' demektir.";
        });
    }

    public override async Task HandleAsync(ObjectAccessCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record ObjectAccessCommand(
    Guid IsolationId,
    string User,
    string Object) : ICommand<ObjectAccessResponse>;

public class ObjectAccessCommandHandler(ILookupManager lookupManager)
    : ICommandHandler<ObjectAccessCommand, ObjectAccessResponse>
{
    public async Task<ObjectAccessResponse> ExecuteAsync(ObjectAccessCommand command, CancellationToken ct)
    {
        var result = await lookupManager.LookupObjectAccessAsync(
            command.IsolationId, command.User, command.Object, ct);

        return new ObjectAccessResponse(result.ObjectType, result.ObjectId, result.Relations);
    }
}

public class ObjectAccessValidator : Validator<ObjectAccessCommand>
{
    public ObjectAccessValidator()
    {
        RuleFor(x => x.IsolationId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.User).UserFormat();

        RuleFor(x => x.Object).ObjectFormat();
    }
}
