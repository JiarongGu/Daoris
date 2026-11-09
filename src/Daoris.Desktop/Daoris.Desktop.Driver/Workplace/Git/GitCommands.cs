using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>
/// The one table of the git commands Daoris starts, a row per command and, where git takes one, per verb. The write test
/// (<see cref="Writes"/>), the bounds (<see cref="GitBounds"/>) and the waits (<see cref="GitStartAsks"/>) read their columns
/// here, so a command gets its row once.
/// </summary>
/// <remarks>
/// <para><b>Total at test time, never refusing at run time.</b> <c>GitCommandsCensusTests</c> holds every start in the sources to a row, and
/// the tests run the gate <see cref="GitStartGate.Strict"/>; a person's run is never refused for want of a row. A start the table cannot read (no row, or a
/// verb its row does not know) fails closed on the branch question alone: it is read as a drop of the branches it names and the folder's
/// current branch.</para>
/// <para><b>Object writers are writes.</b> <c>commit-tree</c>, <c>mktree</c>, <c>hash-object</c> and their kin write only objects, but a
/// flow's read cache would answer a later one with an earlier commit, so they are not read as reads.</para>
/// </remarks>
internal static class GitCommands
{
    private const string Heads = "refs/heads/";
    private const string EveryBranch = "refs/heads/*";
    private const string ZeroId = "0000000000000000000000000000000000000000";
    private const string ZeroId256 = "0000000000000000000000000000000000000000000000000000000000000000";

    /// <param name="Question">Answered from the object store and the refs in seconds (<see cref="GitBounds.Question"/>).</param>
    /// <param name="Local">A write that reaches no remote and runs no hook, waiting on the repository's files alone.</param>
    /// <param name="Verbs">The verbs git takes after it, each with whether it is a question; null where it takes none.</param>
    /// <param name="Default">The verb a start naming none runs.</param>
    /// <param name="Reads">Every start of it reads.</param>
    private sealed record Row(
        bool Question = false, bool Local = false, IReadOnlyDictionary<string, bool>? Verbs = null, string? Default = null, bool Reads = false);

    private static readonly Dictionary<string, Row> Rows = Table();

    // Removed rows, by `command` or `command verb`, in a test's flow alone.
    private static readonly AsyncLocal<ImmutableHashSet<string>?> Removed = new();

    // git's own options that take the next word as their value.
    private static readonly HashSet<string> Valued = new(StringComparer.Ordinal) { "-c", "-C", "--git-dir", "--work-tree", "--namespace" };

    /// <summary>Whether <paramref name="command"/>, with <paramref name="verb"/> where its row reads one, has a row.</summary>
    internal static bool Rowed(string command, string? verb)
    {
        if (!Rows.TryGetValue(command, out var row) || IsRemoved(command, null)) return false;
        if (row.Verbs is null || verb is null || verb.StartsWith('-')) return true;
        return row.Verbs.ContainsKey(verb) && !IsRemoved(command, verb);
    }

    /// <summary>Whether <paramref name="command"/>'s row reads a verb after it.</summary>
    internal static bool HasVerbs(string command) => Rows.TryGetValue(command, out var row) && row.Verbs is not null;

    /// <summary>The table without <paramref name="command"/>'s row (or its <paramref name="verb"/>'s), in this flow until disposed.</summary>
    internal static IDisposable Without(string command, string? verb = null)
    {
        var before = Removed.Value;
        Removed.Value = (before ?? []).Add(verb is null ? command : $"{command} {verb}");
        return new Restore(before);
    }

    /// <summary>Whether a start may write anything but objects.</summary>
    internal static bool Writes(IReadOnlyList<string> arguments) => Read(null, arguments, null).Class != GitClass.Read;

    /// <summary>Whether a start is a question or a ref's move, answered under <see cref="GitBounds.Question"/>.</summary>
    internal static bool IsQuestion(IReadOnlyList<string> arguments)
    {
        var (at, _, _) = Locate(null, arguments);
        if (at >= arguments.Count || !Rows.TryGetValue(arguments[at], out var row)) return false;
        if (row.Verbs is null) return row.Question;
        return VerbOf(row, arguments.Skip(at + 1).ToList()) is { } verb && row.Verbs.TryGetValue(verb, out var question) && question;
    }

    /// <summary>Whether <paramref name="command"/> writes the repository's files alone, reaching no remote and running no hook.</summary>
    internal static bool IsLocal(string command) => Rows.TryGetValue(command, out var row) && row.Local;

    /// <summary>
    /// <paramref name="arguments"/> as the table reads them, started in <paramref name="folder"/> with <paramref name="input"/>. With no
    /// folder, the branches it names are read without the folder's current branch or which names stand.
    /// </summary>
    internal static GitCommandRead Read(string? folder, IReadOnlyList<string> arguments, string? input)
    {
        var (at, runsIn, workTree) = Locate(folder, arguments);
        var start = new GitStartFacts(runsIn, input, Options(arguments, at));
        List<string> trees = [.. new[] { runsIn, workTree }.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)];
        if (at >= arguments.Count) return new(GitClass.Write, true, [], trees, []);
        var command = arguments[at];
        var rest = arguments.Skip(at + 1).ToList();
        if (!Rows.TryGetValue(command, out var row) || IsRemoved(command, null)) return Unclassed(start, rest, trees);
        var verb = VerbOf(row, rest);
        if (row.Verbs is not null && (verb is null || !row.Verbs.ContainsKey(verb) || IsRemoved(command, verb))) return Unclassed(start, rest, trees);
        var read = new Reading(start, trees);
        var kind = command switch
        {
            "branch" => GitCommandReaders.Branch(read, rest),
            "update-ref" => GitCommandReaders.UpdateRef(read, rest),
            "symbolic-ref" => GitCommandReaders.SymbolicRef(read, rest),
            "worktree" => GitCommandReaders.Worktree(read, verb!, rest),
            "switch" => read.Overwrites(rest, "-C", "--force-create"),
            "checkout" => read.Overwrites(rest, "-B"),
            "reset" => GitCommandReaders.Reset(read, rest),
            "commit" => rest.Contains("--amend") ? read.DropCurrent() : read.MovesCurrent(),
            "rebase" => GitCommandReaders.Rebase(read, rest),
            "cherry-pick" or "revert" => rest.Contains("--abort") ? read.DropCurrent() : read.MovesCurrent(),
            "merge" or "am" => read.MovesCurrent(),
            "pull" => GitCommandReaders.Pull(read, rest),
            "fetch" => GitCommandReaders.Fetch(read, rest, prunes: false),
            "push" => GitCommandReaders.Push(read, rest),
            "filter-branch" => read.Drop(EveryBranch, null),
            "remote" => verb is "update" or "prune" ? GitCommandReaders.Fetch(read, rest.Skip(1).ToList(), prunes: verb == "prune") : Verbed(row, verb),
            "tag" => GitCommandReaders.Tag(read, rest),
            "config" => rest.Any(GitCommandReaders.ReadsConfig) ? GitClass.Read : GitClass.Write,
            "replace" => rest.Count == 0 || rest.Any(each => each is "-l" or "--list") ? GitClass.Read : GitClass.Write,
            _ when row.Verbs is not null => Verbed(row, verb),
            _ => row.Reads ? GitClass.Read : GitClass.Write,
        };

        return new(read.Drops.Count > 0 ? GitClass.Drops : kind, true, read.Drops, read.Trees, read.Refs);
    }

    private static GitClass Verbed(Row row, string? verb) => verb is not null && row.Verbs!.TryGetValue(verb, out var question) && question ? GitClass.Read : GitClass.Write;

    /// <summary>An unrowed start, failing closed on the branch question: a drop of every word it names and of the folder's current branch.</summary>
    private static GitCommandRead Unclassed(GitStartFacts start, List<string> rest, List<string> trees)
    {
        var read = new Reading(start, trees);
        foreach (var word in rest.Where(each => !each.StartsWith('-'))) read.Drop(Short(word), null);
        read.DropCurrent();
        return new(GitClass.Drops, false, read.Drops, read.Trees, read.Refs);
    }

    internal static string Short(string reference) => reference.StartsWith(Heads, StringComparison.Ordinal) ? reference[Heads.Length..] : reference;

    internal static bool IsZero(string value) => value is ZeroId or ZeroId256;

    private static string? VerbOf(Row row, List<string> rest) =>
        row.Verbs is null ? null : rest.FirstOrDefault(each => !each.StartsWith('-')) ?? row.Default;

    private static bool IsRemoved(string command, string? verb) =>
        Removed.Value is { } removed && removed.Contains(verb is null ? command : $"{command} {verb}");

    /// <summary>Where the command is past git's own options, the folder <c>-C</c> moves the start to, and a <c>--work-tree</c>.</summary>
    private static (int At, string? Folder, string? WorkTree) Locate(string? folder, IReadOnlyList<string> arguments)
    {
        var (at, runsIn, workTree) = (0, folder is null ? null : Path.GetFullPath(folder), (string?)null);
        while (at < arguments.Count && arguments[at].StartsWith('-'))
        {
            var word = arguments[at];
            var value = Valued.Contains(word) && at + 1 < arguments.Count ? arguments[at + 1] : null;
            if (word == "-C" && runsIn is not null && value is not null) runsIn = Path.GetFullPath(Path.Combine(runsIn, value));
            if (word == "--work-tree" && runsIn is not null && value is not null) workTree = Path.GetFullPath(Path.Combine(runsIn, value));
            if (word.StartsWith("--work-tree=", StringComparison.Ordinal) && runsIn is not null) workTree = Path.GetFullPath(Path.Combine(runsIn, word["--work-tree=".Length..]));
            at += value is not null ? 2 : 1;
        }

        return (at, runsIn, workTree);
    }

    /// <summary>The <c>-c name=value</c> settings before the command, by name blind to case.</summary>
    private static Dictionary<string, string> Options(IReadOnlyList<string> arguments, int command)
    {
        var set = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var at = 0; at + 1 < command; at++)
        {
            if (arguments[at] == "-c" && arguments[at + 1].Split('=', 2) is [var name, var value]) set[name] = value;
        }

        return set;
    }

    private static Dictionary<string, Row> Table()
    {
        var rows = new Dictionary<string, Row>(StringComparer.Ordinal);
        foreach (var read in new[]
                 {
                     "rev-parse", "rev-list", "log", "show", "diff", "diff-tree", "diff-index", "diff-files", "cat-file", "ls-files", "ls-tree",
                     "for-each-ref", "show-ref", "merge-base", "name-rev", "describe", "blame", "cherry", "shortlog", "grep", "count-objects",
                     "check-ignore", "check-attr", "check-ref-format", "var", "version", "status", "merge-tree", "patch-id",
                 })
        {
            rows[read] = new(Question: true, Reads: true);
        }

        rows["ls-remote"] = new(Reads: true);
        foreach (var quick in new[] { "symbolic-ref", "update-ref", "branch", "tag" }) rows[quick] = new(Question: true);
        foreach (var local in new[] { "config", "hash-object" }) rows[local] = new(Question: true, Local: true);
        foreach (var local in new[]
                 {
                     "update-index", "read-tree", "write-tree", "mktree", "commit-tree", "mktag", "add", "rm", "mv", "apply", "init", "unpack-objects",
                     "prune", "repack", "pack-refs", "multi-pack-index", "commit-graph", "merge-file",
                 })
        {
            rows[local] = new(Local: true);
        }

        rows["hook"] = new(Verbs: Verbs([], ["run"]));

        foreach (var work in new[]
                 {
                     "commit", "merge", "cherry-pick", "revert", "am", "restore", "checkout", "switch", "reset", "rebase", "fetch", "pull", "push",
                     "clone", "gc", "clean", "index-pack", "filter-branch", "replace",
                 })
        {
            rows[work] = new();
        }

        rows["worktree"] = new(Verbs: Verbs(["list"], ["add", "lock", "unlock", "move", "remove", "repair", "prune"]));
        rows["stash"] = new(Verbs: Verbs(["list", "show"], ["push", "save", "pop", "apply", "drop", "store", "create", "clear"]), Default: "push");
        rows["notes"] = new(Verbs: Verbs(["list", "show", "get-ref"], ["add", "append", "copy", "edit", "merge", "remove", "prune"]), Default: "list");
        rows["submodule"] = new(Verbs: Verbs(["summary", "status"], ["update", "init", "sync", "add", "deinit", "absorbgitdirs"]), Default: "status");
        rows["remote"] = new(Verbs: Verbs(["get-url", "show"], ["add", "remove", "rename", "set-url", "set-head", "update", "prune"]), Default: "show");
        // `register`, `start`, `stop` and `unregister` write the person's own configuration and scheduler, which Daoris never starts.
        rows["maintenance"] = new(Verbs: Verbs([], ["run"]));
        return rows;
    }

    private static Dictionary<string, bool> Verbs(string[] questions, string[] writes) =>
        questions.Select(each => (each, true)).Concat(writes.Select(each => (each, false))).ToDictionary(each => each.Item1, each => each.Item2, StringComparer.Ordinal);

    private sealed class Restore(ImmutableHashSet<string>? before) : IDisposable
    {
        public void Dispose() => Removed.Value = before;
    }
}
