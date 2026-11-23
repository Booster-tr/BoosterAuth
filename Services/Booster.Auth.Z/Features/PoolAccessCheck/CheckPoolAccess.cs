using Booster.Auth.Common.Internal.Licensing;
using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Z.Constants;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Auth.Z.Features.Schemas._base;
using Booster.Authorization.Fga.Attributes;
using Booster.Authorization.Relational.Fga;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.PoolAccessCheck;

/// <summary>Salt okuma: lisans kisitliyken de calisir (panel ekrani cizebilsin).</summary>
[AllowWhenLicenseRestricted]
public class CheckPoolAccessEndpoint : Endpoint<CheckPoolAccessQuery, CheckPoolAccessResponse>
{
    public override void Configure()
    {
        Post("authz/pool-check");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthZPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, AuthZPolicies.PoolTarget, AuthConstants.ViewerRelation)));

        Summary(s =>
        {
            s.Summary = "Kullanıcının bir kayıt üzerindeki yetkilerini sınar";
            s.Description = "Tipin tüm yetkileri yayındaki modelle tek bir toplu kontrolde denenir.";
        });
    }

    public override async Task HandleAsync(CheckPoolAccessQuery req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record CheckPoolAccessQuery(Guid UserPoolId, Guid UserId, string ObjectType, string ObjectId)
    : ICommand<CheckPoolAccessResponse>;

public record CheckPoolAccessResponse(IReadOnlyList<PoolAccessResult> Items);

public record PoolAccessResult(string Relation, bool Allowed);

public class CheckPoolAccessQueryHandler(
    IAsyncRepository<PoolSchema, AuthZDbContext> schemaRepository,
    FgaRelationManager relationManager)
    : ICommandHandler<CheckPoolAccessQuery, CheckPoolAccessResponse>
{
    public async Task<CheckPoolAccessResponse> ExecuteAsync(CheckPoolAccessQuery command, CancellationToken ct)
    {
        var schema = await schemaRepository.GetAsync(
            s => s.PoolId == command.UserPoolId && s.Status == SchemaVersionStatus.Active,
            enableTracking: false, cancellationToken: ct);
        schema.ThrowCustomExceptionIfDataNull(AuthZMessageKeys.SchemaNotFound);

        var type = schema!.Definition.Types.FirstOrDefault(t => t.Name == command.ObjectType)
                   ?? throw new BusinessException(AuthZMessageKeys.ResourceTypeNotFound);

        // Bağlantılar (Belge → Klasör) kullanıcı yetkisi değildir; yalnızca yetkiler sınanır.
        var permissions = type.Relations.Where(r => !r.IsLink(SchemaValidator.UserType)).Select(r => r.Name).ToList();

        if (permissions.Count == 0)
            return new CheckPoolAccessResponse([]);

        var user = AuthConstants.User(command.UserId);
        var @object = $"{command.ObjectType}:{command.ObjectId}";

        var results = await relationManager.BatchCheckAsync(
            schema.StoreId,
            permissions.Select((relation, i) => new CheckInput(user, relation, @object, i.ToString())),
            schema.ModelId,
            ct);

        var allowed = results.ToDictionary(r => r.CorrelationId, r => r.Allowed);

        return new CheckPoolAccessResponse(permissions
            .Select((relation, i) => new PoolAccessResult(relation, allowed.GetValueOrDefault(i.ToString())))
            .ToList());
    }
}

public class CheckPoolAccessValidator : Validator<CheckPoolAccessQuery>
{
    public CheckPoolAccessValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.UserId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.ObjectType).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.ObjectId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
