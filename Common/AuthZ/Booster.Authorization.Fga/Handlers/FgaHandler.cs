using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Grpc;
using Booster.Authorization.Fga.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Booster.Authorization.Fga.Handlers;

public class FgaHandler(
    IHttpContextAccessor httpContextAccessor,
    IRequestContextAccessor requestContextAccessor,
    AuthorizationService.AuthorizationServiceClient authorizationClient)
    : AuthorizationHandler<FgaRequirement>
{
    private const string SubjectClaimType = "sub";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, FgaRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (context.User.Identity?.IsAuthenticated != true || httpContext is null)
            return;

        if (!Guid.TryParse(context.User.FindFirst(SubjectClaimType)?.Value, out var principalId))
            return;

        var isolationId = requestContextAccessor.Context().Isolation;

        if (isolationId == Guid.Empty)
            return;

        var targetId = TargetSelectorResolver.Resolve(requirement.TargetSelector, httpContext);

        if (string.IsNullOrEmpty(targetId))
            return;

        var response = await authorizationClient.CheckAsync(
            new CheckRequest
            {
                IsolationId = isolationId.ToString(),
                User = $"user:{principalId}",
                Relation = requirement.Permission,
                Object = $"{requirement.ResourceType}:{targetId}"
            },
            cancellationToken: httpContext.RequestAborted);

        if (response.Allowed)
            context.Succeed(requirement);
    }
}
