using Booster.Authorization.Fga.Attributes;
using Booster.Authorization.Fga.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Booster.Authorization.Fga.Authorization;

public class CustomPermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider = new(options);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(RequireScopeAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var scopes = Split(policyName, RequireScopeAttribute.PolicyPrefix.Length, ',');

            return Task.FromResult<AuthorizationPolicy?>(
                Build(new ScopeRequirement(scopes)));
        }

        if (policyName.StartsWith(RequireRoleAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var roles = Split(policyName, RequireRoleAttribute.PolicyPrefix.Length, ',');

            return Task.FromResult<AuthorizationPolicy?>(
                Build(new RoleRequirement(roles)));
        }

        if (policyName.StartsWith(RequireFgaAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var parts = policyName[RequireFgaAttribute.PolicyPrefix.Length..].Split('|');

            if (parts.Length == 3)
            {
                return Task.FromResult<AuthorizationPolicy?>(
                    Build(new FgaRequirement(parts[0], parts[1], parts[2])));
            }
        }

        return _fallbackProvider.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        => _fallbackProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        => _fallbackProvider.GetFallbackPolicyAsync();

    private static AuthorizationPolicy Build(IAuthorizationRequirement requirement)
        => new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(requirement)
            .Build();

    private static string[] Split(string policyName, int prefixLength, char separator)
        => policyName[prefixLength..]
            .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
