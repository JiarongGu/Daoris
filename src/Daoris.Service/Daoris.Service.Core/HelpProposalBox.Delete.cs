namespace Daoris.Knowledge;

/// <summary>The <c>delete</c> kind's writer (HELP6): a quest or an ask made by mistake, by its id.</summary>
public sealed partial class HelpProposalBox
{
    /// <summary>
    /// Write one delete proposal (HELP6): a quest or an ask made by mistake, by its id. Whether the record
    /// may go is the service's own reading, which the driver asks before the person sees the card.
    /// </summary>
    public (string? Id, string Message) ProposeDelete(string? quest, string? ask, string why, string? session, DateTimeOffset at)
    {
        var questId = Blank(quest)?.TrimStart('#');
        var askId = Blank(ask)?.TrimStart('#');
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = (questId is null) == (askId is null)
            ? "a delete names a quest or an ask — name exactly one, by its id."
            : Word(questId ?? askId!, "an id");
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "delete");
            writer.WriteString("door", questId is not null ? "quest" : "ask");
            writer.WriteString("target", questId ?? askId);
            writer.WriteNull("workspace");
            writer.WriteNull("value");
            writer.WriteNull("sentence");
        }, why, session, at);
    }
}
