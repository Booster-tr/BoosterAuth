namespace Booster.Auth.Idp.Stores.PasswordResetStores;

public static class PasswordResetKeys
{
    public static string ForInteraction(string interactionKey) => interactionKey;

    public static string Pending(Guid isolationId, Guid principalId)
        => $"pwreset-pending:{isolationId}:{principalId}";
}
