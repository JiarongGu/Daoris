using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>
/// What decides the three reads the npm signature gives every <c>package.json</c> folder (<c>node --version</c>, <c>npm --version</c>,
/// <c>npm config list --json</c>), written as text without running them: the node program, the npm launcher and its package, and the
/// npm configuration's files (the tree's own, the user's, the global one's and the one npm ships), by their content. Two readings whose
/// fingerprints agree print what the tool prints for both, so the second need not start npm, which takes seconds each on a loaded
/// machine and is most of an opening's read of its keys. Null for any other command, and for a reading that cannot place every part,
/// which is then run, never answered from memory.
/// </summary>
internal static class NpmToolchain
{
    internal const string NodeVersion = "node --version";
    internal const string NpmVersion = "npm --version";
    internal const string NpmConfig = "npm config list --json";

    internal static string? Fingerprint(string command, IReadOnlyDictionary<string, string> environment, string tree)
    {
        var depth = command switch { NodeVersion => 1, NpmVersion => 2, NpmConfig => 3, _ => 0 };
        if (depth == 0) return null;
        var node = OnPath("node", environment);
        if (node is null) return null;
        var lines = new List<string> { $"node\t{node}\t{Stamp(node)}" };
        if (depth == 1) return string.Join('\n', lines);

        var launcher = OnPath("npm", environment);
        var package = launcher is null ? null : PackageOf(launcher);
        if (launcher is null || package is null) return null;
        lines.Add($"npm\t{launcher}\t{Stamp(launcher)}");
        lines.Add($"npm-package\t{package}\t{Stamp(package)}");
        if (depth == 2) return string.Join('\n', lines);

        var prefix = Path.GetDirectoryName(node)!;
        if (!OperatingSystem.IsWindows()) prefix = Path.GetDirectoryName(prefix) ?? prefix;
        var home = Named(environment, "USERPROFILE") ?? Named(environment, "HOME");
        foreach (var (role, file) in new (string, string?)[]
        {
            ("project", Path.Combine(tree, ".npmrc")),
            ("user", home is null ? null : Path.Combine(home, ".npmrc")),
            ("global", Path.Combine(prefix, "etc", "npmrc")),
            ("builtin", Path.Combine(Path.GetDirectoryName(package)!, "npmrc")),
        })
        {
            lines.Add($"{role}\t{(file is null ? "none" : Content(file))}");
        }

        return string.Join('\n', lines);
    }

    /// <summary>The first file <paramref name="name"/> names along the environment's path, in the order a shell tries its extensions.</summary>
    internal static string? OnPath(string name, IReadOnlyDictionary<string, string> environment)
    {
        if (Named(environment, "PATH") is not { Length: > 0 } path) return null;
        var extensions = OperatingSystem.IsWindows() ? (Named(environment, "PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries) : [""];
        foreach (var folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                var file = Path.Combine(folder.Trim('"'), name + extension);
                if (File.Exists(file)) return file;
            }
        }

        return null;
    }

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
