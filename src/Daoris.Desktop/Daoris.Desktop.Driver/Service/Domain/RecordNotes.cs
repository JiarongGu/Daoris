namespace Daoris.Driver;

/// <summary>
/// The note codes the service client's own reading of a session's record judges by (<see cref="ServiceClient.ReadStrikes"/>, <see cref="ServiceClient.NotOffered"/>),
/// never by the words: an update's close, a provider's outage past its bound, a choice its agent refused at the open, and the account's own
/// refusals and sign-outs. The vocabulary is the events' (<see cref="NoteCodes"/>, which the page's catalogue is held to, one declaration per line),
/// so each spelling here is held equal to its declaration there by <c>RecordNotesTests</c>.
/// </summary>
public static class RecordNotes
{
    /// <summary>An update closed the session (<see cref="NoteCodes.UpdateClosed"/>).</summary>
    public const string UpdateClosed = "update.closed";

    /// <summary>An end past a transient bound, its provider's outage (<see cref="NoteCodes.EndedTransient"/>).</summary>
    public const string EndedTransient = "ended.transient";

    /// <summary>A choice its agent refused at the open (<see cref="NoteCodes.EndedChoiceNotOffered"/>).</summary>
    public const string EndedChoiceNotOffered = "ended.choice-not-offered";

    /// <summary>The lines that say the account was refused or signed out, which is the account's fault and never the quest's (<see cref="NoteCodes.AccountsOwn"/>).</summary>
    public static IReadOnlyList<string> AccountsOwn { get; } =
        ["account.refused", "account.refused-own", "account.signed-out", "account.signed-out-own", "account.refused-model"];
}
