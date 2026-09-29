using System.Security.Cryptography;
using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Features._base.Enums;
using Booster.Auth.Idp.Features.PoolServiceAccounts._base;
using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Grpc;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.Hashing.OneWays.Simple;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.WebUtilities;

namespace Booster.Auth.Idp.Features.PoolServiceAccounts.Create;

public class CreatePoolServiceAccountEndpoint : Endpoint<CreatePoolServiceAccountCommand, CreatePoolServiceAccountResponse>
{
    public override void Configure()
    {
        Post("pools/service-accounts");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, IdpPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Havuz için servis hesabı açar";
            s.Description = "Sır yalnızca bu yanıtta döner; saklanmaz, sonradan gösterilemez.";
        });
    }

    public override async Task HandleAsync(CreatePoolServiceAccountCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record CreatePoolServiceAccountCommand(
    Guid UserPoolId,
    string Name,
    string Relation,
    DateTimeOffset? ExpiresAt) : ICommand<CreatePoolServiceAccountResponse>;

public record CreatePoolServiceAccountResponse(Guid Id, string Key, string Secret);

public class CreatePoolServiceAccountCommandHandler(
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IAsyncRepository<ServiceAccount, IdpDbContext> serviceAccountRepository,
    AuthorizationService.AuthorizationServiceClient authorizationClient,
    ISimpleHasher simpleHasher,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<CreatePoolServiceAccountCommand, CreatePoolServiceAccountResponse>
{
    public async Task<CreatePoolServiceAccountResponse> ExecuteAsync(
        CreatePoolServiceAccountCommand command, CancellationToken ct)
    {
        using var _ = requestContextAccessor.BeginSystemContext(AuthConstants.ManagementPoolId);

        var principal = new Principal
        {
            Id = EntityId.New(),
            Type = PrincipalType.Machine,
            Status = PrincipalStatus.Active,
            DisplayName = command.Name.Trim(),
            CreatedTime = DateTimeOffset.UtcNow
        };

        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        var serviceAccount = new ServiceAccount
        {
            Id = EntityId.New(),
            PrincipalId = principal.Id,
            Status = ServiceAccountStatus.Active,
            Key = "sa_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant(),
            SecretHash = simpleHasher.Hash(secret),
            Name = command.Name.Trim(),
            ExpiresAt = command.ExpiresAt,
            OwnerPoolId = command.UserPoolId,
            CreatedTime = DateTimeOffset.UtcNow
        };

        await principalRepository.AddAsync(principal, ct);
        await serviceAccountRepository.AddAsync(serviceAccount, ct);

        var request = new WriteRelationsRequest { IsolationId = ServiceAccountRules.Isolation };
        request.Tuples.Add(new RelationTuple
        {
            User = AuthConstants.User(principal.Id),
            Relation = command.Relation.ToLowerInvariant(),
            Object = AuthConstants.Pool(command.UserPoolId)
        });
        await authorizationClient.WriteRelationsAsync(request, cancellationToken: ct);

        await principalRepository.SaveChangesAsync(ct);

        return new CreatePoolServiceAccountResponse(serviceAccount.Id, serviceAccount.Key, secret);
    }
}

public class CreatePoolServiceAccountValidator : Validator<CreatePoolServiceAccountCommand>
{
    public CreatePoolServiceAccountValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);

        RuleFor(x => x.Relation)
            .Must(r => ServiceAccountRules.Assignable.Contains(r, StringComparer.OrdinalIgnoreCase))
            .WithMessage(IdpMessageKeys.InvalidServiceAccountRelation);

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(_ => DateTimeOffset.UtcNow).When(x => x.ExpiresAt.HasValue)
            .WithMessage(ValidationErrors.GreaterThanError);
    }
}
