using Microsoft.Extensions.Logging;

namespace Booster.Auth.Common.Internal.Helpers;

/// <summary>
/// Servisler arasi bildirim cagrilari (bildirim gonderme, rol / kapsam ad senkronu) icin: is akisi yaniti BEKLEMEZ.
/// <para>
/// Karar (Kafka kaldirildi): veri kaybi kabul edilir. Karsi servis kapaliysa ya da hata verirse cagri yeniden
/// denenmez, yalnizca loglanir. Istegin CancellationToken'i kullanilmaz: istek bitince cagri yarida kesilmesin.
/// </para>
/// </summary>
public static class FireAndForget
{
    // Notification SMTP / SMS gonderimini cagri icinde yapar; kisa tutulursa yavas sunucuda gonderim yarida kalirdi.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public static void Run(ILogger logger, string operation, Func<CancellationToken, Task> work)
    {
        _ = Task.Run(async () =>
        {
            using var cts = new CancellationTokenSource(Timeout);
            try
            {
                await work(cts.Token);
            }
            catch (Exception ex)
            {
                // Istegin icerigi YAZILMAZ: bildirimler alici adresi ve tek kullanimlik kod tasir (KVKK).
                logger.LogWarning(ex, "{Operation} cagrisi basarisiz; yeniden denenmeyecek.", operation);
            }
        });
    }
}
