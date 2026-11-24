using MassTransit;
using Booster.Auth.Database.DbContextBase;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Manager.RequestContexts;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Booster.Auth.Idp.Features._base;

public class IdpDbContext(DbContextOptions<IdpDbContext> options, IRequestContextAccessor requestContextAccessor) : PostgresDbContext(options, requestContextAccessor)
{
    public DbSet<Principal> Principals => Set<Principal>();
    public DbSet<Consent> Consents => Set<Consent>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<LocalCredential> LocalCredentials => Set<LocalCredential>();
    public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();
    public DbSet<PrincipalIdentity> PrincipalIdentities => Set<PrincipalIdentity>();
    public DbSet<ServiceAccount> ServiceAccounts => Set<ServiceAccount>();
    public DbSet<PrincipalLink> PrincipalLinks => Set<PrincipalLink>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PrincipalRole> PrincipalRoles => Set<PrincipalRole>();
    public DbSet<PrincipalScope> PrincipalScopes => Set<PrincipalScope>();
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
    public DbSet<LegalDocumentTranslation> LegalDocumentTranslations => Set<LegalDocumentTranslation>();
    public DbSet<LegalProfile> LegalProfiles => Set<LegalProfile>();
    public DbSet<UiText> UiTexts => Set<UiText>();
    public DbSet<UiTextTranslation> UiTextTranslations => Set<UiTextTranslation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}