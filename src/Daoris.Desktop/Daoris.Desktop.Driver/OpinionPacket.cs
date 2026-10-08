using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One path the candidate changes, as git names it: its status letter, and the path from the root where it now is.</summary>
public sealed record OpinionPath(string Status, string Path);

/// <summary>
/// The candidate as git read it (XAGENT1d; the second-agent design §1, §4): exactly what a pass reads, a base and a tip by their
/// full ids, the commits between them oldest first, and the paths the work changes.
/// </summary>
public sealed record OpinionCandidateRead(
    string Repository, string Base, string Tip, IReadOnlyList<string> Commits, IReadOnlyList<OpinionPath> Paths);

/// <summary>
/// The repository's own rules and records as they stand in the candidate (design §4), named by path and never quoted: the
/// reviewer reads them in its own harness's terms. Paths from the repository's root, with forward slashes.
/// </summary>
/// <param name="Doctrine">Its root instructions, then the indexes it keeps for its own documents.</param>
/// <param name="Decisions">Where it records its decisions, as its manifest declares it; null where it declares none here.</param>
/// <param name="Gates">What it declares safe to run (D122), its manifest's or the file by its own name; null where there is none.</param>
public sealed record OpinionRulesRead(IReadOnlyList<string> Doctrine, string? Decisions, string? Gates)
{
    /// <summary>A tree that holds none of them.</summary>
    public static OpinionRulesRead None { get; } = new([], null, null);

    public bool Equals(OpinionRulesRead? other) =>
        other is not null && Doctrine.SequenceEqual(other.Doctrine) && Decisions == other.Decisions && Gates == other.Gates;

    public override int GetHashCode() => HashCode.Combine(Doctrine.Count, Decisions, Gates);
}

/// <summary>
/// What a pass hands its reviewer (XAGENT1d, D155 point 6; design §4), assembled by Daoris with no model, from facts: the
/// candidate; what was asked, the quests of the work with the person's quoted requirements and the ask's words; what the
/// working session claims, each quest's closing note; what Daoris read of the evidence; and the repository's own rules. Never
/// the working session's conversation: a reviewer anchored on the worker's reasoning reads the work the way the worker did.
/// </summary>
/// <param name="Occasion">Why it is read: <c>landing</c>, <c>steps</c>, <c>failure</c> or <c>asked</c> (design §2.1).</param>
public sealed record OpinionPacket(string Occasion, OpinionCandidateRead Candidate)
{
    /// <summary>The quests of the work in this repository, as the service answered them; empty for a session's tip asked alone.</summary>
    public IReadOnlyList<QuestView> Quests { get; init; } = [];

    /// <summary>The person's words on the ask the work was asked by (DRIFT1b); null for work no ask asked.</summary>
    public AskWords? Words { get; init; }

    /// <summary>The repository's own rules and records, named by path.</summary>
    public OpinionRulesRead Rules { get; init; } = OpinionRulesRead.None;

    /// <summary>
    /// The whole diff's file, in the opinion's own folder under the home, which the reviewer is handed a read of; null where it
    /// could not be written, and the reviewer reads the diff from git in its copy.
    /// </summary>
    public string? Diff { get; init; }

    /// <summary>Whether the reviewer may build and run what the repository declares safe, in its copy (design §5.3).</summary>
    public bool Verify { get; init; }

    /// <summary>How long the pass may take, in minutes: the rule's bound.</summary>
    public int Minutes { get; init; } = OpinionRules.DefaultMinutes;
}

/// <summary>
/// The packet read from git and from the copy (XAGENT1d): the candidate, the diff written beside the opinion, and the rules
/// named. Nothing here is read from the copy after its reviewer starts; the rules are read before it does.
/// </summary>
public static class OpinionPackets
{
    /// <summary>The most commits a candidate names, the service's own bound (<c>Opinions.MostCommits</c>).</summary>
    public const int MostCommits = 1000;

    /// <summary>The diff's file in an opinion's folder.</summary>
    public const string DiffName = "candidate.diff";

    /// <summary>The root files a repository's agents are told to read first, in the order named: one harness reads each (D59).</summary>
    private static readonly string[] Instructions = ["AGENTS.md", "CLAUDE.md"];

    private const string Manifest = "daoris.json";

    /// <summary>The gates file by its own name, where a manifest declares none (D122).</summary>
    private const string GatesFile = "daoris.gates.json";

    /// <summary>
    /// An opinion's own folder under the home (design §4), where the larger parts of its packet are kept. Refused for an id
    /// that is not one plain name, so no id names a path elsewhere.
    /// </summary>
    /// <exception cref="DriverException">The id is not one plain name.</exception>
    public static string Folder(string home, string opinion)
    {
        var id = opinion.Trim();
        if (id.Length == 0 || id.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
        {
            throw new DriverException($"`{opinion}` is no second opinion's id, so it names no folder.");
        }

        return Path.Combine(home, "opinions", id);
    }

    /// <summary>
    /// The candidate from <paramref name="root"/>'s git: <paramref name="base"/> and <paramref name="tip"/> as commits by their
    /// full ids, the commits reachable from the tip and not the base, oldest first, and the paths the work changes since the
    /// two met.
    /// </summary>
    /// <exception cref="DriverException">Git cannot name a commit, there is no work between them, or more than the service keeps.</exception>
    public static async Task<OpinionCandidateRead> ReadAsync(
        string root, string repository, string @base, string tip, CancellationToken ct = default)
    {
        var from = await CommitAsync(root, repository, @base, ct).ConfigureAwait(false);
        var to = await CommitAsync(root, repository, tip, ct).ConfigureAwait(false);
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            throw new DriverException($"`{repository}` holds no work between `{@base}` and `{tip}`: a second opinion reads commits.");
        }

        var (listed, commits, listError) = await WorkingTree.GitAsync(root, ["rev-list", "--reverse", $"{from}..{to}"], ct).ConfigureAwait(false);
        if (listed != 0) throw new DriverException($"git could not list `{repository}`'s commits from `{@base}` to `{tip}`: {listError.Trim()}");
        var read = Commits(commits);
        if (read.Count == 0)
        {
            throw new DriverException($"`{repository}` holds no work between `{@base}` and `{tip}`: the tip adds no commit to the base.");
        }

        if (read.Count > MostCommits)
        {
            throw new DriverException($"`{repository}`'s work from `{@base}` to `{tip}` is {read.Count} commits; a second opinion reads at most {MostCommits}.");
        }

        var (named, paths, nameError) = await WorkingTree.GitAsync(
            root, ["-c", "core.quotepath=false", "diff", "--name-status", "-M", "-z", "--no-ext-diff", $"{from}...{to}"], ct).ConfigureAwait(false);
        if (named != 0) throw new DriverException($"git could not name what `{repository}`'s work changes: {nameError.Trim()}");

        return new OpinionCandidateRead(repository, from, to, read, Paths(paths));
    }

    /// <summary>
    /// The whole diff of the candidate, from where its base and tip met to its tip, written to <paramref name="file"/> by git
    /// itself, never through a console. False where git could not, and the reviewer reads it from git in its copy.
    /// </summary>
    public static async Task<bool> WriteDiffAsync(string root, OpinionCandidateRead candidate, string file, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var (code, _, _) = await WorkingTree.GitAsync(
            root, ["diff", "--no-color", "--no-ext-diff", "--no-textconv", "-M", $"--output={file}", $"{candidate.Base}...{candidate.Tip}"], ct)
            .ConfigureAwait(false);
        return code == 0 && File.Exists(file);
    }

    /// <summary>
    /// The repository's own rules as they stand in <paramref name="tree"/> (design §4): its root instructions, then the indexes
    /// it keeps (<see cref="RepositoryIndexes"/>), each once; then where its manifest says its decisions and its safe work are,
    /// only where something is there, and only a path inside the tree.
    /// </summary>
    public static OpinionRulesRead RulesIn(string tree)
    {
        var doctrine = new List<string>();
        foreach (var path in Instructions.Where(name => File.Exists(Path.Combine(tree, name))).Concat(RepositoryIndexes.Find(tree)))
        {
            if (!doctrine.Contains(path, StringComparer.OrdinalIgnoreCase)) doctrine.Add(path);
        }

        var declared = Declared(tree);
        var decisions = Inside(tree, declared.GetValueOrDefault("decisions"));
        var gates = Inside(tree, declared.GetValueOrDefault("gates")) ?? Inside(tree, GatesFile);
        return new OpinionRulesRead(doctrine, decisions, gates);
    }

    /// <summary>The commits git listed, each a full id, each once, in its order; a line that is no full id is none.</summary>
    internal static IReadOnlyList<string> Commits(string listed)
    {
        var commits = new List<string>();
        foreach (var line in listed.Split('\n').Select(line => line.Trim()))
        {
            if (EvidenceCodes.IsObjectId(line) && !commits.Contains(line, StringComparer.OrdinalIgnoreCase)) commits.Add(line.ToLowerInvariant());
        }

        return commits;
    }

    /// <summary>
    /// The paths of <c>git diff --name-status -z</c>: a status, then its path, or for a rename or a copy the path it came from and
    /// the path it is now, which is the one named.
    /// </summary>
    internal static IReadOnlyList<OpinionPath> Paths(string nameStatus)
    {
        var fields = nameStatus.Split('\0');
        var paths = new List<OpinionPath>();
        for (var at = 0; at < fields.Length; at++)
        {
            var status = fields[at].Trim();
            if (status.Length == 0) continue;
            var letter = status[..1];
            var named = letter is "R" or "C" ? at + 2 : at + 1;
            if (named >= fields.Length) break;
            paths.Add(new OpinionPath(letter, fields[named]));
            at = named;
        }

        return paths;
    }

    /// <summary>A commit's full id as git names it, or the refusal naming what it could not.</summary>
    private static async Task<string> CommitAsync(string root, string repository, string name, CancellationToken ct)
    {
        // A name git would read as an option names no commit.
        var (code, id, _) = name.StartsWith('-')
            ? (1, "", "")
            : await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"{name}^{{commit}}"], ct).ConfigureAwait(false);
        var full = id.Trim();
        if (code != 0 || !EvidenceCodes.IsObjectId(full))
        {
            throw new DriverException($"`{repository}` holds no commit `{name}`, so a second opinion has nothing to read there.");
        }

        return full.ToLowerInvariant();
    }

    /// <summary>The manifest's <c>documents</c>, each role's path: a string, or an object's <c>path</c>. None where it cannot be read.</summary>
    private static IReadOnlyDictionary<string, string> Declared(string tree)
    {
        var declared = new Dictionary<string, string>(StringComparer.Ordinal);
        var manifest = Path.Combine(tree, Manifest);
        if (!File.Exists(manifest)) return declared;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifest));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("documents", out var documents) || documents.ValueKind != JsonValueKind.Object)
            {
                return declared;
            }

            foreach (var role in documents.EnumerateObject())
            {
                var path = role.Value.ValueKind switch
                {
                    JsonValueKind.String => role.Value.GetString(),
                    JsonValueKind.Object when role.Value.TryGetProperty("path", out var named) && named.ValueKind == JsonValueKind.String => named.GetString(),
                    _ => null,
                };
                if (path is { Length: > 0 }) declared[role.Name] = path;
            }
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            // A manifest that cannot be read declares nothing; the files found by their own names are still named.
        }

        return declared;
    }

    /// <summary>
    /// <paramref name="path"/> as the tree holds it, forward slashes and no trailing one, where it is a relative path inside the
    /// tree and something is there; null otherwise.
    /// </summary>
    private static string? Inside(string tree, string? path)
    {
        if (path is not { Length: > 0 } given || Path.IsPathRooted(given) || given.Contains(':')) return null;
        var relative = given.Replace('\\', '/').Trim().TrimEnd('/');
        if (relative.Length == 0 || relative.Split('/').Any(segment => segment is ".." or "")) return null;

        var full = Path.GetFullPath(Path.Combine(tree, relative));
        var within = Path.GetFullPath(tree).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(within, StringComparison.OrdinalIgnoreCase)) return null;
        return File.Exists(full) || Directory.Exists(full) ? relative : null;
    }
}
