namespace Booster.Observability;

/// <summary>
/// Denetim kayitlarinin (kim, ne zaman, neyi yapti) logger kategorisi. Bu kategorideki kayitlar ayri bir akisa
/// (<see cref="ObservabilityOptions.AuditStream"/>) gider; isletim loglarindan ayri ve daha uzun saklanir.
/// </summary>
public static class AuditCategory
{
    public const string Name = "Booster.Audit";
}
