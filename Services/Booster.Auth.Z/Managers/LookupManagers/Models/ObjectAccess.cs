namespace Booster.Auth.Z.Managers.LookupManagers.Models;


public readonly record struct ObjectAccess(
    string ObjectType,
    string ObjectId,
    IReadOnlyList<string> Relations);
