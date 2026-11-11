namespace Daoris.Driver;

/// <summary>
/// The supervisor's decision on a run's review, drawn on the review as the person reads it: its act and where it stands (held with the
/// moment it goes on, released, gone on, or closed), the session that kept it, which <i>Overrule</i> names, and whether it decides the
/// record read. Codes and moments alone; the words are the page's.
/// </summary>
/// <param name="Work">An Accept's kind of work, as the record it is drawn on holds it (<see cref="LandingWork"/>), which the card's sentence is worded by.</param>
public sealed record RunReviewSupervisor(string Id, string Session, string Do, string State, int Attempt, string? Work)
{
    public string? Revision { get; init; }

    /// <summary>Whether it names the record read, so its release decides it (<see cref="SupervisorReviewBinding.Refused(ReviewRecord, string?)"/>).</summary>
    public bool OnRecord { get; init; }

    public string? Holder { get; init; }

    public DateTimeOffset KeptAt { get; init; }

    public DateTimeOffset GoesOnAt { get; init; }

    public DateTimeOffset StateAt { get; init; }

    public string? Why { get; init; }

    /// <summary><paramref name="view"/> drawn on <paramref name="record"/>; null where none is kept.</summary>
    public static RunReviewSupervisor? Of(SupervisorLandingView? view, ReviewRecord? record)
    {
        if (view is null) return null;
        var on = record is not null && record.Attempt == view.Attempt ? record : null;
        return new RunReviewSupervisor(view.Id, view.Session, view.Do, SupervisorStates.Spell(view.State), view.Attempt,
            view.Do == SupervisorLandingActs.Accept ? on?.Kind : null)
        {
            Revision = view.Revision, OnRecord = on is not null && SupervisorReviewBinding.Refused(on, view.Revision) is null, Holder = view.Holder.Kind,
            KeptAt = view.KeptAt, GoesOnAt = view.GoesOnAt, StateAt = view.StateAt, Why = view.Why,
        };
    }
}
