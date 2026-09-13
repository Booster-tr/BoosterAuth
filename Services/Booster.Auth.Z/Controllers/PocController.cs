using Booster.Authorization.Relational.Builders;
using Booster.Authorization.Relational.Fga;
using Microsoft.AspNetCore.Mvc;

namespace Booster.Auth.Z.Controllers;


[ApiController]
[Route("poc")]
public class PocController(
    FgaSchemaWriter schemaWriter,
    FgaRelationManager relationManager, FgaSchemaParser parser) : ControllerBase
{
    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken ct)
    {
        var log = new List<string>();

        // 1. Store oluştur (bir tenant)
        var storeId = await schemaWriter.CreateStoreAsync("tenant-acme-poc", ct);
        log.Add($"Store: {storeId}");

        // 2. Şemayı builder ile kur (tenant→pool→client)
        var model = new FgaSchemaBuilder()
            .Type("tenant", t => t
                .Relation("owner", "user")
                .Relation("manager", r => r.Assignable("user").Includes("owner"))
                .Relation("viewer", r => r.Assignable("user").Includes("manager")))
            .Type("pool", t => t
                .Relation("parent", "tenant")
                .Relation("owner", r => r.Assignable("user").InheritsFrom("owner", "parent"))
                .Relation("manager", r => r.Assignable("user").Includes("owner").InheritsFrom("manager", "parent"))
                .Relation("viewer", r => r.Assignable("user").Includes("manager").InheritsFrom("viewer", "parent")))
            .Type("client", t => t
                .Relation("parent", "pool")
                .Relation("owner", r => r.Assignable("user").InheritsFrom("owner", "parent"))
                .Relation("manager", r => r.Assignable("user").Includes("owner").InheritsFrom("manager", "parent"))
                .Relation("viewer", r => r.Assignable("user").Includes("manager").InheritsFrom("viewer", "parent")))
            .Build();
        var lastModel =await schemaWriter.GetLatestModelAsync("01M2DDACBB60QV5WM0MD4G7X34");

        var moldeasd = parser.Parse(lastModel!);
        // 3. Modeli yaz
        var modelId = await schemaWriter.WriteModelAsync(storeId, model, ct);
        log.Add($"Model: {modelId}");

        // 4. Tuple'lar (3 satır → tüm hiyerarşi)
        await relationManager.WriteBatchAsync(storeId,
        [
            new("user:ali", "owner", "tenant:acme"),
            new("tenant:acme", "parent", "pool:electronics"),
            new("pool:electronics", "parent", "client:laptop"),
        ], ct);
        log.Add("3 tuple yazildi");

        // 5. Check — ali en alttaki client'ta owner/manager/viewer mi? (hesaplanmali)
        log.Add($"ali → client:laptop owner?   {await relationManager.CheckAsync(storeId, "user:ali", "owner", "client:laptop", modelId, ct)}");
        log.Add($"ali → client:laptop manager? {await relationManager.CheckAsync(storeId, "user:ali", "manager", "client:laptop", modelId, ct)}");
        log.Add($"ali → client:laptop viewer?  {await relationManager.CheckAsync(storeId, "user:ali", "viewer", "client:laptop", modelId, ct)}");
        log.Add($"mehmet → client:laptop viewer? {await relationManager.CheckAsync(storeId, "user:mehmet", "viewer", "client:laptop", modelId, ct)}");

        // 6. Batch check
        var batch = await relationManager.BatchCheckAsync(storeId,
        [
            new("user:ali", "owner", "tenant:acme"),
            new("user:ali", "manager", "pool:electronics"),
            new("user:ali", "viewer", "client:laptop"),
        ], modelId, ct);
        foreach (var r in batch) log.Add($"batch [{r.CorrelationId}] allowed={r.Allowed}");
        
        return Ok(log);
    }
}