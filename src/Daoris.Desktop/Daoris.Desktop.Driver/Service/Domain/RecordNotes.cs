namespace Daoris.Driver;

/// <summary>
/// The note codes the service's own reading of a session's record judges by (<see cref="ServiceClient.ReadStrikes"/>, <see cref="SessionReads.NotOffered(SessionResponse)"/>, and the records
/// <see cref="PriorSession"/>), never by the words: an update's close, a provider's outage past its bound, a choice its agent refused at the open, a
/// steward's stop, a record that ended waiting on its helper, and the account's own refusals and sign-outs. The vocabulary is the events' (<see cref="NoteCodes"/>, which the page's catalogue is held to, one declaration per line),
/// so each spelling here is held equal to its declaration there by <c>RecordNotesTests</c>.
/// </summary>
public static class RecordNotes
{
    /// <summary>A run the watcher ended after its quiet window (<see cref="EndNoteCodes.EndedQuiet"/>).</summary>
    public const string EndedQuiet = "ended.quiet";

    /// <summary>An update closed the session (<see cref="EndNoteCodes.UpdateClosed"/>).</summary>
    public const string UpdateClosed = "update.closed";

    /// <summary>An end past a transient bound, its provider's outage (<see cref="EndNoteCodes.EndedTransient"/>).</summary>
    public const string EndedTransient = "ended.transient";

    /// <summary>A stop that was the steward's (<see cref="EndNoteCodes.EndedStoppedSteward"/>).</summary>
    public const string EndedStoppedSteward = "ended.stopped-steward";

    /// <summary>A checkpoint's stop that was the steward's (<see cref="EndNoteCodes.StoppedCheckpointStoppedSteward"/>).</summary>
    public const string StoppedCheckpointStoppedSteward = "stopped.checkpoint-stopped-steward";

    /// <summary>A quest's work paused by the steward (<see cref="EndNoteCodes.StoppedPausedQuestSteward"/>).</summary>
    public const string StoppedPausedQuestSteward = "stopped.paused-quest-steward";

    /// <summary>A session that ended waiting on its own helper (<see cref="EndNoteCodes.EndedAwaitsHelper"/>).</summary>
    public const string EndedAwaitsHelper = "ended.awaits-helper";

    /// <summary>A choice its agent refused at the open (<see cref="EndNoteCodes.EndedChoiceNotOffered"/>).</summary>
    public const string EndedChoiceNotOffered = "ended.choice-not-offered";

    /// <summary>The lines that say the account was refused or signed out, which is the account's fault and never the quest's (<see cref="AccountNoteCodes.AccountsOwn"/>).</summary>
    public static IReadOnlyList<string> AccountsOwn { get; } =
        ["account.refused", "account.refused-own", "account.signed-out", "account.signed-out-own", "account.refused-model"];
}
