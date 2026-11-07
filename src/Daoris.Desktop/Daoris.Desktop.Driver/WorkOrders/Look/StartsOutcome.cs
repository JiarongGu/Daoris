namespace Daoris.Driver;

/// <summary>What the look's starts came to, for its report: whether any opened a session, and what held, what had no signed-in account or what waits.</summary>
/// <param name="Held">What held at spawn, by quest: the plan said Start and the spawn said no.</param>
/// <param name="SignedOut">The accounts each held quest's start passed not signed in, said beside its hold.</param>
/// <param name="Untrusted">The holds that are the harness's trust, as facts: the screen offers exactly these.</param>
internal sealed record StartsOutcome(
    bool Progressed, Dictionary<string, string> Held, Dictionary<string, SignedOutAccounts> SignedOut, List<TrustHold> Untrusted,
    IReadOnlyList<AccountWait> Waits);
