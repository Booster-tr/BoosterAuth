using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using Mapster;

namespace Booster.Auth.Management.Api.Features.Roles.Update;

public class UpdateRoleEndpoint : Endpoint<UpdateRoleCommand, UpdateRoleResponse>
{
    public override void Configure()
    {
        Put("{Id:guid}");
        Group<RolesEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Rolü günceller";
            s.Description = "Görünen ad ve açıklamayı günceller. Anahtar ve havuz değiştirilemez.";
        });
    }

    public override async Task HandleAsync(UpdateRoleCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateRoleCommand(
    Guid Id,
    string DisplayName,
    string? Description) : ICommand<UpdateRoleResponse>;

public readonly record struct UpdateRoleResponse(Guid Id);

public class UpdateRoleCommandHandler(
    IAsyncRepository<Role, AuthNDbContext> roleRepository)
    : ICommandHandler<UpdateRoleCommand, UpdateRoleResponse>
{
    public async Task<UpdateRoleResponse> ExecuteAsync(UpdateRoleCommand command, CancellationToken ct)
    {
        var role = await roleRepository.GetAsync(r => r.Id == command.Id, cancellationToken: ct);
        role.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.RoleNotFound);

        command.Adapt(role);

        await roleRepository.SaveChangesAsync(ct);

        return new UpdateRoleResponse(role!.Id);
    }
}

public class UpdateRoleValidator : Validator<UpdateRoleCommand>
{
    public UpdateRoleValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);
        RuleFor(x => x.Description).MaximumLength(1000).WithMessage(ValidationErrors.MaxLengthError);
    }
}
