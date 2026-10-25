namespace Daoris.Driver;

/// <summary>Where one ask for a helper stands, one of <see cref="HelperStates.All"/> (MIXAGENT2a-i's shapes, D163 point 2).</summary>
public static class HelperStates
{
    public const string Waiting = "waiting";
    public const string Approved = "approved";
    public const string Started = "started";
    public const string Answered = "answered";
    public const string Refused = "refused";

    /// <summary>
    /// An account's limit ended its helper's turn with no answer, and the ask stays open, with its account's reset in
    /// <see cref="HelperState.Until"/> (LIMITRESUME1h, D206 design §3.8): run again by the look while the session that asked waits on it,
    /// on another account or at the reset, and refused <see cref="HelperWhy.Limited"/> once that session no longer does.
    /// </summary>
    public const string Cooling = "cooling";

    public static readonly IReadOnlyList<string> All = [Waiting, Approved, Started, Answered, Refused, Cooling];
}

/// <summary>One ask for a helper as <c>&lt;askId&gt;.ask.json</c> keeps it: written by the connector's <c>helper_ask</c> or the person's door, never changed.</summary>
/// <param name="Agent">The agent the asker or the person named, one word; null where none was.</param>
/// <param name="AskedBy"><c>agent</c> or <c>person</c>.</param>
public sealed record HelperAsk(string Id, string Asker, string Question, string? Agent, string AskedBy, DateTimeOffset At)
{
    public const string ByAgent = "agent";
    public const string ByPerson = "person";

    /// <summary>
    /// The asker asked for the answer at its next step (COWORK1d, D188 point 2; the message-urgency design §9): where its door takes
    /// words then, while it works. False for an ask with no level, every ask from before among them: today's delivery.
    /// </summary>
    public bool Urgent { get; init; }
}

/// <summary>
/// One ask's state as the driver keeps it, <c>&lt;askId&gt;.state.json</c> (the driver alone writes it; the service reads it as
/// <c>HelperState</c> and finds an ask by the one <c>started</c> naming its helper): the shape MIXAGENT2a-i settled, with three
/// driver-only marks it ignores: <see cref="LastWords"/>, <see cref="Handed"/> and <see cref="Agent"/>.
/// </summary>
/// <param name="Code">A refusal's code (<see cref="HelperWhy"/>), or why a waiting ask waits; null otherwise.</param>
/// <param name="Helper">The helper's session once started.</param>
public sealed record HelperState(string Id, string State, string? Code, string? Helper, DateTimeOffset At)
{
    /// <summary>The answer is the helper's last words, bounded, since it never used its tool.</summary>
    public bool LastWords { get; init; }

    /// <summary>The word id the answer went to the asker as, once handed.</summary>
    public string? Handed { get; init; }

    /// <summary>
    /// When the driver began handing the answer (HELPERWAIT1; Codex's round-2 #2 on COWORKUX1): written before the hand door is asked and
    /// cleared where it refused, so one still on file with nothing <see cref="Handed"/> is an attempt whose outcome nobody wrote, which is
    /// never made again without evidence of where it went.
    /// </summary>
    public DateTimeOffset? Attempted { get; init; }

    /// <summary>The agent that was started, for the notes.</summary>
    public string? Agent { get; init; }

    /// <summary>The helper's last words, kept here for the delivery where it never used its tool (<see cref="LastWords"/>); no answer file holds them.</summary>
    public string? Words { get; init; }

    /// <summary>
    /// When the account a limit ended the helper's turn on is ready again (LIMITRESUME1h): kept with <see cref="HelperStates.Cooling"/> and
    /// a <see cref="HelperWhy.Limited"/> refusal, null where the limit named no cool-off this machine still holds.
    /// </summary>
    public DateTimeOffset? Until { get; init; }
}

/// <summary>A helper's answer as <c>&lt;askId&gt;.answer.json</c> keeps it, written by <c>helper_answer</c> once.</summary>
public sealed record HelperAnswer(string Id, string Helper, string Answer, DateTimeOffset At);

/// <summary>
/// A session that waited on its own helper, whose helper ended with no answer, and whose move back to the person the service refused
/// (HELPERWAIT1c; the co-worker design §5 point 4, §12 row 11): <c>&lt;session&gt;.back.json</c>, the driver's alone, kept while the
/// refusal stands so <i>What needs you</i> has a record to say it from. An unreachable service keeps none: that is a retry.
/// </summary>
/// <param name="Session">The session that waits, and the file's name.</param>
/// <param name="Quest">The quest it holds.</param>
/// <param name="Words">The service's refusal, as it said it: content, shown as written.</param>
/// <param name="At">When the service first refused it, which the row's wait counts from.</param>
public sealed record HelperBack(string Session, string? Quest, string Words, DateTimeOffset At);
