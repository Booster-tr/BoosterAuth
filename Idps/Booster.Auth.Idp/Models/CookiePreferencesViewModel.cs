namespace Booster.Auth.Idp.Models;

/// <summary>Cerez tercihleri formu. Karar yoksa kutular KAPALI gelir (varsayilan red).</summary>
public sealed record CookiePreferencesViewModel(
    bool Functional,
    bool Analytics,
    bool HasDecision,
    bool Saved,
    string? ReturnUrl);
