namespace Daoris.Driver;

/// <summary>Why the person's words never reached the session, by code: the page's <c>note.words.why.*</c>.</summary>
public static class UnreachedWhy
{
    /// <summary>The session ended while they waited.</summary>
    public const string EndedFirst = "ended-first";

    /// <summary>Daoris kept no id for the conversation, so there was nothing to resume.</summary>
    public const string Unkept = "unkept";

    /// <summary>
    /// The record did not name them marked attempted: a word is written only once its attempt is acknowledged, so these were
    /// not written, and wait on their record.
    /// </summary>
    public const string Unmarked = "unmarked";

    /// <summary>The English a reason says, as a site writes it.</summary>
    public static string English(string why) => why switch
    {
        EndedFirst => "it ended first",
        Unkept => "Daoris kept no id for its conversation",
        Unmarked => "its record did not mark them attempted, so they wait there for the session's next go-on",
        _ => throw new ArgumentOutOfRangeException(nameof(why), why, "an unreached reason NoteCodes.Unreached does not declare"),
    };
}
