using Booster.Auth.Idp.Features.Token.Handlers._base;
using Booster.Auth.Idp.Features.Token.Handlers.Models;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Idp.Features.Token;

public class TokenEndpoint(TokenHandler tokenHandler) : Endpoint<TokenRequest, TokenResponse>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("token");
        AllowAnonymous();
        AllowFormData(urlEncoded: true);
        Summary(s => s.Summary = "OIDC token endpoint (grant type'a göre token üretir)");
    }

    public override async Task HandleAsync(TokenRequest req, CancellationToken ct)
    {
        var response = await tokenHandler.ExecuteAsync(req, ct);
        await Send.OkAsync(response, ct);
    }
}

public class TokenRequestValidator : Validator<TokenRequest>
{
    public TokenRequestValidator()
    {
        RuleFor(x => x.GrantType).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
