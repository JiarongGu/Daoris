namespace Daoris.Driver;

/// <summary>How the driver talks to the spawned process once it is running (D53).</summary>
public enum SessionWire
{
    /// <summary>
    /// The harness takes its whole target at once and prints text; the driver reads the text, keeps
    /// it as the transcript, and observes the exit. The original door, and still the default.
    /// </summary>
    Pipe,

    /// <summary>
    /// The harness speaks the **Agent Client Protocol** on stdio: JSON-RPC frames, a session created
    /// on the working tree, the target delivered as a prompt, and tool boundaries, turn boundaries
    /// and thoughts arriving as structured updates.
    /// </summary>
    /// <remarks>
    /// The gain over the pipe is a timeline that needs nothing parsed out of another program's
    /// stdout — the coupling D23/D24 exist to prevent, and which D52 rejected by name. What does NOT
    /// change is where a session record comes from: the wire's own stop reason flattens an aborted,
    /// blocked or errored turn to `end_turn`, so the record still moves on the exit code and the
    /// quest's state (D46 §4) and the wire only enriches the console and the transcript.
    /// </remarks>
    Acp,
}
