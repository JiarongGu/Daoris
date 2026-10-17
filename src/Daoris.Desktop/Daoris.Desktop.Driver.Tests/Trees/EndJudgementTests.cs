using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-END2a (the fabric events design §B3 and §B7, canary 6): the end judgement, pure over the facts the proofs found. One test
/// per cycle of §B3, a named workflow's landing included, and one per case never ended by a guess. The same proofs over real git are
/// <see cref="EndJudgementGitTests"/>'.
/// </summary>
public sealed class EndJudgementTests
{
    private const string Tip = "1111111111111111111111111111111111111111";
    private const string Later = "2222222222222222222222222222222222222222";

    private static LandedBranch Landing(string tip = Tip) => new(
        "engine", "aurora", "feature/q1-work", "main", tip, "s1", "q1", "Work", new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero));

    private static EndSubject Branch(EndRole role = EndRole.Landing, LandedBranch? landing = null, string tip = Tip) =>
        role == EndRole.Landing
            ? new("feature/q1-work", tip, role, landing ?? Landing())
            : new("daoris/s1", tip, role, landing);

    private static EndFacts Facts(Held? ancestry = null) => new("main") { Ancestry = ancestry ?? Held.None };

    private static PullRequestState Done() => new(PullRequestStates.Completed)
    {
        MergeCommit = new string('a', 40),
        SourceCommit = new string('b', 40),
        Target = "main",
    };

    // The cycles, as Accept resolves them and as a landing recorded them.

    [Fact]
    public void A_cycle_is_resolved_as_Accept_resolves_it_from_the_repository_then_the_workspace()
    {
        var config = DriverConfig.Empty
            .WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: "forge"))
            .WithWorkspaceLanding("aurora", new LandingRule(LandingForm.Branch, "w/{quest}-{slug}", Squash: true));

        Assert.Equal("branch with a plugin", EndCycle.Accepts(config, "engine", "aurora").Name);
        Assert.Equal("squash", EndCycle.Accepts(config, "other", "aurora").Name);
        Assert.Equal("merge", EndCycle.Accepts(config, "other", "elsewhere").Name);
        Assert.Equal("no landing", EndCycle.NoLanding.Name);
    }

    [Fact]
    public void A_named_workflows_landing_decides_the_cycle_over_the_repositorys_rule()
    {
        var config = DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Merge));
        var process = new WorkflowProcess("engine", new Landing(new LandingRule(LandingForm.Branch, "docs/{quest}-{slug}", Plugin: "forge"), LandingSource.Repository), null, null)
        {
            Binding = new WorkflowRunBinding("q1", "engine", "aurora", "docs-to-pr", "task", DateTimeOffset.UnixEpoch) { Version = 2, Kind = "docs" },
        };

        var cycle = EndCycle.Accepts(config, "engine", "aurora", process);

        Assert.Equal(("branch with a plugin", "docs-to-pr"), (cycle.Name, cycle.Workflow?.Workflow));
        Assert.Equal("merge", EndCycle.Accepts(config, "engine", "aurora").Name);
        Assert.Equal(2, cycle.Workflow!.Version);
    }

    [Fact]
    public void A_handed_landing_is_resolved_from_what_it_recorded_whatever_the_rule_says_now()
    {
        var workflow = new LandingWorkflow("docs-to-pr", 2, "task");

        Assert.Equal("branch", EndCycle.Recorded(Landing()).Name);
        Assert.Equal("branch with a plugin", EndCycle.Recorded(Landing() with { Plugin = "forge" }).Name);
        Assert.Equal("squash", EndCycle.Recorded(Landing() with { Squashed = Later }).Name);
        Assert.Equal(workflow, EndCycle.Recorded(Landing() with { Workflow = workflow }).Workflow);
    }

    [Fact]
    public void Merge_form_ends_when_the_line_holds_the_session_tip_and_is_a_claim_until_then()
    {
        var merge = EndCycle.Of(new Landing(LandingRule.Merge, LandingSource.Default));
        var session = Branch(EndRole.Session);

        var ended = EndJudgement.Judge(merge, session, Facts(Held.At("main")));
        var open = EndJudgement.Judge(merge, session, Facts());

        Assert.Equal((EndState.Ended, EndBy.Merged, "main"), (ended.State, ended.By, ended.Where));
        Assert.Equal((Tip, "main"), (ended.Tip, ended.Line));
        Assert.Equal(EndState.Claim, open.State);
    }

    [Fact]
    public void Branch_form_is_handed_at_Accept_and_ended_when_the_line_holds_the_landing_branch()
    {
        var cycle = EndCycle.Recorded(Landing());

        var handed = EndJudgement.Judge(cycle, Branch(), Facts());
        var merged = EndJudgement.Judge(cycle, Branch(), Facts(Held.At("origin/main")));

        Assert.Equal(EndState.Handed, handed.State);
        Assert.Equal((EndState.Ended, EndBy.Merged, "origin/main"), (merged.State, merged.By, merged.Where));
    }

    [Fact]
    public void Branch_form_ends_by_content_after_a_squash_merge_where_the_content_is_on_the_line()
    {
        var facts = Facts() with { Content = Held.At("origin/main") };

        var verdict = EndJudgement.Judge(EndCycle.Recorded(Landing()), Branch(), facts);

        Assert.Equal((EndState.Ended, EndBy.OnLine, "origin/main"), (verdict.State, verdict.By, verdict.Where));
    }

    [Fact]
    public void Branch_form_with_a_plugin_ends_on_a_completed_pull_request_git_confirms()
    {
        var entry = Landing() with { Plugin = "forge", PullRequestState = Done() };
        var cycle = EndCycle.Recorded(entry);
        var cleared = Facts() with { Verdict = PullRequestProof.Judge(Done(), new PullRequestFacts("main", "origin/main", true, true, true)) };

        var ended = EndJudgement.Judge(cycle, Branch(landing: entry), cleared);

        Assert.Equal((EndState.Ended, EndBy.PullRequest, "origin/main"), (ended.State, ended.By, ended.Where));
    }

    /// <summary>
    /// CLEANUP1b: a landing whose branch git said is missing is judged at the tip its landing recorded, and only the line's ancestry of that
    /// commit ends it. Content and a pull request's word are read of a branch that stands, so they end nothing here; a branch standing
    /// again is not judged as gone, and a line git could not read says nothing.
    /// </summary>
    [Fact]
    public void A_landing_whose_branch_went_ends_only_by_the_lines_ancestry_of_its_recorded_tip()
    {
        var entry = Landing() with { Plugin = "forge", PullRequestState = Done() };
        var cycle = EndCycle.Recorded(entry);
        var gone = Branch(landing: entry) with { Gone = true };
        var away = Facts() with { Standing = false };

        var merged = EndJudgement.Judge(cycle, gone, away with { Ancestry = Held.At("main") });
        var content = EndJudgement.Judge(cycle, gone, away with { Content = Held.At("main") });
        var asked = EndJudgement.Judge(cycle, gone, away with
        {
            Verdict = PullRequestProof.Judge(Done(), new PullRequestFacts("main", "origin/main", true, true, true)),
        });
        var unread = EndJudgement.Judge(cycle, gone, away with { Ancestry = Held.Unread });
        var again = EndJudgement.Judge(cycle, gone, Facts(Held.At("main")));

        Assert.Equal((EndState.Ended, EndBy.Merged, "main", Tip, "main"), (merged.State, merged.By, merged.Where, merged.Tip, merged.Line));
        Assert.Equal((EndState.Handed, (string?)null), (content.State, content.By));
        Assert.Equal((EndState.Handed, (string?)null), (asked.State, asked.By));
        Assert.Equal(EndState.Unknown, unread.State);
        Assert.Equal(EndState.Unknown, again.State);
    }

    [Fact]
    public void A_pull_request_the_branch_moved_beyond_is_handed_and_says_why()
    {
        var entry = Landing() with { Plugin = "forge", PullRequestState = Done() };
        var beyond = Facts() with { Verdict = PullRequestProof.Judge(Done(), new PullRequestFacts("main", "main", true, true, UnderSource: false)) };

        var verdict = EndJudgement.Judge(EndCycle.Recorded(entry), Branch(landing: entry), beyond);

        Assert.Equal(EndState.Handed, verdict.State);
        Assert.Contains(PullRequestCodes.Beyond, verdict.Evidence);
    }

    [Fact]
    public void A_squashed_landing_ends_each_session_it_vouched_for_when_the_line_holds_the_landing_commit()
    {
        var entry = Landing(Later) with { Squashed = Tip };
        var cycle = EndCycle.Recorded(entry);
        var landing = EndJudgement.Judge(cycle, Branch(landing: entry, tip: Later), Facts(Held.At("main")));
        var session = Branch(EndRole.Session, entry);

        var ended = EndJudgement.Judge(cycle, session, Facts() with { Inside = true, LandingEnd = landing });
        var waiting = EndJudgement.Judge(cycle, session, Facts() with { Inside = true, LandingEnd = EndJudgement.Judge(cycle, Branch(landing: entry, tip: Later), Facts()) });

        Assert.Equal("squash", cycle.Name);
        Assert.Equal((EndState.Ended, EndBy.Landed, "feature/q1-work"), (ended.State, ended.By, ended.Where));
        Assert.Equal(EndState.Handed, waiting.State);
    }

    [Fact]
    public void A_session_not_inside_what_its_landing_made_is_not_ended_by_that_landing()
    {
        var entry = Landing();
        var landing = EndJudgement.Judge(EndCycle.Recorded(entry), Branch(landing: entry), Facts(Held.At("main")));

        var verdict = EndJudgement.Judge(EndCycle.Recorded(entry), Branch(EndRole.Session, entry), Facts() with { Inside = false, LandingEnd = landing });

        Assert.NotEqual(EndState.Ended, verdict.State);
    }

    [Fact]
    public void Accept_at_done_ends_as_the_form_it_landed_by()
    {
        var entry = Landing() with { AcceptedBy = AcceptedBy.Auto, Rule = new LandedRule(null, AutoAccept: true, LandingSource.Repository) };

        var verdict = EndJudgement.Judge(EndCycle.Recorded(entry), Branch(landing: entry), Facts(Held.At("main")));

        Assert.Equal((EndState.Ended, EndBy.Merged), (verdict.State, verdict.By));
    }

    [Fact]
    public void A_chains_advance_is_judged_at_its_own_tip()
    {
        var entry = Landing(Later) with { Advances = [new LandedAdvance(Tip, Later, DateTimeOffset.UnixEpoch, "s2")] };

        var verdict = EndJudgement.Judge(EndCycle.Recorded(entry), Branch(landing: entry, tip: Later), Facts(Held.At("main")));

        Assert.Equal((EndState.Ended, Later), (verdict.State, verdict.Tip));
    }

    [Fact]
    public void A_tree_its_agent_moved_is_judged_as_the_branch_it_moved_to_and_work_on_the_persons_other_branch_is_only_handed()
    {
        var moved = new EndSubject("feature/own", Tip, EndRole.Session);

        var other = EndJudgement.Judge(EndCycle.NoLanding, moved, Facts() with { OnPersonsBranch = true });
        var line = EndJudgement.Judge(EndCycle.NoLanding, moved, Facts(Held.At("main")));

        Assert.Equal(EndState.Handed, other.State);
        Assert.Equal(("feature/own", EndState.Ended), (moved.Branch, line.State));
    }

    [Fact]
    public void With_no_landing_the_persons_own_git_ends_it_by_ancestry_or_by_content()
    {
        var session = Branch(EndRole.Session);

        var merged = EndJudgement.Judge(EndCycle.NoLanding, session, Facts(Held.At("origin/main")));
        var squashed = EndJudgement.Judge(EndCycle.NoLanding, session, Facts() with { Content = Held.At("main") });

        Assert.Equal((EndState.Ended, EndBy.Merged), (merged.State, merged.By));
        Assert.Equal((EndState.Ended, EndBy.OnLine), (squashed.State, squashed.By));
    }

    [Fact]
    public void A_branch_with_nothing_beyond_the_line_is_ended_at_once_as_empty()
    {
        var verdict = EndJudgement.Judge(EndCycle.NoLanding, Branch(EndRole.Session) with { Worked = false }, Facts(Held.At("main")));

        Assert.Equal((EndState.Ended, EndBy.Empty), (verdict.State, verdict.By));
    }

    [Fact]
    public void A_session_branch_a_completed_pull_request_carried_is_ended_as_carried()
    {
        var verdict = EndJudgement.Judge(EndCycle.NoLanding, Branch(EndRole.Session), Facts() with { Carrier = "feature/q1-work" });

        Assert.Equal((EndState.Ended, EndBy.Carried, "feature/q1-work"), (verdict.State, verdict.By, verdict.Where));
    }

    // Canary 6, §B7: never ended by a guess.

    [Fact]
    public void The_models_done_is_a_claim_and_never_an_end()
    {
        var done = Branch(EndRole.Session) with { Claimed = true };

        var verdict = EndJudgement.Judge(EndCycle.Recorded(Landing()), done, Facts());

        Assert.Equal(EndState.Claim, verdict.State);
        Assert.Contains("claim", verdict.Evidence);
    }

    [Fact]
    public void A_plugins_word_without_its_merge_commit_on_the_line_here_is_not_an_end()
    {
        var entry = Landing() with { Plugin = "forge", PullRequestState = Done() };
        foreach (var facts in new[]
        {
            new PullRequestFacts("main", MergeOn: null, SourceHeld: true, Standing: true, UnderSource: true),
            new PullRequestFacts("release", "main", true, true, true),
            new PullRequestFacts("main", "main", SourceHeld: false, Standing: true, UnderSource: true),
        })
        {
            var verdict = EndJudgement.Judge(EndCycle.Recorded(entry), Branch(landing: entry), Facts() with { Verdict = PullRequestProof.Judge(Done(), facts) });

            Assert.Equal(EndState.Handed, verdict.State);
        }
    }

    [Fact]
    public void A_pull_request_answer_that_proves_nothing_does_not_end_a_branch_whose_cycle_has_no_plugin()
    {
        var cleared = PullRequestProof.Judge(Done(), new PullRequestFacts("main", "main", true, true, true));

        var verdict = EndJudgement.Judge(new EndCycle(LandingForm.Branch), Branch(), Facts() with { Verdict = cleared });

        Assert.Equal(EndState.Handed, verdict.State);
    }

    [Fact]
    public void An_unread_answer_is_unknown_never_not_ended_and_never_ended()
    {
        var session = EndJudgement.Judge(EndCycle.NoLanding, Branch(EndRole.Session), new EndFacts("main"));
        var landing = EndJudgement.Judge(EndCycle.Recorded(Landing()), Branch(), new EndFacts("main") with { Standing = null });
        var gone = EndJudgement.Judge(EndCycle.Recorded(Landing()), Branch(), Facts() with { Standing = false });

        Assert.Equal(EndState.Unknown, session.State);
        Assert.Equal(EndState.Unknown, landing.State);
        Assert.Equal(EndState.Unknown, gone.State);
    }

    [Fact]
    public void Work_only_a_branch_of_the_persons_holds_is_handed_until_the_line_has_it()
    {
        var verdict = EndJudgement.Judge(EndCycle.NoLanding, Branch(EndRole.Session), Facts() with { Content = Held.At("feature/mine") });

        Assert.Equal((EndState.Handed, "feature/mine"), (verdict.State, verdict.Where));
    }

    [Fact]
    public void A_dirty_tree_does_not_stop_the_committed_tips_end_and_is_said_beside_it()
    {
        var dirty = Branch(EndRole.Session) with { Uncommitted = true };

        var ended = EndJudgement.Judge(EndCycle.NoLanding, dirty, Facts(Held.At("main")));
        var open = EndJudgement.Judge(EndCycle.NoLanding, dirty, Facts());

        Assert.Equal((EndState.Ended, true), (ended.State, ended.Uncommitted));
        Assert.Equal((EndState.Claim, true), (open.State, open.Uncommitted));
    }
}
