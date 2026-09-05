using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Manager.RequestContexts;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace BoosterAuthHub.AuthN.Features.Me;

public class MeEndpoint(
    IRequestContextAccessor requestContextAccessor,
    IAsyncRepository<Principal, IdpDbContext> principalRepository) : EndpointWithoutRequest<UserInfoResponse>
{
    public override void Configure()
    {
        Verbs(Http.GET, Http.POST);
        Routes("me");
        Summary(s => s.Summary = "Oturum açan principal'ın kendi bilgilerini (OIDC UserInfo) döner");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var principalId = requestContextAccessor.RequestContext!.PrincipalId;
        if (principalId == Guid.Empty)
            throw new BusinessException(AuthNMessageKeys.InvalidAccessToken);

        var principal = await principalRepository.Query()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == principalId && p.DeletedTime == null, ct);
        principal.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.PrincipalNotFound);

        await Send.OkAsync(new UserInfoResponse
        {
            Sub = principal!.Id.ToString(),
            Name = principal.DisplayName,
            Email = principal.Email,
            EmailVerified = principal.EmailVerified,
            UserPool = principal.UserPoolId.ToString()
        }, ct);
    }
}

public record UserInfoResponse
{
    [JsonPropertyName("sub")] public string Sub { get; init; } = default!;
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
    [JsonPropertyName("email_verified")] public bool EmailVerified { get; init; }
    [JsonPropertyName("userpool")] public string UserPool { get; init; } = default!;
}
