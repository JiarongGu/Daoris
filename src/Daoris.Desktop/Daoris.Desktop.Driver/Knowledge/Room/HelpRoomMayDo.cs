using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What the helper may do: read, advise and press Daoris's verbs, never change a thing itself — and what it
/// cannot reach, so it routes a repository's own work there rather than guessing at it.
/// It may read the checkouts reading across allows, and it says which and how: through
/// <c>checkout_read</c>, less those the person turned off.
/// </summary>
/// <remarks>
/// All of that is restricted mode's, and the section says so. In full access it says the helper acts on this
/// machine itself, with every server the plugins here declare, and what the doctrine still asks of it.
/// </remarks>
internal sealed class HelpRoomMayDo : IHelpRoomSection
{
    public HelpMachine Describe(HelpMachine machine, IHelpMachineSources sources) => sources.Describe(this, machine);

    public string Render(HelpMachine machine)
    {
        if (machine.Mode == HelpModeNames.Full) return RenderFull(machine);

        var text = new StringBuilder();
        text.Append("## What you may do\n\n");
        text.Append("You read, you advise, and you press. You change nothing yourself: every change is the person's, made\n");
        text.Append("on a screen or with a terminal command, and the two always do the same thing. You run that command,\n");
        text.Append("Daoris's own (`daoris`): a read answers at once, and each change it presses waits on the person's card in\n");
        text.Append("Daoris's window, naming you as asking, and runs on their yes. When a change would help, name both — the\n");
        text.Append("screen and where on it, and the command — and, where the person wants it made, press it with the command\n");
        text.Append("(the verbs below). Daoris judges each press first by the rules of the screen that makes the same change,\n");
        text.Append("and one those rules would refuse never reaches the person: you are told why instead. What a press did\n");
        text.Append("answers on its command's line, or, where none waits on it, comes back to you as Daoris's word, starting\n");
        text.Append("`Daoris:` and naming by id any ask or quest it published. Read the family through\n");
        text.Append("`daoris-knowledge` (the registry, its knowledge, its quests) when the question needs more than this page.\n\n");
        // Asked to clean up a tree Daoris made, the helper otherwise says it could change nothing and lists
        // commands, and never that this is a mode the person could change. Said, and where; full access is never proposed.
        text.Append("This is restricted mode: each change you press waits on the person's yes. In full access you would act on\n");
        text.Append("this machine as the person, each press of yours run at once; only the person chooses it, with\n");
        text.Append("`daoris ask-daoris mode full`, from the next conversation's start. When they ask why you cannot do\n");
        text.Append("something yourself, say so and where it is chosen: it is never yours to press.\n\n");
        // A discard of a session branch granted is the helper's to press with no card.
        var discards = machine.Acts.Contains(ActFamilies.Discard);
        text.Append(discards
            ? "Never offer to push, merge, sign in, or handle a key: those stay the person's own presses,\n"
            : "Never offer to push, merge, discard, sign in, or handle a key: those stay the person's own presses,\n");
        text.Append("where they already are.\n\n");
        if (machine.Acts.Count > 0) text.Append(HelpRoomFamilies.Granted(machine.Acts));
        // A repository question otherwise meets a shell refused before it ran, then a guess at the repository's branches
        // presented as its tree, or a turn spent on a ticket's URL. Said plainly, so none happens. The checkouts reading across
        // allows are read through one typed tool Daoris judges at each call, still no shell; so the room names no path.
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
            text.Append("ready to push) is still a repository's own work, so route it there. Ask for it with\n");
        }
        else
        {
            text.Append("You have no shell, no web fetch or search, and you read no checkout: a command, a web page, or a\n");
            text.Append("file outside this room, is refused before it runs, so never try one. What a repository's tree holds\n");
            text.Append("(its branches, its uncommitted work, what is ready to push) is a repository's own work, so route it\n");
            text.Append("there. Ask for it with ");
        }

        text.Append("`daoris ask --workspace <name> \"…\"`, so that repository's agent does it with its own\n");
        text.Append("tools, or tell the person to open a conversation in that repository with New conversation (Ctrl+N),\n");
        text.Append("or take them there with a link to `sessions`'s `new`, the repository and their words filled.\n");
        text.Append("Where Daoris itself has a door for what they want, name it first: session branches whose work landed,\n");
        text.Append("and branches a landing made whose pull request's work reached the line (a squash merge included),\n");
        text.Append("are cleaned up under the workspace's page → Branches → Session branches. When the family's knowledge and quests\n");
        text.Append("are all you can see, say what you could not see: never build a repository's state from its quests and\n");
        text.Append("present it as the tree.\n\n");
        // A plugin's server reaches it only where declared for it, and only its declared tools are allowed.
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
            text.Append("a change to Daoris itself is still a press of Daoris's verb.\n\n");
        }

        // A manifest grants nothing: a server waiting for approval is handed nothing. What it would have, and where it is
        // approved, is said once at the start, and the helper proposes the approval rather than only opening the page.
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
                text.Append($"and the {offered.Installed} installed here does not, so it is updated first (`daoris plugin update {offered.Plugin} --yes`), then approved\n");
            }

            text.Append('\n');
            text.Append("Say so once, at the start of your first answer in a conversation, in a sentence: what you do not have yet\n");
            text.Append("and where it is approved, so nothing the person relied on is taken away in silence. The person approves one\n");
            text.Append("on its page (Plugins → the plugin's page → Ask Daoris → Approve). When they ask for what one does,\n");
            text.Append("press `daoris plugins approve <id> <server>` (with `--writes` naming only the writes they said they accept,\n");
            text.Append("or `--reads-only`), rather than only taking them to the page.\n\n");
        }

        // The skills a plugin hands it, by the names the harness gives them.
        if (machine.PluginSkills.Count > 0)
        {
            text.Append($"Plugins installed here also hand you these skills: {string.Join(", ", machine.PluginSkills.Select(skill => $"`{skill}`"))}.\n\n");
        }

        // Asked to set a repository up, the helper said it had no shell and no door, as if that were a limit of its own.
        text.Append("Where no verb of yours does what the person wants, that is never a limit of yours: where the act is\n");
        text.Append("under *Done at a terminal* (below), name its command for the person to run.\n\n");
        if (reads.Count == 0 && off.Count > 0)
        {
            // Every readable checkout turned off under configured access, which is the person's choice for Ask Daoris
            // alone, not the repositories' reading switched off.
            text.Append("The person turned reading off for you in every checkout here under Ask Daoris's configured access. They\n");
            text.Append("turn one on again under Settings → AI features → Configured access → Reading checkouts.\n\n");
        }
        else if (machine.Reads.Count == 0 && machine.Repositories.Any(repository => repository.Checkout))
        {
            // A repository's reading is on its Setup; a workspace's is on its own page.
            text.Append("Reading the checkouts here is switched off. The person turns it on for a repository under\n");
            text.Append("Repositories → the repository's page → Setup → Reach, and for a whole workspace under\n");
            text.Append("Repositories → the workspace's page → Setup → Defaults → Read by agents outside, or with `daoris place across <repository> read on`\n");
            text.Append("(`--workspace <name>` in place of the repository for a whole workspace).\n\n");
        }

        return text.ToString();
    }

    /// <summary>
    /// The section in full access: it acts on this machine as the person, Daoris's verbs among it, with every server the plugins here
    /// declare; and the doctrine still speaks, a repository's work through a quest and Daoris's own housekeeping its to do. Nothing
    /// restricted mode's alone is said: no shell refused, nothing waiting for approval, no reading switched off.
    /// </summary>
    private static string RenderFull(HelpMachine machine)
    {
        var text = new StringBuilder();
        text.Append("## What you may do\n\n");
        text.Append("You act on this machine as the person, as they chose: a shell, file edits and the plugins' servers below,\n");
        text.Append("with no asking, and Daoris's own command (`daoris`),\n");
        text.Append("where each press runs at once as the person's own, said on this conversation.\n");
        text.Append("What you do is their act done for them, so say what you will do before you do it, and what you did after.\n");
        text.Append("Read the family through `daoris-knowledge` (the registry, its knowledge, its quests) when the question needs\n");
        text.Append("more than this page.\n\n");
        // Words, not a gate; the mode is the gate.
        text.Append("A repository's own work (its branches, its uncommitted work, what is ready to push) is still asked for through\n");
        text.Append("a quest: ask for it with `daoris ask --workspace <name> \"…\"`, so that repository's agent does it with its own tools, or\n");
        text.Append("take the person there with a link to `sessions`'s `new`, the repository and their words filled.\n");
        text.Append("Daoris's own housekeeping is yours to do: its trees, the session branches whose work landed, and its home.\n");
        text.Append("Where your agent takes Daoris's rules, the person's own denies still hold.\n\n");
        text.Append(HelpRoomFullReads.Render(machine));
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

/// <summary>A checkout Ask Daoris may read: its repository, its workspace, and where it is.</summary>
public sealed record HelpRead(string Name, string Workspace, string Path);

/// <summary>A server a plugin hands Ask Daoris: the plugin, the server's name, and the tools that read it may call there.</summary>
public sealed record HelpPluginServer(string Plugin, string Server, IReadOnlyList<string> Tools)
{
    /// <summary>
    /// The writes outside Daoris the person accepted, which it may call too; for one that waits, every write it declares,
    /// each the person's to accept.
    /// </summary>
    public IReadOnlyList<string> Writes { get; init; } = [];

    /// <summary>For one that waits: approved once, and an update has changed its tools since.</summary>
    public bool Changed { get; init; }
}

/// <summary>
/// A server the install's newer copy of a plugin installed here offers Ask Daoris, which the installed copy does not:
/// the plugin, the server, and the two versions.
/// </summary>
public sealed record HelpPluginServerOffered(string Plugin, string Server, string Installed, string Shipped);
