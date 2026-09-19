using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.UserPools.UpdatePolicy;

public class UpdateUserPoolPolicyEndpoint : Endpoint<UpdateUserPoolPolicyCommand, UpdateUserPoolPolicyResponse>
{
    public override void Configure()
    {
        Put("{Id:guid}/policy");
        Group<UserPoolsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Havuz politikasını günceller";
            s.Description = "Parola, MFA, oturum, kayıt ve giriş politikalarını topluca değiştirir. " +
                            "Kısmi güncelleme yoktur: gönderilen nesne politikanın tamamının yerine geçer.";
        });
    }

    public override async Task HandleAsync(UpdateUserPoolPolicyCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateUserPoolPolicyCommand(Guid Id, UserPoolPolicy Policy) : ICommand<UpdateUserPoolPolicyResponse>;

public readonly record struct UpdateUserPoolPolicyResponse(Guid Id);

public class UpdateUserPoolPolicyCommandHandler(
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository)
    : ICommandHandler<UpdateUserPoolPolicyCommand, UpdateUserPoolPolicyResponse>
{
    public async Task<UpdateUserPoolPolicyResponse> ExecuteAsync(
        UpdateUserPoolPolicyCommand command, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(p => p.Id == command.Id, cancellationToken: ct);
        pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

        pool!.Policy = command.Policy;

        await userPoolRepository.SaveChangesAsync(ct);

        return new UpdateUserPoolPolicyResponse(pool.Id);
    }
}

public class UpdateUserPoolPolicyValidator : Validator<UpdateUserPoolPolicyCommand>
{
    public UpdateUserPoolPolicyValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Policy).NotNull().WithMessage(ValidationErrors.NotNullOrEmptyError);
    }
}
