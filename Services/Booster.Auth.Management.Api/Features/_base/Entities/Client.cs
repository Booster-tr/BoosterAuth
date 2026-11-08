using Booster.Auth.Database.Models.Enums;
using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Auth.Management.Api.Features._base.Entities;

public class Client : Entity
{
    public Guid UserPoolId { get; set; }

    public virtual UserPool UserPool { get; set; } = default!;

    public string ClientKey { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public EntityStatus Status { get; set; } = EntityStatus.Active;

    public OAuthGrantType AllowedGrantTypes { get; set; } = OAuthGrantType.AuthorizationCode | OAuthGrantType.RefreshToken | OAuthGrantType.TokenExchange;

    public List<string> RedirectUris { get; set; } = [];

    public List<string> PostLogoutRedirectUris { get; set; } = [];

    public List<string> AllowedAudiences { get; set; } = [];

    public bool RequireConsent { get; set; } = true;

    public List<TokenScopePolicyItem> TokenScopePolicy { get; set; } = [];

    public virtual ICollection<ClientAllowedScope> AllowedScopes { get; set; } = [];


}

public class TokenScopePolicyItem
{
    public string Key { get; set; } = default!;

    /// <summary>Açıksa, token verilirken bu anahtarın değeri üzerinde yetki grafiği kontrol edilir.</summary>
    public bool PermissionCheckRequired { get; set; }

    public bool IsRequired { get; set; }

    public List<string> RequiredPermissions { get; set; } = [];

    public List<string> GrantedPermissions { get; set; } = [];
}


public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.UserPoolId).IsRequired();
        builder.Property(x => x.ClientKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().HasDefaultValue(EntityStatus.Active);
        builder.Property(x => x.AllowedGrantTypes).HasConversion<int>();
        builder.Property(x => x.RequireConsent).HasDefaultValue(true);

        builder.PrimitiveCollection(x => x.RedirectUris);
        builder.PrimitiveCollection(x => x.PostLogoutRedirectUris);
        builder.PrimitiveCollection(x => x.AllowedAudiences);

        builder.OwnsMany(x => x.TokenScopePolicy, p => p.ToJson());

        builder.HasIndex(x => x.UserPoolId).IsUnique(false);

        builder.HasIndex(x => x.ClientKey)
               .IsUnique()
               .HasFilter("\"DeletedTime\" IS NULL");

        builder.HasOne(x => x.UserPool)
               .WithMany(w=>w.Clients)
               .HasForeignKey(x => x.UserPoolId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.AllowedScopes)
               .WithOne(x => x.Client)
               .HasForeignKey(x => x.ClientId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}