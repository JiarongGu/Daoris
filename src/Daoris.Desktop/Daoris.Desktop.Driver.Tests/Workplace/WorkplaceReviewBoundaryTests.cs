using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-x, the Workplace-names-Review cut: the behaviour the cut keeps, written against the surface that exists on both sides
/// of it (the words the workplace and the review spell alike, the system shell a generated gate's command runs through, and what a landing
/// gate says: its refusal, its plan and its log line), so the same cases pass on the base and after it. The cases that reach a moved class
/// through a new seam are <c>WorkplaceReviewPortsTests</c>'.
/// </summary>
public sealed class WorkplaceReviewBoundaryTests
{
    [Fact]
    public void The_words_the_workplace_and_the_review_spell_alike_are_unchanged()
    {
        Assert.Equal(".checks.json", SessionChecks.Suffix);
        Assert.Equal("o-", OpinionTree.Prefix);
        Assert.StartsWith(OpinionTree.Prefix, Path.GetFileName(OpinionTree.PathFor("home", "default", "engine")), StringComparison.Ordinal);
        Assert.Equal("repository", OpinionSource.Repository);
        Assert.Equal("workspace", OpinionSource.Workspace);
        Assert.Equal("screen", ReviewDoors.Screen);
        Assert.Equal("terminal", ReviewDoors.Terminal);
        Assert.Equal("look", ReviewDoors.Look);
    }

    [Fact]
    public void A_commit_rule_names_where_it_was_set_by_the_same_words_as_the_other_rules()
    {
        var none = CommitRules.Resolve(DriverConfig.Parse("{}"), "engine", "aurora");
        Assert.Equal(CommitRules.DefaultSource, none.Source);

        var own = CommitRules.Resolve(DriverConfig.Parse("""{"commits":{"engine":{"mode":"ask"}}}"""), "engine", "aurora");
        Assert.Equal(OpinionSource.Repository, own.Source);
        Assert.Equal(CommitRules.Ask, own.Rule.Mode);

        var shared = CommitRules.Resolve(DriverConfig.Parse("""{"workspaceCommits":{"aurora":{"mode":"at-end"}}}"""), "engine", "aurora");
        Assert.Equal(OpinionSource.Workspace, shared.Source);
    }

    [Fact]
    public void A_generated_gate_s_command_runs_through_the_system_shell_as_the_devkit_s_gates_do()
    {
        var shell = HeavyStep.SystemShell();
        Assert.True(Path.IsPathRooted(shell));
        Assert.Equal("cmd.exe", Path.GetFileName(shell), ignoreCase: true);
        Assert.Equal($"\"{shell}\" /d /s /c \"echo hi\"", HeavyStep.CommandLine("echo hi"));
    }

    [Fact]
    public void A_gate_that_asks_nothing_lets_the_work_go_and_says_nothing()
    {
        var open = LandingGate.Open("engine");
        Assert.True(open.LetsGo);
        Assert.Null(open.Refusal);
        Assert.Null(open.Tip);
        Assert.Null(open.Process);
        Assert.Empty(LandingGateWords.Plan(open));
        Assert.Null(LandingGateWords.Held("s1", ("aurora", "engine"), open, new TreeLanding(true, "landed"), ReviewDoors.Look));
        Assert.Equal("c0ffee", (open with { Tip = "c0ffee" }).Tip);
    }

    [Fact]
    public void A_gate_whose_second_opinion_holds_refuses_in_its_words_plans_them_and_logs_the_door()
    {
        var open = LandingGate.Open("engine");
        var holds = open with { Opinion = new OpinionGateState(OpinionGateStates.Unread, "engine") { Problem = "the host did not answer" } };

        Assert.False(holds.LetsGo);
        Assert.Equal(AutoLandingCode.Opinion, holds.Refusal!.Refusal);
        Assert.Equal(holds.Opinion.Says, holds.Refusal.Message);
        Assert.Equal([$"trees: {holds.Opinion.Says}"], LandingGateWords.Plan(holds));

        var line = LandingGateWords.Held("s1", ("aurora", "engine"), holds, holds.Refusal, ReviewDoors.Look);
        Assert.Equal("opinion.held", line!.Event);
        Assert.Contains(("door", (object?)"look"), line.Data);
        Assert.Contains(("state", (object?)OpinionGateStates.Unread), line.Data);
    }
}
