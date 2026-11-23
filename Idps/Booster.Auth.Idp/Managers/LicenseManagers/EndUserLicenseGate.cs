using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Database.Models;
using Booster.Licensing.Runtime;

namespace Booster.Auth.Idp.Managers.LicenseManagers;

/// <summary>
/// Son kullanici akislarinin (authorize, token, refresh) lisans kapisi.
/// <para>
/// EN KRITIK KURAL: yalnizca DENEME lisansinin ek suresi bittiginde kapanir. Ucretli lisans hicbir durumda
/// kapatmaz; lisans yoksa, gecersizse ya da durum bilinemiyorsa da acik kalir (bkz. LicenseValidationResult).
/// </para>
/// Yonetim havuzu her zaman aciktir: yonetici panele girip yeni lisansi yapistirabilmeli.
/// </summary>
public sealed class EndUserLicenseGate(ILicenseStatusProvider license)
{
    public async ValueTask<bool> IsAllowedAsync(ClientData client, CancellationToken ct)
    {
        if (client.IsolationId == AuthConstants.ManagementPoolId)
            return true;

        return (await license.GetAsync(ct)).EndUserFlowsAllowed;
    }
}
