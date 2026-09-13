namespace Booster.Authorization.Relational.Models;

public sealed class RelationRule
{
    public required RelationRuleType Type { get; set; }

    public required string Value { get; set; }

    public string? ViaRelation { get; set; }
}

public enum RelationRuleType
{
    Includes = 1,

    InheritsFrom = 2
}