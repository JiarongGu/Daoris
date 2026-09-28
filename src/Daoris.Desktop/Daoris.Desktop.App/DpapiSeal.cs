using System.Security.Cryptography;
using System.Text;

namespace Daoris.Desktop;

/// <summary>
/// The kept sign-in's seal (BRW10, BRW13): DPAPI, to the Windows account this runs as, so the file copied to
/// another account or machine opens nothing.
/// </summary>
internal sealed class DpapiSeal : ICookieSeal
{
    /// <summary>Named, so bytes this account sealed for some other purpose do not open here.</summary>
    private static readonly byte[] Purpose = Encoding.UTF8.GetBytes("daoris/browser/session-cookies/1");

    public byte[] Seal(byte[] plain) => ProtectedData.Protect(plain, Purpose, DataProtectionScope.CurrentUser);

    public byte[] Open(byte[] sealedBytes) => ProtectedData.Unprotect(sealedBytes, Purpose, DataProtectionScope.CurrentUser);
}
