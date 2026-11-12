using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A run whose focus is a repository is told that repository's own with its first words (<see cref="HelpFocusTold"/>): its line, the
/// sessions waiting on the person or ended there, its quests parked or held, and what landed from it, from the facts projected to its
/// workspace. Nothing for a focus on this machine or a workspace.
/// </summary>
internal static class HelpFocusGathered
{
    public static string Render(HelpMachine machine)
    {
        if (machine.Focus is not { Kind: HelpFocus.RepositoryKind, Repository: { } name, Workspace: { } workspace }) return "";
        bool Ours(string repository) => string.Equals(repository, name, StringComparison.OrdinalIgnoreCase);

        var text = new StringBuilder();
        text.Append($"## In focus: repository `{name}`\n\n");
        text.Append($"What Daoris holds of `{name}` in workspace `{workspace}` as this run starts; `daoris-knowledge` reads it again.\n\n");
        text.Append(machine.Repositories.FirstOrDefault(each => Ours(each.Name)) is { } repository
            ? $"- Its line: {(repository.LineNotReadYet ? "not read yet" : HelpRoomWorkspaces.LineOf(repository.Line))}{(repository.Checkout ? "" : "; no checkout here")}.\n"
            : $"- `{name}` is not among workspace `{workspace}`'s repositories now.\n");
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
