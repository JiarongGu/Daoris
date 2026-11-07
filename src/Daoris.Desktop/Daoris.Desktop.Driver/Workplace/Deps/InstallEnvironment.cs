using System.Collections;
using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A dependency's install environment, composed by selection: the system's own names (the path, the system's, the user's and the
/// programs' folders, the processor) and the names its rule's <c>env</c> lists, nothing else inherited. A gate's environment, composed
/// by removal, would pass every variable but Daoris's and git's into what the store shares.
/// </summary>
internal static class InstallEnvironment
{
    /// <summary>The system's own names, each passed where this process has it.</summary>
    internal static readonly IReadOnlyList<string> SystemNames =
    [
        "PATH", "PATHEXT", "SystemRoot", "SystemDrive", "windir", "ComSpec", "OS",
        "USERPROFILE", "HOMEDRIVE", "HOMEPATH", "HOME", "USER", "USERNAME", "APPDATA", "LOCALAPPDATA", "TEMP", "TMP", "TMPDIR",
        "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432", "ProgramData", "CommonProgramFiles", "CommonProgramFiles(x86)", "CommonProgramW6432",
        "PROCESSOR_ARCHITECTURE", "PROCESSOR_IDENTIFIER", "NUMBER_OF_PROCESSORS",
    ];

    /// <summary>The environment <paramref name="rule"/>'s install and its <c>toolchain</c> reads run in.</summary>
    internal static IReadOnlyDictionary<string, string> Of(FileRule rule)
    {
        var mine = Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
            .ToDictionary(each => (string)each.Key, each => (string?)each.Value ?? "", OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var chosen = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in SystemNames.Concat(rule.Env ?? []))
        {
            if (mine.TryGetValue(name, out var value)) chosen[name] = value;
        }

        return chosen;
    }

    /// <summary>Each name and its value's digest under the home's secret, never the value: what the key and a record may hold.</summary>
    internal static IEnumerable<string> Digests(IReadOnlyDictionary<string, string> environment, byte[] secret) =>
        environment.OrderBy(each => each.Key, StringComparer.OrdinalIgnoreCase)
            .Select(each => $"{each.Key.ToUpperInvariant()}={Convert.ToHexStringLower(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(each.Value)))}");

    /// <summary>
    /// The home's secret the environment's values are digested under: made once, never synced or shown, so a digest in a key tells a
    /// reader of the store nothing of a value.
    /// </summary>
    internal static byte[] Secret(string home)
    {
        var path = DaorisHome.Entry(home, HomeEntry.DepsSecret);
        for (var tries = 0; tries < 3; tries++)
        {
            if (File.Exists(path) && Convert.FromHexString(AtomicFile.ReadText(path).Trim()) is { Length: 32 } held) return held;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.TryCreateText(path, Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)));
        }

        throw new IOException("the home's secret for the dependency store could not be made or read");
    }
}
