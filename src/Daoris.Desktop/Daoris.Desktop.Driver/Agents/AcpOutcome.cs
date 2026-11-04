namespace Daoris.Driver;

/// <summary>
/// What a session consumed, as its harness reported it (TOOL3/D57 §4).
/// </summary>
/// <param name="Used">Context the session was holding, in the harness's own units.</param>
/// <param name="Size">The window it was holding it against.</param>
/// <remarks>
/// <para><b>Reported, never computed.</b> Daoris makes no model calls and knows nothing about
/// tokenization — this is a number somebody else's tool volunteered, carried as it was given. No
/// price is attached to it either: what a token costs is the deployment's business (D24), and a
/// price table per model per provider maintained here would be wrong within a month.</para>
///
/// <para><b>The high-water mark, not the last reading.</b> Context drops when a session compacts, so
/// the final number would say a session that nearly filled its window used very little.</para>
/// </remarks>
public sealed record AcpUsage(long Used, long Size);

/// <param name="StopReason">What the agent said ended the turn, verbatim from the wire.</param>
/// <param name="SessionId">The id the agent gave the session, for the cancel verb and the record.</param>
/// <param name="Updates">How many `session/update` notifications arrived — the timeline's raw count.</param>
/// <param name="Usage">
/// The context pressure the agent reported, or <b>null when it reported none</b> — absent, never
/// zero. "Nothing was measured" and "it used nothing" are different claims (TOOL3).
/// </param>
/// <remarks>
/// <b>None of this moves a session record.</b> The record moves on the exit code and the quest's own
/// state (D46 §4), because those are the two signals outside work also produces — and because the ACP
/// wire flattens an aborted, blocked or errored turn to `end_turn`, so its stop reason is a
/// self-report that cannot be told apart from an ordinary ending. What this carries ENRICHES the
/// console and the transcript; it never replaces an observation. The one exception is <see cref="AcpOutcome.Refusal"/>
/// (APIREFUSAL1): the adapter's word that its model's provider refused a call, which is the door saying the work was cut
/// off, as a failed call is (ACPEND1), and not the session describing its work.
/// </remarks>
public sealed record AcpOutcome(
    string StopReason, string SessionId, int Updates, AcpUsage? Usage = null)
{
    /// <summary>
    /// The stop reason a turn ends with when its model's provider refused it (APIREFUSAL1): seen on the install (2026-10-23) after
    /// the adapter said the provider's <c>API Error</c>, with its request id, as the agent's message.
    /// </summary>
    public const string Refusal = "refusal";

    /// <summary>Whether its last turn ended on its provider's refusal (<see cref="Refusal"/>).</summary>
    public bool ProviderRefused => StopReason == Refusal;

    /// <summary>The stop of a session its agent's refusal of the choice ended at the open, before any prompt (D205 §3.3).</summary>
    public const string ChoiceNotOffered = "choice-not-offered";

    /// <summary>The refusal that ended its open before any prompt (D205 §3.3, MODELROLE1c2), or null for every run that prompted.</summary>
    public ModelRefusal? NotOffered { get; init; }

    /// <summary>How its last turn was cut (CODEXPERM1), or null where it was not: see <see cref="TurnCut"/>.</summary>
    public TurnCut? Cut { get; init; }

    /// <summary>What the console says of the turn's end: a cut says what cut it, any other end the agent's stop reason.</summary>
    public string TurnEnd => Cut is { } cut
        ? $"the turn was cut: its agent ended it `{StopReason}` "
          + (cut.Call is { } call ? $"after the permission it asked for, {call}, was refused" : "though nobody stopped it")
          + $", after {Updates} update(s)"
        : $"the turn ended: {StopReason}, after {Updates} update(s)";
}
