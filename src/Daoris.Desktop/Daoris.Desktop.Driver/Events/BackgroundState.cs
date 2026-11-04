namespace Daoris.Driver;

/// <summary>
/// The states background work is known to end in: the page words each from <c>note.background.state.*</c> and shows any other
/// as the harness sent it, so the record's <c>state</c> stays an open value.
/// </summary>
public static class BackgroundState
{
    /// <summary>The harness said it completed.</summary>
    public const string Completed = "completed";

    /// <summary>The harness said it failed.</summary>
    public const string Failed = "failed";

    /// <summary>The harness said it was stopped.</summary>
    public const string Stopped = "stopped";

    /// <summary>The harness kept no state for it (<c>AcpBackground</c>'s fallback).</summary>
    public const string Ended = "ended";

    /// <summary>Every known state, which the page's list is held to key for key.</summary>
    public static readonly IReadOnlyList<string> Known = [Completed, Failed, Stopped, Ended];
}
