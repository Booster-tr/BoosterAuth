using Booster.Logging.Abstraction;

namespace Booster.Logging.BoosterDatabase;

/// <summary>Veritabanı log sink yapılandırması. Bağlantı bilgisi DI kaydında verilir; sink kendi tablosunu yönetir.</summary>
public class DatabaseLoggingOptions
{
    /// <summary>PostgreSQL bağlantı dizesi.</summary>
    public string ConnectionString { get; set; } = default!;

    /// <summary>Şema adı.</summary>
    public string Schema { get; set; } = "public";

    /// <summary>Log tablosu adı. Yoksa ilk yazımda otomatik oluşturulur.</summary>
    public string Table { get; set; } = "Logs";

    /// <summary>Bu seviyenin altındaki kayıtlar yazılmaz.</summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>Arka plana alınan log kuyruğunun kapasitesi. Dolduğunda en yeni kayıt düşürülür (asla bloklamaz).</summary>
    public int QueueCapacity { get; set; } = 10000;

    /// <summary>Arka plan worker'ın tek INSERT'te yazacağı azami kayıt sayısı (batch).</summary>
    public int MaxBatchSize { get; set; } = 200;

    public void Validate() => ArgumentException.ThrowIfNullOrWhiteSpace(ConnectionString);
}
