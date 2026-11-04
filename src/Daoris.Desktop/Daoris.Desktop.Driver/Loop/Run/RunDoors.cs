using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The doors a driven session's run stages reach the driver through: each one a line to the driver's own member or to the part that owns it.</summary>
internal sealed class RunDoors(Driver driver) : IRunWiring
{
    private LoopContext Context => driver.Context;

    LoopContext IRunWiring.Context => Context;

    DriverContinuations IRunWiring.GoOns => driver.GoOns;

    SessionWatch IRunWiring.Watch => driver.Watch;

    string? IRunWiring.ConfigPath => driver.ConfigPath;

    TimeProvider IRunWiring.RetryClock => driver.RetryClock;

    ITransportHost IRunWiring.Transport => new DriverTransportHost(driver);

    TimeSpan IRunWiring.BackgroundLook => driver.BackgroundLook;

    Task<string?> IRunWiring.StepRoleAsync(QuestView quest, string? workspace, CancellationToken ct) => driver.StepRoleAsync(quest, workspace, ct);

    RoleCatalog? IRunWiring.CatalogueForReport() => driver.Walk.CatalogueForReport();

    IReadOnlyList<ActorEffective> IRunWiring.QuestOrder(Consideration start, IReadOnlyList<string>? fence, string? handed, string? role) =>
        driver.Walk.Order(start, fence, handed, role);

    Task<SettledSelection> IRunWiring.WalkBuildPassesAsync(
        Consideration start, IReadOnlyList<RepoView> registry, CancellationToken ct, bool counts, (ActorEffective Entry, string? Account)? refused,
        long except, string? role) => driver.WalkBuildPassesAsync(start, registry, ct, counts, refused, except, role);

    LineCourse IRunWiring.DecideResume(PriorSession record, string questStatus, ResumeChoice resume)
    {
        var course = DrivenCourse.GoOn(record, questStatus, null, false, resume.Answer);
        driver.Decided?.Invoke(course);
        return course;
    }

    async Task<LineCourse> IRunWiring.DecideLimitOwnAsync(PriorSession limited, ResumeChoice own, CancellationToken ct)
    {
        LineAnswer? answer = own is { Own: NextHold.Ready, Selection.Allowed: true } ? new LineAnswer.Resume()
            : own.Answer is LineAnswer.Resume ? null : own.Answer;
        var standing = await DrivenCourse.StandingAsync(driver.Moves.Resolve, limited.Session, ct).ConfigureAwait(false);
        var course = DrivenCourse.LimitOwn(limited, answer, standing);
        driver.Decided?.Invoke(course);
        return course;
    }

    Task<WorkflowBind> IRunWiring.BindAtStartAsync(QuestView quest, string? workspace, CancellationToken ct) =>
        driver.BindAtStartAsync(quest, workspace, ct);

    Action<string>? IRunWiring.RunReturned => driver.RunReturned;

    ReviewDesk? IRunWiring.Reviews => driver.Reviews;

    void IRunWiring.CountMove(string line, string role, string condition, ActorEffective from, string? to) =>
        driver.Walk.CountMove(line, role, condition, from, to);

    SessionHold IRunWiring.Holds => driver.Holds;

    Func<string, string?> IRunWiring.Variable => driver.Variable;

    (string Role, long Revision)? IRunWiring.RoleTaken(Consideration start, string? named) => driver.Walk.RoleTaken(start, named);

    (ProcessStartInfo Info, string? Notice) IRunWiring.Prepare(ISessionAdapter adapter, SessionTarget target, HarnessSelection selection) =>
        driver.Prepare(adapter, target, selection);

    (string? File, object? Meta, HandedSection Handed) IRunWiring.HandRules(
        ISessionAdapter adapter, ProcessStartInfo info, string sessionId, string? workspace, string? repository, string tree, string? kept,
        AcrossReach? across, IReadOnlyList<string>? plugins, IReadOnlyList<string>? continued, HeavyHanded? heavy) =>
        driver.HandRules(adapter, info, sessionId, workspace, repository, tree, kept, across: across, plugins: plugins, continued: continued, heavy: heavy);

    EarlierMaker? IRunWiring.LeftBy(PriorSession prior) => driver.LeftBy(prior);

    Task<SessionParts> IRunWiring.PartsAsync(
        EntryParts entry, string? joinedIn, string? entryTree, TreeOpened? opened, string repository, CancellationToken ct) =>
        driver.PartsAsync(entry, joinedIn, entryTree, opened, repository, ct);

    void IRunWiring.SayMoved(PriorSession prior, string sessionId, string agent, string? account, string why, ComposedBrief brief) =>
        driver.SayMoved(prior, sessionId, agent, account, why, brief);

    Task<(ActorAsked Asked, string Choice)?> IRunWiring.ReaskAsync(
        Consideration start, IReadOnlyList<RepoView> registry, ActorEffective refused, HarnessSelection selection, CancellationToken ct, string? role) =>
        driver.ReaskAsync(start, registry, refused, selection, ct, role);

    Task<(ActorAsked Asked, string Choice)?> IRunWiring.ReaskHandedAsync(
        Consideration start, string agent, HarnessSelection selection, ModelRefusal refusal, HashSet<string> refused, CancellationToken ct, string? role) =>
        driver.Walk.ReaskHandedAsync(start, agent, selection, refusal, refused, ct, role);

    Action<JsonElement> IRunWiring.Said(ISessionAdapter adapter, HarnessSelection selection, string sessionId) => driver.Said(adapter, selection, sessionId);

    Task<bool> IRunWiring.WaitsOnOwnHelperAsync(string session, QuestView quest, DateTimeOffset openedAt, CancellationToken ct) =>
        driver.WaitsOnOwnHelperAsync(session, quest, openedAt, ct);

    Task IRunWiring.ConcludedForOpinionAsync(
        string sessionId, QuestView quest, string? status, string state, string workTree, string? workspace, CancellationToken ct) =>
        driver.ConcludedForOpinionAsync(sessionId, quest, status, state, workTree, workspace, ct);

    SessionConclusion IRunWiring.AccountRefused(
        SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, HarnessEnding ended) =>
        driver.AccountRefused(conclusion, adapter, selection, ended);

    SessionConclusion IRunWiring.ModelRefused(
        SessionConclusion conclusion, string agent, HarnessSelection selection, string? model, HarnessEnding ended, string sessionId) =>
        driver.ModelRefused(conclusion, agent, selection, model, ended, sessionId);

    SessionConclusion IRunWiring.AccountSignedOut(
        SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, string? turnFailed, HarnessEnding? ended) =>
        driver.AccountSignedOut(conclusion, adapter, selection, turnFailed, ended);

    (SessionConclusion Conclusion, bool Limit) IRunWiring.AccountLimited(
        SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, string? turnFailed, string sessionId,
        AcpUsage? used, LimitSeen? byCode) =>
        driver.AccountLimited(conclusion, adapter, selection, turnFailed, sessionId, used, byCode);

    Task<string?> IRunWiring.HandOverAtTurnEndAsync(string session, string quest, CancellationToken ct) =>
        driver.HandOvers.AtTurnEndAsync(session, quest, ct);

    StartReservation IRunWiring.MovingReservation(Consideration start) => ClaimedStart.Moving(Context.Config, start);

    Task<SessionConclusion> IRunWiring.EndStoppedAsync(string sessionId, SessionStop stop) => driver.EndStoppedAsync(sessionId, stop);

    ConversationMoves IRunWiring.Moves => driver.Moves;

    Action<LineCourse>? IRunWiring.Decided => driver.Decided;

    void IRunWiring.Questions(string sessionId, QuestionSettle settled) => driver.Questions(sessionId, settled);

    string? IRunWiring.DesignRoleName(Consideration start, string? named) => driver.Walk.DesignRoleName(start, named);

    ConcurrentDictionary<string, string> IRunWiring.LimitResumed => driver.LimitResumed;

    bool IRunWiring.HandedConnector(ISessionAdapter adapter, string? workspace, string? repository, string tool) =>
        driver.HandedConnector(adapter, workspace, repository, tool);

    AcpMcpServer? IRunWiring.Connector(string sessionId, IReadOnlyDictionary<string, string>? scope, string? proc) =>
        driver.Connector(sessionId, scope, proc);
}
