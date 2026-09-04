namespace Booster.ExceptionManager.Abstraction.Constants;

public static class ValidationRegex
{
    // C# Sınıf/Property İsimlendirme Kuralı: Harf veya alt çizgi ile başlar. Rakamla başlayamaz. Boşluk veya özel karakter içeremez.
    public const string CodeIdentifier = @"^[a-zA-Z_][a-zA-Z0-9_]*$";

    // C# Sınıf/Property İsimlendirme Kuralı: Harf Rakam ve . içerebilir.
    public const string OnlyCharacterDotOrNumeric = @"^[a-zA-Z0-9.]*$";

    // HTTP Route Kuralı: Sadece harf, rakam, tire, alt çizgi, slash ve parametre parantezleri { }
    public const string HttpRoute = @"^[a-zA-Z0-9_\-\/\{\}]+$";
}
