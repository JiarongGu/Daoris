namespace Daoris.Driver;

/// <summary>What the helper's pass is asked with (MIXAGENT2a-ii): the ask, the asker's checkout and tree, and where the copy stands.</summary>
/// <param name="Ask">The ask on file: the question as written, the agent named, who asked.</param>
/// <param name="Root">The repository's registered checkout, where the asker's commits are.</param>
/// <param name="Tip">The commit the asker's tree stands at, which the helper's copy is checked out at.</param>
public sealed record HelperPassAsk(HelperAsk Ask, string Repository, string Root, string Tip)
{
    /// <summary>The workspace the repository is in; the default one where none is named.</summary>
    public string? Workspace { get; init; }

    /// <summary>
    /// The asker's work in progress (COAGENT1a), laid over the copy once it stands at <see cref="Tip"/>, which is the capture's
    /// commit; null for a pass handed none.
    /// </summary>
    public HelperCaptured? Capture { get; init; }

    /// <summary>
    /// Told the helper's session the moment the host opens it, before its turn: the driver keeps the ask <c>started</c> naming it
    /// then, which <c>helper_answer</c> finds its ask by.
    /// </summary>
    public Action<string>? Opened { get; init; }
}

/// <summary>What a helper's pass came to: the helper's session if one opened, and how its turn ended.</summary>
/// <param name="Session">The helper's session; null where none opened.</param>
/// <param name="Line">What a terminal and a look say, one line.</param>
public sealed record HelperPassRun(string? Session, string Line)
{
    /// <summary>The state its record ended in: <c>completed</c>, <c>failed</c> or <c>stopped</c>; null where none opened.</summary>
    public string? State { get; init; }

    /// <summary>Why nothing opened: a code of <see cref="HelperWhy"/> (<c>unreadable</c> or <c>not-started</c>).</summary>
    public string? Code { get; init; }

    /// <summary>What the helper said last, bounded as a parked session's words are, where its turn completed: the fallback answer.</summary>
    public string? LastWords { get; init; }
}

/// <summary>The helper pass's own words and bounds (MIXAGENT2a-ii), each said once for every door.</summary>
public static class HelperPass
{
    /// <summary>The door a helper's record is opened by, as the session log's <c>session.started</c> names it.</summary>
    public const string OpenedKind = "helper";

    /// <summary>
    /// How long one helper's turn may run: its own bound, never capped by a session's time (SESSWATCH1a, D200); only the tests' backstop
    /// (<see cref="DriverConfig.TestBound"/>) comes first.
    /// </summary>
    public static int Bound(DriverConfig config) => Math.Max(1, Math.Min(HelperInstruction.DefaultMinutes, config.TestBound ?? int.MaxValue));

    /// <summary>
    /// What a helper's transcript says where this machine has no connector: it can read, but its answer is said only through the
    /// tool, so its last words stand.
    /// </summary>
    public static string NoConnector =>
        $"— no {KnowledgeConnector.ExecutableName} on this machine, so this helper has no connector: it can read the work, but "
        + "cannot say its answer through `helper_answer`, and its last words stand as the answer.";

    /// <summary>The line a helper's transcript opens with: what it reads, where, and what holds it read-only.</summary>
    public static string Opening(string repository, string tip, string posture) =>
        $"— a helper for a session working in `{repository}`, at `{TextBounds.ShortCommit(tip, 7)}`, {OpinionPosture.Said(posture)}: "
        + "it reads in a copy of its own, and nothing in it is taken back.";

    /// <summary>Why a person's words are refused for a helper while it reads, the sentence the bridge carries verbatim.</summary>
    public static string TakesNoWords(string ask) =>
        $"the session answering helper ask `{ask}` takes no words: a helper reads in one turn and answers through its tool, and "
        + "nobody talks to it while it reads.";
}
