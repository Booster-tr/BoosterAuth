using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Z.Constants;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Authorization.Fga.Attributes;
using Booster.Authorization.Relational.Fga;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using OpenFga.Sdk.Exceptions;

namespace Booster.Auth.Z.Features.PoolRelations.Create;

public class CreatePoolRelationEndpoint : Endpoint<CreatePoolRelationCommand, CreatePoolRelationResponse>
{
    public override void Configure()
    {
        Post("authz/pool-relations");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthZPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, AuthZPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Havuza ilişki yazar";
            s.Description = "Tip, ilişki ve atanan tarafın tipi yayındaki şemada izinli olmalıdır.";
        });
    }

    public override async Task HandleAsync(CreatePoolRelationCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record CreatePoolRelationCommand(
    Guid UserPoolId,
    string ObjectType,
    string ObjectId,
    string Relation,
    string SubjectType,
    string SubjectId) : ICommand<CreatePoolRelationResponse>;

public readonly record struct CreatePoolRelationResponse(string Object, string Relation);

public class CreatePoolRelationCommandHandler(
    IAsyncRepository<PoolSchema, AuthZDbContext> schemaRepository,
    FgaRelationManager relationManager)
    : ICommandHandler<CreatePoolRelationCommand, CreatePoolRelationResponse>
{
    public async Task<CreatePoolRelationResponse> ExecuteAsync(CreatePoolRelationCommand command, CancellationToken ct)
    {
        var schema = await schemaRepository.GetAsync(
            s => s.PoolId == command.UserPoolId && s.Status == SchemaVersionStatus.Active,
            enableTracking: false, cancellationToken: ct);
        schema.ThrowCustomExceptionIfDataNull(AuthZMessageKeys.SchemaNotFound);

        // OpenFGA'nın ham hatası yerine şemaya göre anlaşılır ret.
        var allowed = schema!.Definition.Types
            .FirstOrDefault(t => t.Name == command.ObjectType)?.Relations
            .FirstOrDefault(r => r.Name == command.Relation)?.AssignableTypes
            .Contains(command.SubjectType) ?? false;

        if (!allowed)
            throw new BusinessException(AuthZMessageKeys.PoolRelationNotAllowed);

        var @object = $"{command.ObjectType}:{command.ObjectId}";

        try
        {
            await relationManager.WriteAsync(
                schema.StoreId, $"{command.SubjectType}:{command.SubjectId}", command.Relation, @object, ct);
        }
        catch (FgaApiValidationError)
        {
            throw new BusinessException(AuthZMessageKeys.PoolRelationRejected);
        }

        return new CreatePoolRelationResponse(@object, command.Relation);
    }
}

public class CreatePoolRelationValidator : Validator<CreatePoolRelationCommand>
{
    public CreatePoolRelationValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.ObjectType).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Relation).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.SubjectType).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.ObjectId)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .Matches(PoolRelationRules.IdPattern).WithMessage(AuthZMessageKeys.InvalidObjectFormat);

        RuleFor(x => x.SubjectId)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .Matches(PoolRelationRules.IdPattern).WithMessage(AuthZMessageKeys.InvalidUserFormat);
    }
}

public static class PoolRelationRules
{
    /// <summary>OpenFGA kimliği: boşluk, iki nokta ve # içeremez.</summary>
    public const string IdPattern = @"^[^\s:#]{1,128}$";
}
