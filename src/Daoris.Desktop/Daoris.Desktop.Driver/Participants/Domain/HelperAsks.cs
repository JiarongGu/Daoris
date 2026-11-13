namespace Daoris.Driver;

/// <summary>Where one ask for a helper stands, one of <see cref="HelperStates.All"/>.</summary>
public static class HelperStates
{
    public const string Waiting = "waiting";
    public const string Approved = "approved";
    public const string Started = "started";
    public const string Answered = "answered";
    public const string Refused = "refused";

    /// <summary>
    /// An account's limit ended its helper's turn with no answer, and the ask stays open, with its account's reset in
    /// <see cref="HelperState.Until"/>: run again by the look while the session that asked waits on it,
    /// on another account or at the reset, and refused <see cref="HelperWhy.Limited"/> once that session no longer does.
    /// </summary>
    public const string Cooling = "cooling";

    public static readonly IReadOnlyList<string> All = [Waiting, Approved, Started, Answered, Refused, Cooling];
}

/// <summary>One ask for a helper as <c>&lt;askId&gt;.ask.json</c> keeps it: written by the connector's <c>helper_ask</c> or the person's door, never changed.</summary>
/// <param name="Agent">The agent the asker or the person named, one word; null where none is.</param>
/// <param name="AskedBy"><c>agent</c> or <c>person</c>.</param>
public sealed record HelperAsk(string Id, string Asker, string Question, string? Agent, string AskedBy, DateTimeOffset At)
{
    public const string ByAgent = "agent";
    public const string ByPerson = "person";

    /// <summary>
    /// The asker asked for the answer at its next step: where its door takes
    /// words then, while it works. False for an ask with no level, every ask from before among them: today's delivery.
    /// </summary>
    public bool Urgent { get; init; }

    /// <summary>
    /// The ask this one follows: the person's <i>Ask again…</i> names the helper's ask it asks
    /// again after, whose question and answer its helper is handed as Daoris's record. Null for a first ask, every agent's among them.
    /// </summary>
    public string? After { get; init; }

    /// <summary>
    /// Where the answer goes: <see cref="ToPerson"/> keeps it for the person, reaching the lead only by their <i>Pass to the lead</i>;
    /// null is the lead, today's one path. Only the person's doors write it.
    /// </summary>
    public string? To { get; init; }

    public const string ToPerson = "person";

    /// <summary>
    /// The part of a team's plan the helper is to orient, which makes it a scout: the pass starts under the Scout role in that part's repository,
    /// and its answer is a pack the service keeps with the plan instead of a word to its asker. Null for every other ask.
    /// </summary>
    public HelperScoutPart? Scout { get; init; }

    /// <summary>Whether the answer is the person's, not the lead's.</summary>
    public bool ForPerson => To == ToPerson;
}

/// <summary>
/// One ask's state as the driver keeps it, <c>&lt;askId&gt;.state.json</c> (the driver alone writes it; the service reads it as
/// <c>HelperState</c> and finds an ask by the one <c>started</c> naming its helper): the shape the service reads, with three
/// driver-only marks it ignores: <see cref="LastWords"/>, <see cref="Handed"/> and <see cref="Agent"/>.
/// </summary>
/// <param name="Code">A refusal's code (<see cref="HelperWhy"/>), or why a waiting ask waits; null otherwise.</param>
/// <param name="Helper">The helper's session once started.</param>
public sealed record HelperState(string Id, string State, string? Code, string? Helper, DateTimeOffset At)
{
    /// <summary>The answer is the helper's last words, bounded, since it never used its tool.</summary>
    public bool LastWords { get; init; }

    /// <summary>The word id the answer went to the asker as, once handed; <see cref="KeptForPerson"/> where it is the person's to pass.</summary>
    public string? Handed { get; init; }

    /// <summary>The <see cref="Handed"/> mark of an answer kept for the person: settled for the lead, which never waits on it.</summary>
    public const string KeptForPerson = "person";

    /// <summary>The <see cref="Handed"/> mark of a scout's answer: its pack is kept with the plan by the service, which the lead reads there, so no word goes to the lead.</summary>
    public const string KeptForPlan = "plan";

    /// <summary>
    /// When the person pressed <i>Pass to the lead</i> on an answer kept for them: the look hands it to
    /// the lead then, with the <c>answers.passed</c> note beside it, so the count reads the press as their word.
    /// </summary>
    public DateTimeOffset? Passed { get; init; }

    /// <summary>
    /// When the driver began handing the answer: written before the hand door is asked and
    /// cleared where it refused, so one still on file with nothing <see cref="Handed"/> is an attempt whose outcome nobody wrote, which is
    /// never made again without evidence of where it went.
    /// </summary>
    public DateTimeOffset? Attempted { get; init; }

    /// <summary>The agent that is started, for the notes.</summary>
    public string? Agent { get; init; }

    /// <summary>The helper's last words, kept here for the delivery where it never used its tool (<see cref="LastWords"/>); no answer file holds them.</summary>
    public string? Words { get; init; }

    /// <summary>
    /// When the account a limit ended the helper's turn on is ready again: kept with <see cref="HelperStates.Cooling"/> and
    /// a <see cref="HelperWhy.Limited"/> refusal, null where the limit named no cool-off this machine still holds.
    /// </summary>
    public DateTimeOffset? Until { get; init; }
}

/// <summary>A helper's answer as <c>&lt;askId&gt;.answer.json</c> keeps it, written by <c>helper_answer</c> once.</summary>
public sealed record HelperAnswer(string Id, string Helper, string Answer, DateTimeOffset At);

/// <summary>
/// A session that waited on its own helper, whose helper ended with no answer, and whose move back to the person the service refused:
/// <c>&lt;session&gt;.back.json</c>, the driver's alone, kept while the
/// refusal stands so <i>What needs you</i> has a record to say it from. An unreachable service keeps none: that is a retry.
/// </summary>
/// <param name="Session">The session that waits, and the file's name.</param>
/// <param name="Quest">The quest it holds.</param>
/// <param name="Words">The service's refusal, as it said it: content, shown as written.</param>
/// <param name="At">When the service first refused it, which the row's wait counts from.</param>
public sealed record HelperBack(string Session, string? Quest, string Words, DateTimeOffset At);
