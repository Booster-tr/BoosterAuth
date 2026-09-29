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

public class UpdatePasswordPolicyEndpoint : Endpoint<UpdatePasswordPolicyCommand, UserPoolPolicyChangeResponse>
{
    public override void Configure()
    {
        Put("policy/password");
        Group<UserPoolsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.ManagerRelation)));

        Summary(s => s.Summary = "Havuzun parola politikasını günceller");
    }

    public override async Task HandleAsync(UpdatePasswordPolicyCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdatePasswordPolicyCommand(
    Guid UserPoolId,
    int MinLength,
    bool RequireUppercase,
    bool RequireLowercase,
    bool RequireDigit,
    bool RequireNonAlphanumeric,
    int ExpiryDays,
    int HistoryCount) : ICommand<UserPoolPolicyChangeResponse>;

public class UpdatePasswordPolicyCommandHandler(
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IClientDataCacheStore clientDataCacheStore)
    : ICommandHandler<UpdatePasswordPolicyCommand, UserPoolPolicyChangeResponse>
{
    public async Task<UserPoolPolicyChangeResponse> ExecuteAsync(UpdatePasswordPolicyCommand command, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(p => p.Id == command.UserPoolId, cancellationToken: ct);
        pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

        var policy = pool!.Policy.Adapt<UserPoolPolicy>();
        policy.Password = command.Adapt<PasswordPolicy>();
        pool.Policy = policy;

        await userPoolRepository.SaveChangesAsync(ct);
        await clientDataCacheStore.InvalidatePoolAsync(pool.Id, ct);

        return new UserPoolPolicyChangeResponse(pool.Id);
    }
}

public class UpdatePasswordPolicyValidator : Validator<UpdatePasswordPolicyCommand>
{
    public UpdatePasswordPolicyValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.MinLength).InclusiveBetween(6, 128).WithMessage(AuthNMessageKeys.ValueOutOfRange);
        RuleFor(x => x.ExpiryDays).InclusiveBetween(0, 3650).WithMessage(AuthNMessageKeys.ValueOutOfRange);
        RuleFor(x => x.HistoryCount).InclusiveBetween(0, 24).WithMessage(AuthNMessageKeys.ValueOutOfRange);
    }
}
