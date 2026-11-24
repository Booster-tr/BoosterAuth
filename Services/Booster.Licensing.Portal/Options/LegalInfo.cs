using Booster.Licensing.Portal.Site;

namespace Booster.Licensing.Portal.Options;

/// <summary>
/// Yasal metinlerin (Views/Site/{tr|en}/Legal, siparis ve deneme onayi) okudugu sirket ve ticari bilgiler.
/// appsettings.json "LegalInfo" bolumunden (dagitimda <c>LegalInfo__CompanyName</c> gibi ortam degiskenleriyle) gelir.
/// Koseli parantezle baslayan deger yer tutucudur ve sayfada OLDUGU GIBI gorunur; <see cref="IsComplete"/> ancak hicbir
/// deger "[" ile baslamadiginda true olur. O zamana kadar her yasal sayfanin basinda "taslak" notu gorunur.
/// Bos birakilan istege bagli alanlar (Trademark, ChamberRulesUrl) sayfada gosterilmez.
/// <para>
/// Metinlerde esasli degisiklik olursa <see cref="TermsVersion"/> ve <see cref="LastUpdated"/> degistirilir. Siparis ve
/// deneme kayitlari kabul edilen surumu saklar; eski surumlerin metni ayrica arsivlenmelidir.
/// </para>
/// </summary>
public sealed class LegalInfo
{
    // ---------------------------------------------------------------- sirket (6563 md.3, E-Ticaret Yonetmeligi md.5, TTK md.39/2)
    public string CompanyName { get; set; } = "[Şirket unvanı]";
    public string CompanyTypeTr { get; set; } = "[Şirket türü]";
    public string CompanyTypeEn { get; set; } = "[Company type]";
    public string Mersis { get; set; } = "[MERSİS numarası]";
    public string TradeRegistry { get; set; } = "[Ticaret sicil müdürlüğü ve sicil numarası]";
    public string TaxOffice { get; set; } = "[Vergi dairesi]";
    public string TaxNumber { get; set; } = "[Vergi numarası]";
    public string Address { get; set; } = "[Merkez adresi]";
    public string Kep { get; set; } = "[KEP adresi]";
    public string Email { get; set; } = "[Genel e-posta adresi]";
    public string KvkkEmail { get; set; } = "[KVKK başvuru e-posta adresi]";
    public string Chamber { get; set; } = "[Üyesi olunan oda]";
    /// <summary>Odanin meslek kurallarina erisim adresi; bos ise gosterilmez.</summary>
    public string ChamberRulesUrl { get; set; } = "[Oda meslek kuralları adresi]";
    /// <summary>"Booster Auth" marka tescil bilgisi; tescil yoksa bos birakilir.</summary>
    public string Trademark { get; set; } = "[Marka tescil numarası]";

    // Destek iletisimi SiteCatalog'dan (tek yer); yapilandirilmaz.
    public string SupportEmail => SiteCatalog.SupportEmail;
    public string Phone => SiteCatalog.SupportPhone;
    public string PhoneHref => SiteCatalog.SupportPhoneHref;

    // ---------------------------------------------------------------- altyapi (KVKK md.9 yurt disi aktarim)
    public string HostingTr { get; set; } = "[Web sitesi ve portalın barındırma sağlayıcısı ve ülkesi]";
    public string HostingEn { get; set; } = "[Hosting provider and country of the website and portal]";
    public string SmtpProviderTr { get; set; } = "[E-posta (SMTP) sağlayıcısı ve ülkesi]";
    public string SmtpProviderEn { get; set; } = "[E-mail (SMTP) provider and country]";
    public string HelpdeskProviderTr { get; set; } = "[Destek e-posta / talep sistemi sağlayıcısı ve ülkesi]";
    public string HelpdeskProviderEn { get; set; } = "[Support e-mail / ticketing provider and country]";

    // ---------------------------------------------------------------- ticari
    public string PaymentTermDays { get; set; } = "[7]";
    public string VatNoteTr { get; set; } = "[Fiyatlara KDV dahil mi, hariç mi?]";
    public string VatNoteEn { get; set; } = "[Are prices VAT inclusive or exclusive?]";
    public string WarrantyDays { get; set; } = "[30]";
    public string RefundDays { get; set; } = "[10]";
    public string SupportHoursTr { get; set; } = "[Destek saatleri]";
    public string SupportHoursEn { get; set; } = "[Support hours]";
    public string SupportLogDeleteDays { get; set; } = "[30]";

    // ---------------------------------------------------------------- surum
    public string LastUpdated { get; set; } = "2026-11-24";
    public string TermsVersion { get; set; } = "2026-11-24";

    public string CompanyType(string lang) => lang == "en" ? CompanyTypeEn : CompanyTypeTr;
    public string Hosting(string lang) => lang == "en" ? HostingEn : HostingTr;
    public string SmtpProvider(string lang) => lang == "en" ? SmtpProviderEn : SmtpProviderTr;
    public string HelpdeskProvider(string lang) => lang == "en" ? HelpdeskProviderEn : HelpdeskProviderTr;
    public string VatNote(string lang) => lang == "en" ? VatNoteEn : VatNoteTr;
    public string SupportHours(string lang) => lang == "en" ? SupportHoursEn : SupportHoursTr;

    /// <summary>Hicbir deger yer tutucu ("[" ile baslayan) degilse true; o zaman "taslak" notu kalkar.</summary>
    public bool IsComplete => !new[]
    {
        CompanyName, CompanyTypeTr, CompanyTypeEn, Mersis, TradeRegistry, TaxOffice, TaxNumber, Address, Kep, Email, KvkkEmail,
        Chamber, ChamberRulesUrl, Trademark, HostingTr, HostingEn, SmtpProviderTr, SmtpProviderEn, HelpdeskProviderTr,
        HelpdeskProviderEn, PaymentTermDays, VatNoteTr, VatNoteEn, WarrantyDays, RefundDays, SupportHoursTr, SupportHoursEn,
        SupportLogDeleteDays, LastUpdated, TermsVersion,
    }.Any(v => v.TrimStart().StartsWith('['));
}
