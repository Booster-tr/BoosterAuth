namespace Booster.Auth.Idp.Models;

/// <summary>
/// Yasal metin sayfasi. Metnin kendisi YALNIZCA kaynak dosyalarindadir
/// (Resources/SharedResource*.resx); burada yalnizca anahtar oneki ve bolum
/// sayisi tutulur.
/// <para>
/// Anahtar duzeni: <c>Legal.{Key}.Title</c>, <c>.Intro</c>, <c>.Updated</c>,
/// <c>.S{n}.Title</c>, <c>.S{n}.Body</c> (n = 1..SectionCount). Body HTML
/// icerebilir; yalnizca bizim kaynak dosyalarimizdan gelir, kullanici girdisi
/// asla bu anahtarlara yazilmaz.
/// </para>
/// </summary>
public sealed record LegalDocumentViewModel(string Key, int SectionCount, string? ReturnUrl)
{
    public string Prefix => $"Legal.{Key}";
}
