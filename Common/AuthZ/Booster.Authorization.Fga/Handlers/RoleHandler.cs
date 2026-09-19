using Booster.Authorization.Fga.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace Booster.Authorization.Fga.Handlers;

public class RoleHandler : AuthorizationHandler<RoleRequirement>
{
    private const string RoleClaimType = "role";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RoleRequirement requirement)
    {
        if (requirement.AcceptedRoles.Length == 0)
            return Task.CompletedTask;

        var tokenRoles = context.User.FindAll(RoleClaimType).Select(c => c.Value).ToArray();

        if (tokenRoles.Length > 0 &&
            requirement.AcceptedRoles.Any(
                accepted => tokenRoles.Contains(accepted, StringComparer.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
