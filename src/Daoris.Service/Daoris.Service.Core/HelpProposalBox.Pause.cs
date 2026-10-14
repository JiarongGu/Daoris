using static Daoris.Knowledge.HelpProposalFile;

namespace Daoris.Knowledge;

/// <summary>
/// The <c>pause</c> kind's writer (PAUSE1f-a, D132): an ask's or a quest's work paused or resumed. Abandon is never
/// proposed: it is the person's own, on the ask's or quest's page.
/// </summary>
internal sealed class HelpPauseProposalWriter(HelpProposalFile file)
{
    /// <summary>
    /// Write one proposal to pause or resume an ask's or a quest's work: the door is <c>pause</c> or <c>resume</c>, the
    /// <c>target</c> the one quest's or ask's id. Whether it exists, and whether it may be paused now, is the driver's to judge.
    /// </summary>
    public (string? Id, string Message) Propose(string door, string? quest, string? ask, string why, string? session, DateTimeOffset at)
    {
        var questId = Blank(quest)?.TrimStart('#');
        var askId = Blank(ask)?.TrimStart('#');
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = door is not ("pause" or "resume")
            ? "a pause proposal's door is pause or resume."
            : (questId is null) == (askId is null)
                ? "a pause proposal names a quest or an ask — name exactly one, by its id."
                : Word(questId ?? askId!, "an id");
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return file.Write(writer =>
        {
            writer.WriteString("kind", "pause");
            writer.WriteString("door", door);
            writer.WriteString("target", questId ?? askId);
            writer.WriteNull("workspace");
            // Which one was named, so the driver need not tell an ask from a quest by lookup, which an id both have defeats.
            writer.WriteString("value", questId is not null ? "quest" : "ask");
            writer.WriteNull("sentence");
        }, why, session, at);
    }
}
