namespace Daoris.Knowledge;

/// <summary>
/// The <c>clear</c> kind's writer (HIST1f-a, D153): a quest's, an ask's or a workspace's finished history cleared, as the
/// terminal's <c>history clear</c> does. Which units would be cleared and which kept is the driver's to judge.
/// </summary>
public sealed partial class HelpProposalBox
{
    /// <summary>
    /// Write one proposal to clear finished history: the door is <c>quest</c>, <c>ask</c> or <c>workspace</c>, the
    /// <c>target</c> the quest's id, the ask's id or the workspace's name. Whether it exists, and what is clearable, is the
    /// driver's to judge.
    /// </summary>
    public (string? Id, string Message) ProposeClear(string door, string? quest, string? ask, string? workspace, string why, string? session, DateTimeOffset at)
    {
        var questId = Blank(quest)?.TrimStart('#');
        var askId = Blank(ask)?.TrimStart('#');
        var circle = Blank(workspace);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var named = new[] { questId, askId, circle }.Count(value => value is not null);
        var target = door switch { "quest" => questId, "ask" => askId, _ => circle };
        var refused = door is not ("quest" or "ask" or "workspace")
            ? "a clear proposal's door is quest, ask or workspace."
            : named != 1
                ? "a clear proposal names a quest, an ask or a workspace — name exactly one."
            : target is null
                ? $"the {door} door names a {door}: name that one."
            : door == "workspace" ? null : Word(target, "an id");
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "clear");
            writer.WriteString("door", door);
            writer.WriteString("target", target);
            writer.WriteNull("workspace");
            writer.WriteNull("value");
            writer.WriteNull("sentence");
        }, why, session, at);
    }
}
