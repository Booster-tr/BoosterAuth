using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.Auth.Idp.Grpc;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Microsoft.EntityFrameworkCore;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Licensing.Clock;
using Booster.Licensing.Runtime;
using Booster.Licensing.Validation;

namespace Booster.Auth.Management.Api.Licensing;

/// <summary>Son degerlendirme: dogrulama sonucu + kurulum bilgisi + saat geri alma isareti.</summary>
/// <param name="ClockTampered">Saat kaydi silinmis / degistirilmis: <see cref="Result"/> suresi dolmus (Restricted) sayilir.</param>
/// <param name="OverLimit">Kullanim lisans limitini asiyor: panel salt okunur, silme serbest.</param>
public sealed record LicenseEvaluation(
    Guid InstallationId,
    LicenseValidationResult Result,
    bool ClockRollbackDetected,
    DateTimeOffset? ActivatedAt,
    string? ActivatedBy,
    bool ClockTampered = false,
    bool OverLimit = false)
{
    public LicenseStatusSnapshot ToSnapshot()
    {
        var snapshot = LicenseStatusSnapshot.From(Result);
        return OverLimit && snapshot.PanelWritable
            ? snapshot with { PanelWritable = false, DeletesAllowed = true }
            : snapshot;
    }
}

/// <summary>
/// Kurulumun lisans durumu. Lisansin SAHIBI Management'tir; IdP ve AuthZ durumu gRPC ile buradan alir.
/// <para>
/// Durum en fazla <see cref="Freshness"/> eskiyebilir: her istekte veritabanina gidilmez, ama yeni token
/// yapistirildiginda servis yeniden baslamadan hemen gecerli olur (aktivasyon onbellegi tazeler).
/// Saat basi yeniden degerlendirme <see cref="LicenseMonitor"/> ile yapilir.
/// </para>
/// Singleton; veritabanina her seferinde yeni bir scope ile gider.
/// </summary>
public sealed partial class LicenseService(
    IServiceScopeFactory scopeFactory,
    ILicenseValidator validator,
    TimeProvider time,
    ILogger<LicenseService> logger) : ILicenseStatusProvider
{
    public static readonly TimeSpan Freshness = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private LicenseEvaluation? _current;
    private DateTimeOffset _evaluatedAt = DateTimeOffset.MinValue;

    public async ValueTask<LicenseStatusSnapshot> GetAsync(CancellationToken ct = default) =>
        (await GetEvaluationAsync(ct)).ToSnapshot();

    public async Task<LicenseEvaluation> GetEvaluationAsync(CancellationToken ct = default)
    {
        var current = _current;
        if (current is not null && time.GetUtcNow() - _evaluatedAt < Freshness)
            return current;
        return await EvaluateAsync(ct);
    }

    /// <summary>Durumu veritabanindan yeniden hesaplar. Durum degistiyse loglar.</summary>
    public async Task<LicenseEvaluation> EvaluateAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<InstallationStore>();
            var row = await store.EnsureAsync(ct);

            EffectiveTime now;
            var tampered = false;
            try
            {
                now = await new LicenseClock(store, time).GetEffectiveNowAsync(ct);
            }
            catch (LicenseClockTamperedException ex)
            {
                tampered = true;
                now = new EffectiveTime(time.GetUtcNow(), false);
                if (_current?.ClockTampered != true)
                    logger.LogWarning("Lisans saat kaydi degistirilmis ({Reason}); lisans suresi dolmus sayiliyor.", ex.Message);
            }

            var result = validator.Validate(row.LicenseToken, row.InstallationId, now.Now);
            if (tampered && result.License is not null)
                result = result with { State = LicenseState.Restricted };

            var evaluation = new LicenseEvaluation(
                row.InstallationId,
                result,
                now.ClockRollbackDetected,
                row.LicenseActivatedAt,
                row.LicenseActivatedBy,
                tampered,
                await IsOverLimitAsync(scope.ServiceProvider, result, ct));

            var previous = _current;
            if (previous is null || previous.Result.State != evaluation.Result.State)
                logger.LogWarning("Lisans durumu: {State} (onceki: {Previous}). Lisans: {LicenseId}, tur: {Type}, bitis: {ExpiresAt}, gecersizlik: {Reason}",
                    evaluation.Result.State, previous?.Result.State, evaluation.Result.License?.LicenseId,
                    evaluation.Result.License?.Type, evaluation.Result.License?.ExpiresAt, evaluation.Result.InvalidReason);
            if (now.ClockRollbackDetected && previous?.ClockRollbackDetected != true)
                logger.LogWarning("Sistem saati geri alinmis gorunuyor; lisans suresi icin gorulen en ileri zaman ({Now}) kullaniliyor.", now.Now);

            _current = evaluation;
            _evaluatedAt = time.GetUtcNow();
            return evaluation;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Yapistirilan token'i dogrular ve kaydeder. Gecersiz veya suresi (ek sure dahil) dolmus token REDDEDILIR ve
    /// KAYDEDILMEZ: aksi halde suresi dolmus bir deneme, cop bir token yapistirilarak son kullaniciya yeniden acilabilirdi.
    /// </summary>
    public async Task<LicenseEvaluation> ActivateAsync(string token, string activatedBy, CancellationToken ct)
    {
        var current = await GetEvaluationAsync(ct);

        // Saat kaydi silinmisse ayni anahtari yeniden yapistirmak sureyi sifirlardi. Cozum: kurulum kaydini yenileyip
        // (yeni kurulum GUID'i) lisansi portalda o kuruluma aktarmak.
        if (current.ClockTampered)
            throw new BusinessException(LicenseMessageKeys.ClockTampered);
        var candidate = validator.Validate(token, current.InstallationId, current.Result.EvaluatedAt);

        if (candidate.State == LicenseState.Invalid)
            throw new BusinessException(candidate.InvalidReason switch
            {
                LicenseInvalidReason.InstallationMismatch => LicenseMessageKeys.InstallationMismatch,
                LicenseInvalidReason.UnknownKid => LicenseMessageKeys.UnknownKey,
                LicenseInvalidReason.UnsupportedVersion => LicenseMessageKeys.UnsupportedVersion,
                _ => LicenseMessageKeys.InvalidToken,
            });
        if (candidate.State is LicenseState.None)
            throw new BusinessException(LicenseMessageKeys.InvalidToken);
        if (candidate.State is LicenseState.Restricted)
            throw new BusinessException(LicenseMessageKeys.Expired);

        using (var scope = scopeFactory.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<InstallationStore>();
            await store.SaveLicenseAsync(token.Trim(), activatedBy, time.GetUtcNow(), ct);
        }

        logger.LogWarning("Lisans etkinlestirildi: {LicenseId} ({Type}, {Plan}), bitis {ExpiresAt}, etkinlestiren {By}",
            candidate.License!.LicenseId, candidate.License.Type, candidate.License.Plan, candidate.License.ExpiresAt, activatedBy);

        return await EvaluateAsync(ct);
    }
}

public sealed partial class LicenseService
{
    private int? _lastUserCount;

    /// <summary>
    /// Kullanim limiti asiyor mu? Yeni kayit zaten limitte reddedilir; bu kontrol veritabanina dogrudan eklenen kayitlari
    /// ve daha kucuk pakete gecisi yakalar. Kullanici sayisi IdP'den alinir; alinamazsa son bilinen deger kullanilir.
    /// Yalnizca panel yazilabilir durumdayken anlamlidir (aksi halde panel zaten salt okunur).
    /// </summary>
    private async Task<bool> IsOverLimitAsync(IServiceProvider services, LicenseValidationResult result, CancellationToken ct)
    {
        if (!result.PanelWritable)
            return false;

        var limits = LicenseStatusSnapshot.From(result).Limits!;
        var db = services.GetRequiredService<AuthNDbContext>();

        var pools = await db.Set<UserPool>().CountAsync(p => p.Id != AuthConstants.ManagementPoolId, ct);
        var clients = await db.Set<Client>().CountAsync(c => c.Id != AuthConstants.ManagementClientId, ct);

        try
        {
            var idp = services.GetRequiredService<PrincipalLookupService.PrincipalLookupServiceClient>();
            _lastUserCount = (await idp.CountLicensedUsersAsync(new CountLicensedUsersRequest(),
                deadline: DateTime.UtcNow.AddSeconds(3), cancellationToken: ct)).Count;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Lisans kullanim kontrolu: kullanici sayisi IdP'den alinamadi; son bilinen deger kullaniliyor.");
        }

        var over = pools > limits.Pools || clients > limits.Clients || _lastUserCount > limits.Users;
        if (over && _current?.OverLimit != true)
            logger.LogWarning("Kullanim lisans limitini asiyor: kullanici {Users}/{MaxUsers}, havuz {Pools}/{MaxPools}, uygulama {Clients}/{MaxClients}. Panel salt okunur (silme serbest).",
                _lastUserCount, limits.Users, pools, limits.Pools, clients, limits.Clients);
        return over;
    }
}

/// <summary>Acilista ve saat basi lisansi yeniden degerlendirir (durum degisimleri loglanir).</summary>
public sealed class LicenseMonitor(LicenseService license, ILogger<LicenseMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try { await license.EvaluateAsync(stoppingToken); }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Lisans degerlendirmesi basarisiz; bir sonraki turda tekrar denenecek.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
