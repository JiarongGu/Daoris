using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What decides the three reads the npm signature gives every <c>package.json</c> folder (<c>node --version</c>, <c>npm --version</c>,
/// <c>npm config list --json</c>), written as text without running them: the node program, the npm launcher and its package, and the
/// npm configuration's files (the tree's own, the user's, the global one's and the one npm ships), by their content. Two readings whose
/// fingerprints agree print what the tool prints for both, so the second need not start npm, which takes seconds each on a loaded
/// machine and is most of an opening's read of its keys. Null for any other command, and for a reading that cannot place every part,
/// which is then run, never answered from memory.
/// </summary>
internal static partial class NpmToolchain
{
    internal const string NodeVersion = "node --version";
    internal const string NpmVersion = "npm --version";
    internal const string NpmConfig = "npm config list --json";

    internal static string? Fingerprint(string command, IReadOnlyDictionary<string, string> environment, string tree)
    {
        var depth = command switch { NodeVersion => 1, NpmVersion => 2, NpmConfig => 3, _ => 0 };
        if (depth == 0 || environment.Keys.Any(name => name.StartsWith("npm_config_", StringComparison.OrdinalIgnoreCase))) return null;
        var node = OnPath("node", environment, tree);
        if (node is null) return null;
        var lines = new List<string> { $"node\t{node}\t{Stamp(node)}" };
        if (depth == 1) return string.Join('\n', lines);

        var launcher = OnPath("npm", environment, tree);
        var package = launcher is null ? null : PackageOf(launcher);
        if (launcher is null || package is null) return null;
        lines.Add($"npm\t{launcher}\t{Stamp(launcher)}");
        lines.Add($"npm-package\t{package}\t{Stamp(package)}");
        if (depth == 2) return string.Join('\n', lines);

        var prefix = Path.GetDirectoryName(node)!;
        if (!OperatingSystem.IsWindows()) prefix = Path.GetDirectoryName(prefix) ?? prefix;
        var profile = Named(environment, "USERPROFILE") ?? Named(environment, "HOME");
        var files = new (string Role, string? File)[]
        {
            ("project", Path.Combine(tree, ".npmrc")),
            ("user", profile is null ? null : Path.Combine(profile, ".npmrc")),
            ("global", Path.Combine(prefix, "etc", "npmrc")),
            ("builtin", Path.Combine(Path.GetDirectoryName(package)!, "npmrc")),
        };
        foreach (var (role, file) in files)
        {
            if (file is not null && Redirects(file)) return null;
            lines.Add($"{role}\t{(file is null ? "none" : Content(file))}");
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// The first file <paramref name="name"/> names as the shell finds it, in the order it tries its extensions; null where a file of that
    /// name stands in the tree (the shell looks there first) or a path entry is relative (it moves with the tree), which no cross-tree reading may stand on.
    /// </summary>
    internal static string? OnPath(string name, IReadOnlyDictionary<string, string> environment, string tree)
    {
        if (Named(environment, "PATH") is not { Length: > 0 } path) return null;
        var extensions = OperatingSystem.IsWindows() ? (Named(environment, "PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries) : [""];
        if (OperatingSystem.IsWindows() && extensions.Append("").Any(extension => File.Exists(Path.Combine(tree, name + extension)))) return null;
        foreach (var folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var entry = folder.Trim('"');
            if (!Path.IsPathRooted(entry)) return null;
            foreach (var extension in extensions)
            {
                var file = Path.Combine(entry, name + extension);
                if (File.Exists(file)) return file;
            }
        }

        return null;
    }

    /// <summary>Whether a configuration file names another place npm reads its configuration from, which this fingerprint does not follow.</summary>
    private static bool Redirects(string file)
    {
        try
        {
            return File.Exists(file) && Redirecting().IsMatch(File.ReadAllText(file));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    [GeneratedRegex(@"^\s*(userconfig|globalconfig|prefix)\s*=", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex Redirecting();

    /// <summary>The npm package's manifest beside a launcher (a link followed), where the global folders of node's installs keep it.</summary>
    private static string? PackageOf(string launcher)
    {
        var real = File.ResolveLinkTarget(launcher, returnFinalTarget: true)?.FullName ?? launcher;
        var folder = Path.GetDirectoryName(real);
        if (folder is null) return null;
        foreach (var candidate in new[] { Path.Combine(folder, "node_modules", "npm", "package.json"), Path.Combine(folder, "..", "lib", "node_modules", "npm", "package.json") })
        {
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }

        return null;
    }

    private static string? Named(IReadOnlyDictionary<string, string> environment, string name) =>
        environment.FirstOrDefault(each => string.Equals(each.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>A program's size and write time, which a new version or install moves.</summary>
    private static string Stamp(string file)
    {
        var info = new FileInfo(file);
        return $"{info.Length}\t{info.LastWriteTimeUtc.Ticks}";
    }

    /// <summary>A configuration file's content as a digest, or <c>absent</c>: npm reads it whole, so a changed byte is a changed reading.</summary>
    private static string Content(string file)
    {
        try
        {
            return File.Exists(file) ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file))) : "absent";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return $"unread {Guid.NewGuid():N}";
        }
    }
}
