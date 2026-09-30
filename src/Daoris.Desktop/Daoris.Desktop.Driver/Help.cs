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

/// <summary>An ask not closed, as the room lists it (HELP6): by id, so a delete of one made by mistake can name it.</summary>
public sealed record HelpAsk(string Id, string Sentence, string Workspace, string State, IReadOnlyList<string> Quests);

/// <summary>A plugin installed here, as the room lists it (PLUG9): by id and state, so a switch names one the catalogue holds.</summary>
/// <param name="Points">The points it speaks on; empty where it speaks on none, or is refused.</param>
public sealed record HelpPlugin(string Id, bool Enabled, IReadOnlyList<string> Points)
{
    /// <summary>Why it contributes nothing, in the catalogue's words; null when sound.</summary>
    public string? Problem { get; init; }

    /// <summary>Where it came from (PLUG9 c): `folder`, `offer`, or null for none recorded — never the path itself.</summary>
    public string? Source { get; init; }
}

/// <summary>
/// One of Daoris's own plugins the install offers and this machine has not installed (PLUG9 d), as the room
/// lists it: by id, with what it speaks on and what it needs, so an add can name it — never by a path.
/// </summary>
public sealed record HelpOffer(string Id, string Name, string Version, IReadOnlyList<string> Points, IReadOnlyList<string> Needs)
{
    /// <summary>The harnesses it declares and the servers it hands every session, by name.</summary>
    public IReadOnlyList<string> Harnesses { get; init; } = [];

    public IReadOnlyList<string> Servers { get; init; } = [];
}

/// <summary>A branch a landing made here, as the room lists it (WSR5b): by name and session, so a hand-off names one the record holds.</summary>
public sealed record HelpLanded(string Repository, string Branch, string Session, bool Pushed, string? PullRequest);

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

    /// <summary>The asks not closed, newest first (HELP6).</summary>
    public IReadOnlyList<HelpAsk> OpenAsks { get; init; } = [];

    /// <summary>The plugins a branch rule may name here, by id (HELP8, D100).</summary>
    public IReadOnlyList<string> LandingPlugins { get; init; } = [];

    /// <summary>Every plugin installed here, sound or not, in the catalogue's order (PLUG9).</summary>
    public IReadOnlyList<HelpPlugin> Plugins { get; init; } = [];

    /// <summary>The branches this machine's landings made and recorded, in the order they landed (WSR5b).</summary>
    public IReadOnlyList<HelpLanded> Landed { get; init; } = [];
    /// <summary>The install's own plugins not installed here and sound, which an add may name by id (PLUG9 d).</summary>
    public IReadOnlyList<HelpOffer> Offers { get; init; } = [];
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
/// <para><b>It reads, and it advises.</b> Its allow-list reads the family and proposes, and nothing
/// else, and over the protocol door anything unlisted is refused by construction (D52). A change is the
/// person's, on a screen or at a terminal; a proposal is a card the person confirms (HELP1c, HELP6).</para>
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
        // HELP6: every door built since, each a card the person applies the same way.
        $"mcp__{KnowledgeConnector.ServerName}__agent_propose",
        $"mcp__{KnowledgeConnector.ServerName}__delete_propose",
        $"mcp__{KnowledgeConnector.ServerName}__agent_settings_propose",
        $"mcp__{KnowledgeConnector.ServerName}__go_propose",
        // PLUG9: adding a plugin that has landed, or switching one; making one is an ask, never this.
        $"mcp__{KnowledgeConnector.ServerName}__plugin_propose",
        // WSR5b: a branch a landing made, handed to a landing plugin afterwards — the review's own press.
        $"mcp__{KnowledgeConnector.ServerName}__hand_propose",
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
    /// <param name="standing">The asks the host answered, listed by id where they are not closed (HELP6).</param>
    /// <param name="offers">The install's own plugins (PLUG9 d); the room lists the sound ones not installed here.</param>
    public static HelpMachine Describe(
        DriverConfig config, Snapshot snapshot, IReadOnlyList<RepositoryLine> lines,
        IReadOnlyList<HarnessReport> roster, Func<string, string?> product, int asks,
        IReadOnlyList<AskView>? standing = null, PluginCatalog? plugins = null, IReadOnlyList<LandedBranch>? landed = null,
        IReadOnlyList<PluginOffer>? offers = null)
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
            OpenAsks = [.. (standing ?? [])
                .Where(ask => !string.Equals(ask.State, "Closed", StringComparison.OrdinalIgnoreCase))
                .Select(ask => new HelpAsk(ask.Id, ask.Sentence, ask.Workspace, ask.State, ask.Quests))],
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
            LandingPlugins = LandingPluginsOf(plugins ?? PluginCatalog.None),
            Plugins = [.. (plugins ?? PluginCatalog.None).Plugins.Select(entry =>
                new HelpPlugin(entry.Manifest.Id, entry.Enabled, entry.Manifest.Hooks?.Points ?? [])
                {
                    Problem = entry.Problem,
                    // Its kind only: the folder it came from is a path on this machine, which the room never names.
                    Source = PluginSource.Read(entry.Folder).Source is { } source ? (source.Offer is not null ? "offer" : "folder") : null,
                })],
            Landed = [.. (landed ?? []).Select(entry => new HelpLanded(entry.Repository, entry.Branch, entry.Session, entry.Pushed, entry.PullRequest))],
            Offers = [.. (offers ?? []).Where(offer => !offer.Installed && offer.Problem is null).Select(offer =>
                new HelpOffer(offer.Id, offer.Manifest.Name, offer.Manifest.Version, offer.Manifest.Hooks?.Points ?? [], offer.Needs)
                {
                    Harnesses = [.. offer.Manifest.Harnesses.Select(harness => harness.Name)],
                    Servers = [.. offer.Manifest.Servers.Select(server => server.Name)],
                })],
        };
    }

    /// <summary>
    /// The plugins a branch rule may name on this machine (HELP8): each one the landing route itself
    /// would take, so the room never lists one a proposal would then be refused for.
    /// </summary>
    public static IReadOnlyList<string> LandingPluginsOf(PluginCatalog catalog) =>
        [.. catalog.Plugins.Select(plugin => plugin.Manifest.Id)
            .Where(id => LandingRules.PluginProblem(id, catalog) is null)
            .Order(StringComparer.Ordinal)];

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
        text.Append("propose it with the tool for its kind (below). A proposal is a card with Apply and Not now; nothing\n");
        text.Append("changes until the person presses Apply, and their answer comes back as their next message. Daoris\n");
        text.Append("checks each proposal first by the rules of the screen that makes the same change, and one those rules\n");
        text.Append("would refuse never reaches the person: you are told why instead. Read the family through\n");
        text.Append("`daoris-knowledge` (the registry, its knowledge, its quests) when the question needs more than this page.\n\n");
        text.Append("Never offer to push, merge, discard, sign in, or handle a key: those stay the person's own presses,\n");
        text.Append("where they already are.\n\n");
        // HELP4: the first repository question met a shell refused before it ran, then a guess at the
        // repository's branches presented as its tree. HELP5: a later one spent a turn on a ticket's URL,
        // refused the same way. Said plainly, so none of them happens again.
        text.Append("You have no shell, no web fetch or search, and you read no checkout: a command, a web page, or a\n");
        text.Append("file outside this room, is refused before it runs, so never try one. What a repository's tree holds\n");
        text.Append("(its branches, its uncommitted work, what is ready to push) is a repository's own work, so route it\n");
        text.Append("there. Propose an ask for it with `ask_propose`, so that repository's agent does it with its own\n");
        text.Append("tools, or tell the person to open a conversation in that repository (Sessions → Start a session).\n");
        text.Append("Where Daoris itself has a door for what they want, name it first: session branches whose work landed,\n");
        text.Append("and branches a landing made whose pull request's work reached the line (a squash merge included),\n");
        text.Append("are cleaned up under Settings → Workspace → Session branches. When the family's knowledge and quests\n");
        text.Append("are all you can see, say what you could not see: never build a repository's state from its quests and\n");
        text.Append("present it as the tree.\n\n");

        // HELP6: every door built since HELP1c, each with the rule its route judges it by, said so the
        // helper proposes what the route takes rather than learning it from a refusal.
        text.Append("## What you may propose\n\n");
        text.Append("- `setting_propose`: one of the doors below that `daoris driver` spells.\n");
        text.Append("- `ask_propose`: something to start, as an ask at a workspace.\n");
        text.Append("- `agent_propose`: an agent's Update, or a pin to one version, as Settings → Agents & accounts offers them.\n");
        text.Append("  Update moves a pinned agent's pin to its newest release, or runs an unpinned one's own updater, and is\n");
        text.Append("  offered only where that screen shows Update; a pin names one exact release, like 2.1.300, never `latest`.\n");
        text.Append("- `agent_settings_propose`: an account's own model and effort, as Settings → Agents & accounts → Model &\n");
        text.Append("  effort sets them, for a tool whose settings Daoris knows and one of Daoris's accounts, never the tool's own\n");
        text.Append("  sign-in. The values are the tool's own: one of its model aliases or a full model id, and an effort of low,\n");
        text.Append("  medium, high or xhigh. `max` is for one conversation, never an account's default; `unset` returns either\n");
        text.Append("  to the tool's own default.\n");
        text.Append("- `delete_propose`: a quest or an ask made by mistake (a duplicate, a test), as the quest's drawer and the\n");
        text.Append("  ask's record delete them. Only an open quest nobody has started on can go, and an ask goes with every quest it became, or not at all.\n");
        text.Append("  Never propose deleting a taken, done or declined quest: its record stays,\n");
        text.Append("  the route refuses it, and declining it with the reason is the way instead. A delete cannot be undone.\n");
        text.Append("- `go_propose`: take the person to a place on the window, from the list below. It changes nothing.\n");
        text.Append("- `plugin_propose`: add a plugin that has landed, from its folder in the checkout of the repository that\n");
        text.Append("  holds it (name the repository and the folder there), or switch one installed here on or off. Daoris copies\n");
        text.Append("  an added plugin into its home under its id, and never replaces one already installed. An `add` may name\n");
        text.Append("  one of Daoris's own plugins by its id instead (`offer`, below), and an `update` (the plugin's `id`) takes a\n");
        text.Append("  newer copy from where an installed one came from, the card saying what changes; one with no record of\n");
        text.Append("  where it came from cannot be updated.\n");
        // WSR5b: the review's own hand-off, for a ticket landed before its workspace named a plugin, or whose plugin failed.
        text.Append("- `hand_propose`: hand a branch a landing made (from the list below) to a landing plugin, which pushes it\n");
        text.Append("  and opens the pull request, signed in as the person. Name the session that landed it or the branch, and\n");
        text.Append("  a plugin only where the repository's landing rule names none. Only a branch a landing made and recorded\n");
        text.Append("  here can be handed on; the person's own branches are theirs to push.\n\n");
        // HELP7: the real helper said *press Apply* of a card whose button reads *go there*.
        text.Append("Each proposal reaches the person as a card with two buttons, and you name them as the card does:\n");
        text.Append("every card but a go reads **apply** and **not now** (in 中文 **应用** and **暂不**), and\n");
        text.Append("a go card reads **go there** and **not now** (in 中文 **前往** and **暂不**).\n\n");

        // PLUG9: a plugin runs as the person, so its making is a repository's work, tested and reviewed,
        // and only its installing is a card here. The points come from the wire's own list.
        text.Append("## Making a plugin\n\n");
        text.Append("A plugin runs on this machine as the person (one that lands work pushes with their sign-in), so making\n");
        text.Append("one is work, not a setting: never write one yourself. Propose it\n");
        text.Append("as an ask (`ask_propose`) at the workspace of the repository that holds plugins, addressed to it by name:\n");
        text.Append("what the plugin should do, and the point it speaks on ("
            + string.Join("; ", HookPoints.All.Select(point => PointSaid.TryGetValue(point, out var said) ? $"`{point}`, {said}" : $"`{point}`"))
            + ").\n");
        text.Append("The session there makes it with its tests, and the person reviews and lands it. Find that repository\n");
        text.Append("in `registry` by what it says it owns. If none says so,\n");
        text.Append("the person decides where plugins live (a repository of their own, connected like any other), so ask\n");
        text.Append("rather than pick one. Once it has landed, propose adding it with `plugin_propose`, from the folder the\n");
        text.Append("session named; never propose adding one that has not landed.\n\n");

        // PLUG9 (d): the install carries Daoris's own example plugins as offers, named by id on the wire.
        if (machine.Offers.Count > 0)
        {
            text.Append("## Daoris's own plugins\n\n");
            text.Append("This install carries plugins of Daoris's own, none of them installed here. Nothing of one runs until the\n");
            text.Append("person installs it, and one that lands work runs only where a branch rule names it. Before making a\n");
            text.Append("plugin, see whether one of these does the job: propose installing it with `plugin_propose` (`add`, and\n");
            text.Append("`offer` its id, never a path), and say what it needs, which the person sets up themselves.\n\n");
            foreach (var offer in machine.Offers) text.Append($"- {OfferLine(offer)}\n");
            text.Append('\n');
        }

        text.Append("## The doors\n\n");
        text.Append("| To | On the screen | At a terminal |\n");
        text.Append("|---|---|---|\n");
        foreach (var (to, screen, terminal) in Doors) text.Append($"| {to} | {screen} | {terminal} |\n");
        text.Append('\n');
        // What `LandingRules` accepts, said here: the first real conversation had to guess it (HELP1a).
        text.Append("A landing pattern may say `{quest}`, `{session}`, `{slug}` (the quest's title, as words) and\n");
        text.Append("`{repository}`. It needs `{quest}` or `{session}`, or every session's work would land on one branch,\n");
        text.Append("and git must take what it comes out as: `feature/{quest}-{slug}` is a pattern that works.\n\n");
        // HELP8: a branch rule may name a plugin (D100), and the helper can only name one it can see.
        text.Append("A branch rule may add `--plugin <id>`: once Daoris has made the branch, that plugin pushes it and\n");
        text.Append("opens the pull request, as the person's own platform tools are signed in.\n");
        var offeredLanders = machine.Offers.Where(offer => offer.Points.Contains(HookPoints.Land, StringComparer.Ordinal)).Select(offer => $"`{offer.Id}`").ToList();
        text.Append(machine.LandingPlugins.Count > 0
            ? $"Plugins that can land work here: {string.Join(", ", machine.LandingPlugins.Select(id => $"`{id}`"))}.\n\n"
            : "No plugin that lands work is installed here: the person installs one (`daoris plugin add <folder>`,\n"
              + "Settings → Plugins), so never propose a rule naming one"
              + (offeredLanders.Count > 0
                  ? $" until it is installed; this install offers {string.Join(" and ", offeredLanders)}, which you may propose installing first.\n\n"
                  : ".\n\n"));

        // HELP6: the places a go may name, from the table the driver judges one by, so the two cannot disagree.
        text.Append("## Where you may take the person\n\n");
        text.Append("`go_propose` opens one of these places and changes nothing; the person does the rest there. Name the\n");
        text.Append("view, for Settings its domain, and a part where the place has one.\n\n");
        static string Listed(IEnumerable<(string Id, string Name)> places) =>
            string.Join(", ", places.Select(place => $"`{place.Id}` ({place.Name})"));
        text.Append($"- Views: {Listed(HelpPlaces.Views)}.\n");
        text.Append($"- Settings domains: {Listed(HelpPlaces.Domains)}.\n");
        foreach (var within in HelpPlaces.Parts.Select(part => part.Within).Distinct())
        {
            var parts = HelpPlaces.Parts.Where(part => part.Within == within).Select(part => (part.Id, part.Name));
            text.Append(within == "start"
                ? $"- Parts of `start`, the setup guide's steps: {Listed(parts)}.\n"
                : $"- Parts of `{within}`: {Listed(parts)}.\n");
        }

        text.Append('\n');

        // How the window is laid out (HELP2): asked what the panel held, the helper guessed at a menu that
        // does not exist. Said by the names the window's own labels use (DOCK1b), keys as its menus show them.
        text.Append("## The window\n\n");
        text.Append("The desktop is laid out as VS Code is. The activity bar at the left holds the views: Overview,\n");
        text.Append("Sessions, Quests, Projects, Map, Convergence and Search, with Settings at its foot; `Ctrl+K` opens\n");
        text.Append("the command palette. Every view sits in one frame: the view in the centre, the panel beneath it,\n");
        text.Append("and the right side bar beside it; on Sessions the session list is at the left and the centre is the\n");
        text.Append("attended session. Five views stand in the two regions and move between them:\n");
        text.Append("the timeline, the review and the console of the session attended on Sessions, Ask Daoris, and the\n");
        text.Append("terminal: the person's own shell (PowerShell unless they choose another), in the panel beside the\n");
        text.Append("console, which starts where the attended session works. A session's console takes no typing; the\n");
        text.Append("terminal is the person's, never a session's.\n");
        text.Append("A view moves from its region's tab list (the button at the end of the tab row), by a right-click on\n");
        text.Append("its tab, or by dragging its tab to the other region; View → Reset view locations puts every view\n");
        text.Append("back. The toggles beside the window controls, and the View menu, show or hide the panel (`Ctrl+J`)\n");
        text.Append("and the right side bar (`Ctrl+Alt+B`) on every view, and the session list (`Ctrl+B`) on Sessions.\n");
        text.Append("You open on `F1` or `Ctrl+Alt+I`, and Quick Ask, a box where the palette opens, on\n");
        text.Append("`Ctrl+Shift+Alt+L`.\n\n");
        text.Append("You cannot see the window. Where the person is, and where the views stand, comes with their\n");
        text.Append("message when it changed; for anything else on the screen, ask them rather than guess.\n\n");

        text.Append("## This machine, now\n\n");
        text.Append(machine.Adapter is { Length: > 0 } adapter
            ? $"- The driver: quests run on `{adapter}`, up to {machine.Cap} sessions at once.\n"
            : "- The driver: no agent is set for quests.\n");
        text.Append(machine.Intake is { Length: > 0 } intake
            ? $"- The intake: asks are answered on `{intake}`.\n"
            : "- The intake: no agent answers asks, so an ask the declarations do not settle waits for the person.\n");
        if (machine.Helper is { Length: > 0 } helper) text.Append($"- Ask Daoris: you, on `{helper}`.\n");
        text.Append($"- {Count(machine.Waiting, "session waits", "sessions wait")} on the person; "
            + $"{Count(machine.Asks, "ask waits", "asks wait")} for an answer.\n");
        // PLUG9: by id and state, so a switch names one the catalogue holds.
        text.Append(machine.Plugins.Count > 0
            ? $"- Plugins: {string.Join(", ", machine.Plugins.Select(PluginLine))}.\n"
            : "- Plugins: none installed.\n");
        // WSR5b: by name and session, so a hand-off names one the record holds.
        text.Append(machine.Landed.Count > 0
            ? $"- Branches landings made: {string.Join(", ", machine.Landed.Select(LandedLine))}.\n\n"
            : "- Branches landings made: none recorded.\n\n");

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

        // HELP6: by id, so a delete of one made by mistake can name it. The quests are the family's
        // `quest_list`; the asks are this machine's alone.
        if (machine.OpenAsks.Count > 0)
        {
            text.Append("### Asks\n\n");
            foreach (var ask in machine.OpenAsks)
            {
                var quests = ask.Quests.Count > 0 ? $"; quests {string.Join(", ", ask.Quests.Select(quest => $"`#{quest}`"))}" : "";
                text.Append($"- `#{ask.Id}` at `{ask.Workspace}`: “{Clipped(ask.Sentence)}” ({ask.State}{quests})\n");
            }

            text.Append('\n');
        }

        return text.ToString();
    }

    /// <summary>What each change is, where it is on the screen, and the command that does the same (D50).</summary>
    private static readonly (string To, string Screen, string Terminal)[] Doors =
    [
        // SETUP1b: the page hands the helper a first message asking to be walked through it, naming the
        // steps by these titles, so it is told the guide exists and what each step is done on.
        ("walk through setting this machine up: an agent, Daoris's own agent, a workspace and its repositories, "
            + "what is driven, how work lands, what agents may do", "Settings → Get started (the Daoris menu's *Set up Daoris*)",
            "(each step shows its own command there)"),
        ("drive a repository, or stop", "Projects", "`daoris driver drive|undrive <repository>`"),
        ("pause one, or release it", "Projects", "`daoris driver hold|resume <repository>`"),
        ("give its sessions their own tree", "Projects", "`daoris driver trees <repository> on|off`"),
        ("set the line its work grows from and lands on", "Settings → Workspace → Lines",
            "`daoris driver line <repository> <branch>|--clear` (`--workspace <name>` for a whole workspace)"),
        ("set how accepted work lands", "Settings → Workspace → How work lands",
            "`daoris driver landing <repository> merge|branch <pattern>|--clear` (`--workspace <name>`, `--tidy`, "
            + "and on a branch `--plugin <id>`: an installed plugin that pushes it and opens the pull request)"),
        ("clean up session branches whose work landed, and branches a landing made whose work reached the line",
            "Settings → Workspace → Session branches", "`daoris-driver trees clean`"),
        // WSR5b: a landed branch handed to a landing plugin after its landing.
        ("hand a branch a landing made to a landing plugin, to push it and open the pull request",
            "Sessions → the session's review → hand it to <plugin>", "`daoris-driver trees hand <session|branch> [--plugin <id>]`"),
        ("choose the agent that answers asks", "Settings → Daoris's own AI", "`daoris driver intake <agent>|off`"),
        ("choose the agent Ask Daoris runs on", "Settings → Daoris's own AI", "`daoris driver helper <agent>|off`"),
        ("park a quest after failed sessions", "Settings → Driver", "`daoris driver strikes <n>`"),
        ("bound how long one session runs", "(no screen yet)", "`daoris driver timeout <minutes>`"),
        ("say so when a session parks", "Settings → Driver", "`daoris driver notify on|off`"),
        ("allow, ask or deny what an agent may do", "Settings → Permissions", "`daoris agent rules …`"),
        ("sign an agent in, or add an account", "Settings → Agents & accounts", "`daoris agent login <agent>`"),
        // HELP6: the doors built since, which the helper now proposes too.
        ("update an agent, or pin it to one version", "Settings → Agents & accounts → Update, Pin a version",
            "`daoris agent update <agent>`, `daoris agent pin <agent> <version>`"),
        ("set an account's own model and effort", "Settings → Agents & accounts → Model & effort",
            "`daoris agent settings <agent> --account <name> model <model> effort <effort>`"),
        ("delete a quest or an ask made by mistake", "Quests → the quest's drawer, or the ask's record → delete",
            "`daoris-driver quest delete <id>`, `daoris-driver ask --delete <id>`"),
        // PLUG9: the card installs one that landed; the screen switches one installed, installs one of
        // Daoris's own (d) and updates one from where it came from (c).
        ("add a plugin that has landed, or switch one on or off", "Settings → Plugins (its switch)",
            "`daoris plugin add <folder>`, `daoris plugin enable|disable <id>`"),
        ("install one of Daoris's own plugins, or update one from where it came from",
            "Settings → Plugins (Install beside Daoris's own; Update on an installed one's row)",
            "`daoris plugin add --offer <id>`, `daoris plugin update <id>`"),
        ("start a task", "Quests → ask for something", "`daoris-driver ask --workspace <name> \"…\"`"),
        ("answer what waits on the person", "Sessions, and what needs you", "`daoris-driver answer`"),
    ];

    /// <summary>What each point a plugin speaks on is for, said where the room tells how a plugin is made (PLUG9).</summary>
    private static readonly IReadOnlyDictionary<string, string> PointSaid = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [HookPoints.QuestConsider] = "to hold a quest before it starts",
        [HookPoints.SessionEnded] = "to hear how a session ended",
        [HookPoints.Land] = "to push a branch Daoris made and open its pull request",
    };

    private static string LandedLine(HelpLanded landed) =>
        $"`{landed.Branch}` in `{landed.Repository}` (session `{landed.Session}`, "
        + (landed.Pushed ? "pushed" + (landed.PullRequest is { } pr ? $", pull request {pr}" : "") : "not pushed") + ")";

    private static string PluginLine(HelpPlugin plugin)
    {
        // PLUG9 (c): whether an update has anything to read — its kind, never the path.
        var from = plugin.Source switch
        {
            "offer" => ", installed from this install's offer",
            "folder" => ", added from a folder",
            _ => ", no record of where it came from",
        };
        if (!plugin.Enabled) return $"`{plugin.Id}` (off{from})";
        if (plugin.Problem is { } problem) return $"`{plugin.Id}` (on, contributes nothing: {problem}{from})";
        return plugin.Points.Count > 0
            ? $"`{plugin.Id}` (on, speaks on {string.Join(", ", plugin.Points.Select(point => $"`{point}`"))}{from})"
            : $"`{plugin.Id}` (on{from})";
    }

    /// <summary>An offer as the room lists it: its id, name and version, what it speaks on or hands, and what it needs.</summary>
    private static string OfferLine(HelpOffer offer)
    {
        var titled = string.Join(" ", new[] { offer.Name != offer.Id ? offer.Name : "", offer.Version }.Where(part => part.Length > 0));
        var parts = new List<string>();
        if (offer.Points.Count > 0)
        {
            parts.Add("speaks on " + string.Join(", ", offer.Points.Select(point =>
                PointSaid.TryGetValue(point, out var said) ? $"`{point}`, {said}" : $"`{point}`")));
        }

        if (offer.Harnesses.Count > 0) parts.Add($"declares {string.Join(", ", offer.Harnesses.Select(name => $"`{name}`"))}");
        if (offer.Servers.Count > 0) parts.Add($"hands every session {string.Join(", ", offer.Servers.Select(name => $"`{name}`"))}");
        if (offer.Needs.Count > 0) parts.Add($"needs: {string.Join("; ", offer.Needs)}");
        return $"`{offer.Id}`{(titled.Length > 0 ? $" ({titled})" : "")}: {string.Join("; ", parts)}";
    }

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
        if (landing.Rule.Plugin is { } plugin) rule += $", pushed with a pull request opened by plugin `{plugin}`";
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

    /// <summary>An ask's words on one line, cut where they run long: the id is what a proposal names.</summary>
    private static string Clipped(string sentence)
    {
        var line = string.Join(' ', sentence.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return line.Length <= 120 ? line : line[..119].TrimEnd() + "…";
    }

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
