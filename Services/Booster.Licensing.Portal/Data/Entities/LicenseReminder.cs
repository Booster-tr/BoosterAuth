using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.Licensing.Portal.Data.Entities;

/// <summary>Gonderilmis yenileme hatirlatmasi. (LicenseId, Kind) tekildir: ayni hatirlatma iki kez gitmez.</summary>
public class LicenseReminder : Entity
{
    public Guid LicenseId { get; set; }

    /// <summary>Bitise kac gun kala: 30 veya 7.</summary>
    public int Kind { get; set; }
}

internal sealed class LicenseReminderConfiguration : IEntityTypeConfiguration<LicenseReminder>
{
    public void Configure(EntityTypeBuilder<LicenseReminder> builder)
    {
        builder.ToTable("LicenseReminders");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.LicenseId, x.Kind }).IsUnique();
    }
}
