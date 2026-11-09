using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A dependency rule's <c>setup</c> (the tree-pool design §35.6): the repository's own commands that its install leaves out, run in an
/// unworked tree after the installs. <c>per: repository</c> runs once for each key, kept in the home and not the tree: the commands,
/// the manifest's script text and the blob of each tracked file a command names. <c>per: tree</c> runs in each tree. A run holds the
/// repository's install lock, as an install does, since it may write git's common folder.
/// </summary>
internal static class RepositorySetup
{
    /// <summary>Runs each declared setup due in <paramref name="tree"/>, saying a failure; false where a terminal's visit ended one.</summary>
    internal static async Task<bool> RunAsync(
        string home, string repository, string tree, string head, IReadOnlyList<StoreDependency> dependencies, List<Noted> said, CancellationToken ct, CancellationToken visited)
    {
        foreach (var dependency in dependencies.Where(each => each.Rule.Setup is { Length: > 0 }))
        {
            var commands = dependency.Rule.Setup!;
            var perRepository = dependency.Rule.Per == "repository";
            var key = perRepository ? await KeyAsync(tree, head, dependency, commands, ct).ConfigureAwait(false) : "";
            if (perRepository && Done(home, repository, dependency.Path) == key) continue;
            using var held = await Task.Run(() => RecordLock.Take(RecordLock.PathOf(home, "deps-install-" + InstallAdmission.Short(repository)), DependencyCommands.InstallBound), ct).ConfigureAwait(false);
            if (perRepository && Done(home, repository, dependency.Path) == key) continue;
            var environment = InstallEnvironment.Of(dependency.Rule);
            var failed = false;
            foreach (var command in commands)
            {
                var ran = await DependencyCommands.RunAsync(home, tree, command, Path.Combine(tree, dependency.Runs), environment, DependencyCommands.InstallBound, ct, visited).ConfigureAwait(false);
                if (ran.Code == DependencyCommands.Visited) return false;
                if (ran.Code == 0) continue;
                said.Add(Noted.Of(StartNoteCodes.StartedSetupFailed, $"`{dependency.Folder}`'s setup `{command}` failed: {ran.Tail}.", ("path", dependency.Folder), ("command", command), ("why", ran.Tail)));
                failed = true;
                break;
            }

            if (perRepository && !failed) Record(home, repository, dependency.Path, key);
        }

        return true;
    }

    private static async Task<string> KeyAsync(string tree, string head, StoreDependency dependency, IReadOnlyList<string> commands, CancellationToken ct)
    {
        var lines = new List<string> { $"path\t{dependency.Path}" };
        lines.AddRange(commands.Select(command => $"command\t{command}"));
        var manifest = dependency.Runs.Length == 0 ? "package.json" : dependency.Runs + "/package.json";
        var text = await GitObjectReads.TextAsync(tree, GitObjectName.At(head, manifest), ct).ConfigureAwait(false);
        lines.Add($"scripts\t{(text is not null ? ScriptsOf(text) : "")}");
        foreach (var named in commands.SelectMany(command => command.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Select(word => word.Trim('"', '\'')).Distinct(StringComparer.Ordinal))
        {
            var path = dependency.Runs.Length == 0 ? named : dependency.Runs + "/" + named;
            var (code, blob, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--verify", "-q", $"{head}:{path}"], ct).ConfigureAwait(false);
            if (code == 0 && blob.Trim().Length > 0) lines.Add($"file\t{path}\t{blob.Trim()}");
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines))));
    }

    private static string ScriptsOf(string manifest)
    {
        try
        {
            using var document = JsonDocument.Parse(manifest);
            return document.RootElement.TryGetProperty("scripts", out var scripts) ? scripts.GetRawText() : "";
        }
        catch (JsonException)
        {
            return "";
        }
    }

    private static string RecordOf(string home, string repository) => DaorisHome.Entry(home, HomeEntry.TreeFiles, $"setup.{InstallAdmission.Short(repository)}.json");

    private static string? Done(string home, string repository, string path)
    {
        try
        {
            var file = RecordOf(home, repository);
            if (!File.Exists(file)) return null;
            using var document = JsonDocument.Parse(AtomicFile.ReadText(file));
            return document.RootElement.TryGetProperty(path, out var key) ? key.GetString() : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static void Record(string home, string repository, string path, string key)
    {
        var file = RecordOf(home, repository);
        JsonObject state;
        try
        {
            state = File.Exists(file) ? JsonNode.Parse(AtomicFile.ReadText(file)) as JsonObject ?? [] : [];
        }
        catch (Exception error) when (error is IOException or JsonException)
        {
            state = [];
        }

        state[path] = key;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        AtomicFile.WriteText(file, state.ToJsonString());
    }
}
