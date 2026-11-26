using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.ObservedFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A refused landing says why in words, waits by itself while the line's own state holds it (another landing under way, its branches being
/// brought up to date), and holds no start of its session's quest meanwhile.
/// </summary>
public sealed class LandRefusedTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 2, 0, 0, TimeSpan.Zero);

    private static Consideration Start(string quest, string status, bool goesOn = false) =>
        new(new QuestView(quest, "asker", "engine", "title", "body", status), StartVerdict.Start, "start") { GoesOn = goesOn };

    /// <summary>The step of a run whose landing is refused reads the refusal's own sentence, not only that nothing is handed on.</summary>
    [Fact]
    public void A_refused_landing_is_said_on_its_step_in_the_words_it_was_refused_with()
    {
        var unit = Unit(Grown());
        var bound = new NodeAcceptance("run:r", "t1", Graph, NodeKinds.Land, 1, 1) { Subject = unit.Id };

        var before = NodeObserving.Observe(unit, Node(NodeKinds.Land), bound);
        Assert.DoesNotContain("because the line", before.Said, StringComparison.Ordinal);

        LandOutcomes.Kept(Guid.NewGuid().ToString("N"), unit.Id, "t1",
            new TreeLanding(false, "Nothing was merged: because the line's integration holds `candidate-1`, which it waits for.") { Refusal = LandingCode.Busy });
        var after = NodeObserving.Observe(unit, Node(NodeKinds.Land), bound);

        Assert.Null(after.Outcome);
        Assert.Contains("because the line's integration holds `candidate-1`", after.Said, StringComparison.Ordinal);
        LandOutcomes.Forget(unit.Id, "t1");
        Assert.DoesNotContain("because the line", NodeObserving.Observe(unit, Node(NodeKinds.Land), bound).Said, StringComparison.Ordinal);
    }

    /// <summary>A request already open for the branch is another landing under way: the refusal passes, so it is tried again by itself.</summary>
    [Fact]
    public void A_request_open_for_the_branch_is_a_cause_that_passes()
    {
        var open = new IntegrationAdmission(false, "a request is already open for `feature/x` at 1234567.") { Refusal = IntegrationRefusals.Open };
        var gone = new IntegrationAdmission(false, "`feature/x` is the line itself.") { Refusal = IntegrationRefusals.IsLine };

        Assert.Equal(LandingCode.Busy, IntegrationPresses.CodeOf(open));
        Assert.Null(IntegrationPresses.CodeOf(gone));
        Assert.Contains("tried again", IntegrationPresses.Said(open), StringComparison.Ordinal);
        Assert.Contains("a request is already open for `feature/x`", IntegrationPresses.Said(open), StringComparison.Ordinal);
        Assert.DoesNotContain("tried again", IntegrationPresses.Said(gone), StringComparison.Ordinal);
    }

    /// <summary>A record the integration could not keep (a store another landing holds) is the line's own state, which passes.</summary>
    [Fact]
    public void A_record_that_could_not_be_kept_is_a_cause_that_passes_and_names_what_it_waited_for()
    {
        var (said, code) = IntegrationPresses.Unkept(new IOException("the store is held"), "main");

        Assert.Equal(LandingCode.Busy, code);
        Assert.Contains("the store is held", said, StringComparison.Ordinal);
        Assert.Contains("`main`", said, StringComparison.Ordinal);
        Assert.Contains("tried again", said, StringComparison.Ordinal);
    }

    /// <summary>A waiting reason is a sentence a person reads, never only its code.</summary>
    [Theory]
    [InlineData(LandWaits.Unchanged)]
    [InlineData(LandWaits.Refused)]
    [InlineData(LandWaits.Waiting)]
    [InlineData(LandWaits.Held)]
    [InlineData(LandWaits.Superseded)]
    [InlineData(LandWaits.Away)]
    [InlineData(LandWaits.InUse)]
    [InlineData(LandWaits.Busy)]
    public void Every_wait_reason_has_words(string reason)
    {
        var sentence = LandWaits.Sentence("r1", "landing", reason);

        Assert.DoesNotContain($"({reason}): {reason}", sentence, StringComparison.Ordinal);
        Assert.True(sentence.Length > $"run r1: `landing` waits ({reason}): ".Length + 20, sentence);
    }

    /// <summary>A run whose landing waits is not started again by the start stage at each look: not a first start, not a carry-on.</summary>
    [Fact]
    public void A_quest_whose_runs_landing_waits_is_not_started_again()
    {
        var home = Guid.NewGuid().ToString("N");
        LandStandings.Wait(home, "q1", Now);

        Assert.True(LandStandings.Holds(home, Start("q1", "Taken"), Now));
        Assert.False(LandStandings.Holds(home, Start("q2", "Taken"), Now));
        Assert.False(LandStandings.Holds(Guid.NewGuid().ToString("N"), Start("q1", "Taken"), Now));
    }

    /// <summary>Words said to the session go on in it whatever its landing waits on, and a quest not taken starts as before.</summary>
    [Fact]
    public void Words_waiting_and_an_open_quest_still_start()
    {
        var home = Guid.NewGuid().ToString("N");
        LandStandings.Wait(home, "q1", Now);

        Assert.False(LandStandings.Holds(home, Start("q1", "Taken", goesOn: true), Now));
        Assert.False(LandStandings.Holds(home, Start("q1", "Open"), Now));
    }

    /// <summary>The wait ends with the landing, and a wait no look has read again lapses, so a quest started anew is never held by an old one.</summary>
    [Fact]
    public void The_wait_ends_with_its_landing_and_lapses_unread()
    {
        var home = Guid.NewGuid().ToString("N");
        LandStandings.Wait(home, "q1", Now);
        LandStandings.Wait(home, "q2", Now);

        LandStandings.Done(home, "q2");

        Assert.False(LandStandings.Holds(home, Start("q2", "Taken"), Now));
        Assert.True(LandStandings.Holds(home, Start("q1", "Taken"), Now + LandRetries.Fresh - TimeSpan.FromSeconds(1)));
        Assert.False(LandStandings.Holds(home, Start("q1", "Taken"), Now + LandRetries.Fresh));
    }

    /// <summary>The performer marks the standing for every wait and refusal that keeps a landing owed, and clears it at the landing.</summary>
    [Theory]
    [InlineData(LandingCode.Busy, true)]
    [InlineData(LandingCode.Refused, true)]
    [InlineData(LandingCode.Queued, true)]
    [InlineData(LandingCode.Unreviewed, true)]
    [InlineData(LandingCode.Landed, false)]
    [InlineData(LandingCode.Advanced, false)]
    [InlineData(LandingCode.Nothing, false)]
    [InlineData(LandingCode.Already, false)]
    public void A_try_keeps_the_standing_only_where_the_landing_is_still_owed(string code, bool owed)
    {
        Assert.Equal(owed, LandStandings.Owed(code));
    }
}
