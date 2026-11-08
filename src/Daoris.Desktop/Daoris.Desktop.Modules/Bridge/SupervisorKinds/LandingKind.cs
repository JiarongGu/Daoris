using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The landing kind: a <c>review</c> step waiting for the person's Accept, or a <c>human</c> approval waiting for Approve or Reject, its
/// subject <c>{ session, landing }</c> naming the run, the node and the attempt it waits on. Its decide is held for the role's window with
/// <i>Overrule</i> and then released as the node's own word on the run's record; its leave is a hold, the person's at once. The step, the
/// person's stop or pause of its line and the landing's plan are read again at the keeping, so a decide naming a step that has moved is
/// refused <c>answered</c>, one on a held line <c>owner-held</c>, one whose plan names a plugin that pushes <c>landing-pushes</c>, and any on
/// a run a go-ahead holds <c>go-ahead-open</c>.
/// </summary>
internal sealed class LandingKind : ISupervisorKind
{
    public string Kind => SupervisorKinds.Landing;

    public Task<SupervisorDecided> DecideAsync(DriverLoop loop, SupervisorDecideForm form, SupervisorHolder holder, CancellationToken ct) =>
        KeepAsync(loop, new Keeping(form.Subject, form.Generation, form.Do, form.Reasons, holder, null), ct);

    public Task<SupervisorDecided> LeaveAsync(DriverLoop loop, SupervisorLeaveForm form, SupervisorHolder holder, CancellationToken ct) =>
        KeepAsync(loop, new Keeping(form.Subject, form.Generation, SupervisorLandingActs.Hold, form.Reasons, holder, form.Bound), ct);

    /// <summary>The steps of runs here waiting for a press a landing supervisor makes and no decision answers yet, in a repository whose supervisor's hold covers an act the step offers.</summary>
    public Task<IReadOnlyList<object>> WaitingAsync(DriverLoop loop, IReadOnlyList<PriorSession> records, IDecisionAdmission admission, CancellationToken ct)
    {
        var waiting = new List<object>();
        foreach (var step in LandingSteps.Waiting(loop.Home))
        {
            if (step.GoAheadOpen || SessionOf(records, step) is not { } session) continue;
            if (SupervisorDecisionDoor.Covered(admission, Kind, new RolePlace(Repository: step.Repository)) is not { } covered) continue;
            if (step.Offered(covered.Acts) is not { Count: > 0 } acts) continue;
            var subject = SupervisorSubject.Landing(session.Session, step.Run, step.Node, step.Attempt);
            if (!loop.Supervisor.Refers(subject)) continue;
            waiting.Add(new
            {
                Kind, Subject = subject.Wire(), Repository = step.Repository, step.Run, step.Node, step.Attempt, covered.Generation,
                Acts = acts,
            });
        }

        return Task.FromResult<IReadOnlyList<object>>(waiting);
    }

    public async Task<string?> FactsAsync(DriverLoop loop, SupervisorSubject subject, string fence, CancellationToken ct)
    {
        if (subject.LandingParts is not { } parts || loop.Service is not { } service) return null;
        if (LandingSteps.Find(loop.Home, parts.Run, parts.Node, parts.Attempt) is not { GoAheadOpen: false } step) return null;
        var recorded = $"run: {step.Run}\nstep: {step.Node}, attempt {step.Attempt}\nrepository: {step.Repository}\nit waits for a press: {string.Join(", ", step.Acts)}";
        return await SupervisorKindFacts.ComposeAsync(
            service, loop.Home, subject.Session, fence, [("the step a landing waits on: Daoris's own record, not the asking agent's", recorded)], ct).ConfigureAwait(false);
    }

    /// <summary>The newest part of the run's work in the step's repository: the session whose line the landing waits on.</summary>
    internal static PriorSession? SessionOf(IReadOnlyList<PriorSession> records, LandingStep step) =>
        records.LastOrDefault(record => !record.Teammate && string.Equals(record.Quest, step.Run, StringComparison.Ordinal)
                                        && string.Equals(record.Repository, step.Repository, StringComparison.OrdinalIgnoreCase));

    private async Task<SupervisorDecided> KeepAsync(DriverLoop loop, Keeping keeping, CancellationToken ct)
    {
        var service = loop.Service ?? throw DriverArguments.NotReady();
        if (keeping.Subject.LandingParts is not { } parts) return SupervisorDecided.Refused(SupervisorWhy.Form);
        if (await service.RecordAsync(keeping.Subject.Session, ct).ConfigureAwait(false) is not { Repository: { } repository } record
            || !string.Equals(record.Quest, parts.Run, StringComparison.Ordinal))
        {
            return SupervisorDecided.Refused(SupervisorWhy.NotReferred);
        }

        if (SupervisorDecisionDoor.Covers(loop, keeping.Generation, Kind, repository) is { } uncovered) return SupervisorDecided.Refused(uncovered);
        var landing = new SupervisorLanding(
            keeping.Subject.Session, parts.Run, parts.Node, parts.Attempt, keeping.Generation, keeping.Do, keeping.Reasons, keeping.Holder, keeping.Bound);
        var refused = await LandingJudge.RefusedAsync(
            LandingSteps.Find(loop.Home, parts.Run, parts.Node, parts.Attempt), keeping.Do, OwnerHolds.Holds(record, loop.Look.Latest),
            () => PushesAsync(loop, record.Session, ct)).ConfigureAwait(false);
        var kept = await loop.Supervisor.LandingAsync(landing, repository, refused, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        return SupervisorDecided.Of(kept, SupervisorWhy.NotReferred);
    }

    // The landing's plan, read through the landing's own route: a plugin it names pushes the branch once made, and a plan that cannot be read is no plan that pushes nothing.
    private static async Task<string?> PushesAsync(DriverLoop loop, string session, CancellationToken ct)
    {
        try
        {
            var plan = await new TreesLandDesk(loop).LandAsync(new TreesLandDesk.LandAsk(session, null, Press: false), ct).ConfigureAwait(false);
            var read = JsonSerializer.SerializeToElement(plan, JsonSerializerOptions.Web);
            if (read.ValueKind != JsonValueKind.Object || !read.TryGetProperty("form", out _)) return SupervisorWhy.NoPlan;
            return read.TryGetProperty("plugin", out var plugin) && plugin.ValueKind != JsonValueKind.Null ? SupervisorWhy.LandingPushes : null;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return SupervisorWhy.NoPlan;
        }
    }

    private sealed record Keeping(
        SupervisorSubject Subject, string Generation, string Do, IReadOnlyList<SupervisorReason> Reasons, SupervisorHolder Holder, string? Bound);
}
