using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The browser's native-messaging manifests for the Claude extension, found where the browsers keep them: one per browser and machine,
/// outside every account folder, whose <c>path</c> names the wrapper (<c>chrome/chrome-native-host.bat</c>) an account's folder holds,
/// because the harness writes it there when started with its browser switch under that account's home.
/// </summary>
internal static class NativeHostManifests
{
    /// <summary>The name the extension's host is registered under.</summary>
    public const string Host = "com.anthropic.claude_code_browser_extension";

    /// <summary>The browsers' per-user places, each a registry key under <c>HKCU\Software</c> on Windows, a folder elsewhere.</summary>
    private static readonly string[] Browsers = ["Google/Chrome", "Chromium", "Microsoft/Edge", "BraveSoftware/Brave-Browser"];

    /// <summary>The manifests this user's browsers hold for the host that are files, each once; none where no browser has registered it.</summary>
    public static IReadOnlyList<string> Located()
    {
        var found = new List<string>();
        try
        {
            if (OperatingSystem.IsWindows())
            {
                foreach (var browser in Browsers)
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"Software\{browser.Replace('/', '\\')}\NativeMessagingHosts\{Host}");
                    if (key?.GetValue(null) is string { Length: > 0 } path) found.Add(path);
                }
            }
            else
            {
                var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var roots = OperatingSystem.IsMacOS()
                    ? Browsers.Select(browser => Path.Combine(user, "Library", "Application Support", browser == "Microsoft/Edge" ? "Microsoft Edge" : browser))
                    : Browsers.Select(browser => Path.Combine(user, ".config", browser switch
                    {
                        "Google/Chrome" => "google-chrome",
                        "Microsoft/Edge" => "microsoft-edge",
                        _ => browser,
                    }));
                found.AddRange(roots.Select(root => Path.Combine(root, "NativeMessagingHosts", Host + ".json")));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // A browser's registration that cannot be read is not found: the move never stops for it.
        }

        return [.. found.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase)];
    }
}

/// <summary>
/// A native-messaging manifest whose <c>path</c> names a moving account's folder: the path follows the folder to its new id, every other
/// key kept, so the browser's host starts from the folder that is there. A manifest naming no moving folder, or one that does not read,
/// is left as it is: it is the browser's, and the move never stops for it.
/// </summary>
internal sealed class AccountIdNativeHostStore : IAccountIdStore
{
    public IEnumerable<AccountIdReference> Moved(AccountIdHome at, IReadOnlyList<AccountIdRename> renames, bool write)
    {
        var found = new List<AccountIdReference>();
        foreach (var manifest in at.NativeHosts.Where(File.Exists))
        {
            if (Read(manifest) is not { } root || root["path"] is not JsonValue value || !value.TryGetValue<string>(out var text)) continue;
            foreach (var rename in renames)
            {
                var old = Full(HarnessSettings.ProfileHome(at.Home, rename.Agent, rename.From));
                var moved = Full(HarnessSettings.ProfileHome(at.Home, rename.Agent, rename.To));
                if (Under(text, old) is not { } within) continue;

                var path = Path.Combine(moved, within);
                found.Add(new AccountIdReference(rename, at.Place(manifest), "path"));
                if (write) Write(manifest, root, path);
                break;
            }
        }

        return found;
    }

    /// <summary>The manifest's object, or null where it is not one that reads.</summary>
    private static JsonObject? Read(string manifest)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(manifest)) as JsonObject;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void Write(string manifest, JsonObject root, string path)
    {
        root["path"] = path;
        try
        {
            JsonFile.Write(manifest, root);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new DriverException($"{manifest} did not write ({error.Message.TrimEnd('.')}): the account's folder moved, so set its path to {path}.");
        }
    }

    /// <summary>What <paramref name="text"/> names within <paramref name="folder"/>, relative to it; null where it names none of it.</summary>
    private static string? Under(string text, string folder)
    {
        string full;
        try
        {
            full = Full(text);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return full.StartsWith(folder + Path.DirectorySeparatorChar, comparison) ? full[(folder.Length + 1)..] : null;
    }

    private static string Full(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
