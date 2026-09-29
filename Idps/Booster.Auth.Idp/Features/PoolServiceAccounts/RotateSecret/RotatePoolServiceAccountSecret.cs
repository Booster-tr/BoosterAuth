using System.Security.Cryptography;
using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Features.PoolServiceAccounts.Create;
using Booster.Auth.Manager.RequestContexts;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Hashing.OneWays.Simple;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.WebUtilities;

namespace Booster.Auth.Idp.Features.PoolServiceAccounts.RotateSecret;

public class RotatePoolServiceAccountSecretEndpoint
    : Endpoint<RotatePoolServiceAccountSecretCommand, CreatePoolServiceAccountResponse>
{
    public override void Configure()
    {
        Post("pools/service-accounts/rotate-secret");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, IdpPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Servis hesabının sırrını yeniler";
            s.Description = "Eski sır hemen geçersiz olur. Yeni sır yalnızca bu yanıtta döner.";
        });
    }

    public override async Task HandleAsync(RotatePoolServiceAccountSecretCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record RotatePoolServiceAccountSecretCommand(Guid UserPoolId, Guid ServiceAccountId)
    : ICommand<CreatePoolServiceAccountResponse>;

public class RotatePoolServiceAccountSecretCommandHandler(
    IAsyncRepository<ServiceAccount, IdpDbContext> serviceAccountRepository,
    ISimpleHasher simpleHasher,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<RotatePoolServiceAccountSecretCommand, CreatePoolServiceAccountResponse>
{
    public async Task<CreatePoolServiceAccountResponse> ExecuteAsync(
        RotatePoolServiceAccountSecretCommand command, CancellationToken ct)
    {
        using var _ = requestContextAccessor.BeginSystemContext(AuthConstants.ManagementPoolId);

        var account = await serviceAccountRepository.GetAsync(
            s => s.Id == command.ServiceAccountId && s.OwnerPoolId == command.UserPoolId, cancellationToken: ct)
            ?? throw new BusinessException(IdpMessageKeys.ServiceAccountNotFound);

        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        account.SecretHash = simpleHasher.Hash(secret);
        await serviceAccountRepository.SaveChangesAsync(ct);

        return new CreatePoolServiceAccountResponse(account.Id, account.Key, secret);
    }
}

public class RotatePoolServiceAccountSecretValidator : Validator<RotatePoolServiceAccountSecretCommand>
{
    public RotatePoolServiceAccountSecretValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.ServiceAccountId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
