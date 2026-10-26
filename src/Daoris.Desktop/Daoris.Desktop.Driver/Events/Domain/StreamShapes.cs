namespace Daoris.Driver;

/// <summary>One line a session said, and where it sits in that session's stream.</summary>
/// <param name="Sequence">
/// Monotonic within a session, starting at 1. It is how a reader asks for "what I have not seen"
/// without the buffer having to remember who is reading — and how a subscriber that joined mid-stream
/// discovers it missed something.
/// </param>
public sealed record ConsoleLine(long Sequence, string Text);

/// <summary>The kinds a <see cref="SessionStream"/> can be (CONSOLE2).</summary>
public static class SessionStreamKind
{
    /// <summary>A sub-session the harness spawned, streaming as a session of its own.</summary>
    public const string Subagent = "subagent";

    /// <summary>Background work the session started, such as a dev server, whose output is a file.</summary>
    public const string Task = "task";
}

/// <summary>
/// Something a session is running beside itself (CONSOLE2): a subagent, or background work.
/// </summary>
/// <param name="Id">Unique within its session: <c>subagent/&lt;id&gt;</c> or <c>task/&lt;id&gt;</c>, the wire's id.</param>
/// <param name="Kind">A <see cref="SessionStreamKind"/>.</param>
/// <param name="Name">What the harness called it.</param>
/// <param name="CanStop">
/// Whether its harness said it can be stopped (CONSOLE3a) — a task's <c>canStop</c>, in the wire's word.
/// </param>
public sealed record SessionStream(string Id, string Kind, string Name, bool CanStop = false)
{
    /// <summary>
    /// How a stream ended when its session ended first: the harness never said, and nothing it
    /// started outlives the session (ORPHAN1's job object).
    /// </summary>
    public const string SessionEnded = "session-ended";
}
