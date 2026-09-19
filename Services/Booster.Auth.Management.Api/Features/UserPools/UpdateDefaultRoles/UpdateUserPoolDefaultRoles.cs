using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.UserPools.UpdateDefaultRoles;

public class UpdateUserPoolDefaultRolesEndpoint : Endpoint<UpdateUserPoolDefaultRolesCommand, UpdateUserPoolDefaultRolesResponse>
{
    public override void Configure()
    {
        Put("{Id:guid}/default-roles");
        Group<UserPoolsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Havuzun varsayılan rol listesini günceller";
            s.Description = "Bu havuza kayıt olan kullanıcılara otomatik atanacak rolleri belirler. " +
                            "Liste tamamen değiştirilir; her rol bu havuza ait olmalıdır.";
        });
    }

    public override async Task HandleAsync(UpdateUserPoolDefaultRolesCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateUserPoolDefaultRolesCommand(Guid Id, List<Guid> RoleIds) : ICommand<UpdateUserPoolDefaultRolesResponse>;

public readonly record struct UpdateUserPoolDefaultRolesResponse(Guid Id);

public class UpdateUserPoolDefaultRolesCommandHandler(
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IAsyncRepository<Role, AuthNDbContext> roleRepository)
    : ICommandHandler<UpdateUserPoolDefaultRolesCommand, UpdateUserPoolDefaultRolesResponse>
{
    public async Task<UpdateUserPoolDefaultRolesResponse> ExecuteAsync(
        UpdateUserPoolDefaultRolesCommand command, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(p => p.Id == command.Id, cancellationToken: ct);
        pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

        var distinctRoleIds = command.RoleIds.Distinct().ToList();

        // Sayı eşleşmiyorsa en az bir rol ya yok ya da başka havuza ait.
        if (distinctRoleIds.Count > 0)
        {
            var poolRoleCount = await roleRepository.CountAsync(
                r => r.UserPoolId == command.Id && distinctRoleIds.Contains(r.Id),
                cancellationToken: ct);

            if (poolRoleCount != distinctRoleIds.Count)
                throw new BusinessException(AuthNMessageKeys.RoleNotFound);
        }

        pool!.DefaultRoleIds = distinctRoleIds;

        await userPoolRepository.SaveChangesAsync(ct);

        return new UpdateUserPoolDefaultRolesResponse(pool.Id);
    }
}

public class UpdateUserPoolDefaultRolesValidator : Validator<UpdateUserPoolDefaultRolesCommand>
{
    public UpdateUserPoolDefaultRolesValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.RoleIds).NotNull().WithMessage(ValidationErrors.NotNullOrEmptyError);
    }
}
