namespace Booster.Auth.Idp.Stores.PasswordResetStores;

/// <summary>
/// Parola sıfırlama kayıtlarının önbellek anahtarları.
/// <para>
/// İki ayrı anahtar var çünkü iki ayrı soruya cevap veriyorlar: biri "bu
/// tarayıcıdaki akışın kodu ne?", diğeri "bu kullanıcının beklemede bir talebi
/// var mı?". Tek anahtar kullanılsaydı, kullanıcı yeni bir etkileşim başlatıp
/// (sayfayı tazeleyip) sınırsız kod isteyebilirdi.
/// </para>
/// </summary>
public static class PasswordResetKeys
{
    /// <summary>Kodun tutulduğu kayıt; akışı başlatan tarayıcıya bağlıdır.</summary>
    public static string ForInteraction(string interactionKey) => interactionKey;

    /// <summary>
    /// Kullanıcı başına "beklemede talep var" işareti. Etkileşimden bağımsızdır;
    /// aksi hâlde her tazelemede yeni bir anahtar doğar ve kural boşa çıkardı.
    /// </summary>
    public static string Pending(Guid isolationId, Guid principalId)
        => $"pwreset-pending:{isolationId}:{principalId}";
}
