using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
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

public class UpdateRegistrationPolicyEndpoint : Endpoint<UpdateRegistrationPolicyCommand, UserPoolPolicyChangeResponse>
{
    public override void Configure()
    {
        Put("policy/registration");
        Group<UserPoolsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.ManagerRelation)));

        Summary(s => s.Summary = "Havuzun kayıt politikasını günceller");
    }

    public override async Task HandleAsync(UpdateRegistrationPolicyCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateRegistrationPolicyCommand(
    Guid UserPoolId,
    RegistrationMode Mode,
    bool RequireEmailVerification,
    bool RequirePhoneVerification,
    bool AllowFederatedJit) : ICommand<UserPoolPolicyChangeResponse>;

public class UpdateRegistrationPolicyCommandHandler(
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IClientDataCacheStore clientDataCacheStore)
    : ICommandHandler<UpdateRegistrationPolicyCommand, UserPoolPolicyChangeResponse>
{
    public async Task<UserPoolPolicyChangeResponse> ExecuteAsync(UpdateRegistrationPolicyCommand command, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(p => p.Id == command.UserPoolId, cancellationToken: ct);
        pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

        var policy = pool!.Policy.Adapt<UserPoolPolicy>();
        policy.Registration = command.Adapt<RegistrationPolicy>();
        pool.Policy = policy;

        await userPoolRepository.SaveChangesAsync(ct);
        await clientDataCacheStore.InvalidatePoolAsync(pool.Id, ct);

        return new UserPoolPolicyChangeResponse(pool.Id);
    }
}

public class UpdateRegistrationPolicyValidator : Validator<UpdateRegistrationPolicyCommand>
{
    public UpdateRegistrationPolicyValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Mode).IsInEnum().WithMessage(ValidationErrors.InvalidType);
    }
}
