using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LINESTOGETHER1d (the review-flow design §4): the operation bringing sessions together, its phases each a compare of the phase it
/// expects, and a restart's settlement of a landing attempt, only a proved unmade attempt going back to <i>ready to accept</i>.
/// </summary>
public sealed class TogetherOperationsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "together-ops", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private string Tree(string name) => DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine", name);

    private TogetherHead Head(string record = "s-head") => new("s-line", record, Tree("head"), "daoris/head", "a1");

    private TogetherMember Member(string session) => new(session, Tree(session), "daoris/" + session, "b0", "c-" + session);

    private TogetherOperation Opened(TogetherOperations store)
    {
        var (opened, refused) = store.Open("engine", Head(), [Member("s-beside"), Member("s-other")], At);
        Assert.Null(refused);
        return opened!;
    }

    [Fact]
    public void A_press_is_kept_at_asked_and_binds_the_head_and_every_member_tree()
    {
        var store = new TogetherOperations(_home);
        var opened = Opened(store);

        Assert.Equal(TogetherPhase.Asked, store.Of(opened.Id)!.Phase);
        Assert.Equal($"together:{opened.Id}", opened.Key);
        Assert.Equal(opened.Id, store.Binding(Tree("s-other"))!.Id);
        Assert.Equal(opened.Id, store.Binding(Tree("head"))!.Id);
        Assert.Null(store.Binding(Tree("elsewhere")));
        Assert.Equal(opened.Id, store.OpenFor("s-beside")!.Id);
    }

    [Fact]
    public void A_session_is_in_one_open_operation_at_most()
    {
        var store = new TogetherOperations(_home);
        Opened(store);

        var (again, refused) = store.Open("engine", new TogetherHead("s-x", "s-x", Tree("x"), "daoris/x", "x1"), [Member("s-other")], At);

        Assert.Null(again);
        Assert.Contains("brought together already", refused);
        Assert.Single(store.All());
    }

    [Fact]
    public void The_phases_go_asked_replaying_checking_and_ready_at_the_tip_the_check_proved()
    {
        var store = new TogetherOperations(_home);
        var id = Opened(store).Id;

        Assert.Equal(TogetherPhase.Replaying, store.Replaying(id, At)!.Phase);
        Assert.Null(store.Replaying(id, At));
        Assert.Equal(TogetherPhase.Checking, store.TurnEnded(id, At)!.Phase);
        var ready = store.Checked(id, "h2", [new TogetherPath("s-beside", "c1", "a.txt", TogetherAccounts.Contained)], null, At)!;

        Assert.Equal(TogetherPhase.Ready, ready.Phase);
        Assert.Equal("h2", ready.Checked);
        Assert.Single(store.Of(id)!.Account);
    }

    [Fact]
    public void A_failed_check_stays_checking_with_what_failed_and_ask_again_goes_back_to_asked()
    {
        var store = new TogetherOperations(_home);
        var id = Opened(store).Id;
        store.TurnEnded(id, At);

        var failed = store.Checked(id, "h2", [new TogetherPath("s-beside", "c2", "b.txt", TogetherAccounts.Missing)], "missing", At)!;

        Assert.Equal(TogetherPhase.Checking, failed.Phase);
        Assert.Null(failed.Checked);
        Assert.Equal("missing", failed.Why);
        var again = store.AskedAgain(id, At)!;
        Assert.Equal(TogetherPhase.Asked, again.Phase);
        Assert.Equal($"together:{id}:1", again.Key);
    }

    [Fact]
    public void A_head_moved_after_checked_goes_back_to_checking_and_one_unmoved_changes_nothing()
    {
        var store = new TogetherOperations(_home);
        var id = Opened(store).Id;
        store.TurnEnded(id, At);
        store.Checked(id, "h2", [], null, At);

        Assert.Null(store.HeadAt(id, "h2", At));
        var back = store.HeadAt(id, "h3", At)!;

        Assert.Equal(TogetherPhase.Checking, back.Phase);
        Assert.Null(back.Checked);
    }

    [Fact]
    public void A_member_tip_moving_before_checked_ends_it_stale_holding_nothing()
    {
        var store = new TogetherOperations(_home);
        var id = Opened(store).Id;

        Assert.Null(store.MemberAt(id, "s-beside", "c-s-beside", At));
        var stale = store.MemberAt(id, "s-beside", "c-moved", At)!;

        Assert.Equal(TogetherPhase.Stale, stale.Phase);
        Assert.Equal(TogetherWhy.MemberMoved, stale.Why);
        Assert.Null(store.Binding(Tree("s-beside")));
    }

    [Fact]
    public void Carried_to_the_successor_its_own_word_opened_and_stale_on_another_word()
    {
        var store = new TogetherOperations(_home);
        var id = Opened(store).Id;

        var carried = store.LineMoved(id, "s-next", byOwnWord: true, TogetherWhy.AnotherWord, At)!;
        Assert.Equal("s-next", carried.Head.Record);
        Assert.Equal(TogetherPhase.Asked, carried.Phase);
        Assert.True(carried.Holds("s-next"));

        var stale = store.LineMoved(id, "s-after", byOwnWord: false, TogetherWhy.AnotherWord, At)!;
        Assert.Equal(TogetherPhase.Stale, stale.Phase);
        Assert.Equal(TogetherWhy.AnotherWord, stale.Why);
        Assert.Equal("s-next", stale.Head.Record);
    }

    [Fact]
    public void Let_them_go_ends_it_before_landing_and_never_after()
    {
        var store = new TogetherOperations(_home);
        var gone = Opened(store).Id;
        Assert.Equal(TogetherPhase.LetGo, store.LetGo(gone, At)!.Phase);
        Assert.Null(store.OpenFor("s-beside"));

        var landing = Opened(store).Id;
        store.TurnEnded(landing, At);
        store.Checked(landing, "h2", [], null, At);
        store.Landing(landing, new TogetherAttempt(TogetherForms.Branch, "line", "l1", "h2", At));

        Assert.Null(store.LetGo(landing, At));
        Assert.Equal(TogetherPhase.Landing, store.Of(landing)!.Phase);
    }

    [Fact]
    public void The_attempt_is_written_only_ready_at_its_pinned_tip_then_told_what_came_of_it()
    {
        var store = new TogetherOperations(_home);
        var id = Opened(store).Id;
        store.TurnEnded(id, At);
        store.Checked(id, "h2", [], null, At);

        Assert.Null(store.Landing(id, new TogetherAttempt(TogetherForms.Squash, "line", "l1", "h-other", At)));
        var landing = store.Landing(id, new TogetherAttempt(TogetherForms.Merge, "line", "l1", "h2", At) { Replayed = "r2" })!;
        Assert.Equal(TogetherPhase.Landing, landing.Phase);

        var told = store.Correlate(id, request: "req-1")!;
        Assert.Equal("req-1", told.Attempt!.Request);
        Assert.Equal("r2", told.Attempt.Replayed);
        Assert.Equal("req-1", store.Of(id)!.Attempt!.Request);
    }

    [Fact]
    public void A_restart_settles_landing_each_way_and_uncertain_keeps_the_members_bound()
    {
        var store = new TogetherOperations(_home);
        var id = Opened(store).Id;
        store.TurnEnded(id, At);
        store.Checked(id, "h2", [], null, At);
        store.Landing(id, new TogetherAttempt(TogetherForms.Branch, "daoris/landed", null, "h2", At));

        Assert.Equal(TogetherPhase.Uncertain, store.Settle(id, TogetherSettled.Uncertain, At)!.Phase);
        Assert.Equal(id, store.Binding(Tree("s-other"))!.Id);
        Assert.Null(store.Settle(id, TogetherSettled.Uncertain, At));
        var unmade = store.Settle(id, TogetherSettled.Unmade, At)!;
        Assert.Equal(TogetherPhase.Ready, unmade.Phase);
        Assert.Null(unmade.Attempt);

        store.Landing(id, new TogetherAttempt(TogetherForms.Branch, "daoris/landed", null, "h2", At));
        Assert.Equal(TogetherPhase.Landing, store.Settle(id, TogetherSettled.Open, At, request: "req-2")!.Phase);
        Assert.Equal(TogetherPhase.Landed, store.Settle(id, TogetherSettled.Landed, At)!.Phase);
        Assert.Null(store.Binding(Tree("s-other")));
    }

    [Fact]
    public void A_file_that_does_not_read_refuses_a_press_and_binds_unread()
    {
        var store = new TogetherOperations(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        File.WriteAllText(store.FilePath, "{ not json");

        Assert.Throws<IOException>(() => store.Open("engine", Head(), [Member("s-beside")], At));
        Assert.Throws<IOException>(() => store.Binding(Tree("s-beside")));
    }

    [Theory]
    [InlineData("recorded", "landed")]
    [InlineData("request-settled", "landed")]
    [InlineData("request-open", "open")]
    [InlineData("branch-missing", "unmade")]
    [InlineData("line-unmoved", "unmade")]
    [InlineData("line-unmoved-request-unread", "uncertain")]
    [InlineData("moved-holding", "landed")]
    [InlineData("moved-elsewhere", "uncertain")]
    [InlineData("ref-unread", "uncertain")]
    [InlineData("expected-but-missing", "uncertain")]
    public void A_restart_decides_by_a_correlated_record_else_by_git_effects(string evidence, string settled)
    {
        var branch = new TogetherAttempt(TogetherForms.Branch, "daoris/landed", null, "h2", At);
        var line = new TogetherAttempt(TogetherForms.Merge, "main", "l1", "h2", At) { Replayed = "r2" };
        var (attempt, read) = evidence switch
        {
            "recorded" => (branch, new LandingEvidence(true, null, false, new RefRead("h2", false), true)),
            "request-settled" => (line, new LandingEvidence(false, new("req", true, true), false, new RefRead("l2", false), null)),
            "request-open" => (line, new LandingEvidence(false, new("req", false, false), false, new RefRead("l1", false), null)),
            "branch-missing" => (branch, new LandingEvidence(false, null, false, new RefRead(null, true), null)),
            "line-unmoved" => (line, new LandingEvidence(false, null, false, new RefRead("l1", false), null)),
            "line-unmoved-request-unread" => (line, new LandingEvidence(false, null, true, new RefRead("l1", false), null)),
            "moved-holding" => (line, new LandingEvidence(false, null, false, new RefRead("l2", false), true)),
            "moved-elsewhere" => (line, new LandingEvidence(false, null, false, new RefRead("l2", false), false)),
            "ref-unread" => (branch, new LandingEvidence(false, null, false, RefRead.Unread, null)),
            _ => (line, new LandingEvidence(false, null, false, new RefRead(null, true), null)),
        };

        Assert.Equal(settled, TogetherSettle.Decide(attempt, read) switch
        {
            TogetherSettled.Landed => "landed",
            TogetherSettled.Open => "open",
            TogetherSettled.Unmade => "unmade",
            _ => "uncertain",
        });
    }
}
