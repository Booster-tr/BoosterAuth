using Booster.Auth.Database.Constants;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.SystemMessageService.Seeders._base;

namespace Booster.SystemMessageService.Seeders;

/// <summary>
/// Servisten bağımsız ortak mesajlar (<c>common.*</c>, <c>db.*</c>).
/// <para>
/// Bunlar en kritik küme: doğrulama hataları ve <c>common.internal_error</c>
/// her serviste üretilir. Seed edilmezlerse kullanıcı, hangi uçta olursa olsun
/// metin yerine ham anahtar görür.
/// </para>
/// <para>
/// Yalnızca kodda GERÇEKTEN fırlatılan anahtarlar seed edilir.
/// <see cref="ValidationErrors"/> bu çözümün dışında da kullanılan genel bir
/// kütüphanede durduğu için orada bu projenin kullanmadığı sabitler de var;
/// onların metnini taşımak katalogda hiç görülmeyecek satır biriktirirdi.
/// </para>
/// </summary>
public class CommonMessageSeeder(IServiceScopeFactory scopeFactory, ILogger<CommonMessageSeeder> logger)
    : SystemMessageSeederBase(scopeFactory, logger)
{
    private const string TrTitle = "Hata";
    private const string EnTitle = "Error";

    protected override IReadOnlyList<Msg> Messages =>
    [
        new(ValidationErrors.ProblemTitle, TrTitle, "Bir hata oluştu.", EnTitle, "Something went wrong."),
        new(ValidationErrors.InternalServerError, TrTitle, "Beklenmeyen bir sorun oluştu. Lütfen birkaç dakika sonra tekrar deneyin.", EnTitle, "An unexpected problem occurred. Please try again in a few minutes."),
        new(ValidationErrors.ValidationError, "Doğrulama", "Girdiğiniz bilgilerde hata var.", "Validation", "There are problems with the information you entered."),
        new(ValidationErrors.DataIsNullException, "Kayıt", "Aradığınız kayıt bulunamadı.", "Record", "The record you are looking for was not found."),

        new(ValidationErrors.NotEmptyError, "Zorunlu alan", "Bu alan boş bırakılamaz.", "Required field", "This field cannot be empty."),
        new(ValidationErrors.NotNullOrEmptyError, "Zorunlu alan", "Bu alan zorunludur.", "Required field", "This field is required."),
        new(ValidationErrors.MaxLengthError, "Uzunluk", "Girilen değer izin verilen uzunluğu aşıyor.", "Length", "The value exceeds the allowed length."),
        new(ValidationErrors.PageSizeInvalid, "Sayfalama", "Sayfa boyutu 1 ile 500 arasında olmalıdır.", "Paging", "Page size must be between 1 and 500."),

        // --- İzolasyon damgalama (IsolationInterceptor) ---
        new(IsolationMessageKeys.InvalidIsolation, "Bağlam", "İşlem için havuz bağlamı belirlenemedi.", "Context", "The pool context for this operation could not be determined."),
        new(IsolationMessageKeys.IsolationFieldImmutable, "Bağlam", "Bir kaydın bağlı olduğu havuz sonradan değiştirilemez.", "Context", "A record's pool cannot be changed after it is created."),
    ];
}
