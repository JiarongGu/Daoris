using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The census of what a <c>monitor</c> waits on: every landing a run's steps can leave whose pull request the monitor reads as still open
/// or unanswered, and whose plugin's answer may yet change, is one the moments ask its plugin about (<see cref="PullRequestMoments.Concerns"/>),
/// whichever way its <c>push</c> reached (Daoris's own push, origin's ref read after a failed one, the person's word of a push done
/// outside) and whether or not the keeper holds its end.
/// </summary>
public sealed class MonitorAskedTests
{
    private static readonly DateTimeOffset Then = new(2026, 11, 18, 5, 33, 0, TimeSpan.Zero);

    private const string Tip = "1111111111111111111111111111111111111111";

    /// <summary>The ways a run's push reaches, as the landing keeps them: Daoris's push act marks it pushed; origin's ref and the person's word do not.</summary>
    private static readonly (string Way, bool Pushed)[] PushReached = [("push act", true), ("origin's ref", false), ("done outside", false)];

    private static readonly string?[] Answers = [null, PullRequestStates.Open, PullRequestStates.Unknown, PullRequestStates.Completed, PullRequestStates.Abandoned];

    private static readonly HeldFact?[] Ends = [null, new HeldFact(Then, Then, LandedKind.OnLine, Tip, "main", "origin/main", "f9", "engine")];

    private static LandedBranch Landing(bool pushed, string? address, string? answer, HeldFact? held) =>
        new("engine", "default", "feature/q7", "main", Tip, "s1", "q7", null, Then)
        {
            Pushed = pushed, PushedTip = pushed ? Tip : null, Plugin = address is null ? null : "acme", PullRequest = address,
            PullRequestState = answer is null ? null : new PullRequestState(answer) { AskedAt = Then }, Held = held,
        };

    [Fact]
    public void Every_landing_a_monitor_waits_on_for_its_plugins_answer_is_one_a_moment_asks()
    {
        var unasked = new List<string>();
        foreach (var (way, pushed) in PushReached)
        {
            foreach (var answer in Answers)
            {
                foreach (var held in Ends)
                {
                    var landing = Landing(pushed, "https://example.test/pr/7", answer, held);
                    // A completed answer is final and asked no more; any other the monitor reads no state from waits on the plugin.
                    var waits = PullRequestReading.Of(landing).State is null && answer != PullRequestStates.Completed;
                    if (waits && !PullRequestMoments.Concerns(landing)) unasked.Add($"{way}, answer {answer ?? "none"}, end {(held is null ? "open" : "held")}");
                }
            }
        }

        Assert.Empty(unasked);
    }

    [Fact]
    public void A_landing_with_no_pull_request_recorded_is_asked_only_where_a_push_of_daoris_s_carried_it()
    {
        Assert.True(PullRequestMoments.Concerns(Landing(pushed: true, null, null, null)));
        Assert.False(PullRequestMoments.Concerns(Landing(pushed: false, null, null, null)));
    }

    [Fact]
    public void A_settled_answer_or_an_end_the_line_settles_is_asked_no_more()
    {
        const string address = "https://example.test/pr/7";
        Assert.False(PullRequestMoments.Concerns(Landing(false, address, PullRequestStates.Completed, null)));
        Assert.False(PullRequestMoments.Concerns(Landing(false, address, PullRequestStates.Abandoned, null)));
        // Unanswered, a held end reads as merged (workflows rule 31), so nothing waits on the plugin.
        Assert.Equal(PullRequestReading.Merged, PullRequestReading.Of(Landing(false, address, null, Ends[1])).State);
        Assert.False(PullRequestMoments.Concerns(Landing(false, address, null, Ends[1])));
        Assert.False(PullRequestMoments.Concerns(Landing(false, address, PullRequestStates.Open, null) with { GoneAt = Then }));
    }
}
