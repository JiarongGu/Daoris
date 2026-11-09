namespace Daoris.Driver;

/// <summary>A start's own facts the table reads beside its words: the folder it runs in, its input, and its <c>-c</c> settings.</summary>
internal sealed record GitStartFacts(string? Folder, string? Input, IReadOnlyDictionary<string, string> Settings);

/// <summary>
/// One start being read: the branches it drops, the trees and refs it touches, and what the folder's files say (its current branch, which
/// names stand, its configuration), each read once and nothing started.
/// </summary>
internal sealed class Reading(GitStartFacts start, List<string> trees)
{
    private const string Heads = "refs/heads/";

    private string? _common;
    private bool _commonRead;
    private (bool Read, string? Branch) _current;
    private GitConfigFile? _config;

    public GitStartFacts Start => start;

    public List<GitDrop> Drops { get; } = [];

    public List<string> Trees => trees;

    public List<string> Refs { get; } = [];

    /// <summary>The folder's current branch by its short name, read from its <c>HEAD</c>; null on a detached head or with no folder.</summary>
    public string? Current
    {
        get
        {
            if (_current.Read) return _current.Branch;
            _current = (true, HeadBranch(start.Folder));
            return _current.Branch;
        }
    }

    public string? Common
    {
        get
        {
            if (!_commonRead) (_common, _commonRead) = (start.Folder is { } folder ? RefFiles.CommonDirOf(folder) : null, true);
            return _common;
        }
    }

    /// <summary>The repository's own configuration, read once from its file.</summary>
    public GitConfigFile Config => _config ??= GitConfigFile.Of(Common);

    /// <summary>A setting as git reads it here: a <c>-c</c> before the command, else the repository's file.</summary>
    public string? Setting(string name) => start.Settings.TryGetValue(name, out var set) ? set : Config.Last(name);

    /// <summary>Whether a branch stands: unreadable is standing, so an overwrite fails closed.</summary>
    public bool Stands(string branch) => Common is not { } common || !RefFiles.Absent(common, Heads + branch);

    /// <summary>A drop of <paramref name="branch"/> verified at <paramref name="verifies"/>.</summary>
    public GitClass Drop(string branch, string? verifies)
    {
        if (!Drops.Any(each => each.Branch == branch && each.Verifies == verifies)) Drops.Add(new GitDrop(branch, verifies));
        Touch(branch == "refs/heads/*" ? branch : Heads + branch);
        return GitClass.Drops;
    }

    /// <summary>A drop of the folder's current branch, verifying none; a write where the head is detached.</summary>
    public GitClass DropCurrent() => Current is { } branch ? Drop(branch, null) : GitClass.Write;

    /// <summary>A move of the current branch to a descendant: a write that touches its ref.</summary>
    public GitClass MovesCurrent()
    {
        if (Current is { } branch) Touch(Heads + branch);
        return GitClass.Write;
    }

    /// <summary>A start that makes a branch under one of <paramref name="flags"/>, overwriting it where it stands.</summary>
    public GitClass Overwrites(List<string> rest, params string[] flags)
    {
        for (var at = 0; at + 1 < rest.Count; at++)
        {
            if (flags.Contains(rest[at])) return Stands(rest[at + 1]) ? Drop(rest[at + 1], null) : Touched(Heads + rest[at + 1]);
        }

        return GitClass.Write;
    }

    public void Touch(string reference)
    {
        if (!Refs.Contains(reference)) Refs.Add(reference);
    }

    public GitClass Touched(string reference)
    {
        Touch(reference);
        return GitClass.Write;
    }

    /// <summary>A ref as a drop names it: <c>HEAD</c> followed to the current branch unless told not to, a branch by its short name.</summary>
    public string? BranchOf(string reference, bool follow = true) =>
        reference == "HEAD" ? follow ? Current : null
        : reference.StartsWith(Heads, StringComparison.Ordinal) ? reference[Heads.Length..]
        : null;

    /// <summary>The branch a folder's <c>HEAD</c> names, read from its file; null where it is detached or unread.</summary>
    internal static string? HeadBranch(string? folder)
    {
        if (folder is null || RefFiles.GitDirOf(folder) is not { } dir) return null;
        try
        {
            var head = File.ReadAllText(Path.Combine(dir, "HEAD")).Trim(); // read-shares: git's own HEAD
            return head.StartsWith("ref: " + Heads, StringComparison.Ordinal) ? head[("ref: " + Heads).Length..] : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>The commands whose class or drops their words decide, each read as git reads them.</summary>
internal static class GitCommandReaders
{
    private const string Heads = "refs/heads/";

    // `branch`'s options whose value is the next word.
    private static readonly HashSet<string> BranchValued = new(StringComparer.Ordinal)
    {
        "-u", "--set-upstream-to", "--contains", "--no-contains", "--merged", "--no-merged", "--points-at", "--format", "--sort",
    };

    internal static bool ReadsConfig(string word) =>
        word is "--get" or "--get-all" or "--get-regexp" or "--get-urlmatch" or "--list" or "-l" or "get" or "list";

    public static GitClass Branch(Reading read, List<string> rest)
    {
        var (flags, words) = Split(rest, BranchValued);
        if (flags.Overlaps(["-d", "-D", "--delete"]))
        {
            foreach (var word in words) read.Drop(GitCommands.Short(word), null);
            return GitClass.Drops;
        }

        if (flags.Overlaps(["-m", "-M", "--move"]) && words.Count > 0)
        {
            var (from, to) = words.Count >= 2 ? (words[0], words[1]) : (read.Current, words[0]);
            if (from is not null) read.Drop(from, null);
            if ((flags.Contains("-M") || flags.Contains("--force")) && read.Stands(to)) read.Drop(to, null);
            return read.Touched(Heads + to);
        }

        if (flags.Overlaps(["-c", "-C", "--copy"]) && words.Count > 0)
        {
            var to = words[^1];
            return (flags.Contains("-C") || flags.Contains("--force") || flags.Contains("-f")) && read.Stands(to) ? read.Drop(to, null) : read.Touched(Heads + to);
        }

        if (flags.Overlaps(["-f", "--force"]) && words.Count > 0) return read.Stands(words[0]) ? read.Drop(words[0], null) : read.Touched(Heads + words[0]);
        var writes = flags.Any(each => each is "-u" or "--set-upstream-to" or "--unset-upstream" or "-t" or "--track" or "--no-track" or "--edit-description"
            or "--create-reflog" || each.StartsWith("--set-upstream-to=", StringComparison.Ordinal) || each.StartsWith("--track=", StringComparison.Ordinal));
        var lists = flags.Any(each => each is "--list" or "-l" or "--contains" or "--no-contains" or "--merged" or "--no-merged" or "--points-at"
            or "--show-current" or "-a" or "-r" or "--all" or "--remotes" || each.StartsWith("--format", StringComparison.Ordinal));
        if (!writes && (lists || words.Count == 0)) return GitClass.Read;
        return words.Count > 0 && !writes ? read.Touched(Heads + words[0]) : GitClass.Write;
    }

    public static GitClass UpdateRef(Reading read, List<string> rest)
    {
        var (flags, words) = Split(rest, new HashSet<string>(StringComparer.Ordinal) { "-m" });
        var follow = !flags.Contains("--no-deref");
        if (flags.Contains("--stdin")) return Lines(read, flags.Contains("-z"), follow);
        if (flags.Contains("-d") && words.Count > 0)
        {
            read.Touch(words[0]);
            return read.BranchOf(words[0], follow) is { } branch ? read.Drop(branch, words.ElementAtOrDefault(1) is { Length: > 0 } old ? old : null) : GitClass.Write;
        }

        if (words.Count < 2) return GitClass.Write;
        read.Touch(words[0]);
        var verified = words.ElementAtOrDefault(2) is { Length: > 0 } was ? was : null;
        return GitCommands.IsZero(words[1]) && read.BranchOf(words[0], follow) is { } dropped ? read.Drop(dropped, verified) : GitClass.Write;
    }

    // `update-ref --stdin`'s lines, in either format: a verb, its ref, then its values (on the line, or each its own NUL-ended field).
    private static GitClass Lines(Reading read, bool nul, bool follow)
    {
        var fields = (read.Start.Input ?? "").Split(nul ? '\0' : '\n');
        for (var at = 0; at < fields.Length; at++)
        {
            var line = fields[at].TrimEnd('\r');
            if (line.Length == 0) continue;
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            var arity = words[0] switch { "update" => 2, "symref-update" => 3, "create" or "delete" or "verify" or "symref-delete" or "symref-verify" or "symref-create" => 1, _ => 0 };
            if (nul)
            {
                for (var take = 0; take < arity && at + 1 < fields.Length; take++) words.Add(fields[++at]);
            }

            if (words.Count < 2) continue;
            if (words[0] == "option")
            {
                if (words[1] == "no-deref") follow = false;
                continue;
            }

            var reference = words[1];
            var value = (int index) => words.ElementAtOrDefault(index) is { Length: > 0 } each ? each : null;
            switch (words[0])
            {
                case "verify" or "symref-verify" or "start" or "prepare" or "commit" or "abort":
                    break;
                case "create" or "symref-create":
                    read.Touch(reference);
                    break;
                case "update":
                    read.Touch(reference);
                    if (value(2) is { } updated && GitCommands.IsZero(updated) && read.BranchOf(reference, follow) is { } zeroed) read.Drop(zeroed, value(3));
                    break;
                case "delete":
                    read.Touch(reference);
                    if (read.BranchOf(reference, follow) is { } deleted) read.Drop(deleted, value(2));
                    break;
                default:
                    // symref-delete, symref-update and a verb this reader does not know: a drop of the ref they name.
                    read.Touch(reference);
                    if (read.BranchOf(reference, follow) is { } named) read.Drop(named, null);
                    break;
            }
        }

        return read.Drops.Count > 0 ? GitClass.Drops : GitClass.Write;
    }

    public static GitClass SymbolicRef(Reading read, List<string> rest)
    {
        var (flags, words) = Split(rest, new HashSet<string>(StringComparer.Ordinal) { "-m" });
        if (flags.Overlaps(["-d", "--delete"])) return words.Count > 0 && read.BranchOf(words[0]) is { } deleted ? read.Drop(deleted, null) : GitClass.Write;
        if (words.Count < 2) return GitClass.Read;
        return words[0].StartsWith(Heads, StringComparison.Ordinal) ? read.Drop(words[0][Heads.Length..], null) : read.Touched(words[0]);
    }

    public static GitClass Worktree(Reading read, string verb, List<string> rest)
    {
        if (verb == "list") return GitClass.Read;
        var (_, words) = Split(rest.Skip(1).ToList(), new HashSet<string>(StringComparer.Ordinal) { "-b", "-B", "--reason" });
        if (read.Start.Folder is { } folder)
        {
            foreach (var path in verb == "move" ? words : words.Take(1)) read.Trees.Add(Path.GetFullPath(Path.Combine(folder, path)));
        }

        return verb == "add" ? read.Overwrites(rest, "-B") : GitClass.Write;
    }

    public static GitClass Reset(Reading read, List<string> rest)
    {
        if (rest.Contains("--")) return GitClass.Write;
        var (_, words) = Split(rest, new HashSet<string>(StringComparer.Ordinal) { "--pathspec-from-file" });
        return words.Count == 1 && words[0] != "HEAD" ? read.DropCurrent() : GitClass.Write;
    }

    public static GitClass Rebase(Reading read, List<string> rest)
    {
        if (rest.Any(each => each is "--abort" or "--quit" or "--edit-todo" or "--show-current-patch")) return GitClass.Write;
        if (rest.Any(each => each is "--continue" or "--skip")) return Rebasing(read) is { } named ? read.Drop(named, null) : GitClass.Write;
        var (_, words) = Split(rest, new HashSet<string>(StringComparer.Ordinal) { "--onto", "-s", "--strategy", "-X", "--strategy-option", "-x", "--exec" });
        var branch = words.Count >= 2 ? GitCommands.Short(words[1]) : read.Current;
        return branch is null ? GitClass.Write : read.Drop(branch, null);
    }

    public static GitClass Pull(Reading read, List<string> rest)
    {
        var (flags, words) = Split(rest, []);
        Refspecs(read, words.Skip(1).ToList(), words.FirstOrDefault() ?? "origin", flags, prunes: false);
        var rebases = flags.Overlaps(["--rebase", "-r"]) || flags.Any(each => each.StartsWith("--rebase=", StringComparison.Ordinal) && each != "--rebase=false")
            || (!flags.Contains("--no-rebase") && read.Setting("pull.rebase") is { } set && !set.Equals("false", StringComparison.OrdinalIgnoreCase));
        return rebases ? read.DropCurrent() : read.Drops.Count > 0 ? GitClass.Drops : read.MovesCurrent();
    }

    public static GitClass Fetch(Reading read, List<string> rest, bool prunes)
    {
        var (flags, words) = Split(rest, new HashSet<string>(StringComparer.Ordinal) { "--depth", "--deepen", "--shallow-since", "--shallow-exclude", "-j", "--jobs", "--negotiation-tip", "--refmap", "-o", "--server-option", "--filter" });
        Refspecs(read, words.Skip(1).ToList(), words.FirstOrDefault() ?? "origin", flags, prunes);
        return read.Drops.Count > 0 ? GitClass.Drops : GitClass.Write;
    }

    // A fetch's effective refspecs (its words, else the remote's configured ones), each reaching `refs/heads/` forced or pruned a drop.
    private static void Refspecs(Reading read, List<string> specs, string remote, HashSet<string> flags, bool prunes)
    {
        var mirror = read.Setting($"remote.{remote}.mirror") is { } mirrored && mirrored.Equals("true", StringComparison.OrdinalIgnoreCase);
        var configured = specs.Count == 0;
        if (configured) specs = mirror ? ["+refs/*:refs/*"] : [.. read.Config.All($"remote.{remote}.fetch")];
        var pruned = !flags.Contains("--no-prune") && (prunes || flags.Overlaps(["--prune", "-p", "--prune-tags", "-P"])
            || (configured && (True(read.Setting($"remote.{remote}.prune")) || True(read.Setting("fetch.prune")))));
        var forcedAll = flags.Overlaps(["--force", "-f", "--update-head-ok", "-u"]);
        foreach (var spec in specs)
        {
            var forced = forcedAll || spec.StartsWith('+');
            var parts = spec.TrimStart('+').Split(':', 2);
            if (parts.Length < 2 || parts[1].Length == 0) continue;
            var to = parts[1];
            if (!(to.StartsWith(Heads, StringComparison.Ordinal) || to is "refs/*")) continue;
            if (!forced && !pruned) continue;
            read.Drop(to.Contains('*') ? "refs/heads/*" : to[Heads.Length..], null);
        }

        static bool True(string? value) => value is not null && value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public static GitClass Push(Reading read, List<string> rest)
    {
        var (flags, words) = Split(rest, new HashSet<string>(StringComparer.Ordinal) { "-o", "--push-option", "--receive-pack", "--exec", "--repo" });
        if (words.Count == 0 || !IsSelf(read, words[0])) return GitClass.Write;
        var forcedAll = flags.Overlaps(["--force", "-f", "--mirror", "--prune"]) || flags.Any(each => each.StartsWith("--force-with-lease", StringComparison.Ordinal));
        if (flags.Overlaps(["--mirror", "--prune", "--all", "--branches"]) && forcedAll) read.Drop("refs/heads/*", null);
        var deletes = flags.Overlaps(["--delete", "-d"]);
        foreach (var spec in words.Skip(1))
        {
            var forced = forcedAll || spec.StartsWith('+');
            var parts = spec.TrimStart('+').Split(':', 2);
            var to = parts.Length == 2 ? parts[1] : parts[0];
            var deleting = deletes || (parts.Length == 2 && parts[0].Length == 0);
            var branch = to.StartsWith(Heads, StringComparison.Ordinal) ? to[Heads.Length..] : !to.StartsWith("refs/", StringComparison.Ordinal) && to.Length > 0 ? to : null;
            if (branch is not null && (forced || deleting)) read.Drop(branch, null);
        }

        return read.Drops.Count > 0 ? GitClass.Drops : GitClass.Write;
    }

    public static GitClass Tag(Reading read, List<string> rest)
    {
        var (flags, words) = Split(rest, new HashSet<string>(StringComparer.Ordinal) { "-m", "-F", "-u", "--contains", "--points-at", "--merged", "--no-merged", "--sort", "--format" });
        if (words.Count == 0 || flags.Overlaps(["-l", "--list", "-n", "--contains", "--points-at", "--merged", "--no-merged", "-v", "--verify"])) return GitClass.Read;
        return read.Touched("refs/tags/" + words[0]);
    }

    // Whether a push's remote is this repository: `.`, or a path whose common directory is its own.
    private static bool IsSelf(Reading read, string remote)
    {
        if (remote == ".") return true;
        if (read.Start.Folder is not { } folder || remote.Contains("://", StringComparison.Ordinal) || remote.Contains('@')) return false;
        try
        {
            var path = Path.GetFullPath(Path.Combine(folder, remote));
            return Directory.Exists(path) && RefFiles.SameDir(RefFiles.CommonDirOf(path) ?? path, read.Common);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or IOException)
        {
            return false;
        }
    }

    // The branch a rebase in progress names, from its state.
    private static string? Rebasing(Reading read)
    {
        if (read.Start.Folder is not { } folder || RefFiles.GitDirOf(folder) is not { } dir) return read.Current;
        foreach (var state in new[] { "rebase-merge", "rebase-apply" })
        {
            var named = Path.Combine(dir, state, "head-name");
            try
            {
                if (File.Exists(named)) return File.ReadAllText(named).Trim() is var head && head.StartsWith(Heads, StringComparison.Ordinal) ? head[Heads.Length..] : null; // read-shares: git's own rebase state
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return read.Current;
            }
        }

        return read.Current;
    }

    /// <summary>Words split into the options given and the words past them, an option in <paramref name="valued"/> taking the next.</summary>
    private static (HashSet<string> Flags, List<string> Words) Split(List<string> rest, HashSet<string> valued)
    {
        var (flags, words) = (new HashSet<string>(StringComparer.Ordinal), new List<string>());
        for (var at = 0; at < rest.Count; at++)
        {
            var word = rest[at];
            if (word == "--")
            {
                words.AddRange(rest.Skip(at + 1));
                break;
            }

            if (!word.StartsWith('-') || word == "-")
            {
                words.Add(word);
                continue;
            }

            flags.Add(word);
            if (valued.Contains(word)) at++;
        }

        return (flags, words);
    }
}
