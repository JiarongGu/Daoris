using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONTEXTDRIVE1b and CONTEXTDRIVE1i (the context-counters design §3, §5): a line's counts read as facts below the course, each with the
/// number Daoris acts on it at today, its parts with why each began, and its last go-ons with who answered, read from this home's files over
/// the records' text.
/// </summary>
public sealed class LineFactsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "line-facts", Guid.NewGuid().ToString("N")[..8]);
    private int _minute;

    public LineFactsTests()
    {
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));
        SupervisorRuleBook.SeedFresh(_home,() => StoreSettled.Yes);
    }

    public void Dispose() => GitSeed.Remove(_home);

    private void Keep(string session, params SessionEvent[] events)
    {
        var log = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        foreach (var each in events) log.Append(session, each with { At = At.AddMinutes(_minute++) });
    }

    private static SessionEvent Word(string text, string? from = null) => new() { Kind = SessionEventKind.User, Origin = from is null ? "person" : null, From = from, Text = text };

    private static SessionEvent Answer(string id, string? helper = null, string? opinion = null, string? from = null) =>
        new() { Kind = SessionEventKind.User, Origin = "agent", Helper = helper, Opinion = opinion, From = from, Id = id, Text = "An answer." };

    private static SessionEvent Said() => new() { Kind = SessionEventKind.Message, Text = "On it." };

    private static SessionEvent Ended() => new() { Kind = SessionEventKind.Turn, StopReason = "end_turn" };

    private static readonly string Records = new JsonArray(
        new JsonObject { ["id"] = "s1", ["kind"] = "chat", ["state"] = "ended", ["repository"] = "Owner", ["created"] = "2026-11-03T09:00:00Z" },
        new JsonObject { ["id"] = "s2", ["kind"] = "chat", ["state"] = "ended", ["repository"] = "Owner", ["from"] = "s1", ["fromWhy"] = "limit", ["created"] = "2026-11-03T09:30:00Z" }).ToJsonString();

    [Fact]
    public void CONTEXTDRIVE1i_a_line_s_parts_and_go_ons_say_why_each_began_and_who_answered()
    {
        Keep("s1", Word("Build it."), Said(), Ended(), Answer("w1", helper: "h1"), Said(), Ended());
        Keep("s2", Answer("w2", opinion: "o1"), Answer("w3", from: SupervisorHanded.From), Said(), Ended(), Word("Carry on.", from: DaorisWords.From), Said(), Ended());

        var facts = LineCountsRead.FactsOf(_home, "s2", Records);

        Assert.Equal([new LinePartFact("s1", null), new LinePartFact("s2", HandOverWhy.Limit)], facts.Parts);
        Assert.Equal(
            ["s1 person", "s1 alone helper", "s2 alone opinion supervisor", "s2 daoris"],
            facts.GoOns.Select(goOn => string.Join(" ", new[] { goOn.Session, goOn.By }.Concat(goOn.Answered))));
        var alone = Assert.Single(facts.Counts, count => count.Count == "answers-alone");
        Assert.Equal((0, "exact", new CountWatch(AnswersAlone.Bound, SupervisorRules.Hold, SupervisorRules.Machine)), (alone.N, alone.Known, alone.Watch));
        Assert.DoesNotContain(facts.Counts, count => count.Count == "failures");
    }

    [Fact]
    public void CONTEXTDRIVE1i_the_go_ons_are_the_last_ten_and_the_failures_watched_at_the_home_s_strikes_or_not_at_zero()
    {
        Keep("s1", [Word("Build it."), Said(), Ended(), .. Enumerable.Range(1, 12).SelectMany(n => new[] { Answer($"w{n}", helper: "h"), Said(), Ended() })]);
        var facts = LineCountsRead.FactsOf(_home, "s1", Records);
        Assert.Equal(LineCounts.GoOnsShown, facts.GoOns.Count);
        Assert.All(facts.GoOns, goOn => Assert.Equal(GoOnBy.Alone, goOn.By));
        Assert.Equal(At.AddMinutes(_minute - 2), facts.GoOns[^1].At);

        var driven = new JsonArray(new JsonObject { ["id"] = "s1", ["kind"] = "driven", ["quest"] = "q1", ["state"] = "failed", ["repository"] = "Engine" }).ToJsonString();
        Assert.Equal(new CountWatch(3, SupervisorRules.Hold, SupervisorRules.Machine), LineCountsRead.FactsOf(_home, "s1", driven).Counts.Single(count => count.Count == "failures").Watch);

        // The service did not answer: every count from its records reads unknown, and the line is its part alone.
        var unread = LineCountsRead.FactsOf(_home, "s1", null);
        Assert.Equal("unknown", unread.Counts.Single(count => count.Count == "failures").Known);
        Assert.Equal([new LinePartFact("s1", null)], unread.Parts);
    }

    /// <summary>A go-on and a start read the facts on their way: an answer that is no list of records is the service not answering, never a throw.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("""{"records":[]}""")]
    public async Task CONTEXTDRIVE1b_records_that_do_not_read_count_as_unknown_and_the_course_installs_the_read(string answer)
    {
        using var service = QuestStandIn.Client(_ => (System.Net.HttpStatusCode.OK, answer));

        var facts = await LineFactsReads.Current.OfAsync(service, _home, "s1", CancellationToken.None);

        Assert.IsType<LineFactsOperations>(LineFactsReads.Current);
        Assert.All(facts.Counts.Where(count => count.From == "records"), count => Assert.Equal(("unknown", "service-unanswered"), (count.Known, count.Why)));
    }
}
