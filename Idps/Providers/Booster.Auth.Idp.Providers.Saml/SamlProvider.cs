using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Schemas;
using System.Collections.Specialized;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Security;

namespace Booster.Auth.Idp.Providers.Saml;

public sealed class SamlProvider(IHttpClientFactory httpClientFactory)
    : FederationProviderBase(httpClientFactory)
{
    public const string Type = "saml";

    public override string ProviderType => Type;

    protected override string DefaultScopes => string.Empty;

    public const string ResponseParameter = "SAMLResponse";

    public override Task<FederationChallenge> BuildChallengeAsync(
        FederationContext context, ProviderConfig config, CancellationToken ct)
    {
        var settings = BuildSettings(config);

        var request = new Saml2AuthnRequest(settings)
        {
            AssertionConsumerServiceUrl = new Uri(context.RedirectUri)
        };

        var binding = new Saml2RedirectBinding { RelayState = context.State };

        var bound = binding.Bind(request);

        return Task.FromResult(new FederationChallenge
        {
            RedirectUrl = bound.RedirectLocation.OriginalString,
            State = context.State
        });
    }

    public override Task<FederationResult> HandleCallbackAsync(
        FederationCallback callback, ProviderConfig config, CancellationToken ct)
    {
        if (!config.IsActive)
            return Task.FromResult(FederationResult.Fail(FederationFailures.ProviderInactive));

        if (callback.Value(ResponseParameter) is null)
            return Task.FromResult(FederationResult.Fail(FederationFailures.CodeMissing));

        Saml2AuthnResponse response;
        try
        {
            response = new Saml2AuthnResponse(BuildSettings(config));

            var request = ToSamlRequest(callback);
            var binding = new Saml2PostBinding();

            binding.ReadSamlResponse(request, response);

            binding.Unbind(request, response);
        }
        catch (Exception)
        {
            return Task.FromResult(FederationResult.Fail(FederationFailures.AssertionInvalid));
        }

        if (response.Status != Saml2StatusCodes.Success)
            return Task.FromResult(FederationResult.Fail(FederationFailures.AssertionInvalid));

        var identity = response.ClaimsIdentity;

        var subjectSetting = Setting(config, FederationSettings.SubjectClaim);

        var subject = subjectSetting is not null
            ? identity?.FindFirst(subjectSetting)?.Value
            : response.NameId?.Value;

        if (string.IsNullOrEmpty(subject))
            return Task.FromResult(FederationResult.Fail(FederationFailures.SubjectMissing));

        var email = identity?.FindFirst(
            Setting(config, FederationSettings.EmailClaim) ?? ClaimTypes.Email)?.Value;

        var name = identity?.FindFirst(
            Setting(config, FederationSettings.NameClaim) ?? ClaimTypes.Name)?.Value;

        return Task.FromResult(FederationResult.Success(new ExternalIdentity
        {
            Subject = subject,
            Email = email,
            EmailVerified = !string.IsNullOrEmpty(email),
            DisplayName = name
        }));
    }

    private static Saml2Configuration BuildSettings(ProviderConfig config)
    {
        var spEntityId = Setting(config, FederationSettings.SpEntityId)
            ?? throw new InvalidOperationException(FederationFailures.ProviderMisconfigured);

        var signOnUrl = Setting(config, FederationSettings.SignOnUrl)
            ?? throw new InvalidOperationException(FederationFailures.ProviderMisconfigured);

        var certificate = Setting(config, FederationSettings.SigningCertificate)
            ?? throw new InvalidOperationException(FederationFailures.ProviderMisconfigured);

        var settings = new Saml2Configuration
        {
            Issuer = spEntityId,

            SingleSignOnDestination = new Uri(signOnUrl),

            AllowedIssuer = Setting(config, FederationSettings.IdpEntityId),

            AudienceRestricted = true,

            CertificateValidationMode = X509CertificateValidationMode.None,
            RevocationMode = X509RevocationMode.NoCheck,

            SignAuthnRequest = false,

            DetectReplayedTokens = false
        };

        settings.AllowedAudienceUris.Add(spEntityId);

        settings.SignatureValidationCertificates.Add(
            X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certificate)));

        return settings;
    }

    private static ITfoxtec.Identity.Saml2.Http.HttpRequest ToSamlRequest(FederationCallback callback)
    {
        var form = new NameValueCollection();

        foreach (var (key, value) in callback.Parameters)
            form.Add(key, value);

        return new ITfoxtec.Identity.Saml2.Http.HttpRequest
        {
            Method = "POST",
            Form = form,
            Binding = new Saml2PostBinding()
        };
    }
}
