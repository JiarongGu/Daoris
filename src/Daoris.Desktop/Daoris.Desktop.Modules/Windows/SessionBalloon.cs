namespace Daoris.Desktop;

/// <summary>The balloon's icon, named here because these modules are plain <c>net10.0</c> and know no Windows Forms.</summary>
public enum BalloonIcon
{
    /// <summary>News: an ending or an answer.</summary>
    Info,

    /// <summary>Something waits on the person.</summary>
    Warning,
}

/// <summary>
/// What a session's attention balloon says and shows by the driver's kind (NOTIFY1d): the choice, here where it is
/// tested, while the application's notifier shows the balloon.
/// </summary>
public static class SessionBalloon
{
    /// <summary>
    /// The body: the driver's own detail (D24, verbatim) or, when it has none, a line by kind. A balloon with an empty
    /// body renders as a title with a gap under it.
    /// </summary>
    public static string Body(string? kind, string? detail) =>
        !string.IsNullOrWhiteSpace(detail)
            ? detail
            : kind switch
            {
                "Parked" => "Open Daoris to answer it.",
                // A quest's park waits on the person too (SESSUX1i), and its session is its last, whose page offers Try again.
                "QuestParked" => "Open Daoris to try it again.",
                // A conversation answered the person (NOTIFY1a): news they wait on, read on its page.
                "Answered" => "Open Daoris to read it.",
                // A terminal's press waiting for the window (CONFIRMSEEN1): its card is the person's to answer.
                PipeConfirmations.AttentionKind => "Open Daoris to confirm or refuse it.",
                _ => "Open Daoris to see what it did.",
            };

    /// <summary>
    /// Warn for a park, and for a terminal's press waiting for the window (CONFIRMSEEN1): each is WAITING on somebody. An ending,
    /// or an answer, is news.
    /// </summary>
    public static BalloonIcon Icon(string? kind) =>
        kind is "Parked" or "QuestParked" or PipeConfirmations.AttentionKind ? BalloonIcon.Warning : BalloonIcon.Info;
}
