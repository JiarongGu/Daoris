namespace Daoris.Driver;

/// <summary>
/// Why a protocol-door run's process is ended from outside it, carried from what ends it to the door that reads its stream
/// (BGWAIT2): the driver's wait for the process says the timeout here before it kills the tree, and the capture that made the
/// session hands it the door. Either may come first; the door is told once it has both (<see cref="AcpSession.Ending"/>).
/// </summary>
public sealed class AcpEnding
{
    private readonly object _gate = new();
    private AcpSession? _door;
    private Noted? _why;

    /// <summary>The door whose run this is, once it is made: told now, where why was said first.</summary>
    public void Open(AcpSession door)
    {
        Noted? why;
        lock (_gate)
        {
            _door = door;
            why = _why;
        }

        if (why is not null) door.Ending(why);
    }

    /// <summary>Why the process is about to be ended, said before it is: the first said stands.</summary>
    public void Say(Noted why)
    {
        AcpSession? door;
        lock (_gate)
        {
            if (_why is not null) return;
            _why = why;
            door = _door;
        }

        door?.Ending(why);
    }
}
