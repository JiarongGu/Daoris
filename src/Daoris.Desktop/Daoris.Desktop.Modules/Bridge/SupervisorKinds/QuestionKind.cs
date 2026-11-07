using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The question kind: a parked session's recorded question, its subject <c>{ session, question }</c>. Its decide is an answer with one
/// offered label, held for the supervisor's window; its leave is final at once, nothing said to the session. The park is read again at the
/// keeping, so a decide naming a question the session no longer waits on is refused <c>question-changed</c>.
/// </summary>
internal sealed class QuestionKind : ISupervisorKind
{
    public string Kind => SupervisorKinds.Question;

    public async Task<SupervisorDecided> DecideAsync(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder, CancellationToken ct)
    {
        var (park, why) = await ParkedAsync(loop, form.Subject, form.Generation, ct).ConfigureAwait(false);
        if (park is null) return SupervisorDecided.Refused(why!);
        var kept = await loop.Supervisor.KeepAsync(
            new SupervisorAnswer(form.Subject.Session, form.Subject.Id, form.Generation, form.Choice!, form.Reasons, holder), park, DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        return SupervisorDecided.Of(kept, SupervisorWhy.NotParked);
    }

    public async Task<SupervisorDecided> LeaveAsync(DriverLoop loop, SupervisorLeaveForm form, SupervisorHolder holder, CancellationToken ct)
    {
        var (park, why) = await ParkedAsync(loop, form.Subject, form.Generation, ct).ConfigureAwait(false);
        if (park is null) return SupervisorDecided.Refused(why!);
        var kept = await loop.Supervisor.LeaveAsync(
            new SupervisorDefer(form.Subject.Session, form.Subject.Id, form.Generation, form.Bound, form.Reasons, holder), park, DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        return SupervisorDecided.Of(kept, SupervisorWhy.NotParked);
    }

    /// <summary>
    /// The parked sessions here with a recorded question and no decision on it, in a repository whose supervisor's hold covers an act of
    /// the kind, each with that hold's generation and the acts covered. Names no key.
    /// </summary>
    public async Task<IReadOnlyList<object>> WaitingAsync(DriverLoop loop, IReadOnlyList<PriorSession> records, IDecisionAdmission admission, CancellationToken ct)
    {
        var waiting = new List<object>();
        foreach (var record in records.Where(each => each.Parked && !each.Teammate))
        {
            if (record.Repository is not { } repository || SupervisorDecisionDoor.Covered(admission, Kind, new RolePlace(Repository: repository)) is not { } covered) continue;
            var asked = await Task.Run(() => new QuestionFiles(loop.Home).Read(record.Session), ct).ConfigureAwait(false);
            // A question its agent marked the owner's is never the holder's to look at.
            if (asked is null || asked.Owner is not null || loop.Supervisor.Decided(record.Session, asked.Id)) continue;
            waiting.Add(new
            {
                Kind, Subject = SupervisorSubject.Question(record.Session, asked.Id).Wire(), Repository = repository, Asked = Moments.Write(asked.At),
                covered.Generation, covered.Acts,
            });
        }

        return waiting;
    }

    // The park, where the hold is the one that answers the question's repository; else why not.
    private static async Task<(SupervisedPark? Park, string? Why)> ParkedAsync(DriverLoop loop, SupervisorSubject subject, string generation, CancellationToken ct)
    {
        var service = loop.Service ?? throw DriverArguments.NotReady();
        if (await SupervisedParks.ReadAsync(loop, service, subject.Session, ct).ConfigureAwait(false) is not { } park) return (null, SupervisorWhy.NotParked);
        return SupervisorDecisionDoor.Covers(loop, generation, subject.Kind, park.Repository) is { } uncovered ? (null, uncovered) : (park, null);
    }
}
