using Booster.Auth.Idp.Options;
using Microsoft.AspNetCore.Mvc;

namespace Booster.Auth.Idp.Extensions;

/// <summary>
/// View katmanı için URL yardımcıları.
/// Uygulama bir gateway arkasında path prefix ile yayınlanabildiği için
/// (ör. https://gw.example.com/auth) statik dosya ve form action adresleri
/// <see cref="IdpUiOptions.GetAssetUrl"/> üzerinden üretilir.
/// IdpUiOptions DI'a kayıtlı değilse ASP.NET'in kendi PathBase çözümlemesine düşülür.
/// </summary>
public static class IdpUrlExtensions
{
    /// <summary>Uygulama köküne göreli bir yolu (ör. "/css/auth.css") mutlak yola çevirir.</summary>
    public static string Idp(this IUrlHelper url, string path)
    {
        var options = url.ActionContext.HttpContext.RequestServices.GetService<IdpUiOptions>();
        return options is not null
            ? options.GetAssetUrl(path)
            : url.Content("~" + path);
    }

    /// <summary>Statik varlık yolunu üretir; önbellek kırma için sürüm damgası ekler.</summary>
    public static string IdpAsset(this IUrlHelper url, string path)
        => $"{url.Idp(path)}?v={AssetVersion}";

    /// <summary>Derleme başına sabit, sürüm bazlı cache-busting damgası.</summary>
    private static readonly string AssetVersion =
        (typeof(IdpUrlExtensions).Assembly.GetName().Version?.ToString() ?? "1")
        .Replace('.', '-');
}
