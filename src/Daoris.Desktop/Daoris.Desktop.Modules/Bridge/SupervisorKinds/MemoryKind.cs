using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The memory kind: an ended session's staged changes, its subject <c>{ session, memory: "staged" }</c>, kept or dropped under
/// <c>memory.keep</c>; and a session's memory end or replacement awaiting its approver, <c>{ session, memory: &lt;operation&gt; }</c>, approved or
/// declined under <c>memory.decide</c>. Each is held for the role's window with <i>Overrule</i>, then posted to the service's own door as the
/// role's answer (<see cref="SupervisorDoorAnswers"/>); its leave is the person's at once. An end the service's sweep wrote names no session
/// and stays the person's.
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
            if (SupervisorDecisionDoor.Covered(admission, Kind, new RolePlace(Repository: repository)) is not { } covered) continue;
            var subjects = new List<(SupervisorSubject Subject, string Act)>();
            if (Count(wait, "staged") > 0) subjects.Add((new SupervisorSubject(Kind, session, SupervisorDoorActs.Staged), "keep"));
            if (Count(wait, "ends") + Count(wait, "replacements") > 0)
            {
                subjects.AddRange((await AwaitingAsync(service, session, ct).ConfigureAwait(false)).Select(op => (new SupervisorSubject(Kind, session, op), "decide")));
            }

            foreach (var (subject, act) in subjects.Where(each => covered.Acts.Contains(each.Act) && loop.Supervisor.RefersDoor(each.Subject)))
            {
                waiting.Add(new
                {
                    Kind, Subject = subject.Wire(), Repository = repository, covered.Generation, Acts = (IReadOnlyList<string>)[act],
                    Choices = SupervisorDoorActs.Choices(act),
                });
            }
        }

        return waiting;
    }

    public Task<SupervisorDecided> DecideAsync(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder, CancellationToken ct) =>
        KeepAsync(loop, form.Subject, form.Generation, form.Do, form.Choice, form.Reasons, holder, ct);

    public Task<SupervisorDecided> LeaveAsync(DriverLoop loop, SupervisorLeaveForm form, SupervisorHolder holder, CancellationToken ct) =>
        KeepAsync(loop, form.Subject, form.Generation, SupervisorDoorActs.Hold, null, form.Reasons, holder, ct);

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
        if (SupervisorDecisionDoor.Covers(loop, generation, Kind, repository) is { } uncovered) return SupervisorDecided.Refused(uncovered);
        // What the subject names still waits: a session's staged rows, or the end by its operation, as the service reads them now.
        var waits = subject.Id == SupervisorDoorActs.Staged
            ? (await service.MemoryWaitsAsync(ct).ConfigureAwait(false)).Any(wait => Json.Text(wait, "session") == subject.Session && Count(wait, "staged") > 0)
            : (await AwaitingAsync(service, subject.Session, ct).ConfigureAwait(false)).Contains(subject.Id, StringComparer.Ordinal);
        var named = act == SupervisorDoorActs.Hold || act == (subject.Id == SupervisorDoorActs.Staged ? "keep" : "decide");
        var answer = new SupervisorDoorAnswer(subject, repository, generation, act, choice, reasons, holder);
        var kept = await loop.Supervisor.DoorAsync(answer, !waits ? SupervisorWhy.Answered : named ? null : SupervisorWhy.NotOffered, DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        return SupervisorDecided.Of(kept, SupervisorWhy.NotReferred);
    }

    // A session's ends and replacements awaiting their approver, by their operations' ids, as its page reads them.
    private static async Task<IReadOnlyList<string>> AwaitingAsync(ServiceClient service, string session, CancellationToken ct)
    {
        if (await service.SessionMemoryAsync(session, ct).ConfigureAwait(false) is not { Length: > 0 } json) return [];
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("ops", out var ops) || ops.ValueKind != JsonValueKind.Array) return [];
        return [.. ops.EnumerateArray()
            .Where(op => Json.Text(op, "state") == "awaiting" && op.TryGetProperty("change", out var change) && Json.Text(change, "kind") is "end" or "supersede")
            .Select(op => Json.Text(op, "id")).OfType<string>()];
    }

    private static int Count(JsonElement wait, string name) =>
        wait.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;
}
