namespace Daoris.Knowledge;

/// <summary>
/// The <c>ask</c> kind's writer (HELP1c): something to start, which becomes an ask when the person applies it, with the review
/// choice the person said for its work and their words with it (ENTRY1c), as the composer sends them.
/// </summary>
public sealed partial class HelpProposalBox
{
    /// <summary>The composer's default, <i>As each repository's rule says</i>: no choice is written, as none is sent.</summary>
    private const string ReviewByRule = "rule";

    /// <summary>Write one ask proposal — something to start, which becomes an ask when the person applies it.</summary>
    /// <param name="review">
    /// ENTRY1c: the person's review choice for the ask's work, as the composer and <c>daoris-driver ask --review</c> take it:
    /// <c>on</c>, an environment's name or <c>off</c>; null, blank or <c>rule</c> writes none, and each repository's rule decides.
    /// </param>
    /// <param name="reviewWords">The person's words with the choice, written only with one.</param>
    /// <remarks>
    /// ENTRY1c: the shape only, as the setting kind's <c>review</c> door judges its words: words go with a choice, and a choice is
    /// one word. Whether a name is an environment's, and one the ask's workspace declares, is the driver's, with the rule's own
    /// judgement. An ask's kind and workflow are the person's (D157 point 10), so no proposal carries them.
    /// </remarks>
    public (string? Id, string Message) ProposeAsk(
        string sentence, string workspace, string why, string? session, DateTimeOffset at,
        string? review = null, string? reviewWords = null)
    {
        if (string.IsNullOrWhiteSpace(why)) return (null, "A proposal needs its reason: what the person asked, and what the change would do. Nothing was proposed.");
        if (string.IsNullOrWhiteSpace(sentence)) return (null, "An ask needs its words: what is to be done, as the person would say it. Nothing was proposed.");
        if (string.IsNullOrWhiteSpace(workspace)) return (null, "An ask is made at a workspace — name it. Nothing was proposed.");
        var choice = Blank(review) is { } said && said != ReviewByRule ? said : null;
        var words = Blank(reviewWords);
        if (words is not null && choice is null)
        {
            return (null, "An ask's review words go with a review choice — `on`, an environment's name or `off`; with none, each "
                + "repository's rule decides, and no words are kept. Nothing was proposed.");
        }

        if (choice is not null && Word(choice, "a review choice") is not null)
        {
            return (null, $"A review choice is `on`, an environment's name or `off`: one word, and `{choice}` is not. Nothing was proposed.");
        }

        return Write(writer =>
        {
            writer.WriteString("kind", "ask");
            writer.WriteString("door", "ask");
            writer.WriteNull("target");
            writer.WriteString("workspace", workspace.Trim());
            writer.WriteNull("value");
            writer.WriteString("sentence", sentence.Trim());
            Nullable(writer, "review", choice);
            Nullable(writer, "reviewWords", words);
        }, why, session, at);
    }
}
