using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// Who the actor configuration appoints, now, to take an agents' kind of <i>What needs you</i>: a supervisor the decisions' admission
/// answers for a kind's act at a row's place (held by an agent or by a terminal started this run, granted the act, below high risk), and the
/// steward where a holder holds it. Read from the roles as they stand at each judgement, each answer kept for that judgement alone, so a
/// role appointed, ended or ungranted moves a row's side at the next one.
/// </summary>
/// <remarks>
/// <b>None is appointed by default</b> (<see cref="None"/>, and a shell serving no pipe): every agents' kind is then the owner's, counted
/// and said as no role's yet (<see cref="AttentionOwnership"/>), never left to nobody.
/// </remarks>
public sealed class AttentionTakers
{
    private readonly Func<string, RolePlace, string, bool> _admits;
    private readonly Dictionary<(string Kind, string? Repository, string? Workspace, string Act), bool> _read = [];
    private readonly Lock _gate = new();

    /// <param name="admits">Whether the supervisor that answers a kind at a place is admitted for its act now.</param>
    /// <param name="steward">Whether a holder holds the steward.</param>
    public AttentionTakers(Func<string, RolePlace, string, bool> admits, bool steward)
    {
        _admits = admits;
        Steward = steward;
    }

    /// <summary>No role appointed: no supervisor answers any kind, and no holder holds the steward.</summary>
    public static AttentionTakers None => new((_, _, _) => false, steward: false);

    /// <summary>The roles as <paramref name="judge"/> holds them now: its admission and its steward; none where no pipe is served.</summary>
    public static AttentionTakers Of(StandingConfirmation? judge) =>
        judge is null ? None : new((kind, place, act) => judge.Admit(kind, place, act).Why is null, judge.StewardStaffed);

    /// <summary>Whether a holder holds the steward (<see cref="StandingConfirmation.StewardStaffed"/>).</summary>
    public bool Steward { get; }

    /// <summary>
    /// Whether the supervisor that answers <paramref name="kind"/> at <paramref name="place"/> is admitted for one of <paramref name="acts"/>,
    /// each the kind's own word (<c>answer</c>), as the admission takes it.
    /// </summary>
    public bool Supervises(string kind, RolePlace place, params string[] acts) => acts.Any(act => Admits(kind, place, act));

    private bool Admits(string kind, RolePlace place, string act)
    {
        var key = (kind, place.Repository, place.Workspace, act);
        lock (_gate)
        {
            if (_read.TryGetValue(key, out var known)) return known;
        }

        bool admitted;
        try
        {
            admitted = _admits(kind, place, act);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            // A catalogue or a map that does not read appoints nobody: the row stays the owner's.
            admitted = false;
        }

        lock (_gate) _read[key] = admitted;
        return admitted;
    }
}
