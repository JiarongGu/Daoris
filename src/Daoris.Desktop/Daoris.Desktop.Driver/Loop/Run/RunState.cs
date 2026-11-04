namespace Daoris.Driver;

/// <summary>
/// What one run of a driven session carries from stage to stage, handed through its <see cref="RunRound"/>: what a stage chose or
/// found, for the stages after it and for the run once its phase has passed. A stage reads only what a stage it runs after wrote,
/// so the kit's order is the order these are written in.
/// </summary>
/// <remarks>
/// A member a stage writes is unwritten until that stage runs, and reading it earlier throws, naming the stage that writes it: a
/// stage declared without the <c>after</c> its reads need fails at its first run, never on a value left over or a default.
/// </remarks>
internal sealed class RunState : IDisposable
{
    private Slot<TreeLock> _starting;
    private Slot<TreeFound> _tree;
    private Slot<ISessionAdapter?> _preflight;
    private Slot<string> _agent;
    private Slot<HandedQuest?> _handedQuest;
    private Slot<string?> _stepRole;
    private Slot<string> _policyRole;
    private Slot<bool> _handedFrom;
    private Slot<HarnessSelection> _selection;
    private Slot<ActorEffective?> _chosen;
    private Slot<IReadOnlyList<BuildPass>> _buildPasses;
    private Slot<string?> _passedAccount;
    private Slot<bool> _limitWalks;
    private Slot<StaffMoving?> _policyMove;
    private Slot<ResumeChoice?> _resume;
    private Slot<bool> _limitResume;
    private Slot<ActorEffective?> _movedTo;
    private Slot<ResolvedAccess> _access;
    private Slot<DrivenAccess> _permissions;
    private Slot<string> _workTree;
    private Slot<WorkflowBind> _bound;
    private Slot<ReviewEnvironment?> _setUp;

    /// <summary>
    /// The agent the start runs on: a hand-over's, held until the quest leaves this machine, else a continuation's record's own, else
    /// the work's; then the entry the walk took.
    /// </summary>
    public string Agent
    {
        get => _agent.Get(nameof(Agent), "agent");
        set => _agent.Set(value);
    }

    /// <summary>The hand-over that named the quest's agent and account, or null where none did.</summary>
    public HandedQuest? HandedQuest
    {
        get => _handedQuest.Get(nameof(HandedQuest), "agent");
        set => _handedQuest.Set(value);
    }

    /// <summary>The role the run's work step names, which every walk of this start takes in place of the work's; null for the work's.</summary>
    public string? StepRole
    {
        get => _stepRole.Get(nameof(StepRole), "agent");
        set => _stepRole.Set(value);
    }

    /// <summary>The role whose policy this start answers to: its step's, else the one its line was bound to, else the work's at its place.</summary>
    public string PolicyRole
    {
        get => _policyRole.Get(nameof(PolicyRole), "agent");
        set => _policyRole.Set(value);
    }

    /// <summary>Whether this start carries on from the very session the quest was handed over from.</summary>
    public bool HandedFrom
    {
        get => _handedFrom.Get(nameof(HandedFrom), "agent");
        set => _handedFrom.Set(value);
    }

    /// <summary>The harness and the account it runs as.</summary>
    public HarnessSelection Selection
    {
        get => _selection.Get(nameof(Selection), "account");
        set => _selection.Set(value);
    }

    /// <summary>The entry of the role's order this start runs, kept on its record as its choice; null for a continuation, which keeps its record's.</summary>
    public ActorEffective? Chosen
    {
        get => _chosen.Get(nameof(Chosen), "account");
        set => _chosen.Set(value);
    }

    /// <summary>What the walk passed before the entry it took, said in the opening note's actor line.</summary>
    public IReadOnlyList<BuildPass> BuildPasses
    {
        get => _buildPasses.Get(nameof(BuildPasses), "account");
        set => _buildPasses.Set(value);
    }

    /// <summary>The account a hand-over named that the walk passed, or null.</summary>
    public string? PassedAccount
    {
        get => _passedAccount.Get(nameof(PassedAccount), "account");
        set => _passedAccount.Set(value);
    }

    /// <summary>Whether a line a limit or its role's policy cut off walks the role's order as a new start does.</summary>
    public bool LimitWalks
    {
        get => _limitWalks.Get(nameof(LimitWalks), "account");
        set => _limitWalks.Set(value);
    }

    /// <summary>The move the role's policy decided at the record's end this start carries on from, or null.</summary>
    public StaffMoving? PolicyMove
    {
        get => _policyMove.Get(nameof(PolicyMove), "account");
        set => _policyMove.Set(value);
    }

    /// <summary>The continuation's own conversation, resumed on its record's own account, or null where the start walks.</summary>
    public ResumeChoice? Resume
    {
        get => _resume.Get(nameof(Resume), "account");
        set => _resume.Set(value);
    }

    /// <summary>Whether this start resumes a limited record's own conversation on its own account, ready again.</summary>
    public bool LimitResume
    {
        get => _limitResume.Get(nameof(LimitResume), "account");
        set => _limitResume.Set(value);
    }

    /// <summary>
    /// The entry a continuation moved to where its record asked another agent's choice: this agent's own, whose model it carries in
    /// place of the record's.
    /// </summary>
    public ActorEffective? MovedTo
    {
        get => _movedTo.Get(nameof(MovedTo), "account");
        set => _movedTo.Set(value);
    }

    /// <summary>The access setup this start runs under, resolved once, so its record keeps the one it started under.</summary>
    public ResolvedAccess Access
    {
        get => _access.Get(nameof(Access), "access");
        set => _access.Set(value);
    }

    /// <summary>The permission prompts the access setup names in this harness's own mode.</summary>
    public DrivenAccess Permissions
    {
        get => _permissions.Get(nameof(Permissions), "access");
        set => _permissions.Set(value);
    }

    /// <summary>
    /// The repository's trees, held from the tree's choice until the record is open: the run lets it go there, or with the state on any
    /// way out before.
    /// </summary>
    public TreeLock Starting
    {
        get => _starting.Get(nameof(Starting), "tree");
        set => _starting.Set(value);
    }

    /// <summary>How the tree the session runs in stands, and what the start goes on with there.</summary>
    public TreeFound Tree
    {
        get => _tree.Get(nameof(Tree), "tree");
        set => _tree.Set(value);
    }

    /// <summary>Lets go of the repository's trees where the run never reached the record; once let go, a second does nothing.</summary>
    public void Dispose() => _starting.Peek()?.Dispose();

    /// <summary>The tree the session runs in: the one opened for it, the one its earlier session or part went on in, else the checkout.</summary>
    public string WorkTree
    {
        get => _workTree.Get(nameof(WorkTree), "tree");
        set => _workTree.Set(value);
    }

    /// <summary>The adapter the harness trust is asked of, or null where this machine does not know the agent.</summary>
    public ISessionAdapter? Preflight
    {
        get => _preflight.Get(nameof(Preflight), "trust");
        set => _preflight.Set(value);
    }

    /// <summary>The run's workflow binding as the start made it, before its take names a closure graph.</summary>
    public WorkflowBind Bound
    {
        get => _bound.Get(nameof(Bound), "bind");
        set => _bound.Set(value);
    }

    /// <summary>The set-up step this start follows, or null where it is no set-up step.</summary>
    public ReviewEnvironment? SetUp
    {
        get => _setUp.Get(nameof(SetUp), "setup");
        set => _setUp.Set(value);
    }

    /// <summary>The model a continuation's record carries to its agent: the entry it moved to's, else the one its record asked.</summary>
    public string? CarriedModel(PriorSession? record) => MovedTo is { } moved ? moved.Model : record?.Asked?.NamedModel;

    /// <summary>A member's value once a stage wrote it; read before, it throws naming the stage that writes it.</summary>
    private struct Slot<T>
    {
        private T _value;
        private bool _written;

        public readonly T Get(string member, string stage) =>
            _written
                ? _value
                : throw new InvalidOperationException(
                    $"the run's {member} is written by the {stage} stage, which has not run before the stage reading it: that stage runs after {stage}");

        public readonly T? Peek() => _written ? _value : default;

        public void Set(T value) => (_value, _written) = (value, true);
    }
}
