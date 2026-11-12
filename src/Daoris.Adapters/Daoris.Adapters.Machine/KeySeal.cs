using System.Security.Cryptography;
using System.Text;
using Daoris.Ports.Processes;

namespace Daoris.Adapters.Machine;

/// <summary>
/// An API key as <c>keys.json</c> holds it: encrypted with DPAPI for the Windows user this runs as, so the file copied to another
/// user or machine opens nothing.
/// </summary>
/// <remarks>A value without <see cref="Prefix"/> is a key kept before the seal existed; it is read as written and sealed by the next
/// write of the file (<see cref="Sealed"/>), which is the whole of its life.</remarks>
public static class KeySeal
{
    /// <summary>What marks a value as sealed, with the seal's version.</summary>
    public const string Prefix = "dpapi1:";

    public static SecretSeal Port { get; } = new(Sealed, Open);

    /// <summary>Named, so bytes this user sealed for some other purpose do not open here.</summary>
    private static readonly byte[] Purpose = Encoding.UTF8.GetBytes("daoris/agent/api-keys/1");

    /// <summary>The value to keep for <paramref name="key"/>; a value already sealed is returned as it is; null off Windows, where no user's seal exists.</summary>
    public static string? Sealed(string key)
    {
        if (key.StartsWith(Prefix, StringComparison.Ordinal)) return key;
        if (!OperatingSystem.IsWindows()) return null;

        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), Purpose, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(bytes);
    }

    /// <summary>The key a kept value is, or null where this user cannot open it (another user's, or damaged).</summary>
    public static string? Open(string kept)
    {
        if (!kept.StartsWith(Prefix, StringComparison.Ordinal)) return kept;
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(kept[Prefix.Length..]), Purpose, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception error) when (error is CryptographicException or FormatException)
        {
            return null;
        }
    }
}
