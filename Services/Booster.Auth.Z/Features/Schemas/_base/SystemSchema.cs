using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Z.Features._base.Entities;

namespace Booster.Auth.Z.Features.Schemas._base;

/// <summary>
/// Booster yönetim havuzunun başlangıç şeması. Yalnızca store ilk kurulurken kullanılır; sonrasında
/// arayüzden düzenlenir. Platform kodunun dayandığı tip ve ilişkiler korumalıdır: kodu değiştirilemez,
/// silinemez ve eksik olan sürüm yayına alınamaz.
/// </summary>
public static class SystemSchema
{
    private const string User = AuthConstants.UserPrefix;
    private const string Parent = AuthConstants.ParentRelation;

    private static readonly string[] Roles =
    [
        AuthConstants.OwnerRelation,
        AuthConstants.ManagerRelation,
        AuthConstants.ViewerRelation,
        AuthConstants.GrantManagerRelation
    ];

    /// <summary>"tip" ya da "tip#ilişki" biçiminde korumalı öğeler.</summary>
    public static readonly IReadOnlyList<string> Protected =
    [
        AuthConstants.TenantType,
        .. Roles.Select(r => $"{AuthConstants.TenantType}#{r}"),
        AuthConstants.PoolType,
        $"{AuthConstants.PoolType}#{Parent}",
        $"{AuthConstants.PoolType}#{AuthConstants.EndUserRelation}",
        .. Roles.Select(r => $"{AuthConstants.PoolType}#{r}"),
        AuthConstants.ClientType,
        $"{AuthConstants.ClientType}#{Parent}",
        .. Roles.Select(r => $"{AuthConstants.ClientType}#{r}")
    ];

    public static bool IsSystemPool(Guid poolId) => poolId == AuthConstants.ManagementPoolId;

    public static IReadOnlyList<string> ProtectedFor(Guid poolId) => IsSystemPool(poolId) ? Protected : [];

    public static IEnumerable<SchemaIssue> MissingProtected(SchemaDefinition definition)
    {
        foreach (var key in Protected)
        {
            var parts = key.Split('#');
            var type = definition.Types.FirstOrDefault(t => t.Name == parts[0]);

            if (parts.Length == 1 && type is null)
                yield return new("system_type_required", parts[0]);
            else if (parts.Length == 2 && type is not null && type.Relations.All(r => r.Name != parts[1]))
                yield return new("system_relation_required", parts[0], parts[1]);
        }
    }

    public static SchemaDefinition Definition => new()
    {
        Types =
        [
            Type(AuthConstants.TenantType, "Firma", 40, RoleRelations(inherit: false)),
            Type(AuthConstants.PoolType, "Havuz", 420,
            [
                Link(AuthConstants.TenantType, "Firma"),
                Relation(AuthConstants.EndUserRelation, "Son kullanıcı"),
                .. RoleRelations(inherit: true)
            ]),
            Type(AuthConstants.ClientType, "Uygulama", 800,
            [
                Link(AuthConstants.PoolType, "Havuz"),
                .. RoleRelations(inherit: true)
            ])
        ]
    };

    private static SchemaType Type(string name, string displayName, double x, List<SchemaRelation> relations)
        => new() { Name = name, DisplayName = displayName, X = x, Y = 40, Relations = relations };

    private static List<SchemaRelation> RoleRelations(bool inherit)
    {
        SchemaRelation Role(string name, string displayName, params string[] includes)
        {
            var relation = Relation(name, displayName);
            relation.Includes = [.. includes];

            if (inherit)
                relation.Inherits = [new SchemaInherit { Via = Parent, Relation = name }];

            return relation;
        }

        return
        [
            Role(AuthConstants.OwnerRelation, "Sahip"),
            Role(AuthConstants.ManagerRelation, "Yönetici", AuthConstants.OwnerRelation),
            Role(AuthConstants.ViewerRelation, "Görüntüleyici", AuthConstants.ManagerRelation, AuthConstants.GrantManagerRelation),
            Role(AuthConstants.GrantManagerRelation, "Yetki yöneticisi", AuthConstants.OwnerRelation)
        ];
    }

    private static SchemaRelation Link(string target, string displayName)
        => new() { Name = Parent, DisplayName = displayName, AssignableTypes = [target] };

    private static SchemaRelation Relation(string name, string displayName)
        => new() { Name = name, DisplayName = displayName, AssignableTypes = [User] };
}
