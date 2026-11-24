using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.Licensing;
using Booster.Auth.Idp.Grpc;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Management.Api.Features.SystemLicense;

/// <summary>
/// Sistem &gt; Kullanim ve limitler ile dashboard kartinin verisi: her limit icin kullanilan / toplam.
/// Sayimlar limit kontroluyle AYNI kurala uyar (yonetim havuzu ve silinmisler haric).
/// <para>
/// 30 sn onbellek YALNIZCA gosterim icindir; olusturma anindaki kontrol her zaman canli sayimla yapilir.
/// </para>
/// </summary>
public class GetLicenseUsageEndpoint(
    AuthNDbContext db,
    ILicenseLimitService limits,
    PrincipalLookupService.PrincipalLookupServiceClient idp,
    ILogger<GetLicenseUsageEndpoint> logger)
    : EndpointWithoutRequest<LicenseUsageResponse>
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);
    private static LicenseUsageResponse? _cached;
    private static DateTimeOffset _cachedAt;

    public override void Configure()
    {
        Get("license/usage");
        Group<SystemEndpointGroup>();
        Options(x => x
            .RequireAuthorization(new RequireScopeAttribute(ManagementPolicies.PanelScope))
            .WithMetadata(new AllowWhenLicenseRestricted()));
        Summary(s => s.Summary = "Lisans limitlerinin kullanimi (kullanici, havuz, uygulama)");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (_cached is not null && DateTimeOffset.UtcNow - _cachedAt < CacheFor)
        {
            await Send.OkAsync(_cached, ct);
            return;
        }

        var pools = await db.Set<UserPool>().CountAsync(p => p.Id != AuthConstants.ManagementPoolId, ct);
        var clients = await db.Set<Client>().CountAsync(c => c.Id != AuthConstants.ManagementClientId, ct);

        int? users = null;
        try
        {
            users = (await idp.CountLicensedUsersAsync(new CountLicensedUsersRequest(),
                deadline: DateTime.UtcNow.AddSeconds(5), cancellationToken: ct)).Count;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // IdP'ye ulasilamazsa kullanici sayisi bos doner; ekran diger limitleri gostermeye devam eder.
            logger.LogWarning(ex, "Kullanici sayisi IdP'den alinamadi.");
        }

        var result = new LicenseUsageResponse(
            Item(users, await limits.GetLimitAsync(LicensedResource.Users, ct)),
            Item(pools, await limits.GetLimitAsync(LicensedResource.Pools, ct)),
            Item(clients, await limits.GetLimitAsync(LicensedResource.Clients, ct)));

        if (users is not null)
        {
            _cached = result;
            _cachedAt = DateTimeOffset.UtcNow;
        }

        await Send.OkAsync(result, ct);
    }

    private static LicenseUsageItem Item(int? used, int? limit) =>
        new(used, limit, used is null || limit is null ? null : Math.Max(limit.Value - used.Value, 0));
}

public sealed record LicenseUsageResponse(LicenseUsageItem Users, LicenseUsageItem Pools, LicenseUsageItem Clients);

/// <param name="Used">Kullanilan; bilinemiyorsa null.</param>
/// <param name="Limit">Lisans limiti; lisans durumu bilinemiyorsa null.</param>
/// <param name="Remaining">Kalan (eksiye dusmez; limit asilmissa 0).</param>
public sealed record LicenseUsageItem(int? Used, int? Limit, int? Remaining);
