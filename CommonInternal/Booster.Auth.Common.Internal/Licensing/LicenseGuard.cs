using System.Collections.Concurrent;
using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Licensing.Runtime;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Booster.Auth.Common.Internal.Licensing;

/// <summary>
/// Bu uc lisans kisitliyken de calisir. Iki durum icin:
/// (1) lisans ekranlari (yenileme tek yol budur),
/// (2) POST ile cagrilan ama hicbir sey DEGISTIRMEYEN uclar (yetki kontrolu, arama, okuma). Panel salt okunurken
///     bunlar kapansaydi panel ekran bile cizemezdi.
/// Endpoint sinifina attribute olarak ya da <c>Options(x =&gt; x.WithMetadata(new AllowWhenLicenseRestricted()))</c> ile verilir.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AllowWhenLicenseRestricted : Attribute;

/// <summary>
/// Lisans Restricted / Invalid / None iken yonetim panelini SALT OKUNUR yapar.
/// <para>
/// Yalnizca su istekler reddedilir: panel scope'u (<c>booster:panel</c>) tasiyan token ile gelen, okuma olmayan
/// (GET/HEAD/OPTIONS disi) ve <see cref="AllowWhenLicenseRestricted"/> ile isaretlenmemis istekler.
/// Son kullanici token'larinda panel scope'u YOKTUR; authorize/token/userinfo/logout uclari anonim veya son kullanici
/// token'iyla cagrilir. Bu yuzden bu filtre son kullanici akislarina HICBIR durumda dokunmaz (EN KRITIK KURAL).
/// </para>
/// Durum saglayicisi kayitli degilse (lisansi bilmeyen servis) hicbir sey yapmaz.
/// </summary>
public sealed class LicenseGuard : IGlobalPreProcessor
{
    private static readonly ConcurrentDictionary<Type, bool> ExemptTypes = new();

    public async Task PreProcessAsync(IPreProcessorContext ctx, CancellationToken ct)
    {
        var http = ctx.HttpContext;

        if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method) || HttpMethods.IsOptions(http.Request.Method))
            return;

        if (!HasPanelScope(http) || IsExempt(http))
            return;

        var provider = http.RequestServices.GetService<ILicenseStatusProvider>();
        if (provider is null)
            return;

        var status = await provider.GetAsync(ct);
        if (status.PanelWritable)
            return;

        // Limit asimi: musteri silerek limitin altina inebilsin (or. daha kucuk pakete gecis).
        if (status.DeletesAllowed && HttpMethods.IsDelete(http.Request.Method))
            return;

        throw new ForbiddenException(status.DeletesAllowed ? LicenseMessageKeys.OverLimit : LicenseMessageKeys.Restricted);
    }

    private static bool IsExempt(HttpContext http)
    {
        var metadata = http.GetEndpoint()?.Metadata;
        if (metadata is null) return false;
        if (metadata.GetMetadata<AllowWhenLicenseRestricted>() is not null) return true;

        var type = metadata.GetMetadata<EndpointDefinition>()?.EndpointType;
        return type is not null
               && ExemptTypes.GetOrAdd(type, t => t.IsDefined(typeof(AllowWhenLicenseRestricted), inherit: false));
    }

    private static bool HasPanelScope(HttpContext http)
    {
        var scope = http.User.FindFirst("scope")?.Value;
        return !string.IsNullOrEmpty(scope)
               && scope.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                   .Contains(AuthConstants.ManagementPanelScope, StringComparer.OrdinalIgnoreCase);
    }
}
