using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAPP2a (D204; the git-writes design §5): the act journal's family in the desktop store, over no git. A record kept whole with its steps,
/// one open per checkout under two journals; each step's start before its end, one open at a time; a take only from a dead run, raising the
/// generation, the old claim fenced out and two takers at once one winner; a store this build made written on by an older build that never
/// opens the family. The effects, the bounds and the reconciliation over real git are <c>ActRunGitTests</c>'.
/// </summary>
/// <remarks>Each case has its own scratch home under <c>_fixtures/</c>; "another process" is a second journal under a run of its own, and a dead run is the liveness the case hands in.</remarks>
public sealed class ActJournalTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "act-journal", Guid.NewGuid().ToString("N")[..8]);

    public ActJournalTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private ActJournal Journal(string run, Func<StoreRun, bool>? alive = null) => new(_home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    private static Func<StoreRun, bool> DeadAre(params string[] runs) => run => !runs.Contains(run.Run);

    private static ActOpening Opening(string id, string dir = "c:/engine/.git") => new(
        id, "fixture", "test", "engine", "c:/engine", dir, """{"shown":"main"}""",
        [new(ActStepKinds.Ref, """{"input":"create refs/heads/a 1"}""", """{"refs":[]}""", """{"undo":"delete"}"""), new(ActStepKinds.Config, "{}", "{}")])
    {
        Confirmed = "confirm-1", Recovery = """{"refs":[]}""",
    };

    [Fact]
    public void An_act_is_kept_whole_with_its_steps_and_one_is_open_per_checkout_under_two_journals()
    {
        var mine = Journal("run-a");
        var claim = mine.Open(Opening("act-1"));

        var act = mine.Act("act-1")!;
        Assert.Equal((ActJournal.Format, "fixture", "test", "confirm-1", "engine", "c:/engine", "c:/engine/.git"),
            (act.Format, act.Kind, act.Press, act.Confirmed, act.Repository, act.Checkout, act.CommonDir));
        Assert.Equal((ActJournal.JobOf("act-1"), "run-a", 1L, ActPhases.Judged, true), (act.Job, act.ClaimRun, act.ClaimGeneration, act.Phase, act.Open));
        Assert.Equal(new ActClaim("act-1", "run-a", 1), claim);
        Assert.Equal([(0, ActStepKinds.Ref, """{"undo":"delete"}"""), (1, ActStepKinds.Config, null)],
            mine.Steps("act-1").Select(each => (each.Ordinal, each.Kind, each.Rollback)));

        var refused = Assert.Throws<ActRefusedException>(() => Journal("run-b").Open(Opening("act-2")));
        Assert.Contains("act act-1 (fixture) is still open in `engine`'s checkout", refused.Why);
        Assert.Null(mine.Act("act-2"));
        Assert.NotNull(Journal("run-b").Open(Opening("act-3", "c:/other/.git")));
    }

    [Fact]
    public void A_steps_start_comes_before_its_pid_and_its_end_and_one_step_is_open_at_a_time()
    {
        var journal = Journal("run-a");
        var claim = journal.Open(Opening("act-1"));
        journal.Prepared(claim);
        var deadline = DateTimeOffset.UtcNow.AddMinutes(2);

        var begun = journal.Begin(claim, 0, deadline);
        Assert.True(begun.Open);
        Assert.Equal(Moments.Write(deadline), Moments.Write(begun.Deadline));
        Assert.Equal(ActPhases.Started, journal.Act("act-1")!.Phase);
        Assert.Throws<ActRefusedException>(() => journal.Begin(claim, 1, deadline));
        journal.Launched(claim, 0, 4242, 638000000000000000);

        var ended = journal.End(claim, 0, 0, unended: false, "git answered");
        Assert.Equal((4242, 638000000000000000L, 0, false, "git answered"), (ended.Pid, ended.PidStarted, ended.ExitCode, ended.Unended, ended.Said));
        Assert.Equal(ended.EndedAt, journal.End(claim, 0, 1, unended: true, "again").EndedAt);
        Assert.Equal(0, journal.Steps("act-1")[0].ExitCode);
        Assert.Throws<ActRefusedException>(() => journal.Close(claim, null));

        journal.Reconciled(claim, ActOutcomes.Done, "every postcondition holds", new Dictionary<int, string> { [0] = ActOutcomes.Done });
        journal.Close(claim, null);
        Assert.Equal((ActPhases.Closed, ActOutcomes.Done, false), (journal.Act("act-1")!.Phase, journal.Act("act-1")!.Outcome, journal.Act("act-1")!.Open));
        Assert.Equal(ActOutcomes.Done, journal.Steps("act-1")[0].Outcome);
    }

    [Fact]
    public void An_act_is_taken_only_from_a_dead_run_and_the_claim_it_lost_writes_nothing_more()
    {
        var first = Journal("run-a");
        var claim = first.Open(Opening("act-1"));

        Assert.Equal("the Daoris process that runs it still runs", Journal("run-b").Take("act-1", 1).Refused);
        Assert.Equal("it is this run's own", Journal("run-a").Take("act-1", 1).Refused);
        var taken = Journal("run-b", DeadAre("run-a")).Take("act-1", 1);

        Assert.Equal(new ActClaim("act-1", "run-b", 2), taken.Claim);
        Assert.Throws<ActRefusedException>(() => first.Prepared(claim));
        Assert.Equal(ActPhases.Judged, first.Act("act-1")!.Phase);
        Assert.Equal("another reconciler took it first", Journal("run-c", DeadAre("run-a", "run-b")).Take("act-1", 1).Refused);
    }

    [Fact]
    public async Task Two_takers_at_once_leave_one_winner()
    {
        Journal("run-a").Open(Opening("act-1"));
        var dead = DeadAre("run-a");

        var takes = await Task.WhenAll(Enumerable.Range(0, 4).Select(each => Task.Run(() => Journal($"run-{each}x", dead).Take("act-1", 1))));

        Assert.Single(takes, each => each.Claim is not null);
        Assert.Equal(2, Journal("run-a").Act("act-1")!.ClaimGeneration);
    }

    [Fact]
    public void An_older_build_writes_on_in_a_store_that_holds_the_family_and_never_opens_it()
    {
        Journal("run-a").Open(Opening("act-1"));

        using (var older = DesktopStore.Open(DesktopStore.LocationIn(_home), new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(ActJournal.Since - 1)], Settled = () => StoreSettled.Yes }))
        {
            Assert.Equal((DesktopStore.Steps.Count, DesktopStore.WriterStamp), older.Stamp);
            older.Write("older", write => write.Execute("UPDATE store_version SET at = at"));
        }

        Assert.True(Journal("run-a").Act("act-1")!.Open);
    }

    [Fact]
    public void A_home_with_no_store_reads_as_holding_no_act_and_is_left_without_one()
    {
        var journal = Journal("run-a");

        Assert.Empty(journal.Unclosed());
        Assert.Null(journal.Act("act-1"));
        Assert.False(File.Exists(DesktopStore.LocationIn(_home)));
    }
}
