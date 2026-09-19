using Booster.Auth.Z.Constants;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Fga;
using Booster.Authorization.Relational.Models;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.Schema.ReadSchema;

public class ReadSchemaEndpoint : Endpoint<ReadSchemaCommand, ReadSchemaResponse>
{
    public override void Configure()
    {
        Get("authz/schema");
        Summary(s =>
        {
            s.Summary = "Pool'un authorization model'ini POCO olarak döner";
            s.Description = "Görselleştirme ve şema düzenleme arayüzleri için. isolationId query parametresidir.";
        });
    }

    public override async Task HandleAsync(ReadSchemaCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record ReadSchemaCommand(Guid IsolationId) : ICommand<ReadSchemaResponse>;

public record ReadSchemaResponse(string StoreId, string ModelId, IReadOnlyList<ResourceType> Types);

public class ReadSchemaCommandHandler(
    IPoolStoreProvider poolStoreProvider,
    FgaSchemaWriter schemaWriter,
    FgaSchemaParser schemaParser)
    : ICommandHandler<ReadSchemaCommand, ReadSchemaResponse>
{
    public async Task<ReadSchemaResponse> ExecuteAsync(ReadSchemaCommand command, CancellationToken ct)
    {
        var mapping = await poolStoreProvider.GetRequiredByPoolIdAsync(command.IsolationId, ct);

        // Store açılmış ama henüz model yazılmamış olabilir (CreateStore sonrası).
        if (string.IsNullOrEmpty(mapping.ActiveModelId))
            throw new BusinessException(AuthZMessageKeys.SchemaNotFound);

        // "En son model" değil, AKTİF model okunur: aktif model eski bir sürüme
        // çevrildiğinde (geri alma) ikisi ayrışır ve yanıttaki ModelId ile
        // gövdedeki şema birbirini tutmaz.
        var model = await schemaWriter.GetModelAsync(mapping.StoreId, mapping.ActiveModelId, ct)
            ?? throw new BusinessException(AuthZMessageKeys.SchemaNotFound);

        return new ReadSchemaResponse(mapping.StoreId, mapping.ActiveModelId, schemaParser.Parse(model));
    }
}

public class ReadSchemaValidator : Validator<ReadSchemaCommand>
{
    public ReadSchemaValidator()
    {
        RuleFor(x => x.IsolationId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
