using System.Text;

namespace Daoris.Driver;

/// <summary>One repository as Ask Daoris's room tells it: how it is driven, its line, how its work lands.</summary>
/// <param name="Name">The registry's name for it.</param>
/// <param name="Workspace">Its circle.</param>
public sealed record HelpRepository(string Name, string Workspace)
{
    /// <summary>Whether it has a checkout on this machine.</summary>
    public bool Checkout { get; init; }

    public bool Drivable { get; init; }

    public bool Held { get; init; }

    public bool OwnTree { get; init; }

    public Line Line { get; init; } = new(null, LineSource.None);

    public Landing Landing { get; init; } = new(LandingRule.Merge, LandingSource.Default);
}

/// <summary>One agent as the room tells it: installed or not, and who is signed in, by the tool's own answer.</summary>
/// <param name="Name">The harness's name, as `daoris driver` takes it.</param>
public sealed record HelpAgent(string Name)
{
    /// <summary>What a person calls the tool, where it says.</summary>
    public string? Product { get; init; }

    public bool Present { get; init; }

    public string? Version { get; init; }

    /// <summary>The tool's own home's sign-in: `in`, `out` or `unknown`.</summary>
    public string Login { get; init; } = "unknown";

    public IReadOnlyList<HelpAccount> Accounts { get; init; } = [];
}

/// <summary>An account Daoris keeps for an agent, by its name — never its key (AGT3).</summary>
public sealed record HelpAccount(string Name, string Login);

/// <summary>What the room says this machine holds now: names and states, never a key, never a path.</summary>
public sealed record HelpMachine
{
    public IReadOnlyList<HelpRepository> Repositories { get; init; } = [];

    public IReadOnlyList<HelpAgent> Agents { get; init; } = [];

    /// <summary>The harness quests run on.</summary>
    public string? Adapter { get; init; }

    /// <summary>The harness asks are answered on, or null for none (INT4b).</summary>
    public string? Intake { get; init; }

    /// <summary>The harness Ask Daoris runs on (D89).</summary>
    public string? Helper { get; init; }

    public int Cap { get; init; }

    /// <summary>How many sessions wait on the person.</summary>
    public int Waiting { get; init; }

    /// <summary>How many asks wait for the person's answer.</summary>
    public int Asks { get; init; }
}

/// <summary>
/// Ask Daoris's room (HELP1a, D89): the folder under Daoris's home its conversation runs in, written
/// from the machine at every open — what Daoris is, which screen and which terminal command does each
/// thing (D50), and what this machine holds now.
/// </summary>
/// <remarks>
/// <para><b>Daoris's own directory</b>, the intake's rule (D65 §1b): the one kind of tree the driver may
/// write, since no one else owns it. Never a session tree, and never asked about by git — it is no
/// repository, and git asked about it walks UP.</para>
///
/// <para><b>It reads, and it advises.</b> Its allow-list reads the family and nothing else, and over
/// the protocol door anything unlisted is refused by construction (D52). A change is the person's,
/// on a screen or at a terminal; proposing one for the person to confirm is HELP1c's.</para>
/// </remarks>
public static class HelpRoom
{
    /// <summary>The folder under the home that is the room.</summary>
    public const string Folder = "help";

    /// <summary>
    /// The "repository" its sessions are recorded in: a colon is in no folder name, so no registered
    /// repository is ever called this.
    /// </summary>
    /// <remarks>A twin (`.claude/knowledge/twins.md`): the service's <c>SessionLedger.HelpRepository</c>
    /// and the page's <c>HELP_REPOSITORY</c> spell it too, and each side's test holds the spelling.</remarks>
    public const string Repository = "daoris:help";

    /// <summary>
    /// The mode its session asks for on the protocol door: the agent's own asking mode, whatever it
    /// started in, where the agent offers it.
    /// </summary>
    /// <remarks>
    /// 🔴 Since D81 a session runs in its harness's own <c>auto</c> mode, which judges each action
    /// itself — and the first real conversation ran shell commands under it, reading a checkout to
    /// research its answer. A helper that can run a command can run <c>daoris driver …</c>, a change
    /// nobody confirmed (D89). In this mode every tool off the room's allow-list asks, and over the
    /// protocol door every ask is refused by construction (D52). Never wider than the agent's default.
    /// </remarks>
    public const string Posture = "default";

    /// <summary>The tools the room allows: reading the family, and nothing that writes anywhere.</summary>
    public static readonly IReadOnlyList<string> Allowed =
    [
        $"mcp__{KnowledgeConnector.ServerName}__registry",
        $"mcp__{KnowledgeConnector.ServerName}__knowledge_search",
        $"mcp__{KnowledgeConnector.ServerName}__knowledge_get",
        $"mcp__{KnowledgeConnector.ServerName}__knowledge_repositories",
        $"mcp__{KnowledgeConnector.ServerName}__quest_list",
        // It proposes, and the person applies (HELP1c, D89). Never `permission_propose`: PERM2 applies a
        // narrowing at the tick with nobody's press, and every change here is the person's.
        $"mcp__{KnowledgeConnector.ServerName}__setting_propose",
        $"mcp__{KnowledgeConnector.ServerName}__ask_propose",
    ];

    public static string PathOf(string home) => Path.Combine(home, Folder);

    /// <summary>Write the room from the machine as it stands, and answer where it is.</summary>
    public static string Prepare(string home, HelpMachine machine)
    {
        var room = PathOf(home);
        Directory.CreateDirectory(Path.Combine(room, ".claude"));
        AtomicFile.WriteText(Path.Combine(room, "AGENTS.md"), Render(machine));
        // Two harnesses read AGENTS.md; Claude Code reads CLAUDE.md — the canon's own shape (D59).
        AtomicFile.WriteText(Path.Combine(room, "CLAUDE.md"), "@AGENTS.md\n");
        AtomicFile.WriteText(Path.Combine(room, ".claude", "settings.json"), Settings());
        return room;
    }

    /// <summary>
    /// What its session is handed at spawn (PERM1): the room's allows and a read of the files its
    /// conversation keeps (CONV4c) — never the person's own allows, which are for work in a repository —
    /// and every deny the person wrote, which still wins.
    /// </summary>
    public static RuleLists Rules(PermissionFile file, string kept)
    {
        var composed = PermissionRules.Compose(file, workspace: null, repository: null);
        return new RuleLists([.. Allowed, PermissionRules.ReadRule(kept)], [], composed.Deny);
    }

    /// <summary>
    /// The machine as the driver already holds it — its file, the registry, each repository's line, the
    /// roster — so the room says what the screens say. Names and states only: no root, no profile's
    /// home, no key.
    /// </summary>
    /// <param name="product">What a person calls a harness's tool, where its toolchain says.</param>
    /// <param name="asks">How many asks wait for the person's answer.</param>
    public static HelpMachine Describe(
        DriverConfig config, Snapshot snapshot, IReadOnlyList<RepositoryLine> lines,
        IReadOnlyList<HarnessReport> roster, Func<string, string?> product, int asks)
    {
        var lineOf = lines.ToDictionary(line => line.Repository, StringComparer.OrdinalIgnoreCase);
        bool Named(IReadOnlyList<string> list, string repository) => list.Contains(repository, StringComparer.OrdinalIgnoreCase);
        static string Spell(LoginState login) => login.ToString().ToLowerInvariant();

        return new HelpMachine
        {
            Adapter = config.Adapter,
            Intake = config.IntakeAdapter,
            Helper = config.HelperAdapter,
            Cap = config.Cap,
            Waiting = snapshot.Active.Count(session => session.State == "awaiting-person"),
            Asks = asks,
            Repositories = [.. snapshot.Repositories.Select(known => new HelpRepository(known.Repository, known.Workspace)
            {
                Checkout = known.Root is { Length: > 0 },
                Drivable = Named(config.Drivable, known.Repository),
                Held = Named(config.Holds, known.Repository),
                OwnTree = config.OpensOwnTree(known.Repository),
                Line = lineOf.TryGetValue(known.Repository, out var line) ? new Line(line.Branch, line.Source) : new Line(null, LineSource.None),
                Landing = LandingRules.Choose(config, known.Repository, known.Workspace),
            })],
            Agents = [.. roster.Select(report => new HelpAgent(report.Adapter)
            {
                Product = product(report.Adapter),
                Present = report.Present,
                Version = report.Version,
                Login = Spell(report.OwnLogin),
                Accounts = [.. report.Profiles.Select(profile => new HelpAccount(profile.Name, Spell(profile.Login)))],
            })],
        };
    }

    public static string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        text.Append("# Ask Daoris\n\n");
        text.Append("This room is Daoris's, not any repository's. A session here talks with the person about Daoris on\n");
        text.Append("this machine: how to set up a workspace, what a screen means, why a start was refused, how to drive\n");
        text.Append("a repository, write a rule, or start a task. Written by Daoris each time the conversation opens; an\n");
        text.Append("edit here is overwritten.\n\n");

        text.Append("## What you may do\n\n");
        text.Append("You read, you advise, and you propose. You change nothing yourself: every change is the person's, made\n");
        text.Append("on a screen or with a terminal command, and the two always do the same thing. When a change would\n");
        text.Append("help, name both — the screen and where on it, and the command — and, where the person wants it made,\n");
        text.Append("propose it: `setting_propose` for one of the doors below that `daoris driver` spells, `ask_propose` to\n");
        text.Append("start something as an ask at a workspace. A proposal is a card with Apply and Not now; nothing\n");
        text.Append("changes until the person presses Apply, and their answer comes back as their next message. Read the\n");
        text.Append("family through `daoris-knowledge` (the registry, its knowledge, its quests) when the question needs\n");
        text.Append("more than this page.\n\n");
        text.Append("Never offer to push, merge, discard, sign in, or handle a key: those stay the person's own presses,\n");
        text.Append("where they already are.\n\n");

        text.Append("## The doors\n\n");
        text.Append("| To | On the screen | At a terminal |\n");
        text.Append("|---|---|---|\n");
        foreach (var (to, screen, terminal) in Doors) text.Append($"| {to} | {screen} | {terminal} |\n");
        text.Append('\n');
        // What `LandingRules` accepts, said here: the first real conversation had to guess it (HELP1a).
        text.Append("A landing pattern may say `{quest}`, `{session}`, `{slug}` (the quest's title, as words) and\n");
        text.Append("`{repository}`. It needs `{quest}` or `{session}`, or every session's work would land on one branch,\n");
        text.Append("and git must take what it comes out as: `feature/{quest}-{slug}` is a pattern that works.\n\n");

        text.Append("## This machine, now\n\n");
        text.Append(machine.Adapter is { Length: > 0 } adapter
            ? $"- The driver: quests run on `{adapter}`, up to {machine.Cap} sessions at once.\n"
            : "- The driver: no agent is set for quests.\n");
        text.Append(machine.Intake is { Length: > 0 } intake
            ? $"- The intake: asks are answered on `{intake}`.\n"
            : "- The intake: no agent answers asks, so an ask the declarations do not settle waits for the person.\n");
        if (machine.Helper is { Length: > 0 } helper) text.Append($"- Ask Daoris: you, on `{helper}`.\n");
        text.Append($"- {Count(machine.Waiting, "session waits", "sessions wait")} on the person; "
            + $"{Count(machine.Asks, "ask waits", "asks wait")} for an answer.\n\n");

        if (machine.Repositories.Count == 0)
        {
            text.Append("No repository is registered on this machine yet. The first step is `daoris connect` from inside\n");
            text.Append("one, or Projects → Import a folder as a workspace, to register a folder of them at once\n");
            text.Append("(`daoris import <folder> --workspace <name>`).\n\n");
        }

        foreach (var circle in machine.Repositories
                     .GroupBy(repository => repository.Workspace, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(circle => circle.Key, StringComparer.Ordinal))
        {
            text.Append($"### Workspace `{circle.Key}`\n\n");
            text.Append("| Repository | Driven | Line | Work lands |\n");
            text.Append("|---|---|---|---|\n");
            foreach (var repository in circle.OrderBy(repository => repository.Name, StringComparer.Ordinal))
            {
                text.Append($"| `{repository.Name}` | {Driven(repository)} | {LineOf(repository.Line)} | {Lands(repository.Landing)} |\n");
            }

            text.Append('\n');
        }

        if (machine.Agents.Count > 0)
        {
            text.Append("### Agents\n\n");
            foreach (var agent in machine.Agents) text.Append($"- {AgentLine(agent)}\n");
            text.Append('\n');
        }

        return text.ToString();
    }

    /// <summary>What each change is, where it is on the screen, and the command that does the same (D50).</summary>
    private static readonly (string To, string Screen, string Terminal)[] Doors =
    [
        ("drive a repository, or stop", "Projects", "`daoris driver drive|undrive <repository>`"),
        ("pause one, or release it", "Projects", "`daoris driver hold|resume <repository>`"),
        ("give its sessions their own tree", "Projects", "`daoris driver trees <repository> on|off`"),
        ("set the line its work grows from and lands on", "Settings → Workspace → Lines",
            "`daoris driver line <repository> <branch>|--clear` (`--workspace <name>` for a whole workspace)"),
        ("set how accepted work lands", "Settings → Workspace → How work lands",
            "`daoris driver landing <repository> merge|branch <pattern>|--clear` (`--workspace <name>`, `--tidy`)"),
        ("clean up session branches whose work landed", "Settings → Workspace → Session branches", "`daoris-driver trees clean`"),
        ("choose the agent that answers asks", "Settings → Daoris's own AI", "`daoris driver intake <agent>|off`"),
        ("choose the agent Ask Daoris runs on", "Settings → Daoris's own AI", "`daoris driver helper <agent>|off`"),
        ("park a quest after failed sessions", "Settings → Driver", "`daoris driver strikes <n>`"),
        ("bound how long one session runs", "(no screen yet)", "`daoris driver timeout <minutes>`"),
        ("say so when a session parks", "Settings → Driver", "`daoris driver notify on|off`"),
        ("allow, ask or deny what an agent may do", "Settings → Permissions", "`daoris agent rules …`"),
        ("sign an agent in, or add an account", "Settings → Agents & accounts", "`daoris agent login <agent>`"),
        ("start a task", "Quests → ask for something", "`daoris-driver ask --workspace <name> \"…\"`"),
        ("answer what waits on the person", "Sessions, and what needs you", "`daoris-driver answer`"),
    ];

    private static string Driven(HelpRepository repository)
    {
        var parts = new List<string> { repository.Drivable ? "driven" : "not driven" };
        if (repository.Held) parts.Add("held");
        if (repository.Drivable && repository.OwnTree) parts.Add("in its own tree");
        if (!repository.Checkout) parts.Add("no checkout here");
        return string.Join(", ", parts);
    }

    private static string LineOf(Line line) => line.Branch is not { Length: > 0 } branch
        ? "none git can name"
        : line.Source switch
        {
            LineSource.Repository => $"`{branch}` (set for it)",
            LineSource.Workspace => $"`{branch}` (set for its workspace)",
            _ => $"`{branch}` (the checkout's own)",
        };

    private static string Lands(Landing landing)
    {
        var rule = landing.Rule.Form == "branch"
            ? $"on a branch `{landing.Rule.Pattern}`"
            : "merged into the line";
        if (landing.Rule.Tidy) rule += ", tree removed once landed";
        var source = landing.Source switch
        {
            LandingSource.Repository => "set for it",
            LandingSource.Workspace => "its workspace's rule",
            _ => "the default",
        };
        return $"{rule} ({source})";
    }

    private static string AgentLine(HelpAgent agent)
    {
        if (!agent.Present) return $"`{agent.Name}`: not installed";

        var named = string.Join(" ", new[] { agent.Product, agent.Version }.Where(part => part is { Length: > 0 }));
        var line = named.Length > 0 ? $"`{agent.Name}` ({named}): " : $"`{agent.Name}`: ";
        line += Login(agent.Login);
        foreach (var account in agent.Accounts) line += $"; account `{account.Name}` {Login(account.Login)}";
        return line;
    }

    private static string Login(string login) => login switch
    {
        "in" => "signed in",
        "out" => "signed out",
        _ => "sign-in not known",
    };

    private static string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

    /// <summary>The allow-list, as the harness reads a project's settings.</summary>
    private static string Settings()
    {
        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("permissions");
            writer.WriteStartArray("allow");
            foreach (var rule in Allowed) writer.WriteStringValue(rule);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }
}
