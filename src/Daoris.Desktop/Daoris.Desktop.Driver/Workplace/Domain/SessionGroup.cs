namespace Daoris.Driver;

/// <summary>
/// The five states a session is in, in the order the person acts on them, and Archived, the fold a done or failed one
/// is moved to by its mark: the public spelling the page and <c>daoris session list --group</c> share.
/// </summary>
public static class SessionGroup
{
    /// <summary>Needs you: a human must act before it goes on.</summary>
    public const string NeedsYou = "needs-you";

    /// <summary>Working: under way and needing no one, whatever it waits on.</summary>
    public const string Working = "working";

    /// <summary>Pending: not started, or its next start held.</summary>
    public const string Pending = "pending";

    /// <summary>Failed: a failure nothing goes on from without the person.</summary>
    public const string Failed = "failed";

    /// <summary>Done: complete, a person's stop and an abandon included.</summary>
    public const string Done = "done";

    /// <summary>Archived: a done or failed session out of the way on this machine, kept whole. A mark, never a state.</summary>
    public const string Archived = "archived";

    /// <summary>The groups in the order a list shows them.</summary>
    public static IReadOnlyList<string> Order { get; } = [NeedsYou, Working, Pending, Failed, Done, Archived];
}
