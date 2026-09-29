using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Features._base.Enums;
using Booster.Auth.Manager.RequestContexts;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Idp.Features.PoolServiceAccounts.ChangeStatus;

public class ChangePoolServiceAccountStatusEndpoint
    : Endpoint<ChangePoolServiceAccountStatusCommand, ChangePoolServiceAccountStatusResponse>
{
    public override void Configure()
    {
        Put("pools/service-accounts/status");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, IdpPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s => s.Summary = "Servis hesabını askıya alır ya da yeniden etkinleştirir");
    }

    public override async Task HandleAsync(ChangePoolServiceAccountStatusCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record ChangePoolServiceAccountStatusCommand(Guid UserPoolId, Guid ServiceAccountId, ServiceAccountStatus Status)
    : ICommand<ChangePoolServiceAccountStatusResponse>;

public readonly record struct ChangePoolServiceAccountStatusResponse(Guid Id);

public class ChangePoolServiceAccountStatusCommandHandler(
    IAsyncRepository<ServiceAccount, IdpDbContext> serviceAccountRepository,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<ChangePoolServiceAccountStatusCommand, ChangePoolServiceAccountStatusResponse>
{
    public async Task<ChangePoolServiceAccountStatusResponse> ExecuteAsync(
        ChangePoolServiceAccountStatusCommand command, CancellationToken ct)
    {
        using var _ = requestContextAccessor.BeginSystemContext(AuthConstants.ManagementPoolId);

        var account = await serviceAccountRepository.GetAsync(
            s => s.Id == command.ServiceAccountId && s.OwnerPoolId == command.UserPoolId, cancellationToken: ct)
            ?? throw new BusinessException(IdpMessageKeys.ServiceAccountNotFound);

        account.Status = command.Status;
        await serviceAccountRepository.SaveChangesAsync(ct);

        return new ChangePoolServiceAccountStatusResponse(account.Id);
    }
}

public class ChangePoolServiceAccountStatusValidator : Validator<ChangePoolServiceAccountStatusCommand>
{
    public ChangePoolServiceAccountStatusValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.ServiceAccountId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Status).IsInEnum().WithMessage(ValidationErrors.InvalidType);
    }
}
