namespace Daoris.Desktop;

/// <summary>
/// One connection's hold of one conversation: in the pipe's state alone (<see cref="PipeState.Holds"/>), never on disk, and
/// forwarding that conversation's lines to the connection (<see cref="PipeForwarding"/>).
/// </summary>
internal sealed class PipeHold(string session, PipeConnection connection, DateTimeOffset since, PipeForwarding forwarding)
{
    public string Session { get; } = session;

    public PipeConnection Connection { get; } = connection;

    public DateTimeOffset Since { get; } = since;

    public PipeForwarding Forwarding { get; } = forwarding;
}

/// <summary>A conversation a terminal holds, as the window reads it: the conversation, the terminal as the system names it, since when.</summary>
public sealed record PipeHeld(string Session, PipeAsker Asker, DateTimeOffset Since);

/// <summary>Every hold, told whole at each change, numbered so a window keeps the newest telling (as <see cref="PipeConfirmationsNow"/> is).</summary>
public sealed record PipeHoldsNow(long Seq, IReadOnlyList<PipeHeld> Holds)
{
    /// <summary>None: a shell that serves no pipe.</summary>
    public static PipeHoldsNow None { get; } = new(0, []);
}
