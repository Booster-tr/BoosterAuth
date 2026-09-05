using Microsoft.AspNetCore.Mvc;

namespace Booster.Auth.Idp.Controllers.Base;

public class IdpBaseController : Controller
{
    protected string ResolveLang()
    {
        var lang = Request.Headers.AcceptLanguage.FirstOrDefault() ?? "tr-tr";
        return lang.Length > 5 ? lang[..5].ToLowerInvariant() : lang.ToLowerInvariant();
    }
}
