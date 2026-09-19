using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.UserPools.ChangeStatus;

public class ChangeUserPoolStatusEndpoint : Endpoint<ChangeUserPoolStatusCommand, ChangeUserPoolStatusResponse>
{
    public override void Configure()
    {
        Put("{Id:guid}/status");
        Group<UserPoolsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Havuz durumunu değiştirir";
            s.Description = "Havuzu aktif/pasif yapar. Pasife alma, altında aktif uygulama varken reddedilir.";
        });
    }

    public override async Task HandleAsync(ChangeUserPoolStatusCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record ChangeUserPoolStatusCommand(Guid Id, EntityStatus Status) : ICommand<ChangeUserPoolStatusResponse>;

public readonly record struct ChangeUserPoolStatusResponse(Guid Id);

public class ChangeUserPoolStatusCommandHandler(
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IAsyncRepository<Client, AuthNDbContext> clientRepository)
    : ICommandHandler<ChangeUserPoolStatusCommand, ChangeUserPoolStatusResponse>
{
    public async Task<ChangeUserPoolStatusResponse> ExecuteAsync(ChangeUserPoolStatusCommand command, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(p => p.Id == command.Id, cancellationToken: ct);
        pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

        // Pasife alınan havuzun altında çalışan uygulama kalmamalı; aksi hâlde
        // kapalı bir havuz üzerinden giriş akışı sürmeye devam eder.
        if (command.Status == EntityStatus.Passive)
        {
            var hasActiveClient = await clientRepository.AnyAsync(
                c => c.UserPoolId == command.Id && c.Status == EntityStatus.Active,
                cancellationToken: ct);

            if (hasActiveClient)
                throw new BusinessException(AuthNMessageKeys.ActiveChildrenExist);
        }

        pool!.Status = command.Status;

        await userPoolRepository.SaveChangesAsync(ct);

        return new ChangeUserPoolStatusResponse(pool.Id);
    }
}

public class ChangeUserPoolStatusValidator : Validator<ChangeUserPoolStatusCommand>
{
    public ChangeUserPoolStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Status).IsInEnum();
    }
}
