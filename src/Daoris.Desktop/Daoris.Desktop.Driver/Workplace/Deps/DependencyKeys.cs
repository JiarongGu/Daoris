using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A dependency's key at a commit, kept as its hash alone: its declared path; each <c>key</c> path's blob id at the commit, a path absent
/// counted as absent (with an empty <c>key</c>, the commit's tree id); the <c>install</c> command; each <c>toolchain</c> command's output,
/// run at the tree's root in the install's environment with the tree's path written as a token first; that environment, each value as
/// its digest under the home's secret; the system and the architecture; the repository; and the key of each dependency before it in
/// rule order.
/// </summary>
internal static class DependencyKeys
{
    internal const string TreeToken = "<tree>";

    internal static async Task<string> KeyAsync(
        string home, string tree, string commit, string repository, StoreDependency dependency, IReadOnlyList<string> before, CancellationToken ct)
    {
        var rule = dependency.Rule;
        var environment = InstallEnvironment.Of(rule);
        var lines = new List<string> { $"path\t{dependency.Path}" };
        lines.AddRange(await KeyedAsync(tree, commit, rule.Key ?? [], ct).ConfigureAwait(false));
        lines.Add($"install\t{rule.Install}");
        foreach (var command in rule.Toolchain ?? [])
        {
            var read = await DependencyCommands.RunAsync(home, tree, command, tree, environment, DependencyCommands.ReadBound, ct).ConfigureAwait(false);
            lines.Add($"toolchain\t{command}\t{read.Code}\t{Hash(Tokened(read.Output, tree))}");
        }

        lines.AddRange(InstallEnvironment.Digests(environment, InstallEnvironment.Secret(home)).Select(each => $"env\t{each}"));
        lines.Add($"system\t{System()}\t{RuntimeInformation.OSArchitecture}");
        lines.Add($"repository\t{repository}");
        lines.AddRange(before.Select(each => $"before\t{each}"));
        return Hash(string.Join("\n", lines));
    }

    /// <summary>Each key path's blob id at the commit, absent as absent, in one read of git's objects; an empty key keys the commit's tree.</summary>
    internal static async Task<IReadOnlyList<string>> KeyedAsync(string tree, string commit, IReadOnlyList<string> key, CancellationToken ct)
    {
        if (key.Count == 0)
        {
            var (code, id, err) = await WorkingTree.GitAsync(tree, ["rev-parse", "--verify", $"{commit}^{{tree}}"], ct).ConfigureAwait(false);
            if (code != 0) throw new DriverException($"git could not read the commit's tree: {err.Trim()}");
            return [$"tree\t{id.Trim()}"];
        }

        // ls-tree takes no pathspec magic, so the commit's paths are listed once and each key path or glob matched here.
        var (listed, said, error) = await WorkingTree.GitAsync(tree, ["ls-tree", "-r", "-z", "--full-tree", commit], ct).ConfigureAwait(false);
        if (listed != 0) throw new DriverException($"git could not list the files that key it: {error.Trim()}");
        var globs = key.Where(Globbed).Select(RoomPaths.Declared).ToList();
        var literal = key.Where(path => !Globbed(path)).ToHashSet(StringComparer.Ordinal);
        var blobs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in said.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = entry.IndexOf('\t');
            var path = entry[(tab + 1)..];
            if (literal.Contains(path) || globs.Any(glob => glob.IsMatch(path))) blobs[path] = entry[..tab].Split(' ')[2];
        }

        var lines = blobs.Select(each => $"key\t{each.Key}\t{each.Value}").ToList();
        lines.AddRange(literal.Where(path => !blobs.ContainsKey(path)).Order(StringComparer.Ordinal).Select(path => $"key\t{path}\tabsent"));
        return lines;
    }

    /// <summary>Whether a tracked file that keys <paramref name="dependency"/> has a working change in the tree.</summary>
    internal static async Task<bool> InputChangedAsync(string tree, StoreDependency dependency, CancellationToken ct)
    {
        if (dependency.Rule.Key is not { Length: > 0 } key) return false;
        var (code, said, _) = await WorkingTree.GitAsync(
            tree, ["--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=no", "--", .. key.Select(path => $":(glob){path}")], ct).ConfigureAwait(false);
        return code != 0 || said.Length > 0;
    }

    /// <summary>The tree's path in every spelling a tool writes it, each replaced by one token.</summary>
    internal static string Tokened(string text, string tree)
    {
        foreach (var spelled in TreeSpellings(tree)) text = text.Replace(spelled, TreeToken, StringComparison.OrdinalIgnoreCase);
        return text;
    }

    /// <summary>The tree's full path with either slash, and as JSON escapes a backslash.</summary>
    internal static IReadOnlyList<string> TreeSpellings(string tree)
    {
        var full = Path.GetFullPath(tree).TrimEnd('\\', '/');
        var back = full.Replace('/', '\\');
        var forward = full.Replace('\\', '/');
        return [.. new[] { back.Replace("\\", "\\\\"), back, forward }.Distinct(StringComparer.Ordinal)];
    }

    private static bool Globbed(string path) => path.IndexOfAny(['*', '?', '[', '{']) >= 0;

    private static string System() =>
        OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : OperatingSystem.IsLinux() ? "linux" : RuntimeInformation.OSDescription;

    internal static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
