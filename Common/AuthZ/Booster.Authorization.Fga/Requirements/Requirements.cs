using Microsoft.AspNetCore.Authorization;

namespace Booster.Authorization.Fga.Requirements;

public class ScopeRequirement(string[] acceptedScopes) : IAuthorizationRequirement
{
    public string[] AcceptedScopes { get; } = acceptedScopes;
}

public class RoleRequirement(string[] acceptedRoles) : IAuthorizationRequirement
{
    public string[] AcceptedRoles { get; } = acceptedRoles;
}

public class FgaRequirement(string resourceType, string targetSelector, string permission)
    : IAuthorizationRequirement
{
    public string ResourceType { get; } = resourceType;

    public string TargetSelector { get; } = targetSelector;

    public string Permission { get; } = permission;
}
