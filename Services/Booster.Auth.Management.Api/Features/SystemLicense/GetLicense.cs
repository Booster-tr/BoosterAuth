using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.Licensing;
using Booster.Auth.Common.Internal.Options;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Licensing;
using Booster.Auth.Manager.Options;
using Booster.Authorization.Fga.Attributes;
using Booster.Licensing.Domains;
using Booster.Licensing.Validation;
using FastEndpoints;

namespace Booster.Auth.Management.Api.Features.SystemLicense;

/// <summary>
/// Sistem &gt; Lisans ekraninin verisi. Lisans kisitliyken de calisir (yonetici lisansi gorup yenileyebilsin).
/// Her panel kullanicisi okur (banner ve yonlendirme icin; kurulum GUID'i gizli degildir).
/// Etkinlestirme yalnizca admin rolundedir; <see cref="GetLicenseResponse.CanActivate"/> bunu panele soyler.
/// </summary>
public class GetLicenseEndpoint(LicenseService license, ApiAuthOptions apiAuth, BootstrapOptions bootstrap)
    : EndpointWithoutRequest<GetLicenseResponse>
{
    public override void Configure()
    {
        Get("license");
        Group<SystemEndpointGroup>();
        Options(x => x
            .RequireAuthorization(new RequireScopeAttribute(ManagementPolicies.PanelScope))
            .WithMetadata(new AllowWhenLicenseRestricted()));
        Summary(s => s.Summary = "Kurulumun lisans durumu (kurulum GUID'i, plan, sure, limitler, domain kontrolu)");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var e = await license.GetEvaluationAsync(ct);
        await Send.OkAsync(GetLicenseResponse.From(e, DomainTargets(apiAuth, bootstrap)) with { CanActivate = CanActivate(User) }, ct);
    }

    /// <summary>Lisanstaki domain'lerle karsilastirilan adresler: issuer ve yonetim paneli.</summary>
    /// <summary>Etkinlestirme ucuyla ayni kural: yonetim havuzunun admin rolu.</summary>
    internal static bool CanActivate(System.Security.Claims.ClaimsPrincipal user) =>
        user.FindAll("role").Any(r => string.Equals(r.Value, AuthConstants.AdminRoleName, StringComparison.OrdinalIgnoreCase));

    internal static IReadOnlyList<(string Name, string Url)> DomainTargets(ApiAuthOptions apiAuth, BootstrapOptions bootstrap) =>
    [
        ("issuer", apiAuth.Authority),
        ("panel", bootstrap.ManagementClientRedirectUri),
    ];
}

public sealed record GetLicenseResponse(
    Guid InstallationId,
    LicenseState State,
    LicenseInvalidReason? InvalidReason,
    bool PanelWritable,
    bool EndUserFlowsAllowed,
    bool ClockRollbackDetected,
    DateTimeOffset EvaluatedAt,
    LicenseDetails? License,
    IReadOnlyList<DomainCheckResult> DomainChecks,
    bool CanActivate = false)
{
    /// <summary>Saat kaydi silinmis / degistirilmis: lisans suresi dolmus sayildi, yeniden etkinlestirme kapali.</summary>
    public bool ClockTampered { get; init; }

    /// <summary>Kullanim lisans limitini asiyor: panel salt okunur, silme serbest.</summary>
    public bool OverLimit { get; init; }

    public bool DeletesAllowed { get; init; }

    internal static GetLicenseResponse From(LicenseEvaluation e, IReadOnlyList<(string Name, string Url)> targets)
    {
        var r = e.Result;
        var l = r.License;
        var details = l is null
            ? null
            : new LicenseDetails(l.LicenseId, l.Customer.Name, l.Plan, l.Type, l.Limits.Users, l.Limits.Pools, l.Limits.Clients,
                l.AllowedDomains, l.IssuedAt, l.ExpiresAt, l.GraceDays, r.DaysLeft!.Value, r.GraceEndsAt!.Value, e.ActivatedAt, e.ActivatedBy);

        // Domain kontrolu YALNIZCA uyaridir: uymayan adres girisleri kesmez (bkz. CLAUDE.md lisans kurallari).
        var checks = targets
            .Where(t => !string.IsNullOrWhiteSpace(t.Url))
            .Select(t => new DomainCheckResult(t.Name, t.Url, DomainRules.NormalizeHost(t.Url),
                l is null
                    ? (DomainRules.IsTestHost(t.Url) ? DomainCheck.TestEnvironment : DomainCheck.NotAllowed)
                    : DomainRules.Check(t.Url, l.AllowedDomains)))
            .ToList();

        var snapshot = e.ToSnapshot();
        return new GetLicenseResponse(e.InstallationId, r.State, r.InvalidReason, snapshot.PanelWritable, r.EndUserFlowsAllowed,
            e.ClockRollbackDetected, r.EvaluatedAt, details, checks)
        {
            ClockTampered = e.ClockTampered,
            OverLimit = e.OverLimit,
            DeletesAllowed = snapshot.DeletesAllowed
        };
    }
}

public sealed record LicenseDetails(
    Guid LicenseId,
    string CustomerName,
    string Plan,
    string Type,
    int MaxUsers,
    int MaxPools,
    int MaxClients,
    IReadOnlyList<string> AllowedDomains,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    int GraceDays,
    int DaysLeft,
    DateTimeOffset GraceEndsAt,
    DateTimeOffset? ActivatedAt,
    string? ActivatedBy);

/// <param name="Name">issuer veya panel.</param>
/// <param name="Result">Allowed, TestEnvironment (panelde "test ortami" etiketi) veya NotAllowed (uyari).</param>
public sealed record DomainCheckResult(string Name, string Url, string? Host, DomainCheck Result);
