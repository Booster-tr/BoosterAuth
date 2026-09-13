using Booster.Authorization.Relational.Models;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Model;

namespace Booster.Authorization.Relational.Builders;

public sealed class FgaSchemaBuilder
{
    private const string SchemaVersion = "1.1";
    private readonly List<ResourceType> _types = [];

    public FgaSchemaBuilder Type(string name, Action<ResourceTypeBuilder> configure)
    {
        var rt = new ResourceType { Name = name };
        configure(new ResourceTypeBuilder(rt));
        _types.Add(rt);
        return this;
    }

    public FgaSchemaBuilder AddRange(IEnumerable<ResourceType> types)
    {
        _types.AddRange(types);
        return this;
    }

    public ClientWriteAuthorizationModelRequest Build()
    {
        var typeDefinitions = new List<TypeDefinition> { new() { Type = "user" } };
        foreach (var rt in _types)
            typeDefinitions.Add(ToTypeDefinition(rt));

        return new ClientWriteAuthorizationModelRequest
        {
            SchemaVersion = SchemaVersion,
            TypeDefinitions = typeDefinitions
        };
    }

    private static TypeDefinition ToTypeDefinition(ResourceType rt)
    {
        var relations = new Dictionary<string, Userset>();
        var metadata = new Metadata { Relations = new Dictionary<string, RelationMetadata>() };

        foreach (var rel in rt.Relations)
        {
            relations[rel.Name] = ToUserset(rel);
            metadata.Relations[rel.Name] = new RelationMetadata
            {
                DirectlyRelatedUserTypes = rel.DirectlyAssignable
                    .Select(t => new RelationReference { Type = t })
                    .ToList()
            };
        }

        return new TypeDefinition { Type = rt.Name, Relations = relations, Metadata = metadata };
    }

    private static Userset ToUserset(Relation rel)
    {
        var children = new List<Userset>();

        if (rel.DirectlyAssignable.Count > 0)
            children.Add(new Userset { This = new object() });

        foreach (var rule in rel.Rules)
        {
            children.Add(rule.Type switch
            {
                RelationRuleType.Includes => new Userset
                {
                    ComputedUserset = new ObjectRelation { Relation = rule.Value }
                },
                RelationRuleType.InheritsFrom => new Userset
                {
                    TupleToUserset = new TupleToUserset
                    {
                        Tupleset = new ObjectRelation { Relation = rule.ViaRelation },
                        ComputedUserset = new ObjectRelation { Relation = rule.Value }
                    }
                },
                _ => throw new NotSupportedException($"Bilinmeyen kural: {rule.Type}")
            });
        }

        return children.Count switch
        {
            0 => throw new InvalidOperationException($"'{rel.Name}' ilişkisi tanımsız."),
            1 => children[0],
            _ => new Userset { Union = new Usersets { Child = children } }
        };
    }
}

public sealed class ResourceTypeBuilder(ResourceType type)
{
    public ResourceTypeBuilder Relation(string name, params string[] directlyAssignable)
    {
        type.Relations.Add(new Relation
        {
            Name = name,
            DirectlyAssignable = [.. directlyAssignable]
        });
        return this;
    }

    public ResourceTypeBuilder Relation(string name, Action<RelationBuilder> configure)
    {
        var rel = new Relation { Name = name };
        configure(new RelationBuilder(rel));
        type.Relations.Add(rel);
        return this;
    }
}

public sealed class RelationBuilder(Relation relation)
{
    public RelationBuilder Assignable(params string[] types)
    {
        relation.DirectlyAssignable.AddRange(types);
        return this;
    }

    public RelationBuilder Includes(string relationName)
    {
        relation.Rules.Add(new RelationRule { Type = RelationRuleType.Includes, Value = relationName });
        return this;
    }

    public RelationBuilder InheritsFrom(string relationName, string viaRelation)
    {
        relation.Rules.Add(new RelationRule
        {
            Type = RelationRuleType.InheritsFrom,
            Value = relationName,
            ViaRelation = viaRelation
        });
        return this;
    }
}