namespace Booster.Licensing.Clock;

/// <summary>
/// Gorulen en ileri zamanin saklandigi yer. Urunde veritabanidir (Faz 1.2).
/// Uygulama, kaydi YALNIZCA ileri tasimalidir (or. <c>UPDATE ... SET value = @v WHERE value &lt; @v</c>). Boylece ayni anda
/// calisan iki pod birbirinin degerini geri alamaz.
/// </summary>
public interface ILicenseClockStore
{
    Task<DateTimeOffset?> GetHighWaterMarkAsync(CancellationToken ct = default);
    Task AdvanceHighWaterMarkAsync(DateTimeOffset value, CancellationToken ct = default);
}

public readonly record struct EffectiveTime(DateTimeOffset Now, bool ClockRollbackDetected);

/// <summary>
/// Saat geri alma korumasi. Etkin zaman = max(sistem saati, saklanan en ileri zaman).
/// Sistem saati geri alinirsa lisans suresi uzamaz: eski en ileri zaman kullanilir ve geri alma isaretlenir
/// (panelde uyari icin).
/// </summary>
public sealed class LicenseClock(ILicenseClockStore store, TimeProvider time)
{
    /// <summary>Her istekte veritabanina yazmamak icin kayit en az bu kadar ilerlediginde guncellenir.</summary>
    public static readonly TimeSpan WriteThreshold = TimeSpan.FromMinutes(5);

    /// <summary>Saat esitlemesindeki (NTP) kucuk geri kaymalar geri alma sayilmaz.</summary>
    public static readonly TimeSpan RollbackTolerance = TimeSpan.FromMinutes(10);

    public async Task<EffectiveTime> GetEffectiveNowAsync(CancellationToken ct = default)
    {
        var systemNow = time.GetUtcNow();
        var stored = await store.GetHighWaterMarkAsync(ct);

        if (stored is null || systemNow - stored.Value >= WriteThreshold)
        {
            await store.AdvanceHighWaterMarkAsync(systemNow, ct);
            return new EffectiveTime(systemNow, false);
        }

        if (systemNow >= stored.Value) return new EffectiveTime(systemNow, false);

        return new EffectiveTime(stored.Value, stored.Value - systemNow > RollbackTolerance);
    }
}
