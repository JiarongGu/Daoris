using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A room written for a repository gathers that repository's own first: its line, the sessions waiting on the person or ended there,
/// its quests parked or held, and what landed from it, each as the sections after it list them for the whole workspace. Nothing for
/// a focus on this machine or a workspace, whose room is its whole.
/// </summary>
internal sealed class HelpRoomInFocus : IHelpRoomSection
{
    public string Render(HelpMachine machine)
    {
        if (machine.Focus is not { Kind: HelpFocus.RepositoryKind, Repository: { } name, Workspace: { } workspace }) return "";
        bool Ours(string repository) => string.Equals(repository, name, StringComparison.OrdinalIgnoreCase);

        var text = new StringBuilder();
        text.Append($"## In focus: repository `{name}`\n\n");
        text.Append($"What this room holds of `{name}` in workspace `{workspace}`, gathered first. The sections below list the whole\n");
        text.Append("workspace, this repository among the rest.\n\n");
        text.Append(machine.Repositories.FirstOrDefault(each => Ours(each.Name)) is { } repository
            ? $"- Its line: {(repository.LineNotReadYet ? "not read yet" : HelpRoomWorkspaces.LineOf(repository.Line))}{(repository.Checkout ? "" : "; no checkout here")}.\n"
            : $"- `{name}` is not among this room's repositories now.\n");
        text.Append(Listed("Waiting on the person", machine.Waiting.Where(session => Ours(session.Repository)).Select(session => $"`{session.Id}`")));
        text.Append(Listed("Ended", (machine.Sessions?.Listed ?? []).Where(session => Ours(session.Repository)).Select(session => $"`{session.Id}`")));
        text.Append(Listed("Quests parked by their failed sessions", machine.Parked.Where(quest => Ours(quest.Repository)).Select(quest => $"`#{quest.Quest}`")));
        text.Append(Listed(
            "Quests held by the person's stop",
            machine.Held.Where(quest => Ours(quest.Repository)).Select(quest => $"`#{quest.Quest}` (session `{quest.Session}`)")));
        text.Append(Listed("Landed", machine.Landed.Where(landed => Ours(landed.Repository)).Select(landed => $"`{landed.Branch}` (session `{landed.Session}`)")));
        text.Append('\n');
        return text.ToString();
    }

    private static string Listed(string what, IEnumerable<string> items)
    {
        var list = items.ToList();
        return $"- {what}: {(list.Count == 0 ? "none" : string.Join(", ", list))}.\n";
    }
}
