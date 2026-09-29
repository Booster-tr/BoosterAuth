using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Auth.Common.Internal.Constants;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using FluentValidation;
using Mapster;
using Booster.Auth.Management.Api.Constants;

namespace Booster.Auth.Management.Api.Features.UserPools.Update;

public class UpdateUserPoolEndpoint : Endpoint<UpdateUserPoolCommand, UpdateUserPoolResponse>
{
    public override void Configure()
    {
        Put("");
        Group<UserPoolsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.ManagerRelation)));
        Summary(s =>
        {
            s.Summary = "Havuz bilgisini günceller";
            s.Description = "Kullanıcı havuzunun görünen adını günceller. Tenant değiştirilemez.";
        });
    }

    public override async Task HandleAsync(UpdateUserPoolCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateUserPoolCommand(Guid UserPoolId, string Name) : ICommand<UpdateUserPoolResponse>;

public readonly record struct UpdateUserPoolResponse(Guid Id);

public class UpdateUserPoolCommandHandler(
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository)
    : ICommandHandler<UpdateUserPoolCommand, UpdateUserPoolResponse>
{
    public async Task<UpdateUserPoolResponse> ExecuteAsync(UpdateUserPoolCommand command, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(p => p.Id == command.UserPoolId, cancellationToken: ct);
        pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

        command.Adapt(pool);

        await userPoolRepository.SaveChangesAsync(ct);

        return new UpdateUserPoolResponse(pool!.Id);
    }
}

public class UpdateUserPoolValidator : Validator<UpdateUserPoolCommand>
{
    public UpdateUserPoolValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);
    }
}
