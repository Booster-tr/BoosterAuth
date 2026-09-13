namespace Booster.Authorization.Relational.Models;


public sealed class ResourceType
{
    public required string Name { get; set; }
    public List<Relation> Relations { get; set; } = [];
}

