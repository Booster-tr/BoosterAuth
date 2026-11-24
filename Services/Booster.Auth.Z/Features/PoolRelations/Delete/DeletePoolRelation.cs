using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.Auth.Z.Constants;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Fga.Attributes;
using Booster.Authorization.Relational.Fga;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using OpenFga.Sdk.Exceptions;

namespace Booster.Auth.Z.Features.PoolRelations.Delete;

public class DeletePoolRelationEndpoint : Endpoint<DeletePoolRelationCommand, DeletePoolRelationResponse>
{
    public override void Configure()
    {
        Delete("authz/pool-relations");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthZPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, AuthZPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Havuzdan ilişki siler";
            s.Description = "Parametreler query string ile verilir.";
        });
    }

    public override async Task HandleAsync(DeletePoolRelationCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record DeletePoolRelationCommand(
    Guid UserPoolId,
    string ObjectType,
    string ObjectId,
    string Relation,
    string SubjectType,
    string SubjectId) : ICommand<DeletePoolRelationResponse>;

public readonly record struct DeletePoolRelationResponse(string Object, string Relation);

public class DeletePoolRelationCommandHandler(IPoolStoreProvider poolStoreProvider, FgaRelationManager relationManager)
    : ICommandHandler<DeletePoolRelationCommand, DeletePoolRelationResponse>
{
    public async Task<DeletePoolRelationResponse> ExecuteAsync(DeletePoolRelationCommand command, CancellationToken ct)
    {
        var store = await poolStoreProvider.GetRequiredByPoolIdAsync(command.UserPoolId, ct);
        var @object = $"{command.ObjectType}:{command.ObjectId}";

        try
        {
            await relationManager.DeleteAsync(
                store.StoreId, $"{command.SubjectType}:{command.SubjectId}", command.Relation, @object, store.ActiveModelId, ct);
        }
        catch (FgaApiValidationError)
        {
            throw new BusinessException(AuthZMessageKeys.PoolRelationNotFound);
        }

        return new DeletePoolRelationResponse(@object, command.Relation);
    }
}

public class DeletePoolRelationValidator : Validator<DeletePoolRelationCommand>
{
    public DeletePoolRelationValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.ObjectType).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.ObjectId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Relation).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.SubjectType).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.SubjectId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
