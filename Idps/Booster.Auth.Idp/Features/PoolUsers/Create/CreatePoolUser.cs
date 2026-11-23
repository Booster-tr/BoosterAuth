using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Managers.UserManagers;
using Booster.Auth.Manager.RequestContexts;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;

namespace Booster.Auth.Idp.Features.PoolUsers.Create;

/// <summary>
/// Panelden havuza kullanici ekler. Kullanici PAROLASIZ olusur; parolasini davet baglantisindan kendisi belirler.
/// </summary>
public class CreatePoolUserEndpoint : Endpoint<CreatePoolUserCommand, CreatePoolUserResponse>
{
    public override void Configure()
    {
        Post("pools/users");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, IdpPolicies.PoolTarget, AuthConstants.ManagerRelation)));

        Summary(s => s.Summary = "Havuza kullanici ekler ve (istenirse) davet e-postasi gonderir");
    }

    public override async Task HandleAsync(CreatePoolUserCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record CreatePoolUserCommand(Guid UserPoolId, string Email, string DisplayName, bool SendInvitation = true)
    : ICommand<CreatePoolUserResponse>;

public readonly record struct CreatePoolUserResponse(Guid PrincipalId, bool InvitationSent);

public sealed class CreatePoolUserValidator : Validator<CreatePoolUserCommand>
{
    public CreatePoolUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .EmailAddress().WithMessage(IdpMessageKeys.EmailInvalid)
            .MaximumLength(320).WithMessage(IdpMessageKeys.EmailInvalid);
        RuleFor(x => x.DisplayName).NotEmpty().WithMessage(IdpMessageKeys.DisplayNameRequired)
            .MaximumLength(200).WithMessage(IdpMessageKeys.DisplayNameRequired);
    }
}

public class CreatePoolUserCommandHandler(
    PoolDataReader poolDataReader,
    EndUserFactory factory,
    InvitationManager invitations,
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<CreatePoolUserCommand, CreatePoolUserResponse>
{
    public async Task<CreatePoolUserResponse> ExecuteAsync(CreatePoolUserCommand cmd, CancellationToken ct)
    {
        var pool = await poolDataReader.GetAsync(cmd.UserPoolId, ct)
                   ?? throw new BusinessException(IdpMessageKeys.PoolNotFound);

        var email = EndUserFactory.NormalizeEmail(cmd.Email);

        using var _ = requestContextAccessor.BeginSystemContext(cmd.UserPoolId);

        if (await factory.EmailExistsAsync(email, ct))
            throw new ValidationException([new ValidationFailure(nameof(cmd.Email), IdpMessageKeys.IdentifierAlreadyExists)]);

        var principal = await factory.CreateInvitedAsync(pool, cmd.DisplayName, email, ct);

        if (cmd.SendInvitation)
            await invitations.SendAsync(principal, cmd.UserPoolId, ct);

        // Kullanici ve davet olayi (outbox) AYNI islemde kaydedilir.
        await principalRepository.SaveChangesAsync(ct);

        return new CreatePoolUserResponse(principal.Id, cmd.SendInvitation);
    }
}
