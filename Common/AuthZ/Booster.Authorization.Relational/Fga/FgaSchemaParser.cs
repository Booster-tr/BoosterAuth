using Booster.Authorization.Relational.Models;
using OpenFga.Sdk.Model;

namespace Booster.Authorization.Relational.Fga;

public sealed class FgaSchemaParser
{
    public IReadOnlyList<ResourceType> Parse(AuthorizationModel model)
    {
        var result = new List<ResourceType>();

        foreach (var typeDef in model.TypeDefinitions)
        {
            if (typeDef.Type == "user")
                continue;

            result.Add(ParseTypeDefinition(typeDef));
        }

        return result;
    }

    private static ResourceType ParseTypeDefinition(TypeDefinition typeDef)
    {
        var rt = new ResourceType { Name = typeDef.Type };

        if (typeDef.Relations is null)
            return rt;

        foreach (var (relationName, userset) in typeDef.Relations)
        {
            var relation = new Relation { Name = relationName };

            if (typeDef.Metadata?.Relations is not null
                && typeDef.Metadata.Relations.TryGetValue(relationName, out var relMeta)
                && relMeta.DirectlyRelatedUserTypes is not null)
            {
                relation.DirectlyAssignable = relMeta.DirectlyRelatedUserTypes
                    .Select(rr => rr.Type)
                    .ToList();
            }

            ParseUserset(userset, relation);

            rt.Relations.Add(relation);
        }

        return rt;
    }

    private static void ParseUserset(Userset userset, Relation relation)
    {
        if (userset.Union?.Child is not null)
        {
            foreach (var child in userset.Union.Child)
                ParseSingleUserset(child, relation);
            return;
        }

        ParseSingleUserset(userset, relation);
    }

    private static void ParseSingleUserset(Userset userset, Relation relation)
    {
        if (userset.This is not null)
            return;

        if (userset.ComputedUserset is not null)
        {
            relation.Rules.Add(new RelationRule
            {
                Type = RelationRuleType.Includes,
                Value = userset.ComputedUserset.Relation ?? ""
            });
            return;
        }

        if (userset.TupleToUserset is not null)
        {
            relation.Rules.Add(new RelationRule
            {
                Type = RelationRuleType.InheritsFrom,
                Value = userset.TupleToUserset.ComputedUserset.Relation ?? "",
                ViaRelation = userset.TupleToUserset.Tupleset.Relation
            });
            return;
        }
    }
}