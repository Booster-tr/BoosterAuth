using System.Text.RegularExpressions;
using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Authorization.Relational.Options;

namespace Booster.Auth.Z.Features.Schemas._base;

/// <summary>Arayüzün ilgili alanın yanında gösterdiği sorun. Code arayüzde çevrilir.</summary>
public record SchemaIssue(string Code, string? Type = null, string? Relation = null, string? Value = null);

/// <summary>
/// Taslak yarım hâliyle kaydedilebilir; sorunlar arayüze listelenir ve yayınlama sorun varken engellenir.
/// Kurallar OpenFGA'nın modeli reddedeceği durumları önceden yakalar; böylece kullanıcı ham
/// OpenFGA hatası yerine hangi ilişkide neyin eksik olduğunu görür.
/// </summary>
public static partial class SchemaValidator
{
    public const string UserType = AuthConstants.UserPrefix;

    [GeneratedRegex("^[a-z][a-z0-9_]{0,39}$")]
    private static partial Regex NamePattern();

    public static bool IsValidName(string? name) => name is not null && NamePattern().IsMatch(name);

    public static List<SchemaIssue> Validate(SchemaDefinition definition, Guid poolId, FgaOptions options)
    {
        var issues = new List<SchemaIssue>();
        var types = definition.Types;

        if (SystemSchema.IsSystemPool(poolId))
            issues.AddRange(SystemSchema.MissingProtected(definition));

        // Bos sema canliya alinirsa havuzdaki tum kontroller "iliski semaya uymuyor" diye reddedilir.
        if (types.Count == 0)
            issues.Add(new("no_types"));

        if (types.Count > options.MaxSchemaTypes)
            issues.Add(new("too_many_types", Value: options.MaxSchemaTypes.ToString()));

        var byName = types
            .Where(t => IsValidName(t.Name))
            .GroupBy(t => t.Name)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var type in types)
        {
            if (!IsValidName(type.Name))
                issues.Add(new("type_name_invalid", type.Name));
            else if (type.Name == UserType)
                issues.Add(new("type_name_reserved", type.Name));
            else if (types.Count(t => t.Name == type.Name) > 1)
                issues.Add(new("type_name_duplicate", type.Name));

            if (type.Relations.Count == 0)
                issues.Add(new("type_no_relations", type.Name));

            if (type.Relations.Count(r => !r.IsLink(UserType)) > options.MaxPermissionsPerType)
                issues.Add(new("too_many_permissions", type.Name, Value: options.MaxPermissionsPerType.ToString()));

            foreach (var relation in type.Relations)
                ValidateRelation(type, relation, byName, issues);
        }

        if (issues.Count == 0)
            ValidateEntrypoints(types, issues);

        return issues
            .DistinctBy(i => (i.Code, i.Type, i.Relation, i.Value))
            .ToList();
    }

    private static void ValidateRelation(
        SchemaType type, SchemaRelation relation, Dictionary<string, SchemaType> types, List<SchemaIssue> issues)
    {
        void Add(string code, string? value = null) => issues.Add(new(code, type.Name, relation.Name, value));

        if (!IsValidName(relation.Name))
            Add("relation_name_invalid");
        else if (type.Relations.Count(r => r.Name == relation.Name) > 1)
            Add("relation_name_duplicate");

        if (relation.AssignableTypes.Count == 0 && relation.Includes.Count == 0 && relation.Inherits.Count == 0)
            Add("relation_empty");

        foreach (var assignable in relation.AssignableTypes)
            if (assignable != UserType && !types.ContainsKey(assignable))
                Add("assignable_unknown", assignable);

        foreach (var included in relation.Includes)
        {
            if (included == relation.Name)
                Add("includes_self", included);
            else if (type.Relations.All(r => r.Name != included))
                Add("includes_unknown", included);
        }

        foreach (var inherit in relation.Inherits)
        {
            var via = type.Relations.FirstOrDefault(r => r.Name == inherit.Via);

            if (via is null)
            {
                Add("inherit_via_unknown", inherit.Via);
                continue;
            }

            // OpenFGA, bağlantı ilişkisinin yalnızca doğrudan atama olmasını ister.
            if (via.Includes.Count > 0 || via.Inherits.Count > 0)
                Add("inherit_via_not_direct", inherit.Via);

            var parents = via.AssignableTypes.Where(t => t != UserType).ToList();

            if (parents.Count == 0 || via.AssignableTypes.Contains(UserType))
            {
                Add("inherit_via_not_link", inherit.Via);
                continue;
            }

            foreach (var parent in parents)
                if (types.TryGetValue(parent, out var parentType) && parentType.Relations.All(r => r.Name != inherit.Relation))
                    Add("inherit_relation_missing", $"{parent}.{inherit.Relation}");
        }
    }

    /// <summary>
    /// Her ilişki sonunda bir doğrudan atamaya ulaşmalı; birbirini kapsayan ama hiçbirine kimsenin
    /// atanamadığı ilişkiler OpenFGA'da reddedilir. Ulaşılabilirlik sabit noktaya kadar yayılır.
    /// </summary>
    private static void ValidateEntrypoints(List<SchemaType> types, List<SchemaIssue> issues)
    {
        var reachable = new HashSet<string>(types
            .SelectMany(t => t.Relations.Where(r => r.AssignableTypes.Count > 0).Select(r => $"{t.Name}#{r.Name}")));

        var changed = true;
        while (changed)
        {
            changed = false;

            foreach (var type in types)
            foreach (var relation in type.Relations)
            {
                var key = $"{type.Name}#{relation.Name}";
                if (reachable.Contains(key))
                    continue;

                var viaIncludes = relation.Includes.Any(i => reachable.Contains($"{type.Name}#{i}"));

                var viaInherits = relation.Inherits.Any(inherit =>
                    type.Relations.First(r => r.Name == inherit.Via).AssignableTypes
                        .Any(parent => reachable.Contains($"{parent}#{inherit.Relation}")));

                if (viaIncludes || viaInherits)
                    changed = reachable.Add(key);
            }
        }

        foreach (var type in types)
        foreach (var relation in type.Relations)
            if (!reachable.Contains($"{type.Name}#{relation.Name}"))
                issues.Add(new("relation_no_entrypoint", type.Name, relation.Name));
    }
}
