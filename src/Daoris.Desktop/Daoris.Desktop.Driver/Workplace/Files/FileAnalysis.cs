using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// One reading: the found layer it writes, the ignored tops it read them over, the floor's facts, and the signature rule that fills
/// a field another layer's rule leaves out.
/// </summary>
internal sealed record FileReading(FoundReading Found, IReadOnlyList<string> Tops, FileFloorFacts Floor, Func<string, FileKind, FileRule?> Signature);

/// <summary>
/// The analysis that writes the found layer (the tree-pool design §28.4 and §29.4), generic and cheap: no model, no build, no install,
/// nothing in the tree written. Its sources, weighed in order: git's view (what is tracked, what is ignored at its top, the linked
/// worktrees) and every top that is or holds another repository; the local signatures and the gates' local inputs; the toolchains'
/// signatures; the repository's tracked scripts, read at the commit from git's objects; what sessions were seen to write; and
/// <c>unknown</c> where nothing names a top. Of what git does not manage it reads names, sizes and stats only.
/// </summary>
internal static partial class FileAnalysis
{
    private static readonly string[] Lifecycle = ["preinstall", "install", "postinstall", "prepare"];

    internal static async Task<FileReading> ReadAsync(
        string root, string commit, FileSignatures signatures, IReadOnlyList<string> observed, CancellationToken ct, DateTimeOffset? at = null)
    {
        var tracked = await FileGit.TrackedAsync(root, commit, ct).ConfigureAwait(false);
        var known = new HashSet<string>(tracked, StringComparer.Ordinal);
        var tops = await FileGit.IgnoredTopsAsync(root, ct).ConfigureAwait(false);
        var worktrees = (await FileGit.WorktreesAsync(root, ct).ConfigureAwait(false)).Select(path => Inside(root, path)).OfType<string>().ToList();
        var walks = tops.ToDictionary(top => top, top => FileWalk.Of(Path.Combine(root, top)), StringComparer.Ordinal);
        var gates = await FileGit.ShowAsync(root, commit, GeneratedWriters.DeclarationFile, ct).ConfigureAwait(false);
        var found = new Dictionary<string, FileRule>(StringComparer.Ordinal);

        foreach (var top in tops)
        {
            var walk = walks[top];
            var worktree = worktrees.Count(path => path.StartsWith(top, StringComparison.Ordinal));
            if (!walk.Holds && worktree == 0) continue;
            found[top] = new FileRule(FileKind.Repository, Top(top))
            {
                Evidence = [walk.Repository ? "a repository at its top" : worktree > 0 ? $"holds {Count(worktree, "linked worktree")}" : $"holds {Count(walk.Nested, "repository", "repositories")}"],
            };
        }

        foreach (var top in tops.Where(top => !found.ContainsKey(top)))
        {
            if (signatures.LocalOf(top) is { } pattern) found[top] = new FileRule(FileKind.Local, Top(top)) { Evidence = [$"a local signature, `{pattern}`"], Size = walks[top].Size };
        }

        foreach (var input in LocalInputs(gates).Where(input => !known.Contains(input) && !FileFloor.IsSettings(input)))
        {
            found[input] = new FileRule(FileKind.Local, Top(input)) { Reach = "copy", Evidence = ["read by a gate: the gates file names it"] };
        }

        var hits = signatures.Over(tracked);
        var lifecycleText = await Signatures(root, commit, hits, tops, walks, found, known, ct).ConfigureAwait(false);
        await Scripts(root, commit, signatures, tops, found, gates, lifecycleText, ct).ConfigureAwait(false);

        foreach (var top in tops.Where(top => !found.ContainsKey(top)))
        {
            found[top] = observed.Contains(top, StringComparer.Ordinal)
                ? new FileRule(FileKind.Scratch, Top(top)) { Evidence = ["written by a session"], Size = walks[top].Size }
                : new FileRule(FileKind.Unknown, Top(top)) { Size = walks[top].Size };
        }

        var rules = found.Values.OrderBy(rule => rule.Kind == FileKind.Dependency ? rule.Key is { Count: 0 } ? 1 : 0 : 2)
            .ThenBy(rule => rule.Paths[0], StringComparer.Ordinal).ToList();
        var ignoreCase = await FileGit.IgnoresCaseAsync(root, ct).ConfigureAwait(false);
        FileRule? Signature(string path, FileKind kind) => hits.LastOrDefault(hit => hit.Rule.Kind == kind && hit.Rule.Matches(path, ignoreCase))?.Rule;
        return new FileReading(
            new FoundReading(commit, at ?? DateTimeOffset.UtcNow, rules), tops,
            new FileFloorFacts(known, [.. found.Where(pair => pair.Value.Kind == FileKind.Repository).Select(pair => pair.Key)]) { IgnoreCase = ignoreCase }, Signature);
    }

    private static IReadOnlyList<string> Top(string top) => [GitPattern.Anchored(top)];

    /// <summary>
    /// The signatures' rules over the tops: a dependency beside its signature whether or not it stands yet, with its workspace
    /// members' folders and, where its install runs a lifecycle script, keyed by the commit's tree; every other kind for each top its
    /// folder holds. A rule reaching a tracked path is git's and written for no top. Answers the root manifest's lifecycle scripts.
    /// </summary>
    private static async Task<string> Signatures(
        string root, string commit, IReadOnlyList<SignatureHit> hits, IReadOnlyList<string> tops, Dictionary<string, FileWalk> walks,
        Dictionary<string, FileRule> found, HashSet<string> tracked, CancellationToken ct)
    {
        var lifecycle = "";
        foreach (var hit in hits)
        {
            if (hit.Rule.Kind != FileKind.Dependency)
            {
                foreach (var path in hit.Rule.Paths.Where(path => !path.StartsWith('!')))
                {
                    foreach (var top in tops.Where(top => !found.ContainsKey(top) && GitPattern.Covers([path, .. hit.Rule.Paths.Where(each => each.StartsWith('!'))], top, false)))
                    {
                        found[top] = hit.Rule with
                        {
                            Paths = Top(top), Evidence = [$"beside {hit.File}, by the {hit.Row.Toolchain} signature"], Size = walks[top].Size,
                            Take = hit.Rule.Kind == FileKind.Output && hit.Rule.Builds is not null ? "base" : hit.Rule.Take,
                        };
                    }
                }

                continue;
            }

            if (GitPattern.LiteralOf(hit.Rule.Paths[0]) is not { } folder || found.ContainsKey(folder) || FileFloor.TrackedIn(folder, tracked) is not null) continue;
            var rule = hit.Rule with { Evidence = [$"beside {hit.File}, by the {hit.Row.Toolchain} signature"], Size = walks.GetValueOrDefault(folder)?.Size };
            if (hit.Row.Manifest is { } manifestName && await FileGit.ShowAsync(root, commit, hit.Folder + manifestName, ct).ConfigureAwait(false) is { } manifest)
            {
                var (members, scripts) = Manifest(manifest);
                if (hit.Row.Workspaces) rule = WithMembers(rule, hit.Folder, members);
                if (hit.Folder.Length == 0) lifecycle = string.Join('\n', scripts.Values);
                if (scripts.Keys.FirstOrDefault() is { } script)
                {
                    rule = rule with { Key = [], Evidence = [.. rule.Evidence, $"its install runs `{script}`, so the commit's tree keys it"] };
                }
            }

            if (hit.Row.WorkspaceFile is { } workspaceFile && await FileGit.ShowAsync(root, commit, hit.Folder + workspaceFile, ct).ConfigureAwait(false) is { } workspace)
            {
                rule = WithMembers(rule, hit.Folder, WorkspaceMembers.OfPnpm(workspace));
            }

            if (rule.Key is not { Count: 0 } && await FileGit.ShowAsync(root, commit, hit.File, ct).ConfigureAwait(false) is { } lockfile
                && InstallScript(lockfile, hit.Row.ReadsNothing) is { } package)
            {
                rule = rule with { Key = [], Evidence = [.. rule.Evidence, $"{hit.File} marks `{package}`'s own install script, so the commit's tree keys it"] };
            }

            found[folder] = rule;
        }

        return lifecycle;
    }

    /// <summary>
    /// The repository's tracked scripts, read at the commit in one search: a script running an intermediate without its tool's check
    /// makes it an output, the other kind kept as its evidence, and one running a base sets it aside; a folder no source reached is an output where a step a gate, a hook or a
    /// lifecycle script runs writes it, scratch where another script writes it, and stays for the later sources where one only names it.
    /// </summary>
    private static async Task Scripts(
        string root, string commit, FileSignatures signatures, IReadOnlyList<string> tops, Dictionary<string, FileRule> found, string? gates,
        string lifecycle, CancellationToken ct)
    {
        var asked = tops.Where(top => !found.TryGetValue(top, out var rule) || rule.Kind == FileKind.Intermediate || rule.Take == "base").ToList();
        var names = asked.ToDictionary(top => top, top => top.TrimEnd('/'), StringComparer.Ordinal);
        var lines = await FileGit.LinesAsync(root, commit, [.. names.Values.Distinct(StringComparer.Ordinal)], signatures.Scripts, ct).ConfigureAwait(false);
        foreach (var top in asked)
        {
            var naming = Named(names[top]);
            var said = lines.Where(line => naming.IsMatch(line.Line)).ToList();
            if (said.Count == 0) continue;
            if (found.TryGetValue(top, out var known))
            {
                if (said.FirstOrDefault(line => Runs(names[top]).IsMatch(line.Line)) is { File: { } runner })
                {
                    found[top] = known.Kind == FileKind.Intermediate
                        ? known with
                        {
                            Kind = FileKind.Output, Toolchain = null, Other = FileKind.Intermediate,
                            Evidence = [.. known.Evidence, $"{runner} runs it without building"],
                        }
                        : known with { Take = "aside", Evidence = [.. known.Evidence, $"{runner} runs it without building, so it is set aside, never kept as a base"] };
                }

                continue;
            }

            var writers = said.Where(line => Writes().IsMatch(line.Line)).Select(line => line.File).Distinct(StringComparer.Ordinal).ToList();
            if (writers.FirstOrDefault(file => Stepped(file, gates, lifecycle)) is { } step)
            {
                found[top] = new FileRule(FileKind.Output, Top(top)) { Evidence = [$"written by {step}, a step a gate, a hook or a lifecycle script runs"] };
            }
            else if (writers.Count > 0)
            {
                found[top] = new FileRule(FileKind.Scratch, Top(top)) { Evidence = [$"written by {writers[0]}, which no gate, hook or lifecycle script runs"] };
            }
            else
            {
                found[top] = new FileRule(FileKind.Unknown, Top(top)) { Evidence = [$"named by {said[0].File}, which only reads it"] };
            }
        }
    }

    private static bool Stepped(string file, string? gates, string lifecycle) =>
        (gates?.Contains(file, StringComparison.Ordinal) ?? false) || lifecycle.Contains(file, StringComparison.Ordinal)
        || file.Contains("hook", StringComparison.OrdinalIgnoreCase) || file.StartsWith(".husky/", StringComparison.Ordinal);

    /// <summary>A manifest's workspace members and its lifecycle scripts, those an install runs, in the order an install runs them.</summary>
    private static (IReadOnlyList<string> Members, IReadOnlyDictionary<string, string> Scripts) Manifest(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return ([], new Dictionary<string, string>());
            var members = WorkspaceMembers.OfManifest(root);
            var scripts = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("scripts", out var named) && named.ValueKind == JsonValueKind.Object)
            {
                foreach (var stage in Lifecycle) if (Json.Text(named, stage) is { Length: > 0 } command) scripts[stage] = command;
            }

            return (members, scripts);
        }
        catch (JsonException)
        {
            return ([], new Dictionary<string, string>());
        }
    }

    private static FileRule WithMembers(FileRule rule, string folder, IReadOnlyList<string> members) =>
        members.Count == 0 ? rule : rule with { Paths = [.. rule.Paths.Concat(WorkspaceMembers.Modules(folder, members)).Distinct(StringComparer.Ordinal)] };

    /// <summary>The first package a lockfile marks as running its own install script, but those its signature lists as reading nothing.</summary>
    private static string? InstallScript(string lockfile, IReadOnlyList<string> readsNothing)
    {
        try
        {
            using var document = JsonDocument.Parse(lockfile);
            if (!document.RootElement.TryGetProperty("packages", out var packages) || packages.ValueKind != JsonValueKind.Object) return null;
            return packages.EnumerateObject().Where(each => Json.Flag(each.Value, "hasInstallScript"))
                .Select(each => each.Name[(each.Name.LastIndexOf("node_modules/", StringComparison.Ordinal) is var at and >= 0 ? at + "node_modules/".Length : 0)..])
                .FirstOrDefault(package => !readsNothing.Contains(package, StringComparer.Ordinal));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The gates file's local inputs at the commit: <c>localInputs</c> and the sensitive patterns file it names.</summary>
    private static List<string> LocalInputs(string? gates)
    {
        if (gates is null) return [];
        try
        {
            using var document = JsonDocument.Parse(gates.TrimStart('﻿'));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return [];
            var inputs = Json.Strings(root, "localInputs").Where(input => RoomPaths.Unfit(input) is null).ToList();
            if (root.TryGetProperty("sensitive", out var sensitive) && sensitive.ValueKind == JsonValueKind.Object && Json.Text(sensitive, "patternsFile") is { Length: > 0 } named)
            {
                inputs.Add(named);
            }

            return [.. inputs.Distinct(StringComparer.Ordinal)];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>A path git printed, as one inside <paramref name="root"/> with a trailing <c>/</c>, or null for the root or one outside it.</summary>
    private static string? Inside(string root, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path)).Replace('\\', '/');
        return relative == "." || relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? null : relative.TrimEnd('/') + "/";
    }

    private static Regex Named(string name) => new($@"(^|[^A-Za-z0-9_.-]){Regex.Escape(name)}([/\\'""`\s]|$)", RegexOptions.CultureInvariant);

    private static Regex Runs(string name) =>
        new($@"--no-build|{Regex.Escape(name)}/(debug|release|Debug|Release)/|{Regex.Escape(name)}/\S*\.(exe|dll)\b", RegexOptions.CultureInvariant);

    [GeneratedRegex(@"\b(mkdir|mkdirSync|writeFile|writeFileSync|createWriteStream|CreateDirectory|WriteAllText|WriteAllBytes|outDir|outputPath|OutputPath|Out-File|New-Item|cpSync|copyFile)\b|(^|\s)(-o|--out|--output|--outdir|--out-dir)(\s|=)")]
    private static partial Regex Writes();

    private static string Count(int count, string noun, string? plural = null) => $"{count} {(count == 1 ? noun : plural ?? noun + "s")}";
}
