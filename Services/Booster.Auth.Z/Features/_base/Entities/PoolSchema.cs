using Booster.Authorization.Relational.Builders;
using Booster.Authorization.Relational.Models;
using Booster.Database.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenFga.Sdk.Client.Model;

namespace Booster.Auth.Z.Features._base.Entities;

public enum SchemaVersionStatus
{
    /// <summary>Hiç yayına alınmamış, düzenlenebilir.</summary>
    Draft = 1,

    /// <summary>Yayında; yetki kontrolleri bu sürümün modelini kullanır. Havuz başına tek.</summary>
    Active = 2,

    /// <summary>Daha önce yayındaydı; düzenlenebilir ve tekrar aktif edilebilir.</summary>
    Inactive = 3
}

/// <summary>
/// Havuzun adlandırılmış bir şema sürümü. Sürüm değiştirmek yalnızca durumları değiştirir;
/// OpenFGA'ya model yalnızca sürümün modeli yoksa (yeni ya da modeli etkileyen düzenleme) yazılır.
/// </summary>
public class PoolSchema : Entity
{
    public Guid PoolId { get; set; }

    public string StoreId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public SchemaVersionStatus Status { get; set; }

    public SchemaDefinition Definition { get; set; } = new();

    /// <summary>Tanımın OpenFGA modeli; modeli etkileyen her düzenlemede boşaltılır, aktif edilirken yazılır.</summary>
    public string? ModelId { get; set; }

    /// <summary>Her tanım kaydında artar; eşzamanlı iki düzenlemeden sonrakinin öncekini ezmesini engeller.</summary>
    public int Revision { get; set; }

    public DateTimeOffset? ActivatedTime { get; set; }

    public Guid? ActivatedBy { get; set; }
}

/// <summary>Havuzun kendi kaynak tipleri. <c>user</c> tipi her şemada vardır ve burada tutulmaz.</summary>
public class SchemaDefinition
{
    public List<SchemaType> Types { get; set; } = [];

    /// <summary>Modeli etkileyen kısmın sıradan bağımsız özeti; yalnızca görünüm değiştiyse yeni model yazılmaz.</summary>
    public string ModelFingerprint()
        => string.Join(';', Types
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => t.Name + "{" + string.Join(';', t.Relations
                .OrderBy(r => r.Name, StringComparer.Ordinal)
                .Select(r => $"{r.Name}=" +
                             $"[{string.Join(',', r.AssignableTypes.Order(StringComparer.Ordinal))}]" +
                             $"[{string.Join(',', r.Includes.Order(StringComparer.Ordinal))}]" +
                             $"[{string.Join(',', r.Inherits.Select(i => $"{i.Via}>{i.Relation}").Order(StringComparer.Ordinal))}]")) + "}"));

    public ClientWriteAuthorizationModelRequest ToFgaModel()
        => new FgaSchemaBuilder()
            .AddRange(Types.Select(t => new ResourceType
            {
                Name = t.Name,
                Relations = t.Relations.Select(r => new Relation
                {
                    Name = r.Name,
                    DirectlyAssignable = [.. r.AssignableTypes],
                    Rules =
                    [
                        .. r.Includes.Select(i => new RelationRule { Type = RelationRuleType.Includes, Value = i }),
                        .. r.Inherits.Select(i => new RelationRule
                        {
                            Type = RelationRuleType.InheritsFrom,
                            Value = i.Relation,
                            ViaRelation = i.Via
                        })
                    ]
                }).ToList()
            }))
            .Build();
}

/// <remarks>X/Y, ad ve açıklama yalnızca arayüz içindir; OpenFGA modeline girmez.</remarks>
public class SchemaType
{
    public string Name { get; set; } = default!;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public List<SchemaRelation> Relations { get; set; } = [];
}

public class SchemaRelation
{
    public string Name { get; set; } = default!;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Doğrudan atanabilecek tipler (<c>user</c> veya kaynak tipleri).</summary>
    public List<string> AssignableTypes { get; set; } = [];

    /// <summary>Aynı tipteki bu ilişkilere sahip olanlar bu ilişkiye de sahiptir.</summary>
    public List<string> Includes { get; set; } = [];

    /// <summary>Bağlı üst kayıttaki ilişkiden miras.</summary>
    public List<SchemaInherit> Inherits { get; set; } = [];

    /// <summary>Kaydı başka kayıtlara bağlar (Belge → Klasör): yalnızca kaynak tipleri atanır, kuralı yoktur.</summary>
    public bool IsLink(string userType)
        => AssignableTypes.Count > 0 && !AssignableTypes.Contains(userType) && Includes.Count == 0 && Inherits.Count == 0;
}

public class SchemaInherit
{
    /// <summary>Üst kaydı bağlayan ilişki.</summary>
    public string Via { get; set; } = default!;

    /// <summary>Üst kayıttaki ilişki.</summary>
    public string Relation { get; set; } = default!;
}

public class PoolSchemaConfiguration : IEntityTypeConfiguration<PoolSchema>
{
    public void Configure(EntityTypeBuilder<PoolSchema> builder)
    {
        builder.ToTable("PoolSchemas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);
        builder.Property(x => x.DeletedTime);

        builder.Property(x => x.PoolId).IsRequired();
        builder.Property(x => x.StoreId).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.ModelId).HasMaxLength(50);
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.Property(x => x.ActivatedTime);
        builder.Property(x => x.ActivatedBy);

        builder.OwnsOne(x => x.Definition, definition =>
        {
            definition.ToJson();
            definition.OwnsMany(d => d.Types, type =>
                type.OwnsMany(t => t.Relations, relation => relation.OwnsMany(r => r.Inherits)));
        });

        builder.HasIndex(x => x.PoolId)
               .HasDatabaseName("IX_PoolSchemas_PoolId_Active")
               .IsUnique()
               .HasFilter($"\"Status\" = {(int)SchemaVersionStatus.Active} AND \"DeletedTime\" IS NULL");

        builder.HasIndex(x => new { x.PoolId, x.Name })
               .IsUnique()
               .HasFilter("\"DeletedTime\" IS NULL");
    }
}
