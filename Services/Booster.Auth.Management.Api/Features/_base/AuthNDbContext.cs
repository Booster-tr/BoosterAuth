using Booster.Amqp.Outbox;
using Booster.Auth.Database.DbContextBase;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Manager.RequestContexts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Booster.Auth.Management.Api.Features._base;

public class AuthNDbContext(
    DbContextOptions<AuthNDbContext> options,
    IRequestContextAccessor requestContextAccessor)
    : PostgresDbContext(options, requestContextAccessor)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<UserPool> UserPools => Set<UserPool>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientAllowedScope> ClientAllowedScopes => Set<ClientAllowedScope>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RoleScope> RoleScopes => Set<RoleScope>();
    public DbSet<ScopeDefinition> ScopeDefinitions => Set<ScopeDefinition>();
    public DbSet<IdentityProvider> IdentityProviders => Set<IdentityProvider>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        modelBuilder.AddBoosterOutbox();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
