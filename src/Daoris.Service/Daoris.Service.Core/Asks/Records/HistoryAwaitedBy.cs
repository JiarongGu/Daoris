namespace Daoris.Knowledge;

/// <summary>
/// The open work that names the unit, where it stays for <see cref="HistoryRefusal.Awaited"/>: named by the desk that
/// judged it, so the driver says *chain* from the refusal rather than from a second read of the quest it names.
/// </summary>
public enum HistoryAwaitedBy
{
    /// <summary>An open question a session of the work published.</summary>
    Question,

    /// <summary>A taken quest outside the work that asked it and waits on its answer (H7).</summary>
    Asker,

    /// <summary>A chain's open next step, which builds on the work's last session here.</summary>
    Step,
}
