using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every kind of pass takes the one retry, within its own minutes, and ends past
/// its bound with its kind's transient reason, never <i>timed out</i> and never <i>no answer</i>.
/// </summary>
public sealed class TransientEndedTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 14, 0, 0, TimeSpan.Zero);

    private static readonly AgentError Overloaded =
        new(AgentErrorKind.Transient, new("protocol", -32603, "overloaded", 529, "API Error: 529 Overloaded", DoorWhere.Prompt), "stand");

    private static TransientEpisode Episode(int tries, bool exhausted) => new(Now, Overloaded) { Tries = tries, Exhausted = exhausted };

    // A run's ending as its hold hands it to the conclusion: its retry, with an episode of `tries` tries where one opened.
    private static HarnessEnding Ended(int? tries, bool exhausted)
    {
        var retry = new TransientRetry { Read = _ => throw new InvalidOperationException("not read here"), Clock = new HeldClock(Now) };
        if (tries is { } made)
        {
            var episode = retry.Open(Overloaded);
            episode.Tries = made;
            episode.Exhausted = exhausted;
        }

        return new HarnessEnding(1, null, []) { Transient = retry };
    }

    [Theory]
    [InlineData(TransientEnded.Helper, "helper.transient")]
    [InlineData(TransientEnded.Opinion, "opinion.transient")]
    [InlineData(TransientEnded.Intake, "intake.transient")]
    [InlineData(TransientEnded.Partner, "partner.transient")]
    [InlineData(TransientEnded.Voice, "voice.transient")]
    public void A_pass_past_its_bound_fails_with_its_kinds_transient_reason_and_the_providers_words(string kind, string code)
    {
        var ended = Ended(3, exhausted: true);

        var conclusion = TransientEnded.Over(SessionConclusion.Of("failed", Observation.TimedOut(15)), ended, kind, "acp-stub");

        Assert.Equal("failed", conclusion.State);
        Assert.Contains(conclusion.Parts!, part => part.Code == code);
        Assert.Contains("API Error: 529 Overloaded", conclusion.Note);
        Assert.DoesNotContain("minutes", conclusion.Note);
    }

    [Fact]
    public void A_pass_whose_episode_went_on_or_never_opened_keeps_its_own_end()
    {
        var timedOut = SessionConclusion.Of("failed", Observation.TimedOut(15));

        Assert.Same(timedOut, TransientEnded.Over(timedOut, Ended(2, exhausted: false), TransientEnded.Helper, "acp-stub"));
        Assert.Same(timedOut, TransientEnded.Over(timedOut, Ended(null, exhausted: false), TransientEnded.Helper, "acp-stub"));
    }

    [Fact]
    public void Minutes_that_end_inside_an_episode_end_with_the_transient_reason_never_timed_out()
    {
        var timedOut = SessionConclusion.Of("failed", Observation.TimedOut(15));

        var conclusion = TransientEnded.Over(timedOut, Ended(2, exhausted: false), TransientEnded.Opinion, "acp-stub", timedOut: true);

        Assert.Contains(conclusion.Parts!, part => part.Code == "opinion.transient");
        Assert.Same(timedOut, TransientEnded.Over(timedOut, Ended(null, exhausted: false), TransientEnded.Opinion, "acp-stub", timedOut: true));
    }

    [Fact]
    public void A_retry_waits_no_wait_past_its_passes_minutes()
    {
        // The first wait is a minute, the second two, the third four: the clock has run the waits already spent, so the third would end
        // at minute seven, past the five minutes, and the episode ends there.
        TransientRetry Retry(int spent) => new() { Read = _ => throw new InvalidOperationException(), Clock = new HeldClock(Now.AddMinutes(spent)), Deadline = Now.AddMinutes(5) };

        Assert.False(Retry(0).PastBound(Episode(0, false)));
        Assert.False(Retry(1).PastBound(Episode(1, false)));
        Assert.True(Retry(3).PastBound(Episode(2, false)));
    }

    [Fact]
    public void A_retry_with_no_minutes_of_its_own_ends_at_its_tries()
    {
        var retry = new TransientRetry { Read = _ => throw new InvalidOperationException(), Clock = new HeldClock(Now), Limit = 2 };

        Assert.False(retry.PastBound(Episode(1, false)));
        Assert.True(retry.PastBound(Episode(2, false)));
    }

    [Fact]
    public void A_helper_refused_for_its_provider_says_why_in_its_own_words()
    {
        Assert.Equal("transient", HelperWhy.Transient);
        Assert.Contains("provider kept failing", HelperWhy.Said(HelperWhy.Transient));
    }

    private sealed class HeldClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
