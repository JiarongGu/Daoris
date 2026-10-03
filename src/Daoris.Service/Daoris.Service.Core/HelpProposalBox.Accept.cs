namespace Daoris.Knowledge;

/// <summary>The <c>accept</c> kind's writer (DRIFT1d2): the person's yes to a done's departure from what they required, by the quest's id.</summary>
public sealed partial class HelpProposalBox
{
    /// <summary>
    /// Write one proposal to accept a departure (DRIFT1d2, D133 §4): a quest a done's departure holds, by its id, for the
    /// person to apply — the quest page's yes and <c>daoris-driver quest accept</c>. Whether a departure holds it is the
    /// driver's to judge against the quest as the service answers it; the yes stays the person's press, never the tool's.
    /// </summary>
    public (string? Id, string Message) ProposeAccept(string quest, string why, string? session, DateTimeOffset at)
    {
        var id = Blank(quest)?.TrimStart('#');
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = string.IsNullOrEmpty(id)
            ? "an accept names the quest whose departure it accepts, by its id."
            : Word(id, "an id");
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "accept");
            writer.WriteString("door", "accept");
            writer.WriteString("target", id);
            writer.WriteNull("workspace");
            writer.WriteNull("value");
            writer.WriteNull("sentence");
        }, why, session, at);
    }
}
