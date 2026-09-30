namespace Daoris.Knowledge;

/// <summary>The <c>ask</c> kind's writer (HELP1c): something to start, which becomes an ask when the person applies it.</summary>
public sealed partial class HelpProposalBox
{
    /// <summary>Write one ask proposal — something to start, which becomes an ask when the person applies it.</summary>
    public (string? Id, string Message) ProposeAsk(string sentence, string workspace, string why, string? session, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(why)) return (null, "A proposal needs its reason: what the person asked, and what the change would do. Nothing was proposed.");
        if (string.IsNullOrWhiteSpace(sentence)) return (null, "An ask needs its words: what is to be done, as the person would say it. Nothing was proposed.");
        if (string.IsNullOrWhiteSpace(workspace)) return (null, "An ask is made at a workspace — name it. Nothing was proposed.");
        return Write(writer =>
        {
            writer.WriteString("kind", "ask");
            writer.WriteString("door", "ask");
            writer.WriteNull("target");
            writer.WriteString("workspace", workspace.Trim());
            writer.WriteNull("value");
            writer.WriteString("sentence", sentence.Trim());
        }, why, session, at);
    }
}
