---
name: booster-auth
description: Integrate an application with Booster Auth, a self-hosted OpenID Connect identity provider with user pools, roles, scopes and OpenFGA-based authorization. Use when adding sign-in / sign-up / logout with Booster Auth, protecting an API with its tokens, hiding sign-in methods per platform, granting roles or scopes to users from a backend, or writing / checking authorization relations.
---

# Booster Auth integration

Booster Auth is a standard OpenID Connect (OIDC) provider. Prefer the platform's standard OIDC library over hand-written HTTP.
Replace `https://auth.company.com` with the customer's Booster Auth address; ask the user for it and for the `client_id` if unknown.

## Core facts

| Item | Value |
| --- | --- |
| Issuer / authority | `https://auth.company.com/idp` (the same for every pool) |
| Discovery | `https://auth.company.com/idp/.well-known/openid-configuration` |
| Authorize / token | `/idp/authorize`, `/idp/token` |
| JWKS | `/idp/.well-known/jwks.json` (RS256) |
| Userinfo | `/idp/me` |
| Logout | `/idp/logout` |
| Flow | Authorization Code + PKCE. `code_challenge_method=S256` is REQUIRED; implicit flow is not supported. |
| Scopes | `openid profile email offline_access` + scopes allowed for the application |
| Audience | Pass `resource=<API address>`; it becomes the access token's `aud`. It must be in the application's allowed APIs. |

- The user pool is selected by the `client_id`; there is no pool parameter.
- Access token claims: `sub`, `aud`, `azp`, `scope` (space separated), `role` (one per role), `sid`, `principal_type`, `actor` (pool id).
- ID token claims: `sub`, `name`, `email`, `email_verified`, `nonce`, `auth_time`.
- In .NET set `MapInboundClaims = false` so `sub`, `email`, `role` keep their names.
- Role / scope changes appear in the user's NEXT token, not in tokens already issued.

## Sign-in options

- Sign-up screen: add `prompt=create` to the authorize request. Force re-authentication: `prompt=login`.
- Hide sign-in methods on a platform: `hidden_providers=<comma separated>` on the authorize request.
  Values: a provider type (`apple`, `google`, `microsoft`, `github`, `facebook`, `linkedin`, `x`, `oidc`, `saml`), a provider key,
  `email_code`, `phone_code`. Example for Android: `hidden_providers=apple`. It only narrows the screen; it is not a security rule.
- Self-service account page (profile, e-mail, phone, password, sessions, account deletion):
  `https://auth.company.com/idp/ui/account?client_id=<client_id>&return_url=<url>`. `return_url` must share scheme+host+port with a
  registered redirect or post-logout URI.

## Logout

Redirect to `/idp/logout` with `post_logout_redirect_uri` AND either `client_id` or `id_token_hint`; otherwise the user stays on the
Booster Auth "signed out" page. The URI must be registered as a post-logout URI on the application.
ASP.NET Core does not send `client_id` and only sends `id_token_hint` when `SaveTokens = true`, so add:

```csharp
o.Events.OnRedirectToIdentityProviderForSignOut = ctx =>
{
    ctx.ProtocolMessage.ClientId = "<client_id>";
    return Task.CompletedTask;
};
```

## Protecting an API

Validate the bearer token with the issuer's JWKS; check `aud` equals your API address and authorize with `scope` / `role` claims.

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = "https://auth.company.com/idp";
        o.Audience = "https://api.company.com";
        o.MapInboundClaims = false;
    });
```

## Backend: pool APIs (grant roles / scopes, relations)

Use these ONLY from a backend. They require the token of a service account created on the pool's "Service Accounts" screen in the
panel; user tokens get `403`. Never ship the service account secret to a browser or mobile app. The pool is the one the service account
was created for; it is never sent in the request.

The permission chosen for the service account decides what it may call:
- Viewer: `GET .../access`, `relations/check`
- Manager: viewer + `access/grant`, `access/revoke`
- Grant manager: viewer + `relations`, `relations/delete`

1. Get a token with the built-in `management` application (service accounts belong to it):

```http
POST https://auth.company.com/idp/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials&client_id=management&service_account_key=<key>&service_account_secret=<secret>&resource=https://auth.company.com/idp
```

2. Call with `Authorization: Bearer <token>` (JSON bodies, camelCase, responses are not wrapped):

| Request | Body | Response |
| --- | --- | --- |
| `GET /idp/apis/users/{sub}/access` | - | `{ roles: [{ id, key }], scopes: [] }` |
| `POST /idp/apis/users/{sub}/access/grant` | `{ roleIds: [guid], scopes: [string] }` | same as GET |
| `POST /idp/apis/users/{sub}/access/revoke` | `{ roleIds: [guid], scopes: [string] }` | same as GET |
| `POST /idp/apis/relations` | `{ tuples: [{ user, relation, object }] }` (max 100) | `{ affectedCount }` |
| `POST /idp/apis/relations/delete` | same | `{ affectedCount }` |
| `POST /idp/apis/relations/check` | `{ user, relation, object }` | `{ allowed }` |

- Only roles and scopes defined in the pool can be granted (`400` otherwise). Role IDs are on the pool's Roles screen.
- Relation format: `type:id`; user sets `type:id#relation`. Types and relations must exist in the pool's published authorization schema.
- Typical pattern: when your app creates a record, write `user:<sub>` as its owner and link it to its parent; check with `relations/check`.

## Common mistakes

- Missing PKCE or using `plain` -> request rejected. Always S256.
- Token `aud` empty or wrong -> pass `resource` when requesting the token; it must be in the application's allowed APIs.
- Using a user token for `/idp/apis/*` -> `403`. Use a service account token from a backend.
- Using the application's own `client_id` for the service account token -> `invalid_service_account`. Use `client_id=management`.
- `403` with a service account token -> its permission is not enough (e.g. a viewer granting roles) or it is suspended.
- Expecting a new role in an existing token -> refresh or sign in again.
- Logout without `client_id` / `id_token_hint` -> user is not returned to the app.

Docs: https://licensing.appnuraymind.com/en/docs (integration, pool APIs, authorization, account).
