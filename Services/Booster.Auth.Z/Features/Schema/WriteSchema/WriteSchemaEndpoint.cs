using Booster.Auth.Z.Features.PoolStores.UpdateActiveModel;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Builders;
using Booster.Authorization.Relational.Fga;
using Booster.Authorization.Relational.Models;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Z.Features.Schema.WriteSchema;

public class WriteSchemaEndpoint : Endpoint<WriteSchemaCommand, WriteSchemaResponse>
{
    public override void Configure()
    {
        Post("authz/schema");
        Summary(s =>
        {
            s.Summary = "Pool için yeni bir authorization model yazar ve aktif eder";
            s.Description = "Model'ler değişmezdir: mevcut model güncellenmez, yenisi yazılır ve " +
                            "eşlemedeki activeModelId ona çevrilir. Pool'un store'u yoksa hata döner; " +
                            "ilk store oluşturma ayrı bir uçtur.";
        });
    }

    public override async Task HandleAsync(WriteSchemaCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record WriteSchemaCommand(
    Guid IsolationId,
    List<ResourceType> Types) : ICommand<WriteSchemaResponse>;

public readonly record struct WriteSchemaResponse(string StoreId, string ModelId);

public class WriteSchemaCommandHandler(
    IPoolStoreProvider poolStoreProvider,
    FgaSchemaWriter schemaWriter)
    : ICommandHandler<WriteSchemaCommand, WriteSchemaResponse>
{
    public async Task<WriteSchemaResponse> ExecuteAsync(WriteSchemaCommand command, CancellationToken ct)
    {
        var mapping = await poolStoreProvider.GetRequiredByPoolIdAsync(command.IsolationId, ct);

        var model = new FgaSchemaBuilder()
            .AddRange(command.Types)
            .Build();

        var modelId = await schemaWriter.WriteModelAsync(mapping.StoreId, model, ct);

        await new UpdateActiveModelCommand(command.IsolationId, modelId).ExecuteAsync(ct);

        return new WriteSchemaResponse(mapping.StoreId, modelId);
    }
}

public class WriteSchemaValidator : Validator<WriteSchemaCommand>
{
    public WriteSchemaValidator()
    {
        RuleFor(x => x.IsolationId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Types).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleForEach(x => x.Types).ChildRules(type =>
        {
            type.RuleFor(t => t.Name).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

            type.RuleFor(t => t.Relations).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

            type.RuleForEach(t => t.Relations).ChildRules(relation =>
            {
                relation.RuleFor(r => r.Name).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

                relation.RuleFor(r => r)
                    .Must(r => r.DirectlyAssignable.Count > 0 || r.Rules.Count > 0)
                    .WithMessage(ValidationErrors.NotEmptyError);
            });
        });
    }
}
