using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What the helper may do (HELP1a, D89): read, advise and propose, never change a thing itself — and what it
/// cannot reach, so it routes a repository's own work there rather than guessing at it (HELP4, HELP5). Since
/// D107 it may read the checkouts reading across allows, and it says which and how: since HELPMODE1c4 through
/// <c>checkout_read</c>, less those the person turned off.
/// </summary>
/// <remarks>
/// HELPMODE1a1 (D183): all of that is restricted mode's, and the section says so. In full access it says the helper acts on this
/// machine itself, with every server the plugins here declare, and what the doctrine still asks of it (D183 point 5).
/// </remarks>
internal sealed class HelpRoomMayDo : IHelpRoomSection
{
    /// <summary>
    /// The checkouts it may read: every readable one here, from the driver's own setting, in every workspace in <c>default</c>'s room.
    /// A workspace's room (ASKSCOPE1c, D158's ASKSCOPE1 note point 5) reads its own workspace's alone, of those reading across already
    /// allows (D107): they are what its spawn is granted, so the room lists what its rules allow.
    /// </summary>
    public HelpMachine Describe(HelpMachine machine, HelpMachineSources sources)
    {
        var registry = sources.Registry ?? sources.Snapshot.Repositories;
        var workspaceOf = registry
            .GroupBy(known => known.Repository, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(named => named.Key, named => named.First().Workspace, StringComparer.OrdinalIgnoreCase);
        var described = Restricted(machine, sources, registry, workspaceOf);
        return machine.Mode == HelpModes.Full ? Full(described, sources) : described;
    }

    /// <summary>
    /// HELPMODE1a1 (D170 as D183 amends it): in full access its spawn is handed every server the plugins here declare, for it or for
    /// sessions, approved or not (<c>ChatRunner.HandServersAsync</c>), each once by name; so none waits, and none is offered.
    /// </summary>
    private static HelpMachine Full(HelpMachine machine, HelpMachineSources sources)
    {
        var named = new HashSet<string>(StringComparer.Ordinal);
        return machine with
        {
            PluginServers = [.. sources.Plugins.Contributing.SelectMany(plugin => plugin.Manifest.Servers
                .Where(server => named.Add(server.Name))
                .Select(server => new HelpPluginServer(plugin.Manifest.Id, server.Name, server.Allow) { Writes = server.Writes }))],
            PluginServersWaiting = [],
            PluginServersOffered = [],
        };
    }

    private static HelpMachine Restricted(
        HelpMachine machine, HelpMachineSources sources, IReadOnlyList<RepoView> registry, Dictionary<string, string> workspaceOf)
    {
        // ASKSCOPE1c: the grants intersect, its workspace's checkouts and those reading across allows; the whole registry stays the
        // one each checkout's workspace is named from.
        var readable = sources.Workspace is { } circle
            ? registry.Where(known => string.Equals(RemoteTarget.Workspace(known.Workspace), circle, StringComparison.OrdinalIgnoreCase))
            : registry;
        return machine with
        {
            Reads = [.. AcrossRules.Readable(sources.Config, readable)
                .Select(checkout => new HelpRead(checkout.Repository, sources.Workspace ?? workspaceOf[checkout.Repository], checkout.Path))],
            // HELPPLUG1a: the servers the person approved for it, as its spawn is handed them (HelpGrants), and those waiting.
            PluginServers = [.. (sources.Grants?.All ?? []).Where(grant => grant.State == HelpGrants.Approved)
                .Select(grant => new HelpPluginServer(grant.Plugin, grant.Server.Name, grant.Server.Allow) { Writes = grant.Accepted })],
            // HELPPLUG1c: what each would bring, reads and writes apart, and one an update changed since its approval.
            PluginServersWaiting = [.. (sources.Grants?.All ?? []).Where(grant => grant.State == HelpGrants.Waits)
                .Select(grant => new HelpPluginServer(grant.Plugin, grant.Server.Name, grant.Server.Allow)
                {
                    Writes = grant.Server.Writes, Changed = grant.Changed,
                })],
            PluginServersOffered = [.. Offered(sources)],
            // HELPPLUG1b: the skills its spawn is handed (SpawnSkills.Write), as the harness names them.
            PluginSkills = [.. sources.Plugins.Contributing.SelectMany(plugin => plugin.Manifest.Skills
                .Where(skill => skill.HandedTo(PluginServerTo.Help) && SpawnSkills.Judge(plugin.Folder, skill.Name) is null)
                .Select(skill => $"{plugin.Manifest.Id}:{skill.Name}"))],
        };
    }

    /// <summary>
    /// HELPPLUG1c: each server an installed plugin's newer copy in the install would offer Ask Daoris and the installed copy does
    /// not, as an installed in-app browser 1.0.0 is beside the shipped 1.1.0: said with its update first, so it is not missing
    /// in silence.
    /// </summary>
    private static IEnumerable<HelpPluginServerOffered> Offered(HelpMachineSources sources)
    {
        foreach (var offer in sources.Offers.Where(offer => offer.Installed && offer.Problem is null))
        {
            if (sources.Plugins.Plugins.FirstOrDefault(entry => string.Equals(entry.Manifest.Id, offer.Id, StringComparison.OrdinalIgnoreCase))
                is not { } installed)
            {
                continue;
            }

            foreach (var server in offer.Manifest.Servers.Where(server => server.HandedTo(PluginServerTo.Help)
                && !installed.Manifest.Servers.Any(each => each.Name == server.Name && each.HandedTo(PluginServerTo.Help))))
            {
                yield return new HelpPluginServerOffered(installed.Manifest.Id, server.Name, installed.Manifest.Version, offer.Manifest.Version);
            }
        }
    }

    public string Render(HelpMachine machine)
    {
        if (machine.Mode == HelpModes.Full) return RenderFull(machine);

        var text = new StringBuilder();
        text.Append("## What you may do\n\n");
        text.Append("You read, you advise, and you propose. You change nothing yourself: every change is the person's, made\n");
        text.Append("on a screen or with a terminal command, and the two always do the same thing. When a change would\n");
        text.Append("help, name both — the screen and where on it, and the command — and, where the person wants it made,\n");
        text.Append("propose it with the tool for its kind (below). A proposal is a card with Apply and Not now; nothing\n");
        // ASKQUESTS1: what an Apply did is Daoris's word, never the person's; a Not now is still theirs.
        text.Append("changes until the person presses Apply. What the Apply did comes back to you as Daoris's word, starting\n");
        text.Append("`Daoris:` and naming by id any ask or quest it published; a Not now comes back as their next message. Daoris\n");
        text.Append("checks each proposal first by the rules of the screen that makes the same change, and one those rules\n");
        text.Append("would refuse never reaches the person: you are told why instead. Read the family through\n");
        text.Append("`daoris-knowledge` (the registry, its knowledge, its quests) when the question needs more than this page.\n\n");
        // HELPMODE1a1 (D183 point 2): asked to clean up a tree Daoris made, the helper said it could change nothing and listed
        // commands, and never that this was a mode the person could change. Said, and where; full access is never proposed.
        text.Append("This is restricted mode. In full access you would act on this machine yourself; only the person chooses it,\n");
        text.Append("with `daoris-driver help mode full`, from the next conversation's start. When they ask why you cannot do\n");
        text.Append("something yourself, say so and where it is chosen: it is never yours to propose.\n\n");
        // HELPMODE1c2a (D183's note): a discard of a session branch granted is the helper's to do through its tool.
        var discards = machine.Housekeeping.Contains(ConfiguredAccess.DiscardBranch);
        text.Append(discards
            ? "Never offer to push, merge, sign in, or handle a key: those stay the person's own presses,\n"
            : "Never offer to push, merge, discard, sign in, or handle a key: those stay the person's own presses,\n");
        text.Append("where they already are.\n\n");
        if (machine.Housekeeping.Count > 0) text.Append(Housekeeping(machine.Housekeeping, alone: machine.Settings.Count == 0));
        // HELPMODE1c3 (D183's note): the chosen settings granted, changed by the helper's own tools with no Apply.
        if (machine.Settings.Count > 0) text.Append(Settings(machine.Settings, alone: machine.Housekeeping.Count == 0));
        // HELP4: the first repository question met a shell refused before it ran, then a guess at the
        // repository's branches presented as its tree. HELP5: a later one spent a turn on a ticket's URL,
        // refused the same way. Said plainly, so none of them happens again. D107: the checkouts reading
        // across allows are read, since HELPMODE1c4, through one typed tool Daoris judges at each call, on
        // every agent, which is still no shell; so the room names no path, which no tool of its takes.
        var reads = machine.Reads.Where(read => !machine.CheckoutsOff.Contains(read.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        var off = machine.Reads.Where(read => !reads.Contains(read)).Select(read => $"`{read.Name}`").ToList();
        if (reads.Count > 0)
        {
            text.Append("You have no shell and no web fetch or search: a command or a web page is refused before it runs, so\n");
            text.Append("never try one. You may read the checkouts below, and change nothing in them, with `checkout_read` alone:\n");
            text.Append("name the repository and a path from the top of its checkout (`src/Game.cs`; none for its top) to read a\n");
            text.Append("file's text or list a folder, or ask `status` or `branches` for how it stands (`git status` and\n");
            text.Append("`git branch --list`). Daoris judges each read: a path out of the checkout, git's own folder and\n");
            text.Append("anything git ignores are refused, and a file past 200 KB is cut. Any other file outside this room is\n");
            text.Append("refused.\n\n");
            foreach (var read in reads)
            {
                text.Append($"- `{read.Name}` (workspace `{read.Workspace}`)\n");
            }

            text.Append('\n');
            if (off.Count > 0)
            {
                text.Append($"The person turned reading off for you in {string.Join(", ", off)} under Ask Daoris's configured access\n");
                text.Append("(Settings → AI features): say so when one is asked about, and that they turn it on there.\n\n");
            }

            text.Append("What you read is how a tree stands now. Changing it (its branches, its uncommitted work, what is\n");
            text.Append("ready to push) is still a repository's own work, so route it there. Propose an ask for it with\n");
        }
        else
        {
            text.Append("You have no shell, no web fetch or search, and you read no checkout: a command, a web page, or a\n");
            text.Append("file outside this room, is refused before it runs, so never try one. What a repository's tree holds\n");
            text.Append("(its branches, its uncommitted work, what is ready to push) is a repository's own work, so route it\n");
            text.Append("there. Propose an ask for it with ");
        }

        text.Append("`ask_propose`, so that repository's agent does it with its own\n");
        text.Append("tools, or tell the person to open a conversation in that repository with New conversation (Ctrl+N),\n");
        text.Append("or take them there with `go_propose`, the repository and their words filled.\n");
        text.Append("Where Daoris itself has a door for what they want, name it first: session branches whose work landed,\n");
        text.Append("and branches a landing made whose pull request's work reached the line (a squash merge included),\n");
        text.Append("are cleaned up under the workspace's page → Branches → Session branches. When the family's knowledge and quests\n");
        text.Append("are all you can see, say what you could not see: never build a repository's state from its quests and\n");
        text.Append("present it as the tree.\n\n");
        // HELPPLUG1a (D170 point 3): a plugin's server reaches it only where declared for it, and only its declared tools are
        // allowed; what such a tool does outside Daoris is the plugin's, and Daoris itself still changes only by a proposal.
        if (machine.PluginServers.Count > 0)
        {
            text.Append("Plugins installed here hand you these servers, as the person approved them. Call only the tools listed for\n");
            text.Append("each; any other is refused:\n\n");
            foreach (var server in machine.PluginServers)
            {
                static string Named(IEnumerable<string> tools) => string.Join(", ", tools.Select(tool => $"`{tool}`"));
                var said = new List<string>();
                if (server.Tools.Count > 0) said.Add($"reads with {Named(server.Tools)}");
                if (server.Writes.Count > 0) said.Add($"changes things outside Daoris with {Named(server.Writes)}, a write the person accepted");
                text.Append($"- `{server.Server}` (plugin `{server.Plugin}`): {(said.Count > 0 ? string.Join("; ", said) : "none of its tools")}\n");
            }

            text.Append('\n');
            text.Append("What such a tool does outside Daoris is the plugin's own act, as the person who installed it;\n");
            text.Append("a change to Daoris itself is still a proposal, with the tool for its kind.\n\n");
        }

        // A manifest describes and grants nothing (D170's note): a server waiting for the person's approval is handed nothing.
        // HELPPLUG1c: a server it lacked left Ask Daoris with nobody told; what it would have, and where it is approved, is said
        // once at the start, and the helper proposes the approval rather than only opening the page.
        if (machine.PluginServersWaiting.Count > 0 || machine.PluginServersOffered.Count > 0)
        {
            static string Named(IEnumerable<string> tools) => string.Join(", ", tools.Select(tool => $"`{tool}`"));
            text.Append("### Not yours yet\n\n");
            text.Append("Plugins here offer you these servers, and you are handed none of them until the person approves its exact\n");
            text.Append("tools: all its reads, and each write outside Daoris only as they accept it.\n\n");
            foreach (var server in machine.PluginServersWaiting)
            {
                var said = new List<string>();
                if (server.Tools.Count > 0) said.Add($"would read with {Named(server.Tools)}");
                if (server.Writes.Count > 0) said.Add($"would change things outside Daoris with {Named(server.Writes)}, each write only as the person accepts it");
                if (said.Count == 0) said.Add("would call none of its tools");
                if (server.Changed) said.Add("approved once, and an update has changed its tools since, so it asks again");
                text.Append($"- `{server.Server}` (plugin `{server.Plugin}`): {string.Join("; ", said)}\n");
            }

            foreach (var offered in machine.PluginServersOffered)
            {
                text.Append($"- `{offered.Server}` (plugin `{offered.Plugin}`): this install's own `{offered.Plugin}` {offered.Shipped} offers it you, ");
                text.Append($"and the {offered.Installed} installed here does not, so it is updated first (`plugin_propose`, `update`), then approved\n");
            }

            text.Append('\n');
            text.Append("Say so once, at the start of your first answer in a conversation, in a sentence: what you do not have yet\n");
            text.Append("and where it is approved, so nothing the person relied on is taken away in silence. The person approves one\n");
            text.Append("on its page (Plugins → the plugin's page → Ask Daoris → Approve). When they ask for what one does,\n");
            text.Append("propose the approval with `plugin_propose` (`approve`, the plugin's `id` and its `server`, and in `writes`\n");
            text.Append("only the writes they said they accept), rather than only taking them to the page.\n\n");
        }

        // HELPPLUG1b (D170 point 4): the skills a plugin hands it, by the names the harness gives them.
        if (machine.PluginSkills.Count > 0)
        {
            text.Append($"Plugins installed here also hand you these skills: {string.Join(", ", machine.PluginSkills.Select(skill => $"`{skill}`"))}.\n\n");
        }

        // HELPOWED1 (D170 point 2): asked to set a repository up, the helper said it had no shell and no door, as if that were
        // the design. A door it lacks is one Daoris owes, and the room lists them.
        text.Append("Where you have no door to propose what the person wants, that is never a limit of yours: where the act is\n");
        text.Append("under *Doors Daoris still owes* (below), say Daoris still owes that door and name its command.\n\n");
        if (reads.Count == 0 && off.Count > 0)
        {
            // HELPMODE1c4: every readable checkout turned off under configured access, which is the person's choice for Ask Daoris
            // alone, not the repositories' reading switched off.
            text.Append("The person turned reading off for you in every checkout here under Ask Daoris's configured access. They\n");
            text.Append("turn one on again under Settings → AI features → Configured access → Reading checkouts.\n\n");
        }
        else if (machine.Reads.Count == 0 && machine.Repositories.Any(repository => repository.Checkout))
        {
            // HELPSETUP1: a repository's reading is on its Setup since UX6f; a workspace's is on its own page since UX6g.
            text.Append("Reading the checkouts here is switched off. The person turns it on for a repository under\n");
            text.Append("Repositories → the repository's page → Setup → Reach, and for a whole workspace under\n");
            text.Append("Repositories → the workspace's page → Setup → Defaults → Read by agents outside, or with `daoris driver across <repository> read on`\n");
            text.Append("(`--workspace <name>` in place of the repository for a whole workspace).\n\n");
        }

        return text.ToString();
    }

    /// <summary>
    /// HELPMODE1c2a (D183's note): the housekeeping the person granted, a change restricted mode makes itself (the one, where no
    /// setting is granted beside it), each tool named with what it does, and what holds every one of them.
    /// </summary>
    private static string Housekeeping(IReadOnlyList<string> granted, bool alone)
    {
        var text = new StringBuilder();
        text.Append(alone
            ? "The person granted you Daoris's own housekeeping, the one change you make yourself here, with these tools alone:\n\n"
            : "The person granted you Daoris's own housekeeping, a change you make yourself here, with these tools alone:\n\n");
        if (granted.Contains(ConfiguredAccess.Inspect))
        {
            text.Append("- `housekeeping_inspect`: Daoris's session branches and the branches its landings made, each cleared to go or kept, and why\n");
        }

        if (granted.Contains(ConfiguredAccess.CleanUp))
        {
            text.Append("- `housekeeping_clean_up`: removes the branches you name, with their trees, where the cleanup clears them\n");
        }

        if (granted.Contains(ConfiguredAccess.DiscardBranch))
        {
            text.Append("- `housekeeping_discard_branch`: discards one session branch by name, where its own proof lets it go\n");
        }

        text.Append('\n');
        text.Append("Each target is a repository of this conversation's and a branch's name, never a path; each is judged again\n");
        text.Append("before it goes, nothing is forced, and a branch a running or waiting session holds is kept. Say what you will\n");
        text.Append("remove before you do, and what went and what was kept after. A tool the person has not granted is refused.\n\n");
        return text.ToString();
    }

    /// <summary>
    /// HELPMODE1c3 (D183's note): the chosen settings the person granted, each changed by its own tool with no Apply, through the
    /// checks and writer Settings uses; every other setting is still proposed.
    /// </summary>
    private static string Settings(IReadOnlyList<string> granted, bool alone)
    {
        var text = new StringBuilder();
        text.Append(alone
            ? "The person granted you these settings, the one change you make yourself here, with these tools alone:\n\n"
            : "The person also granted you these settings, changed yourself with these tools alone:\n\n");
        foreach (var setting in ConfiguredAccess.SettingsBuilt.Where(granted.Contains))
        {
            text.Append($"- `{ConfiguredAccess.SettingToolOf(setting)}`: {SettingSaid(setting)}\n");
        }

        text.Append('\n');
        text.Append("Each changes the setting at once, with no Apply, through the same checks and writer as Settings, and the change\n");
        text.Append("is recorded. Do it only when the person asks for it, and say what you changed, and to what. Any other setting is\n");
        text.Append("still proposed with `setting_propose` for their Apply. A tool the person has not granted is refused.\n\n");
        return text.ToString();
    }

    private static string SettingSaid(string setting) => setting switch
    {
        ConfiguredAccess.Notify => "whether Daoris says so when a session parks, or ends without being asked",
        ConfiguredAccess.Language => "the language a repository's or a workspace's sessions write to the person in",
        ConfiguredAccess.Cap => "how many sessions run at once on this machine",
        _ => "how long an account whose limit names no time cools before it is offered again",
    };

    /// <summary>
    /// The section in full access (HELPMODE1a1, D183 points 1, 5): it acts on this machine itself, with every server the plugins here
    /// declare; and the doctrine still speaks, a repository's work through a quest and Daoris's own housekeeping its to do. Nothing
    /// restricted mode's alone is said: no shell refused, nothing waiting for approval, no reading switched off.
    /// </summary>
    private static string RenderFull(HelpMachine machine)
    {
        var text = new StringBuilder();
        text.Append("## What you may do\n\n");
        text.Append("You act on this machine yourself, as the person chose: a shell, file edits, Daoris's own commands (`daoris`,\n");
        text.Append("`daoris-driver`) and the plugins' servers below, with no asking. What you do is your own act, not a proposal,\n");
        text.Append("so say what you will do before you do it, and what you did after; the proposal tools (below) still serve\n");
        text.Append("where the person would rather press it themselves. Read the family through `daoris-knowledge` (the registry,\n");
        text.Append("its knowledge, its quests) when the question needs more than this page.\n\n");
        // D183 point 5: words, not a gate; the mode is the gate.
        text.Append("A repository's own work (its branches, its uncommitted work, what is ready to push) is still asked for through\n");
        text.Append("a quest: propose an ask for it with `ask_propose`, so that repository's agent does it with its own tools, or\n");
        text.Append("take the person there with `go_propose`, the repository and their words filled.\n");
        text.Append("Daoris's own housekeeping is yours to do: its trees, the session branches whose work landed, and its home.\n");
        text.Append("Never push, sign in, or handle a key: those stay the person's own presses, and where your agent takes\n");
        text.Append("Daoris's rules, the person's own denies still hold.\n\n");
        if (machine.PluginServers.Count > 0)
        {
            var servers = string.Join(", ", machine.PluginServers.Select(server => $"`{server.Server}` (plugin `{server.Plugin}`)"));
            text.Append($"Plugins installed here hand you every server they declare, with nothing to approve: {servers}.\n");
            text.Append("What such a tool does outside Daoris is the plugin's own act, as the person who installed it.\n\n");
        }

        if (machine.PluginSkills.Count > 0)
        {
            text.Append($"Plugins installed here also hand you these skills: {string.Join(", ", machine.PluginSkills.Select(skill => $"`{skill}`"))}.\n\n");
        }

        return text.ToString();
    }
}

/// <summary>A checkout Ask Daoris may read (D107): its repository, its workspace, and where it is.</summary>
public sealed record HelpRead(string Name, string Workspace, string Path);

/// <summary>A server a plugin hands Ask Daoris (HELPPLUG1a): the plugin, the server's name, and the tools that read it may call there.</summary>
public sealed record HelpPluginServer(string Plugin, string Server, IReadOnlyList<string> Tools)
{
    /// <summary>
    /// The writes outside Daoris the person accepted, which it may call too; for one that waits, every write it declares
    /// (HELPPLUG1c), each the person's to accept.
    /// </summary>
    public IReadOnlyList<string> Writes { get; init; } = [];

    /// <summary>For one that waits: approved once, and an update has changed its tools since (HELPPLUG1c).</summary>
    public bool Changed { get; init; }
}

/// <summary>
/// A server the install's newer copy of a plugin installed here offers Ask Daoris, which the installed copy does not
/// (HELPPLUG1c): the plugin, the server, and the two versions.
/// </summary>
public sealed record HelpPluginServerOffered(string Plugin, string Server, string Installed, string Shipped);
