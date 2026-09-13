using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Booster.Auth.Idp.Extensions;

/// <summary>
/// View katmanının ModelState'i okurken kullandığı yardımcılar.
/// Hata durumunun tek kaynağı ModelState'tir; view'lar buradan türetir.
/// </summary>
public static class ModelStateExtensions
{
    /// <summary>
    /// Belirtilen alan için GÖSTERİLEBİLİR bir doğrulama hatası var mı?
    /// <para>
    /// Mesajı boş olan kayıtlar (model binding hataları böyle üretilir) hata
    /// sayılmaz: aksi hâlde alan kırmızıya boyanır ama ne alanın altında ne de
    /// özet bloğunda tek satır metin çıkar. <see cref="AllErrors"/> ile aynı
    /// süzgeci kullanmak zorunludur, yoksa iki taraf birbirine yalan söyler.
    /// </para>
    /// </summary>
    public static bool HasError(this ModelStateDictionary modelState, string key)
        => modelState.TryGetValue(key, out var entry)
           && entry is not null
           && entry.Errors.Any(error => !string.IsNullOrWhiteSpace(error.ErrorMessage));

    /// <summary>
    /// Tüm hataları, alan anahtarıyla birlikte düz liste hâlinde döndürür.
    /// Boş anahtarlı kayıtlar form seviyesi hatalardır (ör. "Bilgiler eşleşmedi")
    /// ve hiçbir alana bağlanamaz — özet bloğunda bağlantısız gösterilirler.
    /// </summary>
    public static IReadOnlyList<ModelStateError> AllErrors(this ModelStateDictionary modelState)
        => modelState
            .Where(entry => entry.Value is not null)
            .SelectMany(entry => entry.Value!.Errors
                .Where(error => !string.IsNullOrWhiteSpace(error.ErrorMessage))
                .Select(error => new ModelStateError(entry.Key, error.ErrorMessage)))
            .ToList();

    /// <summary>
    /// Hata özetindeki bağlantı, odaklanabilir bir alana gitmelidir.
    /// Gizli alanların (InteractionKey, ProviderType, Scope…) hatası kullanıcının
    /// düzeltebileceği bir şey değildir; bağlantı verilirse hiçbir şey yapmaz.
    /// Bu anahtarlar özet içinde form seviyesi hata gibi, bağlantısız gösterilir.
    /// </summary>
    public static bool IsFocusableField(string key)
        => !string.IsNullOrEmpty(key) && !HiddenKeys.Contains(key);

    private static readonly HashSet<string> HiddenKeys =
        new(StringComparer.Ordinal) { "InteractionKey", "ProviderType", "ClientKey", "Scope" };
}

/// <param name="Key">Alan adı; form seviyesi hatalarda boştur.</param>
/// <param name="Message">Kullanıcıya gösterilecek, çevrilmiş mesaj.</param>
public sealed record ModelStateError(string Key, string Message);
