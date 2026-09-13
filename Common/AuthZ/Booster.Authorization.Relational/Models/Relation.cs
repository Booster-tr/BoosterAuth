namespace Booster.Authorization.Relational.Models;

public sealed class Relation
{
    public required string Name { get; set; }

    public List<string> DirectlyAssignable { get; set; } = [];

    public List<RelationRule> Rules { get; set; } = [];
}