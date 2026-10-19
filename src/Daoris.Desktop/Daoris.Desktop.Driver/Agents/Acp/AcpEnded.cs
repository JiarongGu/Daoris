namespace Daoris.Driver;

/// <summary>
/// A protocol-door run whose agent's process was ended from outside it (BGWAIT2): the stream's end that followed, said as that
/// cause by its own coded line (<see cref="AcpSession.Ending"/>), the timeout's among them, never as the agent having exited.
/// </summary>
/// <remarks>
/// A <see cref="DriverException"/> like every failure of this door, so a caller that reads it as one still does; its message is
/// the line's English, and the record says <see cref="Why"/> by its code.
/// </remarks>
public sealed class AcpEnded(Noted why) : DriverException(why.Note)
{
    /// <summary>Why the stream ended, by its coded line.</summary>
    public Noted Why { get; } = why;
}
