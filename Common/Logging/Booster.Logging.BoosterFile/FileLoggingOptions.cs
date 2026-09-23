using Booster.Logging.Abstraction;

namespace Booster.Logging.BoosterFile;

/// <summary>Dosya log sink yapılandırması.</summary>
public class FileLoggingOptions
{
    /// <summary>Log dosyalarının yazılacağı klasör.</summary>
    public string Directory { get; set; } = "logs";

    /// <summary>Dosya adı ön eki. Günlük rolling ile <c>{Prefix}-{yyyyMMdd}.log</c> üretilir.</summary>
    public string FilePrefix { get; set; } = "booster";

    /// <summary>Bu seviyenin altındaki kayıtlar yazılmaz.</summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>Arka plana alınan log kuyruğunun kapasitesi. Dolduğunda en yeni kayıt düşürülür (asla bloklamaz).</summary>
    public int QueueCapacity { get; set; } = 10000;

    /// <summary>Arka plan worker'ın tek seferde diske yazacağı azami kayıt sayısı (batch).</summary>
    public int MaxBatchSize { get; set; } = 200;
}
