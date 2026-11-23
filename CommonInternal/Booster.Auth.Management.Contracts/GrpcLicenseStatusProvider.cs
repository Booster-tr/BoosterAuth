using Booster.Auth.Management.Grpc;
using Booster.Licensing.Runtime;
using Booster.Licensing.Validation;

namespace Booster.Auth.Management.Contracts;

/// <summary>
/// IdP ve AuthZ icin lisans durumu: Management'tan gRPC ile alinir, <see cref="CacheFor"/> boyunca bellekte tutulur.
/// <para>
/// Management'a ulasilamazsa son bilinen durum kullanilir; hic bilinmiyorsa <see cref="LicenseStatusSnapshot.Unknown"/>
/// (her sey acik). Boylece bir altyapi arizasi son kullanici girisini ASLA kesmez.
/// </para>
/// Singleton olarak kaydedilir.
/// </summary>
public sealed class GrpcLicenseStatusProvider(LicenseStatusService.LicenseStatusServiceClient client) : ILicenseStatusProvider
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(3);

    private readonly SemaphoreSlim _refresh = new(1, 1);
    private LicenseStatusSnapshot? _last;
    private DateTimeOffset _fetchedAt = DateTimeOffset.MinValue;

    public async ValueTask<LicenseStatusSnapshot> GetAsync(CancellationToken ct = default)
    {
        if (_last is not null && DateTimeOffset.UtcNow - _fetchedAt < CacheFor)
            return _last;

        // Ayni anda gelen isteklerden yalnizca biri Management'i cagirir; digerleri son bilinen durumu kullanir.
        if (!await _refresh.WaitAsync(0, ct))
            return _last ?? LicenseStatusSnapshot.Unknown;

        try
        {
            var r = await client.GetStatusAsync(new GetLicenseStatusRequest(),
                deadline: DateTime.UtcNow.Add(CallTimeout), cancellationToken: ct);
            _last = new LicenseStatusSnapshot((LicenseState)r.State, r.IsTrial, r.PanelWritable, r.EndUserFlowsAllowed);
            _fetchedAt = DateTimeOffset.UtcNow;
            return _last;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Bir sonraki istek yeniden denesin diye zaman damgasi ilerletilmez.
            return _last ?? LicenseStatusSnapshot.Unknown;
        }
        finally
        {
            _refresh.Release();
        }
    }
}
