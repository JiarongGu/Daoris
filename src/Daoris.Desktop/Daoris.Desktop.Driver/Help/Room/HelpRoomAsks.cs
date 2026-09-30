using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The asks not closed (HELP6), by id, so a delete of one made by mistake can name it. The quests are the
/// family's <c>quest_list</c>; the asks are this machine's alone.
/// </summary>
internal sealed class HelpRoomAsks : IHelpRoomSection
{
    public HelpMachine Describe(HelpMachine machine, HelpMachineSources sources) => machine with
    {
        OpenAsks = [.. sources.Standing
            .Where(ask => !string.Equals(ask.State, "Closed", StringComparison.OrdinalIgnoreCase))
            .Select(ask => new HelpAsk(ask.Id, ask.Sentence, ask.Workspace, ask.State, ask.Quests))],
    };

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
        var line = string.Join(' ', sentence.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return line.Length <= 120 ? line : line[..119].TrimEnd() + "…";
    }
}

/// <summary>An ask not closed, as the room lists it (HELP6): by id, so a delete of one made by mistake can name it.</summary>
public sealed record HelpAsk(string Id, string Sentence, string Workspace, string State, IReadOnlyList<string> Quests);

public sealed partial record HelpMachine
{
    /// <summary>The asks not closed, newest first (HELP6).</summary>
    public IReadOnlyList<HelpAsk> OpenAsks { get; init; } = [];
}
