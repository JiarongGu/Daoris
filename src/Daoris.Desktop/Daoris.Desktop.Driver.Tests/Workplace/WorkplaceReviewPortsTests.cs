using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-x, the Workplace-names-Review cut: the seams it adds. The landing gate read by the workplace as <see cref="IWorkplaceGate"/>,
/// the world the look reads as <see cref="IWorkplaceLandingWorld"/> that the review's <see cref="IAutoLandingWorld"/> answers whole, the
/// operation the review installs for a second opinion owed, the lower words the review's names alias, and the review's parts of the trees'
/// gate read through the port the workflows install. The behaviour they keep is <see cref="WorkplaceReviewBoundaryTests"/>'.
/// </summary>
public sealed class WorkplaceReviewPortsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-review", Guid.NewGuid().ToString("N")[..8]);

    public WorkplaceReviewPortsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    private static LandingGate Holds() =>
        LandingGate.Open("engine") with { Opinion = new OpinionGateState(OpinionGateStates.Unread, "engine") { Problem = "the host did not answer" } };

    // ——— The gate, as the workplace reads it

    [Fact]
    public void The_landing_gate_answers_the_facet_with_the_values_it_has()
    {
        IWorkplaceGate open = LandingGate.Open("engine");
        Assert.Null(open.Refusal);
        Assert.Null(open.Process);
        Assert.Null(open.Tip);
        Assert.Null(open.ReviewLanding);
        Assert.Null(open.OpinionLanding);
        Assert.False(open.GraphAsksPass);
        Assert.Empty(open.PlanLines());
        Assert.Null(open.Held("s1", ("aurora", "engine"), new TreeLanding(true, "landed"), LandingDoors.Look));

        IWorkplaceGate holds = Holds();
        Assert.Equal(AutoLandingCode.Opinion, holds.Refusal!.Refusal);
        Assert.Equal(LandingGateWords.Plan((LandingGate)holds), holds.PlanLines());
        var line = holds.Held("s1", ("aurora", "engine"), holds.Refusal, LandingDoors.Look);
        Assert.Equal(LandingGateWords.Held("s1", ("aurora", "engine"), (LandingGate)holds, holds.Refusal, ReviewDoors.Look)!.Event, line!.Event);
        Assert.Equal(LandingGateWords.Held("s1", ("aurora", "engine"), (LandingGate)holds, holds.Refusal, ReviewDoors.Look)!.Data, line.Data);
    }

    [Fact]
    public void A_gate_pinned_to_a_tip_is_the_same_gate_with_only_that_tip_changed()
    {
        var holds = Holds();
        var pinned = ((IWorkplaceGate)holds).WithTip("c0ffee");

        var gate = Assert.IsType<LandingGate>(pinned);
        Assert.Equal("c0ffee", pinned.Tip);
        Assert.Equal(holds with { Tip = "c0ffee" }, gate);
        Assert.Null(holds.Tip);
        Assert.Equal(holds.Refusal!.Message, pinned.Refusal!.Message);
    }

    [Fact]
    public void The_words_the_workplace_spells_are_the_ones_the_review_names_alias()
    {
        Assert.Equal(LandingDoors.Screen, ReviewDoors.Screen);
        Assert.Equal(LandingDoors.Terminal, ReviewDoors.Terminal);
        Assert.Equal(LandingDoors.Look, ReviewDoors.Look);
        Assert.Equal(SessionHomeFiles.ChecksSuffix, SessionChecks.Suffix);
        Assert.Equal(PassLeftovers.OpinionCopyPrefix, OpinionTree.Prefix);
        Assert.Equal(CommitRules.RepositorySource, OpinionSource.Repository);
        Assert.Equal(CommitRules.WorkspaceSource, OpinionSource.Workspace);
        Assert.Equal(GeneratedWriters.SystemShell(), HeavyStep.SystemShell());
    }

    // ——— The look's world

    private sealed class World : IAutoLandingWorld
    {
        public IAutoLandingWorld? Next { get; init; }

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([]);

        public Task<AskView?> AskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);

        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => Task.FromResult<OpinionView?>(null);

        public Task<QuestView?> QuestAsync(string id, CancellationToken ct) => Task.FromResult<QuestView?>(null);

        public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SessionRecord>>([]);

        public Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

        public IAutoLandingWorld Again() => Next ?? this;
    }

    [Fact]
    public void A_world_that_keeps_nothing_answers_itself_and_one_that_reads_afresh_answers_through_the_workplaces_facet()
    {
        var plain = new PlainWorld();
        IWorkplaceLandingWorld asked = plain;
        Assert.Same(plain, asked.Again());
        Assert.Same(plain, ((IAutoLandingWorld)plain).Again());

        var fresh = new World();
        var reading = new World { Next = fresh };
        Assert.Same(fresh, ((IWorkplaceLandingWorld)reading).Again());
        Assert.Same(fresh, ((IAutoLandingWorld)reading).Again());
    }

    /// <summary>A world that implements none of the optional members, as a stand-in that keeps nothing would.</summary>
    private sealed class PlainWorld : IAutoLandingWorld
    {
        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([]);

        public Task<AskView?> AskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);

        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => Task.FromResult<OpinionView?>(null);

        public Task<QuestView?> QuestAsync(string id, CancellationToken ct) => Task.FromResult<QuestView?>(null);

        public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SessionRecord>>([]);

        public Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
    }

    [Fact]
    public async Task The_say_door_a_world_has_none_of_says_nothing()
    {
        IWorkplaceLandingWorld world = new PlainWorld();
        Assert.Null(await world.SayAsync("s1", "words", "key", CancellationToken.None));
    }

    // ——— A second opinion owed

    [Fact]
    public void The_installed_operation_is_the_reviews_and_owes_one_opinion_at_the_landing_occasion()
    {
        var dues = new OpinionDues(_home);
        var reads = 0;
        DateTimeOffset Clock() { reads++; return At; }

        WorkplaceReview.Current.OweLanding(_home, "s1", "q1", "engine", "aurora", "/tree", Clock, since: null);
        var owed = Assert.Single(dues.All());
        Assert.Equal(("s1", "q1", "engine", "aurora", OpinionRules.Landing, At), (owed.Session, owed.Quest, owed.Repository, owed.Workspace, owed.Occasion, owed.At));
        Assert.Equal(1, reads);

        // One owed there already: nothing more is made, and the clock is not read.
        WorkplaceReview.Current.OweLanding(_home, "s1", "q1", "engine", "aurora", "/tree", Clock, since: null);
        Assert.Single(dues.All());
        Assert.Equal(1, reads);
    }

    [Fact]
    public void A_due_the_loop_closed_as_landed_since_the_done_is_not_opened_again_but_a_due_closed_before_it_is()
    {
        var dues = new OpinionDues(_home);
        WorkplaceReview.Current.OweLanding(_home, "s1", "q1", "engine", "aurora", "/tree", () => At, since: null);
        dues.Close("s1", OpinionRules.Landing, OpinionDueClosed.Landed, At.AddMinutes(5));

        WorkplaceReview.Current.OweLanding(_home, "s1", "q1", "engine", "aurora", "/tree", () => At.AddMinutes(9), since: At);
        Assert.Empty(dues.Open());

        WorkplaceReview.Current.OweLanding(_home, "s1", "q1", "engine", "aurora", "/tree", () => At.AddMinutes(9), since: At.AddMinutes(6));
        var reopened = Assert.Single(dues.Open());
        Assert.Equal(At.AddMinutes(9), reopened.At);
    }

    [Fact]
    public void A_due_list_that_cannot_be_written_leaves_the_person_s_ask_and_throws_nothing()
    {
        var blocked = Path.Combine(_home, "blocked");
        File.WriteAllText(blocked, "not a folder");

        WorkplaceReview.Current.OweLanding(blocked, "s1", "q1", "engine", "aurora", "/tree", () => At, since: null);
    }

    [Fact]
    public void A_missing_operation_is_required_only_where_it_is_reached()
    {
        var actual = WorkplaceReview.Current;
        try
        {
            WorkplaceReview.Current = null!;
            Assert.Contains("Review installs them", Assert.Throws<InvalidOperationException>(() => WorkplaceReview.Current).Message);
        }
        finally { WorkplaceReview.Current = actual; }
    }

    // ——— The review's parts of the trees' gate

    private sealed class Gates(LandingGate gate) : IWorkplaceGateWorkflows
    {
        public string? Handed { get; private set; }

        public Task<IWorkplaceGate> GateAsync(
            TreeGates gates, string path, string? quest, IReviewWorld world, string? session, CancellationToken ct, IWorkplaceProcess? process,
            string? tip, string? run)
        {
            Handed = $"{Path.GetFileName(path)}:{quest}:{session}:{tip}:{run}:{world.GetType().Name}";
            return Task.FromResult<IWorkplaceGate>(gate);
        }

        public Task<IWorkplaceProcess> ProcessAsync(
            TreeGates gates, DriverConfig config, string repository, string? workspace, string? quest, IReviewWorld world, CancellationToken ct, string? run) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task The_trees_answer_the_whole_gate_as_the_review_s_record_over_the_workplace_s_port()
    {
        var actual = WorkplaceGateWorkflows.Current;
        try
        {
            var holds = Holds();
            var gates = new Gates(holds);
            WorkplaceGateWorkflows.Current = gates;
            var trees = new SessionTrees(_home);
            var world = new World();

            var read = await trees.GateAsync(Path.Combine(_home, "tree"), "q1", world, "s1", tip: "abc", run: "chat-s1");
            Assert.Same(holds, read);
            Assert.Equal("tree:q1:s1:abc:chat-s1:World", gates.Handed);

            var asked = await trees.GateOfAsync(Path.Combine(_home, "tree"), "q1", world, "s1");
            Assert.Same(holds, asked);
        }
        finally { WorkplaceGateWorkflows.Current = actual; }
    }

    [Fact]
    public async Task The_second_opinion_s_part_needs_no_port_and_asks_nothing_where_no_rule_reads()
    {
        var trees = new SessionTrees(_home);
        var state = await trees.OpinionAsync(Path.Combine(_home, "tree"), "engine", "aurora", "q1", "s1", new World(), null, OpinionRules.Landing);
        Assert.Equal(OpinionGateStates.None, state.State);
        Assert.Equal("engine", state.Repository);
        Assert.Equal("s1", state.Session);

        Assert.Equal(ReviewStates.None, (await trees.ReviewAsync(Path.Combine(_home, "tree"), null, new World())).State);
    }

    // ——— An entry's lead

    [Fact]
    public void An_entry_s_lead_is_the_workplace_s_and_the_accept_s_answer_is_the_same()
    {
        SessionRecord Record(string id, int minute, string? entry = null) =>
            new(id, "engine", "completed") { Created = At.AddMinutes(minute), Entry = entry };
        SessionRecord[] records = [Record("b", 2, "quest:q1/engine"), Record("a", 1, "quest:q1/engine"), Record("c", 3)];

        Assert.Equal("a", EntryLandings.LeadOf(records, "b")!.Id);
        Assert.Equal(EntryAccepts.EntryLead(records, "b")!.Id, EntryLandings.LeadOf(records, "b")!.Id);
        Assert.Null(EntryLandings.LeadOf(records, "c"));
        Assert.Null(EntryLandings.LeadOf(records, "gone"));
    }
}
