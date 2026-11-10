namespace Daoris.Driver;

/// <summary>
/// The brief a session's start is given, as the page's *Started with* reads it: the first message of a part that continues
/// another (a go-on's or a hand-over's), which the record keeps whole as an event and no one said, so it is no opening.
/// </summary>
public static class StartBrief
{
    /// <summary>How long a brief may be when it is answered: a page of reading, not the whole instruction.</summary>
    public const int Limit = 4000;

    /// <summary>The brief this event is, cut to <see cref="Limit"/> with its full length said where it ran past; null for any other event.</summary>
    public static string? Of(SessionEvent e) =>
        e.Kind == SessionEventKind.User && e.Continues is not null && !string.IsNullOrWhiteSpace(e.Text)
            ? TextBounds.ClipCounted(e.Text, Limit) : null;
}
