namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.CredentialBased.Models;

public sealed record CredentialRequest(string Identifier, string Password);
