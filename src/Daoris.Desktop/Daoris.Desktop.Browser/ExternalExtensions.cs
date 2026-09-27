using Microsoft.Win32;

namespace Daoris.Desktop.Browser;

/// <summary>
/// The extensions other software registered for Chrome on this machine, which the engine reads and
/// offers for the person's approval (`docs/2026-09-28-chromium-embedding-evidence.md` §14). Measured
/// under Google Chrome's key; Chromium's own key is read too, being the same mechanism.
/// </summary>
internal static class ExternalExtensions
{
    private static readonly (RegistryKey Hive, string Path)[] Keys =
    [
        (Registry.LocalMachine, @"SOFTWARE\Google\Chrome\Extensions"),
        (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Google\Chrome\Extensions"),
        (Registry.CurrentUser, @"Software\Google\Chrome\Extensions"),
        (Registry.LocalMachine, @"SOFTWARE\Chromium\Extensions"),
        (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Chromium\Extensions"),
        (Registry.CurrentUser, @"Software\Chromium\Extensions"),
    ];

    /// <summary>Every registered extension id, read only; a key this account cannot open is skipped.</summary>
    public static IReadOnlyList<string> Registered()
    {
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (hive, path) in Keys)
        {
            try
            {
                using var key = hive.OpenSubKey(path);
                if (key is null) continue;
                foreach (var name in key.GetSubKeyNames())
                {
                    if (EngineProfile.IsExtensionId(name)) ids.Add(name);
                }
            }
            catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                // A key this account may not read offers nothing to refuse.
            }
        }

        return [.. ids];
    }
}
