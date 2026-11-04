namespace Daoris.Desktop;

/// <summary>
/// What the pipe holds of its presses and connections, which its parts reach (<see cref="InstancePipe"/>): every member is read and
/// written under <see cref="Gate"/> alone.
/// </summary>
/// <remarks>
/// <para><b>Who writes what.</b> The admission (<see cref="PipeAdmission"/>) binds a press's id in <see cref="Running"/> and
/// <see cref="Owners"/>, and a person's press in <see cref="Reserved"/> while its grounds are read. The asking (<see cref="PipeAsking"/>)
/// claims the reservation and puts the press in <see cref="WaitingFor"/> and <see cref="Lines"/> once its card is shown, as the restore
/// (<see cref="PipeRestore"/>) does for a press brought back; the take-backs (<see cref="PipeTakeBacks"/>) mark <see cref="Withdrawn"/>
/// and take a reservation back. An id leaves <see cref="Running"/> and <see cref="Owners"/> when its run answers, its ending is taken
/// or the close keeps it. The listener adds and removes <see cref="Connections"/>. <see cref="Journal"/> is made once, by the restore or
/// the serve, whichever runs first.</para>
/// <para><b>The order.</b> <see cref="Gate"/> is never held across a wait, a file write or a dispatch. The waiting records' lock
/// (<see cref="PipeWaitingKeep"/>) is never taken inside it, nor it inside that one. The confirmations' lock is only ever taken inside
/// this one, never around it: the confirmations call the pipe's hooks outside their own.</para>
/// </remarks>
internal sealed class PipeState
{
    /// <summary>The one lock every member here is read and written under.</summary>
    public Lock Gate { get; } = new();

    /// <summary>Each press under way by its request's id, with its binding and its answer.</summary>
    public Dictionary<string, (PipeBinding Binding, Task<string> Answer)> Running { get; } = new(StringComparer.Ordinal);

    /// <summary>The connection each press's progress is told to, by its request's id: the last that sent it.</summary>
    public Dictionary<string, PipeConnection> Owners { get; } = new(StringComparer.Ordinal);

    /// <summary>RPC1c: a person's press waiting for the window, by its request's id, to the confirmation that shows it (a cancel takes it back).</summary>
    public Dictionary<string, string> WaitingFor { get; } = new(StringComparer.Ordinal);

    /// <summary>CONFIRMWAIT1a-i (§4.3): the ids the pipe's own WITHDRAW took back, answered `withdrawn`; the kit's CANCEL is answered as before.</summary>
    public HashSet<string> Withdrawn { get; } = new(StringComparer.Ordinal);

    /// <summary>CONFIRMWAIT1a-ii (§7.2): each waiting press's line answer by its request's id, which the close completes `waiting`.</summary>
    public Dictionary<string, TaskCompletionSource<string>> Lines { get; } = new(StringComparer.Ordinal);

    /// <summary>CONFIRMWAIT1 (§15.2 item 9): each press whose grounds are being read before its card, by its request's id.</summary>
    public Dictionary<string, PipeReserved> Reserved { get; } = new(StringComparer.Ordinal);

    /// <summary>The connections open now.</summary>
    public HashSet<PipeConnection> Connections { get; } = [];

    /// <summary>The ids' journal (§6.1), made once by the restore or the serve.</summary>
    public PipeJournal? Journal { get; set; }
}
