namespace Daoris.Driver;

/// <summary>What visits a slot.</summary>
public static class SlotVisitKinds
{
    /// <summary>A terminal the person opened in it.</summary>
    public const string Terminal = "terminal";

    /// <summary>A build served from it to a tab.</summary>
    public const string Served = "served";
}

/// <summary>
/// A terminal or a served build in a slot, admitted before its shell starts or its serving begins and let go when it stops. It holds back
/// a claim that switches the slot, and a claim in flight refuses it.
/// </summary>
/// <param name="Visitor">The terminal's or the served build's id.</param>
/// <param name="Run">The store run that admitted it, so a dead run's visit holds nothing.</param>
public sealed record SlotVisit(string Workspace, string Repository, int Number, string Kind, string Visitor, string Run, DateTimeOffset At);
