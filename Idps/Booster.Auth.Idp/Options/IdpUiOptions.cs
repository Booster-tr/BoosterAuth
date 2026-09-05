namespace Booster.Auth.Idp.Options;

public class IdpUiOptions
{
    public string BaseUrl { get; set; } = default!;

    public string PathPrefix =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
            ? uri.AbsolutePath.TrimEnd('/')
            : string.Empty;

    private string Local(string path) => $"{PathPrefix}{path}";

    public string GetAssetUrl(string path) => Local(path);

    public string LoginPath { get; set; } = "/login";
    public string RegisterPath { get; set; } = "/register";
    public string ConsentPath { get; set; } = "/consent";
    public string ErrorPath { get; set; } = "/error";
    public string AuthorizePath { get; set; } = "/authorize";

    public string GetLoginUrl(string interactionKey) =>
        Local($"{LoginPath}?interaction_key={Uri.EscapeDataString(interactionKey)}");

    public string GetRegisterUrl(string interactionKey) =>
        Local($"{RegisterPath}?interaction_key={Uri.EscapeDataString(interactionKey)}");

    public string GetConsentUrl(string clientKey, string scope, string interactionKey) =>
        Local($"{ConsentPath}?client_id={Uri.EscapeDataString(clientKey)}&scope={Uri.EscapeDataString(scope)}&interaction_key={Uri.EscapeDataString(interactionKey)}");

    public string GetAuthorizeUrl(string interactionKey) =>
        Local($"{AuthorizePath}?interaction_key={Uri.EscapeDataString(interactionKey)}");

    public string GetErrorUrl(string error, string? description = null)
    {
        var url = $"{ErrorPath}?error={Uri.EscapeDataString(error)}";
        if (!string.IsNullOrEmpty(description))
            url += $"&error_description={Uri.EscapeDataString(description)}";
        return Local(url);
    }
}