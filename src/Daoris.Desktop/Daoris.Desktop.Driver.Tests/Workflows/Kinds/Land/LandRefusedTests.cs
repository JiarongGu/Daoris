using Microsoft.Data.Sqlite;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.ObservedFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A refused landing says why in words, waits by itself while the line's own state holds it (another landing under way, its record held),
/// and holds no start of its session's quest meanwhile, while a session that failed and the person's words still go on.
/// </summary>
public sealed class LandRefusedTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 2, 0, 0, TimeSpan.Zero);
    private static readonly DriverConfig Config = DriverConfig.Empty with { Drivable = ["Game"], Cap = 2 };
    private readonly string _homes = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "land-refused", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_homes);
    }

    private static QuestView Submitted(string id = "q1") =>
        new(id, "Asker", "Game", $"Ask {id}", "Here is why.", "Taken")
        {
            Closure = "workflow", Submission = new QuestSubmissionView("desk", 41, null, null),
        };

    private static PriorSession Cut(string state, bool interrupted = false, bool personSaid = false) =>
        new("s1", "D:/trees/s1", state, "ended", "Game") { Interrupted = interrupted, Said = personSaid ? [new SaidWordView("w1", "go on", Now, [], false)] : null };

    private static Snapshot Ran(QuestView quest, PriorSession last, params string[] owed) =>
        new([quest], [new RepoView("Game", true, "D:/fam/Game")], [])
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { [quest.Id] = last },
            OwedLandings = new HashSet<string>(owed, StringComparer.OrdinalIgnoreCase),
        };

    /// <summary>The step of a run whose landing is refused reads the refusal's own sentence, as waiting or as refused.</summary>
    [Theory]
    [InlineData(LandingCode.Busy, "its landing waits: ")]
    [InlineData(LandingCode.Queued, "its landing waits: ")]
    [InlineData(LandingCode.Refused, "its landing was refused: ")]
    public void A_refused_landing_is_said_on_its_step_in_the_words_it_was_refused_with(string code, string lead)
    {
        var unit = Unit(Grown());
        var bound = new NodeAcceptance("run:r", "t1", Graph, NodeKinds.Land, 1, 1) { Subject = unit.Id };
        Assert.DoesNotContain("its landing", NodeObserving.Observe(unit, Node(NodeKinds.Land), bound).Said, StringComparison.Ordinal);

        var words = IntegrationPresses.Said(new IntegrationAdmission(false, "a request is already open for `feature/x`.") { Refusal = IntegrationRefusals.Open });
        LandOutcomes.Kept(_homes, unit.Id, "t1", new TreeLanding(false, words) { Refusal = code });
        try
        {
            var after = NodeObserving.Observe(unit, Node(NodeKinds.Land), bound);
            Assert.Null(after.Outcome);
            Assert.Contains(lead + words, after.Said, StringComparison.Ordinal);
        }
        finally
        {
            LandOutcomes.Forget(unit.Id, "t1");
        }
    }

    /// <summary>A request already open for the branch is refused busy, in words that name the landing it waits for, through the real press.</summary>
    [Fact]
    public async Task A_press_refused_for_the_open_request_of_its_branch_is_busy_and_tried_again_by_itself()
    {
        var home = Path.Combine(_homes, "home");
        var open = new IntegrationRequest("r1", "engine", "aurora", "feature/x", "1111111111111111111111111111111111111111", IntegrationRoutes.Merge, "main",
            IntegrationRequester.Press, new LandingIntegrate().Resolved(), Now) { Epoch = IntegrationEpoch.Of(new LandingIntegrate().Resolved(), null) };
        Assert.Null(new IntegrationStore(home).Add(open));

        var (merged, said, code, _) = await IntegrationPresses.MergeAsync(
            home, DriverConfig.Empty, Path.Combine(_homes, "unmade"), "engine", "aurora", "main", "feature/x", "2222222222222222222222222222222222222222", CancellationToken.None);

        Assert.False(merged);
        Assert.Equal(LandingCode.Busy, code);
        Assert.Contains("a request is already open for `feature/x`", said, StringComparison.Ordinal);
        Assert.Contains("tried again by itself", said, StringComparison.Ordinal);
    }

    /// <summary>Store contention is the line's own state and passes; any other fault carries no code and is not called a landing under way.</summary>
    [Fact]
    public void Only_store_contention_is_busy_and_names_the_candidate_that_holds_it()
    {
        var (held, heldCode) = IntegrationPresses.Unkept(new SqliteException("database is locked", 5), "main", "candidate-a1");
        Assert.Equal(LandingCode.Busy, heldCode);
        Assert.Contains("candidate candidate-a1", held, StringComparison.Ordinal);
        Assert.Contains("tried again by itself", held, StringComparison.Ordinal);

        Assert.Equal(LandingCode.Busy, IntegrationPresses.Unkept(new IOException("in use", unchecked((int)0x80070020)), "main").Code);

        var (fault, faultCode) = IntegrationPresses.Unkept(new UnauthorizedAccessException("denied"), "main");
        Assert.Null(faultCode);
        Assert.Contains("denied", fault, StringComparison.Ordinal);
        Assert.DoesNotContain("another write", fault, StringComparison.Ordinal);
        Assert.Null(IntegrationPresses.Unkept(new SqliteException("disk is full", 13), "main").Code);
    }

    /// <summary>A submitted quest whose landing is owed starts no session, spending no place; without the standing it carries on as before.</summary>
    [Fact]
    public void A_submitted_quest_whose_landing_is_owed_is_planned_as_a_sit_and_spends_no_place()
    {
        var quest = Submitted();
        var cutOff = Cut("stopped", interrupted: true);

        Assert.Equal(StartVerdict.Start, Assert.Single(Planner.Plan(Ran(quest, cutOff), Config)).Verdict);

        var sat = Assert.Single(Planner.Plan(Ran(quest, cutOff, "q1"), Config));
        Assert.Equal((StartVerdict.RepositoryBusy, SittingFact.LandingOwed), (sat.Verdict, sat.Said?.Code));
        Assert.Equal(StartVerdict.Start, Assert.Single(Planner.Plan(Ran(Submitted("q2"), cutOff, "q1"), Config)).Verdict);
    }

    /// <summary>A session that failed, and the person's own words, still go on whatever the landing waits on.</summary>
    [Theory]
    [InlineData("failed", false)]
    [InlineData("completed", true)]
    public void A_failed_session_and_the_persons_words_still_carry_on_while_the_landing_is_owed(string state, bool personSaid)
    {
        var planned = Assert.Single(Planner.Plan(Ran(Submitted(), Cut(state, personSaid: personSaid), "q1"), Config));

        Assert.Equal(StartVerdict.Start, planned.Verdict);
    }

    /// <summary>The standing is set for each look that judges a landing, read once with its clock, and ended by a try that closed or by lapsing unread.</summary>
    [Fact]
    public void The_standing_is_set_ended_by_a_closing_try_and_lapses_unread()
    {
        LandStandings.Wait(_homes, "q1", Now);
        LandStandings.Wait(_homes, "q2", Now);
        Assert.Equal(["q1", "q2"], LandStandings.Owed(_homes, Now).Order());

        LandStandings.Done(_homes, "q2");
        Assert.Equal(["q1"], LandStandings.Owed(_homes, Now + LandRetries.Fresh - TimeSpan.FromSeconds(1)));
        Assert.Empty(LandStandings.Owed(_homes, Now + LandRetries.Fresh));
        Assert.Empty(LandStandings.Owed(Path.Combine(_homes, "other"), Now));

        Assert.All(new[] { LandingCode.Busy, LandingCode.Refused, LandingCode.Queued, LandingCode.Unreviewed }, code => Assert.True(LandStandings.IsOwed(code)));
        Assert.All(new[] { LandingCode.Landed, LandingCode.Advanced, LandingCode.Nothing, LandingCode.Already }, code => Assert.False(LandStandings.IsOwed(code)));
    }
}
