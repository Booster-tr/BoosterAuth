using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booster.LoggingService.Features.Logs._base.Entities;

public class Log : Entity
{
    public Guid? UserId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string Level { get; set; } = "Information";
    public string Message { get; set; } = default!;
    public string? Exception { get; set; }
    public string? CorrelationId { get; set; }
}

public class LogConfiguration : IEntityTypeConfiguration<Log>
{
    public void Configure(EntityTypeBuilder<Log> builder)
    {
        builder.ToTable("Logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime).IsRequired();
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.UserId).IsRequired(false);
        builder.Property(x => x.IpAddress).IsRequired().HasMaxLength(150);
        builder.Property(x => x.ServiceName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Level).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.Exception).IsRequired();
        builder.Property(x => x.CorrelationId).IsRequired(false);

        builder.HasIndex(w => w.UserId).IsUnique(false);

    }
}

