using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Common.Internal.Licensing;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Ldap;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features.IdentityProviders._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Hashing.TwoWays.Aes;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.IdentityProviders.TestLdap;

/// <summary>
/// Panel "Baglantiyi test et": LDAP sunucusuna baglanir, servis hesabiyla bind eder ve arama kokunu okur.
/// Kayit OLUSTURMADAN once denenebilsin diye formdaki ayarlarla calisir; parola bos gelirse kayitli saglayicinin
/// (sifreli) parolasi kullanilir. Parola hicbir yanitta donmez.
/// </summary>
public class TestLdapConnectionEndpoint(LdapDirectory directory, IAsyncRepository<IdentityProvider, AuthNDbContext> providerRepository,
    ISecretProtector secretProtector) : Endpoint<TestLdapConnectionRequest, TestLdapConnectionResponse>
{
    public override void Configure()
    {
        Post("ldap/test");
        Group<IdentityProvidersEndpointGroup>();
        // Yazma yapmaz: lisans kisitliyken de baglanti denenebilsin.
        Options(x => x
            .RequireAuthorization(
                new RequireScopeAttribute(ManagementPolicies.PanelScope),
                new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.ManagerRelation))
            .WithMetadata(new AllowWhenLicenseRestricted()));
        Summary(s => s.Summary = "LDAP baglantisini dener (baglan, servis hesabi bind, arama koku)");
    }

    public override async Task HandleAsync(TestLdapConnectionRequest req, CancellationToken ct)
    {
        var settings = new Dictionary<string, string>(IdentityProviderSettings.Normalize(req.Settings, FederationProviderCatalog.Ldap), StringComparer.Ordinal);

        var password = req.ClientSecret;
        if (string.IsNullOrEmpty(password) && req.IdentityProviderId is { } id)
        {
            var stored = await providerRepository.GetAsync(
                p => p.Id == id && p.UserPoolId == req.UserPoolId && p.ProviderType == FederationProviderCatalog.Ldap,
                enableTracking: false, cancellationToken: ct);
            if (stored?.ClientSecretEncrypted is { } enc)
                password = secretProtector.Unprotect(enc);
        }
        if (!string.IsNullOrEmpty(password))
            settings[LdapSettings.KeyBindPassword] = password;

        if (LdapSettings.Validate(settings) is { } invalid)
        {
            await Send.OkAsync(new TestLdapConnectionResponse(false, "invalid_" + invalid), ct);
            return;
        }

        var parsed = LdapSettings.From(settings);
        if (parsed is null)
        {
            await Send.OkAsync(new TestLdapConnectionResponse(false, "missing_settings"), ct);
            return;
        }

        var result = await directory.TestAsync(parsed, ct);
        await Send.OkAsync(new TestLdapConnectionResponse(result.Ok, result.Code), ct);
    }
}

public record TestLdapConnectionRequest(Guid UserPoolId, Guid? IdentityProviderId, Dictionary<string, string>? Settings, string? ClientSecret);

/// <param name="Code">ok | bind_failed | base_not_found | unreachable | tls_failed | missing_settings | invalid_port | invalid_security | invalid_user_filter | failed</param>
public record TestLdapConnectionResponse(bool Ok, string Code);
