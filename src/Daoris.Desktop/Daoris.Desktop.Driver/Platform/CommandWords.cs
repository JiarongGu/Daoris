namespace Daoris.Driver;

/// <summary>One group of the tree: its sub-verbs, what it reads where none is named, and the sub-verbs a sub-verb has.</summary>
/// <param name="Takes">What the group reads where its first word is no sub-verb (a new ask's words, a role's id); null where it needs one.</param>
/// <param name="Whole">Whether the group's own command judges every word after it, its sub-verbs listed for the usage only.</param>
public sealed record TreeGroup(string Name, IReadOnlyList<string> Verbs, string? Takes = null, bool Whole = false)
{
    /// <summary>A sub-verb's own sub-verbs (<c>agent account …</c>), by the sub-verb.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Nested { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
}

/// <summary>An old form and the form it is now: <paramref name="Bare"/> is the sub-verb a bare old form names, <paramref name="Append"/> a word the new form ends in.</summary>
/// <param name="Launcher">Whether the old form is the old launcher's alone: at <c>daoris</c> its first word is a doctrine command.</param>
public sealed record TreeMove(string From, string To, string? Bare = null, string? Append = null, bool Launcher = false);

/// <summary>A flag with one name now, by the verb that takes it.</summary>
public sealed record TreeFlag(string Verb, string From, string To);

/// <summary>
/// The words of Daoris's one command tree (ONEWAY1ai-i, D228; the oneway design §11): <c>daoris …</c> for everything, its doctrine commands
/// the CLI's and every group the desktop's program's. Declared here and in the driver tests' <c>fixtures/command-tree.json</c>, which the
/// CLI's table is held to as well; Loop's <c>CommandTree</c> routes by them.
/// </summary>
public static class CommandWords
{
    /// <summary>The one name a person and a session type.</summary>
    public const string Name = "daoris";

    /// <summary>The variable a program sets on the other when it hands words across, so a word handed once is never handed back.</summary>
    public const string HandedVariable = "DAORIS_HANDED";

    /// <summary>The variable the old launcher sets, so its words are refused naming their new form.</summary>
    public const string OldNameVariable = "DAORIS_OLD_NAME";

    /// <summary>The old launcher's name, which no one types now.</summary>
    public const string OldName = "daoris-driver";

    /// <summary>The CLI's commands, offline, at the root.</summary>
    public static IReadOnlyList<string> Doctrine { get; } = ["analyze", "check", "doctor", "index", "init", "reconcile", "status", "sync", "upstream"];

    public static IReadOnlyList<TreeGroup> Groups { get; } =
    [
        new("agent", ["list", "install", "update", "login", "key", "pin", "unpin", "which", "trust", "settings", "rules", "account"])
        {
            Nested = new Dictionary<string, IReadOnlyList<string>> { ["account"] = ["list", "add", "remove", "default", "order", "use", "ready", "rename", "join"] },
        },
        new("ask", ["publish", "close", "delete", "pause", "resume", "abandon", "clear", "go-ahead", "review", "workflow", "channel", "at-work"], Takes: "\"…\""),
        new("ask-daoris", ["list", "resume", "rename", "pin", "unpin", "delete", "mode"]),
        new("brainstorm", ["show", "send", "stop", "clear", "list", "measure"], Takes: "\"<question>\""),
        new("browser", ["favorite", "extensions", "use", "links"])
        {
            Nested = new Dictionary<string, IReadOnlyList<string>> { ["favorite"] = ["list", "add", "remove"] },
        },
        new("chat", [], Whole: true),
        new("convert", [], Whole: true),
        new("drive", [], Whole: true),
        new("gate", ["run", "verify"]),
        new("git", ["branches", "branch", "tag", "fetch", "pick", "revert", "rebase", "review", "identity", "use", "access", "asks", "answer"], Whole: true),
        new("history", ["clear"], Takes: "(read)", Whole: true),
        new("integrate", [], Whole: true),
        new("logs", [], Whole: true),
        new("notices", [], Whole: true),
        new("place", ["list", "drive", "undrive", "hold", "resume", "trees", "line", "landing", "work-cycle", "across", "standing", "language", "review",
            "opinion", "helpers", "commits", "access", "heavy", "checks"], Takes: "<repository>…"),
        new("plugin", ["list", "add", "update", "remove", "enable", "disable", "new", "try", "install", "show", "activity", "approve", "withdraw"]),
        new("press", ["result", "withdraw"]),
        new("quest", ["pause", "resume", "abandon", "delete", "accept", "done", "reopen", "go-on", "check", "review", "workflow", "clear", "retry"]),
        new("remote", ["list", "add", "remove", "sync"])
        {
            Nested = new Dictionary<string, IReadOnlyList<string>> { ["sync"] = ["status", "dismiss", "forward"] }, // optional-locks: no git runs; the remotes' sync sub-verbs
        },
        new("repository", ["connect", "register", "retire", "wire", "import"]),
        new("role", ["set", "remove", "default", "policy", "brief"], Takes: "[<id>]"),
        new("session", ["list", "stop", "finish", "decline", "archive", "unarchive", "delete", "say", "take-back", "reword", "go-on-new", "start-from",
            "hand-over", "helper", "partner", "handback", "opinion", "supervise", "at-work"])
        {
            Nested = new Dictionary<string, IReadOnlyList<string>> { ["opinion"] = ["ask", "show", "stop", "anyway", "myself"] },
        },
        new("settings", ["notify", "strikes", "cooloff", "keep", "cap", "handover", "brainstorms", "timeout", "adapter", "intake", "helper", "actor"]),
        new("space", ["free"], Takes: "(read)", Whole: true),
        new("steward", ["acts", "drive", "undrive", "hold", "resume", "trees", "stop", "finish", "retry", "desk", "request"], Whole: true),
        new("tool", ["list", "path", "use", "download", "update", "delete", "locations", "look", "git"], Whole: true),
        new("trace", [], Whole: true),
        new("tree", ["list", "remove", "clean", "land", "hand", "state", "sync"]),
        new("update", [], Whole: true),
        new("wiring", [], Whole: true),
        new("work", ["state", "held", "recovery"], Whole: true),
        new("workflow", ["show", "list", "defaults", "new", "edit", "apply", "export", "import", "use", "kind", "run", "keep", "skip", "stop", "changes"]),
    ];

    /// <summary>Each old form, refused naming the form it is now; the longest that matches is taken.</summary>
    // transitional: old-command-names
    public static IReadOnlyList<TreeMove> Moved { get; } =
    [
        .. new[] { "list", "drive", "undrive", "hold", "resume", "trees", "line", "landing", "work-cycle", "across", "standing", "language", "review",
            "opinion", "helpers", "commits" }.Select(verb => new TreeMove($"driver {verb}", $"place {verb}")),
        .. new[] { "notify", "strikes", "cooloff", "keep", "cap", "handover", "brainstorms", "timeout", "adapter", "intake", "helper", "actor" }
            .Select(verb => new TreeMove($"driver {verb}", $"settings {verb}")),
        new("driver retry", "quest retry"),
        new("driver role", "role"),
        new("driver workflow", "workflow"),
        new("driver", "help"),
        new("sessions", "session", Bare: "list"),
        new("trees", "tree", Bare: "list"),
        new("plugins", "plugin"),
        .. new[] { "list", "resume", "rename", "pin", "unpin", "delete", "mode" }.Select(verb => new TreeMove($"help {verb}", $"ask-daoris {verb}")),
        new("run-heavy", "gate run"),
        new("verify", "gate verify"),
        new("opinion", "session opinion"),
        new("answer", "session say"),
        new("result", "press result"),
        new("withdraw", "press withdraw"),
        new("register", "repository register"),
        new("connect", "repository connect"),
        new("retire", "repository retire"),
        new("wire", "repository wire"),
        new("import", "repository import"),
        new("access", "place access"),
        new("heavy", "place heavy"),
        new("checks", "place checks"),
        new("roles brief", "role brief"),
        new("meeting ask", "brainstorm", Append: "--kind meeting"),
        new("meeting", "brainstorm", Append: "--kind meeting"),
        new("workflow convert", "convert"),
        new("agent profile", "agent account"),
        new("--once", "drive --once"),
        new("--until-idle", "drive --until-idle"),
        .. AskActs.Select(act => new TreeMove($"ask {act.Value}", $"ask {act.Key}")),
        new("sync", "remote sync", Launcher: true),
    ];

    /// <summary>Each flag with one name now, by the verb that takes it.</summary>
    public static IReadOnlyList<TreeFlag> Flags { get; } =
    [
        new("chat", "--adapter", "--agent"),
        new("chat", "--profile", "--account"),
        new("agent login", "--profile", "--account"),
        new("agent trust", "--profile", "--account"),
        new("agent settings", "--profile", "--account"),
        new("session opinion ask", "--reviewer", "--agent"),
        new("place opinion", "--reviewers", "--agents"),
    ];

    /// <summary>An ask's acts, each the sub-verb a person types and the word the ask's command reads.</summary>
    internal static IReadOnlyDictionary<string, string> AskActs => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["publish"] = "--publish", ["close"] = "--close", ["delete"] = "--delete", ["pause"] = "--pause", ["resume"] = "--resume",
        ["abandon"] = "--abandon", ["clear"] = "--clear", ["go-ahead"] = "--go-ahead", ["review"] = "--set-review", ["workflow"] = "--set-workflow",
    };

    /// <summary>
    /// The CLI an install carries, found from the desktop's program's own folder (<c>app/daoris-driver/</c>): the publish's
    /// <c>app/cli/node_modules/daoris/bin/daoris.mjs</c> (<c>CLI_PACKAGE</c> and <c>CLI_ENTRY</c>); null where none is there.
    /// </summary>
    public static string? CarriedCli(string programFolder)
    {
        var entry = Path.GetFullPath(Path.Combine(programFolder, "..", "cli", "node_modules", "daoris", "bin", "daoris.mjs"));
        return File.Exists(entry) ? entry : null;
    }

    /// <summary>What a doctrine command says where no CLI is carried beside this program.</summary>
    public static string NoCli(string command) =>
        $"`{command}` is a doctrine command, which the CLI runs, and this program carries none beside it (an install's app/cli/): "
        + "run the CLI's own `daoris` (the npm package) for it; nothing was done.";

    /// <summary>What a doctrine command says where the tools' Node.js cannot run it, with the tools' reason where they gave one.</summary>
    public static string NoNode(string? why) =>
        $"the doctrine commands run on Node.js 22 or later, and none runs here: {(why ?? "`node` is not on this machine's PATH").TrimEnd('.')}; nothing was done.";

    /// <summary>
    /// The words a lease and a refusal name a command by: <c>daoris</c>, its group, and its sub-verb where the second word is one;
    /// never an argument.
    /// </summary>
    public static string Named(IReadOnlyList<string> typed)
    {
        if (typed.Count == 0) return Name;
        var group = Group(typed[0]);
        return group is not null && typed.Count > 1 && group.Verbs.Contains(typed[1], StringComparer.Ordinal)
            ? $"{Name} {typed[0]} {typed[1]}"
            : $"{Name} {typed[0]}";
    }

    /// <summary>
    /// How a person types a command whose own words are an old form inside the host (<c>driver opinion</c> is <c>place opinion</c>), so
    /// its refusals name the tree's form; the words themselves where they moved nowhere.
    /// </summary>
    public static string Now(string old) => Moved.FirstOrDefault(move => move.From == old && !move.Launcher)?.To ?? old;

    /// <summary>The group named <paramref name="name"/>, or null.</summary>
    public static TreeGroup? Group(string? name) => Groups.FirstOrDefault(group => group.Name == name);

    /// <summary>
    /// What an old form is now, said as its refusal, or null where the words are no old form. <paramref name="launcher"/> reads them as the
    /// old launcher's words, where <c>sync</c> is the remotes' and not the doctrine's.
    /// </summary>
    public static string? MovedTo(IReadOnlyList<string> args, bool launcher)
    {
        var said = launcher ? OldName : Name;
        var move = Moved
            .Where(entry => (launcher || !entry.Launcher) && Starts(args, entry.From))
            .OrderByDescending(entry => entry.From.Split(' ').Length)
            .FirstOrDefault();
        if (move is null) return null;
        if (move.To == "help") return $"`{said} {move.From}` is no command now: its words are the tree's groups, which `{Name} help` names; nothing was done.";
        var length = move.From.Split(' ').Length;
        var to = move.Bare is { } bare && (args.Count == length || args[length].StartsWith('-')) ? $"{move.To} {bare}" : move.To;
        var shape = move.Append is { } append ? $"`{Name} {to} … {append}`" : $"`{Name} {to}`";
        var rest = args.Count > length || move.Append is not null ? ", with the same words after it" : "";
        return $"`{said} {move.From}` is {shape} now{rest}; nothing was done.";
    }

    /// <summary>The refusal the old launcher says for its words (the design §11): their new form, and nothing done.</summary>
    // transitional: old-command-names
    public static string OldLauncher(IReadOnlyList<string> args)
    {
        if (MovedTo(args, launcher: true) is { } moved) return moved;
        if (args.Count > 0 && (Group(args[0]) is not null || args[0] == "help"))
        {
            return $"`{OldName} {args[0]}` is `{Name} {args[0]}` now, with the same words after it; nothing was done.";
        }

        return $"`{OldName}` is `{Name}` now: `{Name} help` names each command; nothing was done.";
    }

    /// <summary>A flag's old name among the words, said as its refusal naming the new; null where none is.</summary>
    public static string? FlagMoved(IReadOnlyList<string> args)
    {
        foreach (var flag in Flags)
        {
            if (Starts(args, flag.Verb) && args.Skip(flag.Verb.Split(' ').Length).Contains(flag.From, StringComparer.Ordinal))
            {
                return $"`{flag.From}` is `{flag.To}` now: `{Name} {flag.Verb} … {flag.To} …`; nothing was done.";
            }
        }

        return null;
    }

    /// <summary>Whether the words begin with <paramref name="words"/>, word by word.</summary>
    public static bool Starts(IReadOnlyList<string> args, string words)
    {
        var parts = words.Split(' ');
        return args.Count >= parts.Length && parts.Select((part, at) => args[at] == part).All(same => same);
    }
}
