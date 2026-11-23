using Booster.Auth.Common.Internal.Licensing;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Fga;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.Auth.Z.Features._base;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.Check.CheckPermission;

/// <summary>Salt okuma: lisans kisitliyken de calisir (panel ekrani cizebilsin).</summary>
[AllowWhenLicenseRestricted]
public class CheckPermissionEndpoint : Endpoint<CheckPermissionCommand, CheckPermissionResponse>
{
    public override void Configure()
    {
        Post("authz/check");
        Summary(s =>
        {
            s.Summary = "Bir öznenin bir kaynak üzerindeki yetkisini sorgular";
            s.Description = "Sorulan özne (user) istekten gelir; token yalnızca ÇAĞIRANIN bu ucu " +
                            "kullanma yetkisini doğrular, sorgunun öznesi değildir.";
        });
    }

    public override async Task HandleAsync(CheckPermissionCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record CheckPermissionCommand(
    Guid IsolationId,
    string User,
    string Relation,
    string Object) : ICommand<CheckPermissionResponse>;

public readonly record struct CheckPermissionResponse(bool Allowed);

public class CheckPermissionCommandHandler(
    IPoolStoreProvider poolStoreProvider,
    FgaRelationManager relationManager)
    : ICommandHandler<CheckPermissionCommand, CheckPermissionResponse>
{
    public async Task<CheckPermissionResponse> ExecuteAsync(CheckPermissionCommand command, CancellationToken ct)
    {
        var mapping = await poolStoreProvider.GetRequiredByPoolIdAsync(command.IsolationId, ct);

        var allowed = await relationManager.CheckAsync(
            mapping.StoreId,
            command.User,
            command.Relation,
            command.Object,
            mapping.ActiveModelId,
            ct);

        return new CheckPermissionResponse(allowed);
    }
}

public class CheckPermissionValidator : Validator<CheckPermissionCommand>
{
    public CheckPermissionValidator()
    {
        RuleFor(x => x.IsolationId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.User).UserFormat();
        RuleFor(x => x.Relation).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Object).ObjectFormat();
    }
}
