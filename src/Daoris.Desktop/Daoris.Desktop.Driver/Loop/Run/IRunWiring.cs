using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a driven session's run reaches of the driver that runs it: its stages, the hold that runs its process and the protocol door it
/// drives. The driver implements it; a stage names only these, never the driver, so it lives in its own subdomain and the driver holds
/// the order and the wiring alone.
/// </summary>
internal interface IRunWiring
{
    /// <summary>The driver's home, configuration, service, records, harnesses and the sessions it runs.</summary>
    LoopContext Context { get; }

    /// <summary>A continuation's words, what holds them, and a native run's go-on with what the person said while it worked.</summary>
    DriverContinuations GoOns { get; }

    /// <summary>The watcher's view of the running sessions, which a run's stream stamps and its waits tell.</summary>
    SessionWatch Watch { get; }

    /// <summary>The driver's configuration file, read again where a running session's permission prompts may have changed; null where none is kept.</summary>
    string? ConfigPath { get; }

    /// <summary>The clock a transient retry's waits are on.</summary>
    TimeProvider RetryClock { get; }

    /// <summary>What a session's process reaches of the driver: its lineage, its stream and its spawn.</summary>
    ITransportHost Transport { get; }

    /// <summary>How often a session waiting on its own background work has its quest read again.</summary>
    TimeSpan BackgroundLook { get; }

    /// <summary>The role the run's work step names, which every walk of the start takes in place of the work's; null for the work's.</summary>
    Task<string?> StepRoleAsync(QuestView quest, string? workspace, CancellationToken ct);

    /// <summary>The machine's role catalogue as a start's report reads it; null where it does not read.</summary>
    RoleCatalog? CatalogueForReport();

    /// <summary>The order the role taking a quest's work walks, with the fence, or the hand-over's agent, a caller names.</summary>
    IReadOnlyList<ActorEffective> QuestOrder(
        Consideration start, IReadOnlyList<string>? fence = null, string? handed = null, string? role = null);

    /// <summary>The walk of the role's order for a start, with each choice it passed and why.</summary>
    Task<SettledSelection> WalkBuildPassesAsync(
        Consideration start, IReadOnlyList<RepoView> registry, CancellationToken ct, bool counts = true,
        (ActorEffective Entry, string? Account)? refused = null, long except = 0, string? role = null);

    /// <summary>Whether a continuation's own conversation resumes now or waits for its account.</summary>
    LineCourse DecideResume(PriorSession record, string questStatus, ResumeChoice resume);

    /// <summary>Whether a line a limit cut off resumes on its own account, now ready again.</summary>
    Task<LineCourse> DecideLimitOwnAsync(PriorSession limited, ResumeChoice own, CancellationToken ct);

    /// <summary>The run bound to its workflow as it starts, once its tree is open; a bind not made, or a binding that does not read, holds.</summary>
    Task<WorkflowBind> BindAtStartAsync(QuestView quest, string? workspace, CancellationToken ct);

    /// <summary>Told a graph run that ended and whose chain came back to a start, so the runtime re-arms for it; null where nothing listens.</summary>
    Action<string>? RunReturned { get; }

    /// <summary>The desk a local set-up step's tab opens on; null where no shell carries one.</summary>
    ReviewDesk? Reviews { get; }

    /// <summary>A move of a line from one choice to another, counted against its role's bound.</summary>
    void CountMove(string line, string role, string condition, ActorEffective from, string? to);

    /// <summary>What starts a prepared session and holds it to its conclusion.</summary>
    SessionHold Holds { get; }

    /// <summary>The environment the driver reads its home and the remote's variables from.</summary>
    Func<string, string?> Variable { get; }

    /// <summary>The role a start took and its revision, as the binding names them; null for a continuation.</summary>
    (string Role, long Revision)? RoleTaken(Consideration start, string? named);

    /// <summary>The process a session starts as, with the notice its harness leaves; <paramref name="prepared"/> a resume's own spawn, where it is one.</summary>
    (ProcessStartInfo Info, string? Notice) Prepare(AgentDoor adapter, SessionTarget target, HarnessSelection selection, ProcessStartInfo? prepared = null);

    /// <summary>The rules one session may run under, composed for its circle and repository and handed as the harness's own settings tier.</summary>
    (string? File, object? Meta, HandedSection Handed) HandRules(
        AgentDoor adapter, ProcessStartInfo info, string sessionId, string? workspace, string? repository, string tree, string? kept,
        AcrossReach? across, IReadOnlyList<string>? plugins, IReadOnlyList<string>? continued, HeavyHanded? heavy);

    /// <summary>The maker a carry-on's earlier part ran on, which its brief credits the uncommitted work to; null where none ran.</summary>
    EarlierMaker? LeftBy(PriorSession prior);

    /// <summary>What a further part of a session is handed of the parts before it.</summary>
    Task<SessionParts> PartsAsync(
        EntryParts entry, string? joinedIn, string? entryTree, TreeOpened? opened, string repository, CancellationToken ct);

    /// <summary>The note a carry-on's brief leaves on the record it goes on from; <paramref name="notCarried"/> why a limit's move could not carry its conversation.</summary>
    void SayMoved(PriorSession prior, string sessionId, string agent, string? account, string why, ComposedBrief brief, string? notCarried = null);

    void SayCarried(PriorSession prior, string sessionId, string agent, string? account, string why, CarryPlan.Carried carried);

    /// <summary>The re-ask of a start whose agent refused the model it asked at the open, on the walk's later entries.</summary>
    Task<(ActorAsked Asked, string Choice)?> ReaskAsync(
        Consideration start, IReadOnlyList<RepoView> registry, ActorEffective refused, HarnessSelection selection, CancellationToken ct, string? role);

    /// <summary>The re-ask of a handed quest's start whose agent refused the model it asked at the open.</summary>
    Task<(ActorAsked Asked, string Choice)?> ReaskHandedAsync(
        Consideration start, string agent, HarnessSelection selection, ModelRefusal refusal, HashSet<string> refused, CancellationToken ct, string? role);

    /// <summary>What the harness's lines of a run say to the session's structure and its console.</summary>
    Action<JsonElement> Said(AgentDoor adapter, HarnessSelection selection, string sessionId);

    /// <summary>The account's part of a run's sink for the native door's limit (<see cref="NativeLimits"/>).</summary>
    NativeLimits NativeLimit(AgentDoor adapter);

    /// <summary>Whether a driven run that ends holding its quest waits on its own helper.</summary>
    Task<bool> WaitsOnOwnHelperAsync(string session, QuestView quest, DateTimeOffset openedAt, CancellationToken ct);

    /// <summary>A driven record that just concluded: a done whose rule reads it is owed a second opinion, which a later look asks.</summary>
    Task ConcludedForOpinionAsync(
        string sessionId, QuestView quest, string? status, string state, string workTree, string? workspace, CancellationToken ct);

    /// <summary>The conclusion of a run its harness refused the account for, said with the harness's own words.</summary>
    SessionConclusion AccountRefused(SessionConclusion conclusion, AgentDoor adapter, HarnessSelection selection, HarnessEnding ended);

    /// <summary>The conclusion of a run whose model its agent refused.</summary>
    SessionConclusion ModelRefused(
        SessionConclusion conclusion, string agent, HarnessSelection selection, string? model, HarnessEnding ended, string sessionId);

    /// <summary>The conclusion of a run whose account is signed out.</summary>
    SessionConclusion AccountSignedOut(
        SessionConclusion conclusion, AgentDoor adapter, HarnessSelection selection, string? turnFailed, HarnessEnding? ended);

    /// <summary>The conclusion of a run an account's limit cut off, and the limit it read.</summary>
    (SessionConclusion Conclusion, bool Limit) AccountLimited(
        SessionConclusion conclusion, AgentDoor adapter, HarnessSelection selection, string? turnFailed, string sessionId,
        AcpUsage? used, LimitSeen? byCode);

    /// <summary>The reservation that keeps a look from planning a quest again between a start's refused end and its move to another account.</summary>
    StartReservation MovingReservation(Consideration start);

    /// <summary>The quest handed over at a turn's end where the session's agent asked and the person's rule or yes lets it go; null where none.</summary>
    Task<string?> HandOverAtTurnEndAsync(string session, string quest, CancellationToken ct);

    /// <summary>The record of a run whose wire broke after a stop made first, ended as that stop's conclusion would have ended it.</summary>
    Task<SessionConclusion> EndStoppedAsync(string sessionId, SessionStop stop);

    /// <summary>The move record a driven carry-on keeps, decided before its open.</summary>
    ConversationMoves Moves { get; }

    /// <summary>Told each answer the course gave a driven path as it is decided; a test's, no person's.</summary>
    Action<LineCourse>? Decided { get; }

    /// <summary>A park's question the disk would not let go at a run's opening or conclusion, logged.</summary>
    void Questions(string sessionId, QuestionSettle settled);

    /// <summary>The name of the role a design quest's start took, as its opening says it; null where the catalogue will not read.</summary>
    string? DesignRoleName(Consideration start, string? named);

    /// <summary>The lines resumed in their own conversation on a limit's word, by quest, which the look says once it sees the line go on.</summary>
    System.Collections.Concurrent.ConcurrentDictionary<string, string> LimitResumed { get; }

    /// <summary>Whether the rules a session would be handed let it call the connector's tool unasked, which a harness that takes no rules cannot.</summary>
    bool HandedConnector(AgentDoor adapter, string? workspace, string? repository, string tool);

    /// <summary>The knowledge server a session is offered, located per run; null where this machine has no connector.</summary>
    ServerEntry? Connector(string sessionId, IReadOnlyDictionary<string, string>? scope, string? proc);
}
