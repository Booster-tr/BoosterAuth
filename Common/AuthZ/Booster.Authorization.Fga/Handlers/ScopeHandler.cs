using Booster.Authorization.Fga.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace Booster.Authorization.Fga.Handlers;

public class ScopeHandler : AuthorizationHandler<ScopeRequirement>
{
    private const string ScopeClaimType = "scope";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        var scopeClaim = context.User.FindFirst(ScopeClaimType)?.Value;

        if (requirement.AcceptedScopes.Length > 0 && !string.IsNullOrEmpty(scopeClaim))
        {
            var tokenScopes = scopeClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (requirement.AcceptedScopes.Any(
                    accepted => tokenScopes.Contains(accepted, StringComparer.OrdinalIgnoreCase)))
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
