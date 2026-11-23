using Booster.Licensing.Portal.Data;
using Booster.Licensing.Portal.Data.Entities;
using Booster.Licensing.Tokens;
using Booster.Licensing.Validation;
using Microsoft.EntityFrameworkCore;

namespace Booster.Licensing.Portal.Services;

public sealed record IssueLicenseRequest(
    Guid CustomerId,
    Guid InstallationId,
    string Plan,
    string Type,
    int MaxUsers,
    int MaxPools,
    int MaxClients,
    IReadOnlyList<string> AllowedDomains,
    int DurationDays,
    int GraceDays,
    string IssuedBy,
    string? Note);

/// <summary>Lisans uretir, imzalar, kendi public key'iyle hemen dogrular ve kaydeder.</summary>
public sealed class LicenseIssuer(LicensingDbContext db, SigningKeyStore keys, TimeProvider time)
{
    /// <summary>Varsayilanlar (kullanici aksini girmedikce).</summary>
    public static class Defaults
    {
        public const int TrialDays = 7, TrialGraceDays = 3, TrialUsers = 100, TrialPools = 1, TrialClients = 3;
        public const int PaidDays = 365, PaidGraceDays = 14;
    }

    public async Task<IssuedLicense> IssueAsync(IssueLicenseRequest req, CancellationToken ct)
    {
        if (!LicenseTypes.IsKnown(req.Type)) throw new ArgumentException("Lisans turu trial veya paid olmali.");
        if (req.InstallationId == Guid.Empty) throw new ArgumentException("Kurulum GUID'i bos olamaz.");
        if (req.AllowedDomains.Count == 0) throw new ArgumentException("En az bir domain gerekli.");
        if (req.MaxUsers < 1 || req.MaxPools < 1 || req.MaxClients < 1) throw new ArgumentException("Limitler en az 1 olmali.");
        if (req.DurationDays < 1 || req.GraceDays < 0) throw new ArgumentException("Sure en az 1 gun, ek sure 0 veya fazla olmali.");

        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == req.CustomerId, ct)
                       ?? throw new ArgumentException("Musteri bulunamadi.");

        var signingKey = await keys.GetActiveAsync(ct);
        // Saniye alti kesilir: token'daki tarihler okunakli olsun ve DB'deki degerle birebir eslessin.
        var now = DateTimeOffset.FromUnixTimeSeconds(time.GetUtcNow().ToUnixTimeSeconds());

        var payload = new LicensePayload
        {
            Kid = signingKey.Kid,
            LicenseId = Guid.CreateVersion7(),
            Customer = new LicenseCustomer(customer.Id, customer.Name),
            Plan = req.Plan.Trim(),
            Type = req.Type,
            Limits = new LicenseLimits(req.MaxUsers, req.MaxPools, req.MaxClients),
            AllowedDomains = req.AllowedDomains,
            IssuedAt = now,
            ExpiresAt = now.AddDays(req.DurationDays),
            GraceDays = req.GraceDays,
            InstallationId = req.InstallationId,
        };

        var token = signingKey.Issue(payload);

        // Bozuk bir token musteriye ASLA gitmesin: urunun kullandigi dogrulayiciyla ayni kontrol.
        var check = new LicenseValidator(await keys.GetPublicKeysAsync(ct)).Validate(token, req.InstallationId, now);
        if (check.State != LicenseState.Active && check.State != LicenseState.Warning)
            throw new InvalidOperationException($"Uretilen lisans dogrulanamadi ({check.State}/{check.InvalidReason}).");

        var entity = new IssuedLicense
        {
            Id = payload.LicenseId,
            CustomerId = customer.Id,
            InstallationId = req.InstallationId,
            Plan = payload.Plan,
            Type = payload.Type,
            MaxUsers = req.MaxUsers,
            MaxPools = req.MaxPools,
            MaxClients = req.MaxClients,
            AllowedDomains = [.. req.AllowedDomains],
            IssuedAt = payload.IssuedAt,
            ExpiresAt = payload.ExpiresAt,
            GraceDays = payload.GraceDays,
            Kid = payload.Kid,
            Token = token,
            IssuedBy = req.IssuedBy,
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
        };
        db.IssuedLicenses.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }
}
