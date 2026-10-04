using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PLUGHOOK1a (D148 points 1, 2 and 4, design §2.1 and §2.3): what a kept answer proves once git has said what it holds, and
/// who is asked and when. Pure, so this is the suite's fast half; the same codes over real git, with a plugin faked on its
/// channel, are <see cref="PullRequestStateTests"/>.
/// </summary>
public sealed class PullRequestProofTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 14, 0, 0, TimeSpan.Zero);

    private static PullRequestState Completed(string? target = "main") => new(PullRequestStates.Completed)
    {
        MergeCommit = new string('a', 40),
        SourceCommit = new string('b', 40),
        Target = target,
    };

    /// <summary>Each code of §2.3, in the order git is asked, and the two that clear.</summary>
    public static TheoryData<string, string?, bool, bool, bool, string?, bool, bool> Rows => new()
    {
        // state, target, merge on the line, source held, standing, code, carries, clears
        { PullRequestStates.Open, "main", true, true, true, PullRequestStates.Open, false, false },
        { PullRequestStates.Abandoned, "main", true, true, true, PullRequestStates.Abandoned, false, false },
        { PullRequestStates.Unknown, null, true, true, true, PullRequestStates.Unknown, false, false },
        { PullRequestStates.Completed, "release", true, true, true, PullRequestCodes.OtherTarget, false, false },
        { PullRequestStates.Completed, "main", false, true, true, PullRequestCodes.MergeNotHere, false, false },
        { PullRequestStates.Completed, "main", true, false, true, PullRequestCodes.SourceNotHere, false, false },
        { PullRequestStates.Completed, "main", true, true, true, null, true, true },
        // A target the answer does not name is not held against it.
        { PullRequestStates.Completed, null, true, true, true, null, true, true },
        // A trace: nothing of the landed branch to clear, and its pull request still carries the session branches under it.
        { PullRequestStates.Completed, "main", true, true, false, null, true, false },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void A_kept_answer_clears_only_where_git_confirms_it(
        string state, string? target, bool mergeOnLine, bool sourceHeld, bool standing, string? code, bool carries, bool clears)
    {
        var kept = state == PullRequestStates.Completed ? Completed(target) : new PullRequestState(state) { Target = target };

        var verdict = PullRequestProof.Judge(kept, new PullRequestFacts("main", mergeOnLine ? "origin/main" : null, sourceHeld, standing, UnderSource: true));

        Assert.Equal((code, carries, clears), (verdict.Code, verdict.Carries, verdict.Clears));
        if (clears) Assert.Equal("origin/main", verdict.Form);
    }

    /// <summary>After LAND2c advanced it, the branch holds commits the pull request did not carry: it stays, and what it did carry may go.</summary>
    [Fact]
    public void A_branch_beyond_the_merged_commit_stays_and_the_pull_request_still_carries_what_it_merged()
    {
        var verdict = PullRequestProof.Judge(Completed(), new PullRequestFacts("main", "main", SourceHeld: true, Standing: true, UnderSource: false));

        Assert.Equal(new PullRequestVerdict(PullRequestCodes.Beyond, "main", Carries: true, Clears: false), verdict);
    }

    [Fact]
    public void Nothing_kept_is_not_known_and_clears_nothing() =>
        Assert.Equal(new PullRequestVerdict(PullRequestStates.Unknown, null, false, false),
            PullRequestProof.Judge(null, new PullRequestFacts("main", "main", true, true, true)));

    // ——— who is asked, and when (design §2.1)

    private static LandedBranch Entry(string? plugin = null) =>
        new("engine", "aurora", "feature/q1-fix", "main", new string('b', 40), "s1", "q1", "Fix", Now.AddDays(-1)) { Plugin = plugin };

    /// <summary>The plugin that pushed the branch is asked, since it opened the pull request; else the one the rule names now; a merge names none.</summary>
    [Fact]
    public void The_plugin_that_pushed_it_is_asked_else_the_rules()
    {
        var rule = new LandingRule(LandingForm.Branch, "feature/{quest}", Plugin: "acme.now");

        Assert.Equal("acme.pushed", PullRequestAsking.PluginFor(Entry("acme.pushed"), rule));
        Assert.Equal("acme.now", PullRequestAsking.PluginFor(Entry(), rule));
        Assert.Null(PullRequestAsking.PluginFor(Entry(), new LandingRule(LandingForm.Branch, "feature/{quest}")));
        Assert.Null(PullRequestAsking.PluginFor(Entry(), LandingRule.Merge));
    }

    /// <summary>
    /// A completed answer is final; an entry asked in the last minute, answered or not, is not asked again, so a page that lists
    /// twice asks once; <i>Ask again</i> asks whatever the kept answer's age, but never past a completed one.
    /// </summary>
    [Fact]
    public void An_entry_is_due_unless_completed_or_asked_in_the_last_minute()
    {
        var open = new PullRequestState(PullRequestStates.Open) { AskedAt = Now.AddSeconds(-30) };
        var completed = Completed() with { AskedAt = Now.AddDays(-2) };

        Assert.True(PullRequestAsking.Due(Entry(), Now));
        Assert.False(PullRequestAsking.Due(Entry() with { PullRequestState = open }, Now));
        Assert.True(PullRequestAsking.Due(Entry() with { PullRequestState = open with { AskedAt = Now.AddMinutes(-1) } }, Now));
        Assert.True(PullRequestAsking.Due(Entry() with { PullRequestState = open }, Now, again: true));
        Assert.False(PullRequestAsking.Due(Entry() with { PullRequestState = completed }, Now));
        Assert.False(PullRequestAsking.Due(Entry() with { PullRequestState = completed }, Now, again: true));
        // A failure counts as an ask: it is not tried again within the minute either.
        var failed = new PullRequestAskFailed(PluginEvents.Late, "acme.pushed", Now.AddSeconds(-10));
        Assert.False(PullRequestAsking.Due(Entry() with { PullRequestAskFailed = failed }, Now));
        Assert.Equal(Now.AddSeconds(-10), PullRequestAsking.LastAsked(Entry() with { PullRequestState = open with { AskedAt = Now.AddHours(-1) }, PullRequestAskFailed = failed }));
    }

    /// <summary>D100's four reasons, each its own sentence: not installed, switched off, unsound, speaking on no <c>work/state</c> point.</summary>
    [Fact]
    public void A_plugin_that_cannot_be_asked_says_which_of_the_four_reasons()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-pr-asking-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Install(home, "acme.asks", [HookPoints.Land, HookPoints.State]);
            Install(home, "acme.lands", [HookPoints.Land]);
            Install(home, "acme.off", [HookPoints.State]);
            Directory.CreateDirectory(Path.Combine(home, "plugins", "acme.broken"));
            File.WriteAllText(Path.Combine(home, "plugins", "acme.broken", "plugin.json"), "not json");
            PluginState.Disable(home, "acme.off");
            var catalog = PluginCatalog.Load(home);

            Assert.Null(PullRequestAsking.Problem("acme.asks", catalog));
            Assert.Contains("is not installed", PullRequestAsking.Problem("acme.gone", catalog));
            Assert.Contains("speaks on no `work/state` point", PullRequestAsking.Problem("acme.lands", catalog));
            Assert.Contains("contributes nothing", PullRequestAsking.Problem("acme.broken", catalog));
            Assert.Contains("switched off", PullRequestAsking.Problem("acme.off", catalog));
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static void Install(string home, string id, IReadOnlyList<string> points)
    {
        var folder = Path.Combine(home, "plugins", id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), JsonSerializer.Serialize(new
        {
            id,
            hooks = new { command = new[] { "node", "plugin.mjs" }, points },
        }));
    }
}
