using FastEndpoints;

namespace Booster.Auth.Management.Api.Features._base;

public class TenantsEndpointGroup : Group
{
    public TenantsEndpointGroup()
        => Configure("tenants", ep => ep.Description(b => b.WithTags("Tenants")));
}

public class UserPoolsEndpointGroup : Group
{
    public UserPoolsEndpointGroup()
        => Configure("userpools", ep => ep.Description(b => b.WithTags("UserPools")));
}

public class RolesEndpointGroup : Group
{
    public RolesEndpointGroup()
        => Configure("roles", ep => ep.Description(b => b.WithTags("Roles")));
}

public class RoleScopesEndpointGroup : Group
{
    public RoleScopesEndpointGroup()
        => Configure("role-scopes", ep => ep.Description(b => b.WithTags("RoleScopes")));
}

public class ScopeDefinitionsEndpointGroup : Group
{
    public ScopeDefinitionsEndpointGroup()
        => Configure("scope-definitions", ep => ep.Description(b => b.WithTags("ScopeDefinitions")));
}

public class ClientsEndpointGroup : Group
{
    public ClientsEndpointGroup()
        => Configure("clients", ep => ep.Description(b => b.WithTags("Clients")));
}

public class IdentityProvidersEndpointGroup : Group
{
    public IdentityProvidersEndpointGroup()
        => Configure("identity-providers", ep => ep.Description(b => b.WithTags("IdentityProviders")));
}
