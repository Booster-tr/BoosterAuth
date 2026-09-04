using Booster.Database.Settings;
using System.ComponentModel.DataAnnotations;

namespace Booster.Database.Postgres.Settings;

public class PostgresSettings : IDbSettings
{
    [Obsolete]
    public string DatabaseName { get; set; } = "Unknown";
    [Required]
    public string ConnectionString { get; set; } = default!;
    [Required]
    public bool ApplyMigration { get; set; } = default!;
}
