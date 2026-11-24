using Booster.Auth.Common.Internal.Constants;
using Booster.Authorization.Fga.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace Booster.Auth.Common.Internal.Authorization;

/// <summary>
/// Panel scope'u (<c>booster:panel</c>) isteyen her uc YALNIZCA yonetim havuzunun token'ini kabul eder.
/// Rol / scope kontrolu yalnizca token'daki ifadeye bakar; musteri havuzu kendi icinde <c>admin</c> rolu ve
/// <c>booster:panel</c> scope'u tanimlayip kurulum geneli uclara ulasabiliyordu. FGA kontrolu de deposunu token'in
/// <c>actor</c>'undan sectigi icin musterinin kendi deposuna yazdigi bir kayitla yonetim nesnelerine yetki alinabiliyordu.
/// Handler basari vermez; yalnizca kosul tutmazsa <see cref="AuthorizationHandlerContext.Fail()"/> ile reddeder
/// (Fail, diger handler'larin basarisini ezer).
/// </summary>
public sealed class PanelActorHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        if (!requirement.AcceptedScopes.Contains(AuthConstants.ManagementPanelScope, StringComparer.OrdinalIgnoreCase))
            return Task.CompletedTask;

        if (!IsManagementActor(context.User.FindFirst("actor")?.Value))
            context.Fail();

        return Task.CompletedTask;
    }

    public static bool IsManagementActor(string? actor)
        => Guid.TryParse(actor, out var id) && id == AuthConstants.ManagementPoolId;
}
