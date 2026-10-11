namespace Daoris.Knowledge;

/// <summary>The <c>delete</c> kind's writer (HELP6): a quest or an ask made by mistake, by its id.</summary>
public sealed partial class HelpProposalBox
{
    /// <summary>
    /// Write one delete proposal (HELP6): a quest or an ask made by mistake, by its id. Whether the record
    /// may go is the service's own reading, which the driver asks before the person sees the card.
    /// </summary>
    public (string? Id, string Message) ProposeDelete(string? quest, string? ask, string? deleteSession, string why, string? session, DateTimeOffset at)
    {
        var questId = Blank(quest)?.TrimStart('#');
        var askId = Blank(ask)?.TrimStart('#');
        var sessionId = Blank(deleteSession);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = new[] { questId, askId, sessionId }.Count(named => named is not null) != 1
            ? "a delete names a quest, an ask or a session — name exactly one, by its id."
            : Word(questId ?? askId ?? sessionId!, "an id");
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "delete");
            writer.WriteString("door", questId is not null ? "quest" : askId is not null ? "ask" : "session");
            writer.WriteString("target", questId ?? askId ?? sessionId);
            writer.WriteNull("workspace");
            writer.WriteNull("value");
            writer.WriteNull("sentence");
        }, why, session, at);
    }
}
