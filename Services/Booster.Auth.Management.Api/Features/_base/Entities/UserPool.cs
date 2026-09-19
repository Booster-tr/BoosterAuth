using Booster.Auth.Database.Models.Enums;
using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Auth.Management.Api.Features._base.Entities;

public class UserPool : Entity
{
    public Guid TenantId { get; set; }

    public virtual Tenant Tenant { get; set; } = default!;

    public string Name { get; set; } = default!;

    public EntityStatus Status { get; set; } = EntityStatus.Active;

    public UserPoolPolicy Policy { get; set; } = new();

    public List<string> DefaultScopes { get; set; } = [];

    public List<Guid> DefaultRoleIds { get; set; } = [];

    public virtual ICollection<Client> Clients { get; set; } = [];
    public virtual ICollection<Role> Roles { get; set; } = [];
    public virtual ICollection<ScopeDefinition> ScopeDefinitions { get; set; } = [];
    
}


public class UserPoolPolicy
{
    public PasswordPolicy Password { get; set; } = new();
    public MfaPolicy Mfa { get; set; } = new();
    public SessionPolicy Session { get; set; } = new();
    public RegistrationPolicy Registration { get; set; } = new();
    public LoginPolicy Login { get; set; } = new();
}

public class PasswordPolicy
{
    public int MinLength { get; set; } = 8;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireNonAlphanumeric { get; set; } = false;
    public int ExpiryDays { get; set; } = 0;
    public int HistoryCount { get; set; } = 0;
}

public class MfaPolicy
{
    public bool Required { get; set; } = false;
    public MfaMethod AllowedMethods { get; set; } = MfaMethod.None;
}

public class SessionPolicy
{
    public int AccessTokenTtlSeconds { get; set; } = 900;        // 15 dk
    public int RefreshTokenTtlSeconds { get; set; } = 1_209_600; // 14 gün
    public int IdleTimeoutSeconds { get; set; } = 0;
}

public class RegistrationPolicy
{
    public RegistrationMode Mode { get; set; } = RegistrationMode.SelfSignup;
    public bool RequireEmailVerification { get; set; } = true;

    public bool RequirePhoneVerification { get; set; } = false;

    public bool AllowFederatedJit { get; set; } = true;
}

public class LoginPolicy
{
    public bool AllowLocalLogin { get; set; } = true;
    public List<IdentifierType> AllowedIdentifiers { get; set; } = [IdentifierType.Email];
    public bool AllowFederatedLogin { get; set; } = true;
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutSeconds { get; set; } = 900;
}

public class UserPoolConfiguration : IEntityTypeConfiguration<UserPool>
{
    public void Configure(EntityTypeBuilder<UserPool> builder)
    {
        builder.ToTable("UserPools");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().HasDefaultValue(EntityStatus.Active);

        builder.OwnsOne(x => x.Policy, policy =>
        {
            policy.ToJson();
            policy.OwnsOne(p => p.Password);
            policy.OwnsOne(p => p.Mfa);
            policy.OwnsOne(p => p.Session);
            policy.OwnsOne(p => p.Registration);
            policy.OwnsOne(p => p.Login);
        });

        builder.PrimitiveCollection(x => x.DefaultScopes);
        builder.PrimitiveCollection(x => x.DefaultRoleIds);

        builder.HasOne(w => w.Tenant).WithMany(t => t.UserPools).HasForeignKey(w => w.TenantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(w => w.Clients).WithOne(t => t.UserPool).HasForeignKey(w => w.UserPoolId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(w => w.Roles).WithOne(t => t.UserPool).HasForeignKey(w => w.UserPoolId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(w => w.ScopeDefinitions).WithOne(t => t.UserPool).HasForeignKey(w => w.UserPoolId).OnDelete(DeleteBehavior.Cascade);
    }
}
