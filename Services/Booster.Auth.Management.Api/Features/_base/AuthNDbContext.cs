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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MassTransit outbox/inbox tabloları. Log mesajları iş işlemiyle AYNI
        // transaction'da outbox'a yazılır: broker o an kapalıysa bile kayıt
        // kaybolmaz, bağlantı geri geldiğinde teslim edilir.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
