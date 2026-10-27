namespace Daoris.Driver;

/// <summary>
/// The groups a session is listed in by state (D126 §2.1), in the order the person acts on them, and Archived: the
/// public spelling the page and <c>daoris-driver sessions --group</c> share.
/// </summary>
public static class SessionGroup
{
    /// <summary>Waiting on you: only the person's press moves it.</summary>
    public const string You = "you";

    /// <summary>To review: an ended session whose own tree holds work no branch of the person's holds.</summary>
    public const string Review = "review";

    /// <summary>Working: queued, starting, working, and a live chat between turns.</summary>
    public const string Working = "working";

    /// <summary>Resumes later: it moves by itself when what it waits on arrives.</summary>
    public const string Later = "later";

    /// <summary>Ended: a record.</summary>
    public const string Ended = "ended";

    /// <summary>Archived: out of the way on this machine, kept whole (§5.2).</summary>
    public const string Archived = "archived";

    /// <summary>The groups in the order a list shows them.</summary>
    public static IReadOnlyList<string> Order { get; } = [You, Review, Working, Later, Ended, Archived];
}
