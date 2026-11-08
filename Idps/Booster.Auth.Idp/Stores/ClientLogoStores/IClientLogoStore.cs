namespace Booster.Auth.Idp.Stores.ClientLogoStores;

/// <summary>
/// İstemci logolarını wwwroot altında <c>{clientId}.{uzantı}</c> adıyla tutar.
/// Bir istemcinin aynı anda tek logosu olur; yeni yükleme eskisinin yerini alır.
/// </summary>
public interface IClientLogoStore
{
    /// <summary>Logoların tutulduğu fiziksel klasör (wwwroot/client-logos).</summary>
    string Directory { get; }

    /// <summary>Var olan logoyu (uzantısı ne olursa olsun) siler ve yenisini kaydeder.</summary>
    Task SaveAsync(Guid clientId, ClientLogoFormat format, Stream content, CancellationToken ct);

    /// <summary>İstemcinin logosu; yoksa null.</summary>
    ClientLogoFile? Find(Guid clientId);
}

/// <param name="PhysicalPath">Diskteki tam yol.</param>
/// <param name="RelativeUrl">Uygulama köküne göreli adres (ör. "/client-logos/{id}.png?v=..").</param>
/// <param name="ContentType">Dosyanın MIME tipi.</param>
public sealed record ClientLogoFile(string PhysicalPath, string RelativeUrl, string ContentType);
