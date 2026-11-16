using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The memory kind: an ended session's staged changes, its subject <c>{ session, memory: "staged" }</c>, kept or dropped under
/// <c>memory.keep</c>; and a session's memory end or replacement awaiting its approver, <c>{ session, memory: &lt;operation&gt; }</c>, approved or
/// declined under <c>memory.decide</c>. Each is listed with its facts (the staged rows and their texts; the end's target, reason and
/// proposer, <see cref="SupervisorDoorSubjects.FactsAsync"/>), composed for an agent as for a terminal, and kept pinned to what it
/// judged; held for the role's window with <i>Overrule</i>, then posted to the service's own door as the role's answer
/// (<see cref="SupervisorDoorAnswers"/>); its leave is the person's at once. An end the service's sweep wrote names no session and stays the person's.
/// </summary>
internal sealed class MemoryKind : ISupervisorKind
{
    public string Kind => SupervisorKinds.Memory;

    public async Task<IReadOnlyList<object>> WaitingAsync(DriverLoop loop, IReadOnlyList<PriorSession> records, IDecisionAdmission admission, CancellationToken ct)
    {
        var waiting = new List<object>();
        if (loop.Service is not { } service) return waiting;
        IReadOnlyList<JsonElement> waits;
        try
        {
            waits = await service.MemoryWaitsAsync(ct).ConfigureAwait(false);
        }
        catch (DriverException)
        {
            // A host that answers no memory waits lists none for a supervisor; What needs you reads them as it does.
            return waiting;
        }

        foreach (var wait in waits)
        {
            if (Json.Text(wait, "session") is not { } session || records.LastOrDefault(each => each.Session == session) is not { Repository: { } repository }) continue;
            var subjects = new List<(SupervisorSubject Subject, string Act)>();
            if (Count(wait, "staged") > 0) subjects.Add((new SupervisorSubject(Kind, session, SupervisorDoorActs.Staged), SupervisorDoorActs.Keep));
            if (Count(wait, "ends") + Count(wait, "replacements") > 0)
            {
                var ops = await SupervisorDoorSubjects.OpsAsync(service, session, ct).ConfigureAwait(false);
                subjects.AddRange(SupervisorDoorSubjects.Awaiting(ops).Select(op => (new SupervisorSubject(Kind, session, op), SupervisorDoorActs.Decide)));
            }

            foreach (var (subject, act) in subjects.Where(each => loop.Supervisor.RefersDoor(each.Subject)))
            {
                if (SupervisorDecisionDoor.Covered(admission, subject, new RolePlace(Repository: repository)) is not { } covered || !covered.Acts.Contains(act)) continue;
                // A subject whose facts no longer read stopped waiting between the two reads: it is not listed.
                if (await SupervisorDoorSubjects.FactsAsync(service, subject, ct).ConfigureAwait(false) is not { } facts) continue;
                waiting.Add(new
                {
                    Kind, Subject = subject.Wire(), Repository = repository, covered.Generation, Acts = (IReadOnlyList<string>)[act],
                    Choices = SupervisorDoorActs.Choices(act), Facts = facts.Wire(),
                });
            }
        }

        return waiting;
    }

    public Task<SupervisorDecided> DecideAsync(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder, CancellationToken ct) =>
        KeepAsync(loop, form.Subject, form.Generation, form.Do, form.Choice, form.Reasons, holder, ct);

    public Task<SupervisorDecided> LeaveAsync(DriverLoop loop, SupervisorLeaveForm form, SupervisorHolder holder, CancellationToken ct) =>
        KeepAsync(loop, form.Subject, form.Generation, SupervisorDoorActs.Hold, null, form.Reasons, holder, ct);

    public async Task<string?> FactsAsync(DriverLoop loop, SupervisorSubject subject, string fence, CancellationToken ct) =>
        loop.Service is { } service && await SupervisorDoorSubjects.FactsAsync(service, subject, ct).ConfigureAwait(false) is { } facts
            ? await SupervisorKindFacts.ComposeAsync(service, loop.Home, fence, facts, ct).ConfigureAwait(false)
            : null;

    public async Task<RolePlace?> PlaceOfAsync(DriverLoop loop, SupervisorSubject subject, CancellationToken ct) =>
        loop.Service is { } service && await service.RecordAsync(subject.Session, ct).ConfigureAwait(false) is { Repository: { } repository }
            ? new RolePlace(Repository: repository)
            : null;

    private async Task<SupervisorDecided> KeepAsync(
        DriverLoop loop, SupervisorSubject subject, string generation, string act, string? choice, IReadOnlyList<SupervisorReason> reasons,
        SupervisorHolder holder, CancellationToken ct)
    {
        var service = loop.Service ?? throw DriverArguments.NotReady();
        if (await service.RecordAsync(subject.Session, ct).ConfigureAwait(false) is not { Repository: { } repository }) return SupervisorDecided.Refused(SupervisorWhy.NotReferred);
        if (SupervisorDecisionDoor.Covers(loop, generation, subject, repository) is { } uncovered) return SupervisorDecided.Refused(uncovered);
        // What the subject names still waits, pinned as it stands now: a session's staged rows by their ids, or the end by its operation.
        var pin = await SupervisorDoorSubjects.PinAsync(service, subject, ct).ConfigureAwait(false);
        var named = act == SupervisorDoorActs.Hold || act == (subject.Id == SupervisorDoorActs.Staged ? SupervisorDoorActs.Keep : SupervisorDoorActs.Decide);
        var answer = new SupervisorDoorAnswer(subject, repository, generation, act, choice, reasons, holder) { Pin = pin };
        var kept = await loop.Supervisor.DoorAsync(answer, pin is null ? SupervisorWhy.Answered : named ? null : SupervisorWhy.NotOffered, DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        return SupervisorDecided.Of(kept, SupervisorWhy.NotReferred);
    }

    private static int Count(JsonElement wait, string name) =>
        wait.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;
}
