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

public class UpdateMfaPolicyEndpoint : Endpoint<UpdateMfaPolicyCommand, UserPoolPolicyChangeResponse>
{
    public override void Configure()
    {
        Put("policy/mfa");
        Group<UserPoolsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.ManagerRelation)));

        Summary(s => s.Summary = "Havuzun çok faktörlü doğrulama politikasını günceller");
    }

    public override async Task HandleAsync(UpdateMfaPolicyCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateMfaPolicyCommand(
    Guid UserPoolId,
    bool Required,
    MfaMethod AllowedMethods) : ICommand<UserPoolPolicyChangeResponse>;

public class UpdateMfaPolicyCommandHandler(
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IClientDataCacheStore clientDataCacheStore)
    : ICommandHandler<UpdateMfaPolicyCommand, UserPoolPolicyChangeResponse>
{
    public async Task<UserPoolPolicyChangeResponse> ExecuteAsync(UpdateMfaPolicyCommand command, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(p => p.Id == command.UserPoolId, cancellationToken: ct);
        pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

        var policy = pool!.Policy.Adapt<UserPoolPolicy>();
        policy.Mfa = command.Adapt<MfaPolicy>();
        pool.Policy = policy;

        await userPoolRepository.SaveChangesAsync(ct);
        await clientDataCacheStore.InvalidatePoolAsync(pool.Id, ct);

        return new UserPoolPolicyChangeResponse(pool.Id);
    }
}

public class UpdateMfaPolicyValidator : Validator<UpdateMfaPolicyCommand>
{
    public UpdateMfaPolicyValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.AllowedMethods)
            .Must(v => (v & ~(MfaMethod.Totp | MfaMethod.Sms | MfaMethod.Email)) == 0)
            .WithMessage(ValidationErrors.InvalidType);
        RuleFor(x => x.AllowedMethods)
            .NotEqual(MfaMethod.None).When(x => x.Required)
            .WithMessage(AuthNMessageKeys.MfaMethodRequired);
    }
}
