using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What waits on what (Ask Daoris reads the exchanges as a fact): each line's requests of others that it
/// still waits on, as the exchanges' one read finds them over this machine's records and the terminal's <c>daoris session at-work</c> says
/// them, so the helper answers from the records and proposes only acts that exist.
/// </summary>
internal sealed class HelpRoomExchanges : IHelpRoomSection
{
    /// <summary>The most waits the room lists, the rest counted.</summary>
    internal const int Listed = 12;

    public HelpMachine Describe(HelpMachine machine, IHelpMachineSources sources) => sources.Describe(this, machine);

    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        if (machine.Waits is not { } waits)
        {
            text.Append("## What waits on what\n\n");
            text.Append(machine.NotReadYet.Waits ? "What each line waits on is not read yet" : "What each line waits on was not read");
            text.Append(", so do not say what any session waits on: the person may ask in the terminal, `daoris session at-work <session>`.\n\n");
            return text.ToString();
        }

        if (waits.Waits.Count == 0 && waits.Unread.Count == 0) return "";

        text.Append("## What waits on what\n\n");
        if (waits.Waits.Count > 0)
        {
            text.Append("Each line below waits on a request it made of another session, agent or repository, read from this machine's records and\n");
            text.Append("never judged again here: whom it asked, where the request stands, and what Daoris saw of its delivery. Say it as listed; the\n");
            text.Append("terminal's `daoris session at-work <session>` says the same for one session. None of it waits on the person: what does is in\n");
            text.Append("*What needs you*, and an answer to another agent is that agent's to give, so press no act for one.\n\n```text\n");
            foreach (var wait in waits.Waits.Take(Listed)) text.Append($"{wait.Session} ({wait.Repository}): {wait.Line}\n");
            text.Append("```\n");
            if (waits.Waits.Count > Listed) text.Append($"and {waits.Waits.Count - Listed} more.\n");
            text.Append('\n');
        }

        if (waits.Unread.Count > 0)
        {
            text.Append($"- not read: {string.Join(", ", waits.Unread)}, so what a line waits on may be more.\n\n");
        }

        return text.ToString();
    }
}

/// <summary>One request a line waits on, as the room lists it: the line's head, its repository and workspace, and the request in the verb's words.</summary>
public sealed record HelpWait(string Session, string Repository, string Workspace, string Line);

/// <summary>What waits on what across this machine's lines, and the sources that could not be read: said, never drawn as none.</summary>
public sealed record HelpWaits(IReadOnlyList<HelpWait> Waits, IReadOnlyList<string> Unread)
{
    public static HelpWaits None { get; } = new([], []);
}
