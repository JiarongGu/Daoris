namespace Daoris.Driver;

/// <summary>Where one repository of a workspace plan stands, read from the facts every time (WSSETUP6, D124 §4.1).</summary>
public enum SetupState
{
    /// <summary>Registered from its line: adopted, and declaring what it owns.</summary>
    SetUp,

    /// <summary>Its set-up closed done, and its work is not on the line yet: the person's review, then <i>Bring up to date</i>.</summary>
    Waiting,

    /// <summary>Its set-up quest is open or taken: a session sets it up, or will.</summary>
    Open,

    /// <summary>Its set-up quest is open or taken and its failed sessions parked it (DRV6).</summary>
    Parked,

    /// <summary>Nothing asked yet: the plan publishes it in its turn.</summary>
    ToGo,

    /// <summary>The press refused it when its turn came, and the plan said why.</summary>
    Skipped,

    /// <summary>Its set-up closed declined: never asked again by the plan.</summary>
    Declined,

    /// <summary>The set-up the plan published is gone, deleted by the person (D95): never asked again by the plan.</summary>
    Deleted,
}

/// <summary>One repository's standing in a plan: its state, the set-up quest that says so, and the sentence a person reads.</summary>
public sealed record SetupStanding(string Repository, SetupState State, string? Quest, string Said);

/// <summary>Who paused a plan (D124 §4.1): the person, the pilot's close, or a doctrine tool this machine cannot run.</summary>
public static class SetupPausedBy
{
    public const string Person = "person";

    public const string Pilot = "pilot";

    public const string Tool = "tool";
}

/// <summary>A plan's pause: who paused it, the sentence that says why, and when.</summary>
public sealed record SetupPause(string By, string Said, DateTimeOffset At);

/// <summary>A repository the press refused when its turn came (D124 §4.1): the refusal's code, its sentences, and when.</summary>
public sealed record SetupSkip(string Code, string Said, DateTimeOffset At);

/// <summary>
/// A workspace's set-up plan, <c>&lt;home&gt;/setup/&lt;workspace&gt;.json</c> (WSSETUP6, D124 §4.1): the person's choices and
/// the quests it published. <b>Nothing in it is a count</b> (D58): progress is read from the quests and the registry every time.
/// </summary>
/// <param name="Order">The repositories, in §4.2's order, less the ones the person unticked.</param>
/// <param name="AtOnce">How many of its set-ups may be open at once, as the person chose it; a tick holds it to the cap's <c>cap − 1</c>.</param>
/// <param name="Pilot">How many go first: once they have all closed, the plan pauses itself until the person resumes it.</param>
public sealed record WorkspaceSetupPlan(string Workspace, IReadOnlyList<string> Order, int AtOnce, int Pilot)
{
    /// <summary>When the press wrote it.</summary>
    public DateTimeOffset Created { get; init; }

    /// <summary>Why it publishes nothing for now, or null while it works.</summary>
    public SetupPause? Paused { get; init; }

    /// <summary>Whether the person resumed it after the pilot's pause, so the pilot never pauses it again.</summary>
    public bool PilotResumed { get; init; }

    /// <summary>When the person stopped it, or null: a stopped plan publishes nothing, and a new press may replace it.</summary>
    public DateTimeOffset? Stopped { get; init; }

    /// <summary>Each repository's set-up quest, by the repository's name, as the plan published it.</summary>
    public IReadOnlyDictionary<string, string> Published { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Each repository's index quest (ORIENT2c), by the repository's name, as the plan published it once the repository was
    /// set up: the plan asks it after the set-up, one at a time with the set-ups.
    /// </summary>
    public IReadOnlyDictionary<string, string> Indexed { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Each repository the press refused at its turn, with why, by the repository's name.</summary>
    public IReadOnlyDictionary<string, SetupSkip> Skipped { get; init; } =
        new Dictionary<string, SetupSkip>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the plan still works: made, and not stopped.</summary>
    public bool Live => Stopped is null;
}

/// <summary>What other work touched a repository, from the facts this machine keeps (D124 §4.2).</summary>
/// <param name="Asked">Quests addressed to it by anyone but itself: another repository, an ask, the person. A set-up is not one.</param>
/// <param name="Read">Tool calls of other repositories' sessions that touched a file in its checkout, each call once (D76).</param>
/// <param name="Sessions">Sessions this machine ran in it.</param>
public sealed record SetupTouches(int Asked, int Read, int Sessions)
{
    public static SetupTouches None { get; } = new(0, 0, 0);
}

/// <summary>A file one tool call touched, as the conversation record names it (D76): the call's id and the path.</summary>
public sealed record ToolTouch(string Call, string Path);

/// <summary>The catalogue of <see cref="SetupLine"/> lines, which the owner of what they say keeps: each builder writes the fields its line gives it and nothing else.</summary>
public static class SetupLines
{
    extension(SetupLine)
    {
        /// <summary>The press wrote a plan.</summary>
        public static SetupLine Planned(string workspace, int repositories, int atOnce, int pilot) =>
            new("setup.planned", [("workspace", workspace), ("repositories", repositories), ("atOnce", atOnce), ("pilot", pilot)]);

        /// <summary>A tick published a repository's set-up.</summary>
        public static SetupLine Published(string workspace, string repository, string quest) =>
            new("setup.published", [("workspace", workspace), ("repository", repository), ("quest", quest)]);

        /// <summary>A tick skipped a repository the press refused, by the refusal's code.</summary>
        public static SetupLine Skipped(string workspace, string repository, string refusal) =>
            new("setup.skipped", [("workspace", workspace), ("repository", repository), ("refusal", refusal)]);

        /// <summary>The plan paused, by <see cref="SetupPausedBy"/>'s word.</summary>
        public static SetupLine Paused(string workspace, string by) => new("setup.paused", [("workspace", workspace), ("by", by)]);

        /// <summary>The person resumed it.</summary>
        public static SetupLine Resumed(string workspace) => new("setup.resumed", [("workspace", workspace)]);

        /// <summary>The person stopped it.</summary>
        public static SetupLine Stopped(string workspace) => new("setup.stopped", [("workspace", workspace)]);
    }
}

/// <summary>The person's choices for a workspace press (D124 §4.5): each null or empty is the default.</summary>
public sealed record WorkspaceSetupOptions(
    int? AtOnce = null, int? Pilot = null, IReadOnlyList<string>? First = null, IReadOnlyList<string>? Skip = null);

/// <summary>One repository of a press's list: what touched it, where it stands, and the press's refusals now, where it is still to go.</summary>
public sealed record WorkspaceSetupRow(string Repository, SetupTouches Touches, SetupStanding Standing)
{
    public IReadOnlyList<SetupRefusal> Refusals { get; init; } = [];
}

/// <summary>What a workspace press would write, or why it would not: plan, never apply.</summary>
public sealed record WorkspaceSetupPreview(string Workspace)
{
    /// <summary>The repositories with a checkout here, in the order the plan would work them.</summary>
    public IReadOnlyList<WorkspaceSetupRow> Rows { get; init; } = [];

    /// <summary>The repositories the person unticked (<c>--skip</c>).</summary>
    public IReadOnlyList<string> Unticked { get; init; } = [];

    /// <summary>The workspace's rows with no checkout here: their own machine's driver sets them up.</summary>
    public IReadOnlyList<string> NoCheckout { get; init; } = [];

    public int AtOnce { get; init; } = WorkspaceSetupRules.DefaultAtOnce;

    public int Pilot { get; init; } = WorkspaceSetupRules.DefaultPilot;

    /// <summary>The driver's cap when this was read, which bounds <see cref="AtOnce"/>.</summary>
    public int Cap { get; init; }

    /// <summary>The agent this machine's driven sessions start on, which would carry each set-up.</summary>
    public string? Adapter { get; init; }

    /// <summary>Why the press as a whole would not write a plan; none is a press that writes.</summary>
    public IReadOnlyList<SetupRefusal> Refusals { get; init; } = [];

    public bool Pressable => Refusals.Count == 0 && Rows.Count > 0;
}

/// <summary>What a workspace press came to: the plan written, and the rules it added to the workspace's scope.</summary>
public sealed record WorkspaceSetupOutcome(bool Written, string Message, WorkspaceSetupPlan? Plan, IReadOnlyList<string> Added);

/// <summary>What a pause, a resume or a stop came to.</summary>
public sealed record WorkspaceSetupSteer(bool Ok, string Message, WorkspaceSetupPlan? Plan);

/// <summary>Why a workspace press as a whole would not write a plan (D124 §4.1, §4.5), beside the tools' own refusals.</summary>
public static class WorkspaceSetupRefusals
{
    /// <summary>No repository of the workspace has a checkout here.</summary>
    public const string NoRepository = "no-repository";

    /// <summary>A plan for the workspace already works: never two, so never two set-ups for one repository.</summary>
    public const string PlanWorking = "plan-working";

    /// <summary>More at once than the cap's <c>cap − 1</c>, or fewer than one.</summary>
    public const string AtOnce = "at-once";

    /// <summary>A repository named to go first or to be left out that is not one of the workspace's here, or named both ways.</summary>
    public const string Named = "named";

    /// <summary>The word a skip carries when the service refused the ask, beside the press's own codes (<see cref="SetupRefusals"/>).</summary>
    public const string ServiceRefused = "service-refused";
}
