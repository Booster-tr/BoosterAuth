using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Z.Constants;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.Schemas.Rename;

public class RenameSchemaVersionEndpoint : Endpoint<RenameSchemaVersionCommand, RenameSchemaVersionResponse>
{
    public override void Configure()
    {
        Put("authz/schema/versions/name");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthZPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, AuthZPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Sürümün adını değiştirir";
            s.Description = "Yayındaki sürüm de yeniden adlandırılabilir; ad havuz içinde benzersizdir.";
        });
    }

    public override async Task HandleAsync(RenameSchemaVersionCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record RenameSchemaVersionCommand(Guid UserPoolId, Guid VersionId, string Name) : ICommand<RenameSchemaVersionResponse>;

public readonly record struct RenameSchemaVersionResponse(Guid Id);

public class RenameSchemaVersionCommandHandler(IAsyncRepository<PoolSchema, AuthZDbContext> schemaRepository)
    : ICommandHandler<RenameSchemaVersionCommand, RenameSchemaVersionResponse>
{
    public async Task<RenameSchemaVersionResponse> ExecuteAsync(RenameSchemaVersionCommand command, CancellationToken ct)
    {
        var schema = await schemaRepository.GetAsync(
            s => s.Id == command.VersionId && s.PoolId == command.UserPoolId, cancellationToken: ct);
        schema.ThrowCustomExceptionIfDataNull(AuthZMessageKeys.SchemaVersionNotFound);

        var name = command.Name.Trim();

        if (await schemaRepository.AnyAsync(
                s => s.PoolId == command.UserPoolId && s.Name == name && s.Id != command.VersionId, cancellationToken: ct))
            throw new BusinessException(AuthZMessageKeys.SchemaVersionNameTaken);

        schema!.Name = name;
        await schemaRepository.SaveChangesAsync(ct);

        return new RenameSchemaVersionResponse(schema.Id);
    }
}

public class RenameSchemaVersionValidator : Validator<RenameSchemaVersionCommand>
{
    public RenameSchemaVersionValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.VersionId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(100).WithMessage(ValidationErrors.MaxLengthError);
    }
}
