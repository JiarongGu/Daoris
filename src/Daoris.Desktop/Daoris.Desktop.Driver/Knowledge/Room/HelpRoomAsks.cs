using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The asks not closed (HELP6), by id, so a delete of one made by mistake can name it. The quests are the
/// family's <c>quest_list</c>; the asks are this machine's alone.
/// </summary>
internal sealed class HelpRoomAsks : IHelpRoomSection
{
    public HelpMachine Describe(HelpMachine machine, IHelpMachineSources sources) => sources.Describe(this, machine);

    public string Render(HelpMachine machine)
    {
        if (machine.OpenAsks.Count == 0) return "";

        var text = new StringBuilder();
        text.Append("### Asks\n\n");
        foreach (var ask in machine.OpenAsks)
        {
            var quests = ask.Quests.Count > 0 ? $"; quests {string.Join(", ", ask.Quests.Select(quest => $"`#{quest}`"))}" : "";
            text.Append($"- `#{ask.Id}` at `{ask.Workspace}`: “{Clipped(ask.Sentence)}” ({ask.State}{quests})\n");
        }

        text.Append('\n');
        return text.ToString();
    }

    /// <summary>An ask's words on one line, cut where they run long: the id is what a proposal names.</summary>
    private static string Clipped(string sentence)
    {
        return TextBounds.Clip(TextBounds.Fold(sentence), 120, keep: 119, trimEnd: true);
    }
}

/// <summary>An ask not closed, as the room lists it (HELP6): by id, so a delete of one made by mistake can name it.</summary>
public sealed record HelpAsk(string Id, string Sentence, string Workspace, string State, IReadOnlyList<string> Quests);
