using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Licensing.Clock;
using Booster.Licensing.Runtime;
using Booster.Licensing.Validation;

namespace Booster.Auth.Management.Api.Licensing;

/// <summary>Son degerlendirme: dogrulama sonucu + kurulum bilgisi + saat geri alma isareti.</summary>
public sealed record LicenseEvaluation(
    Guid InstallationId,
    LicenseValidationResult Result,
    bool ClockRollbackDetected,
    DateTimeOffset? ActivatedAt,
    string? ActivatedBy);

/// <summary>
/// Kurulumun lisans durumu. Lisansin SAHIBI Management'tir; IdP ve AuthZ durumu gRPC ile buradan alir.
/// <para>
/// Durum en fazla <see cref="Freshness"/> eskiyebilir: her istekte veritabanina gidilmez, ama yeni token
/// yapistirildiginda servis yeniden baslamadan hemen gecerli olur (aktivasyon onbellegi tazeler).
/// Saat basi yeniden degerlendirme <see cref="LicenseMonitor"/> ile yapilir.
/// </para>
/// Singleton; veritabanina her seferinde yeni bir scope ile gider.
/// </summary>
public sealed class LicenseService(
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
        LicenseStatusSnapshot.From((await GetEvaluationAsync(ct)).Result);

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
            var now = await new LicenseClock(store, time).GetEffectiveNowAsync(ct);

            var evaluation = new LicenseEvaluation(
                row.InstallationId,
                validator.Validate(row.LicenseToken, row.InstallationId, now.Now),
                now.ClockRollbackDetected,
                row.LicenseActivatedAt,
                row.LicenseActivatedBy);

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
