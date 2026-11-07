namespace Daoris.Driver;

/// <summary>
/// What a protocol session hands the part that judges a run's turn ends (<see cref="AcpWaits"/>): the waits on its own work and the
/// carry-on of a done quest's tree. It reads the run's clock, bounds and door through this, and writes only through its prompt door.
/// </summary>
internal interface ITurnEndWiring : ITurnWiring
{
    /// <summary>The driver's own sentence by its code, on the console and in the record.</summary>
    void Note(Noted noted);

    /// <summary>The stream's end as a failure: its cause's where one is said before it, else <paramref name="sentence"/>.</summary>
    DriverException StreamEnded(string sentence);

    /// <summary>One of the run's prompts, read for a transient failure where the run is handed a retry.</summary>
    Task<string> PromptingAsync(Func<Task<string>> prompt, CancellationToken ct);

    /// <summary>One prompt of the driver's own words, written only while <paramref name="when"/> holds (null always holds).</summary>
    Task<string> SendOwnAsync(string text, CancellationToken ct, Action? sent, Func<bool>? when);

    /// <summary>Completes when the agent's stream is over.</summary>
    Task Ended { get; }

    /// <summary>The clock the session's own waits are read off.</summary>
    TimeProvider Clock { get; }

    /// <summary>How long a turn may run silent before a session whose quest no longer waits ends anyway.</summary>
    TimeSpan QuestDoneQuiet { get; }

    /// <summary>How long the session waits, once its work ended, for its harness to wake its agent.</summary>
    TimeSpan WakeQuiet { get; }

    /// <summary>How often a session that waits on its own work reads its quest again.</summary>
    TimeSpan BackgroundLook { get; }

    /// <summary>Whether the run's door has shut, a silent turn left: the run ends at once.</summary>
    bool DoorShut { get; }

    /// <summary>The lock every frame is written under, which a judgement of the run's end is taken under.</summary>
    SemaphoreSlim WriteLock { get; }

    /// <summary>The run's door shuts. Called under <see cref="WriteLock"/>.</summary>
    void Shut();

    /// <summary>The turn the harness woke ended: closed as an answered turn is, with no counts.</summary>
    void EndWokenTurn();
}
