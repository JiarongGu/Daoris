namespace Daoris.Driver;

/// <summary>
/// What a harness itself said about how its run ended, as the session's end reads it (FOUNDATION1 mod-vii, the design §14.3): whether it said
/// the run failed, its door's failure in its own words, and its own last lines. <see cref="HarnessEnding"/>, which the agents build as a run
/// ends, answers this port, so the judgement of a refused credential and of a failed turn's words names no type above the events.
/// </summary>
public interface IHarnessEnding
{
    /// <summary>Whether the harness itself said the run failed: its door's failure, or an exit that was not 0.</summary>
    bool Failed { get; }

    /// <summary>The door's failure in the harness's words; null where the door said the turn ended.</summary>
    string? Failure { get; }

    /// <summary>The harness's own last lines: on the native door its stderr and what it printed outside its protocol.</summary>
    IReadOnlyList<string> Lines { get; }
}
