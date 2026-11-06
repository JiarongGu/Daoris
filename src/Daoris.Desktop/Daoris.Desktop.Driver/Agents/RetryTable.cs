namespace Daoris.Driver;

/// <summary>The run a failure's retry is decided by (the agent-lyntai design §5.3's table, round 5).</summary>
public enum RetryRun
{
    /// <summary>A driven quest's start or continuation, handed the transient retry.</summary>
    QuestTurn,

    /// <summary>A conversation.</summary>
    Conversation,

    /// <summary>A run handed no retry: a helper's, an intake's, an opinion's, a partner's or a brainstorm's pass.</summary>
    Pass,
}

/// <summary>What a transient failure is met with.</summary>
public enum RetryRule
{
    /// <summary>No episode: the failure ends the run, or its turn, as it came.</summary>
    None,

    /// <summary>The same opening operation repeated inside D210's episode, its handshake not run again and no prompt sent.</summary>
    RepeatOpen,

    /// <summary>D210's episode: the held words and a new guarded go-on, the original never resent.</summary>
    Episode,
}

/// <summary>
/// Retry as a table by door, phase and the run that holds it (the agent-lyntai design §5.3, rounds 4 and 5): each row today's, so a bound
/// kind repeats what today's repeats and nothing else. The native door has no episode on any run until m-ii's native retry.
/// </summary>
public static class RetryTable
{
    /// <summary>The rule for a transient failure met at <paramref name="where"/> (<see cref="DoorWhere"/>).</summary>
    public static RetryRule Of(Transport door, RetryRun run, string where) => (door, run, where) switch
    {
        (Transport.Protocol, RetryRun.QuestTurn, DoorWhere.Open) => RetryRule.RepeatOpen,
        (Transport.Protocol, RetryRun.QuestTurn, DoorWhere.Prompt) => RetryRule.Episode,
        (Transport.Protocol, RetryRun.Conversation, DoorWhere.Prompt) => RetryRule.Episode,
        _ => RetryRule.None,
    };

    /// <summary>
    /// Whether a native resume is tried once more as the same resume (SESSCTX1c, D189's amendment): it exited failing before it named its
    /// conversation, and was not already the once more. The executor's rule above the run, which states whether the run named its
    /// conversation and its exit.
    /// </summary>
    public static bool RestartsResume(Transport door, bool named, bool exitedFailing, bool restarted) =>
        door == Transport.Native && !named && exitedFailing && !restarted;
}
