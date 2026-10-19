using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What the helper may do (HELP1a, D89): read, advise and propose, never change a thing itself — and what it
/// cannot reach, so it routes a repository's own work there rather than guessing at it (HELP4, HELP5). Since
/// D107 it may read the checkouts reading across allows, and it says which, where and how.
/// </summary>
internal sealed class HelpRoomMayDo : IHelpRoomSection
{
    /// <summary>The checkouts it may read: every readable one here, in every workspace, from the driver's own setting.</summary>
    public HelpMachine Describe(HelpMachine machine, HelpMachineSources sources)
    {
        var workspaceOf = sources.Snapshot.Repositories
            .GroupBy(known => known.Repository, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(named => named.Key, named => named.First().Workspace, StringComparer.OrdinalIgnoreCase);
        return machine with
        {
            Reads = [.. AcrossRules.Readable(sources.Config, sources.Snapshot.Repositories)
                .Select(checkout => new HelpRead(checkout.Repository, workspaceOf[checkout.Repository], checkout.Path))],
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
        var text = new StringBuilder();
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
        // refused the same way. Said plainly, so none of them happens again. D107: the checkouts reading
        // across allows are read by file and by two git commands its rules allow, which is still no shell.
        if (machine.Reads.Count > 0)
        {
            text.Append("You have no shell and no web fetch or search: a command or a web page is refused before it runs, so\n");
            text.Append("never try one. You may read the checkouts below and change nothing in them: their files where they lie,\n");
            text.Append("and how each stands with `git -C <path> status` and `git -C <path> branch --list`, the path written as\n");
            text.Append("below. Any other file outside this room is refused.\n\n");
            foreach (var read in machine.Reads)
            {
                text.Append($"- `{read.Name}` (workspace `{read.Workspace}`) — `{AcrossRules.GitPath(read.Path)}`\n");
            }

            text.Append('\n');
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
        if (machine.Reads.Count == 0 && machine.Repositories.Any(repository => repository.Checkout))
        {
            // HELPSETUP1: a repository's reading is on its Setup since UX6f; a workspace's is on its own page since UX6g.
            text.Append("Reading the checkouts here is switched off. The person turns it on for a repository under\n");
            text.Append("Repositories → the repository's page → Setup → Reach, and for a whole workspace under\n");
            text.Append("Repositories → the workspace's page → Setup → Defaults → Read by agents outside, or with `daoris driver across <repository> read on`\n");
            text.Append("(`--workspace <name>` in place of the repository for a whole workspace).\n\n");
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
