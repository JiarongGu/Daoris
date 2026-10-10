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
        };
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
        text.Append("tools, or tell the person to open a conversation in that repository (Sessions → Start a session).\n");
        text.Append("Where Daoris itself has a door for what they want, name it first: session branches whose work landed,\n");
        text.Append("and branches a landing made whose pull request's work reached the line (a squash merge included),\n");
        text.Append("are cleaned up under the workspace's page → Branches → Session branches. When the family's knowledge and quests\n");
        text.Append("are all you can see, say what you could not see: never build a repository's state from its quests and\n");
        text.Append("present it as the tree.\n\n");
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

public sealed partial record HelpMachine
{
    /// <summary>The checkouts it may read, by name; a path here is one reading across allows, and no other.</summary>
    public IReadOnlyList<HelpRead> Reads { get; init; } = [];
}
