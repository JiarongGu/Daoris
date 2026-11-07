namespace Daoris.Driver;

/// <summary>
/// git's common folder as an install may write it (this repository's <c>prepare</c> writes its hooks): its hooks and its local
/// configuration listed by name, size and write time, read before and after each install, what changed said.
/// </summary>
internal static class GitCommonFolder
{
    internal static async Task<IReadOnlyDictionary<string, string>> ListAsync(string tree, CancellationToken ct)
    {
        var (code, said, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
        var listed = new Dictionary<string, string>(StringComparer.Ordinal);
        if (code != 0 || said.Trim() is not { Length: > 0 } common || !Directory.Exists(common)) return listed;
        var config = new FileInfo(Path.Combine(common, "config"));
        if (config.Exists) listed["config"] = Stamp(config);
        var hooks = new DirectoryInfo(Path.Combine(common, "hooks"));
        if (hooks.Exists)
        {
            foreach (var hook in hooks.EnumerateFiles()) listed["hooks/" + hook.Name] = Stamp(hook);
        }

        return listed;
    }

    internal static IReadOnlyList<string> Changed(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after) =>
        [.. after.Where(each => !before.TryGetValue(each.Key, out var was) || was != each.Value).Select(each => each.Key)
            .Concat(before.Keys.Where(name => !after.ContainsKey(name)))
            .Order(StringComparer.Ordinal)];

    private static string Stamp(FileInfo file) => $"{file.Length}:{file.LastWriteTimeUtc.Ticks}";
}
