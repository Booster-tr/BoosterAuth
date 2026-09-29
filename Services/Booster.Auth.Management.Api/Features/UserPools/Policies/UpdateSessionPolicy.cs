using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;
using Mapster;

namespace Booster.Auth.Management.Api.Features.UserPools.Policies;

public class UpdateSessionPolicyEndpoint : Endpoint<UpdateSessionPolicyCommand, UserPoolPolicyChangeResponse>
{
    public override void Configure()
    {
        Put("policy/session");
        Group<UserPoolsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.ManagerRelation)));

        Summary(s => s.Summary = "Havuzun oturum ve token süresi politikasını günceller");
    }

    public override async Task HandleAsync(UpdateSessionPolicyCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateSessionPolicyCommand(
    Guid UserPoolId,
    int AccessTokenTtlSeconds,
    int RefreshTokenTtlSeconds,
    int IdleTimeoutSeconds) : ICommand<UserPoolPolicyChangeResponse>;

public class UpdateSessionPolicyCommandHandler(
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IClientDataCacheStore clientDataCacheStore)
    : ICommandHandler<UpdateSessionPolicyCommand, UserPoolPolicyChangeResponse>
{
    public async Task<UserPoolPolicyChangeResponse> ExecuteAsync(UpdateSessionPolicyCommand command, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(p => p.Id == command.UserPoolId, cancellationToken: ct);
        pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

        var policy = pool!.Policy.Adapt<UserPoolPolicy>();
        policy.Session = command.Adapt<SessionPolicy>();
        pool.Policy = policy;

        await userPoolRepository.SaveChangesAsync(ct);
        await clientDataCacheStore.InvalidatePoolAsync(pool.Id, ct);

        return new UserPoolPolicyChangeResponse(pool.Id);
    }
}

public class UpdateSessionPolicyValidator : Validator<UpdateSessionPolicyCommand>
{
    public UpdateSessionPolicyValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.AccessTokenTtlSeconds).InclusiveBetween(60, 86400).WithMessage(AuthNMessageKeys.ValueOutOfRange);
        RuleFor(x => x.RefreshTokenTtlSeconds).InclusiveBetween(300, 31536000).WithMessage(AuthNMessageKeys.ValueOutOfRange);
        RuleFor(x => x.RefreshTokenTtlSeconds)
            .GreaterThanOrEqualTo(x => x.AccessTokenTtlSeconds)
            .WithMessage(AuthNMessageKeys.RefreshTtlShorterThanAccess);
        RuleFor(x => x.IdleTimeoutSeconds).InclusiveBetween(0, 31536000).WithMessage(AuthNMessageKeys.ValueOutOfRange);
    }
}
