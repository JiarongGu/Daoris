using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONTEXTDRIVE1i (D229 point 6; the context-counters design §5): the agent supervisor's reading. Its print gains the line's course, every
/// count with how it is known and its source, cited as a reason names it, what Daoris does on each and whose that is, the line's parts and
/// its last go-ons with who answered; and a reason's <c>count:</c> source names a count only where it is one of those handed.
/// </summary>
public sealed class CountTextsTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    internal static readonly CountFact[] Handed =
    [
        new("answers-alone", 2, "exact", null, "admission") { Watch = new CountWatch(2, "hold") },
        new("failures", 1, "exact", null, "records") { Watch = new CountWatch(3, "hold") },
        new("quiet", 0, "at-least", "walk-cut", "ended.quiet"),
        new("moves", null, "unknown", "store-unreadable", "role_moves"),
    ];

    [Fact]
    public void CONTEXTDRIVE1i_the_print_s_part_says_every_count_its_source_what_Daoris_does_the_parts_and_the_last_go_ons()
    {
        var facts = new LineFacts(
            Handed,
            [new LinePartFact("s1", null), new LinePartFact("s2", HandOverWhy.Limit)],
            [
                new GoOnFact(At, "s1", GoOnBy.Person, []),
                new GoOnFact(At.AddMinutes(5), "s2", GoOnBy.Alone, [GoOnBy.Helper]),
                new GoOnFact(At.AddMinutes(9), "s2", GoOnBy.Alone, [GoOnBy.Opinion, GoOnBy.Supervisor]),
                new GoOnFact(At.AddMinutes(12), "s2", GoOnBy.Passed, []),
            ]);

        Assert.Equal(
            """
            Every count of this line as Daoris read it; a reason names one by its first words here.
            - count:answers-alone=2 (exact): answers alone in a row, from the line's admission; Daoris's own number: it waits for the person after 2
            - count:failures=1 (exact): failed sessions on this quest, from the service's records; Daoris's own number: it waits for the person after 3
            - count:quiet=0 (at-least): its last part ended quiet, from its last part's end; its walk back was cut
            - count:moves (unknown): moves to another choice in 24 hours, from the moves kept here; the store did not read
            No rule of the person's is in force on this line: Daoris acts on its own numbers, named beside each count.
            Its parts, oldest first:
            - session s1: opened
            - session s2: moved on at a limit
            Its last go-ons, oldest first:
            - 2026-11-03 09:00 UTC, session s1: the person's word
            - 2026-11-03 09:05 UTC, session s2: by answers alone: a helper's answer
            - 2026-11-03 09:09 UTC, session s2: by answers alone: a second opinion's findings and the supervisor's answer
            - 2026-11-03 09:12 UTC, session s2: the person's Go on with this answer
            """.ReplaceLineEndings("\n"),
            CountTexts.Print(facts));

        // A rule's acts follow the go-ons, oldest first, said from the rule's codes and numbers alone; none, no section.
        Assert.DoesNotContain("Its rules' acts", CountTexts.Print(facts), StringComparison.Ordinal);
        var rule = new SupervisorRule("failures", SupervisorRules.Failures, "in-a-row", 3, SupervisorRules.Hold, null, "repository:engine", null);
        var acted = CountTexts.Print(facts with { Acts = [new RuleActed("q1", "s2", rule, 4, 3, Known: true, "start", At.AddMinutes(20))] });
        Assert.EndsWith(
            "Its rules' acts, oldest first:\n- 2026-11-03 09:20 UTC, session s2: the person's rule for this repository: failed sessions on this quest stood at 3, and Daoris waits for the person after 3",
            acted, StringComparison.Ordinal);

        // A rule of the person's is named as theirs, with its place.
        var ruled = CountTexts.Print(new LineFacts([Handed[0] with { Watch = new CountWatch(3, "hold", "repository:engine") }], [], []));
        Assert.Contains("- count:answers-alone=2 (exact): answers alone in a row, from the line's admission; the person's rule for this repository: Daoris waits for the person after 3", ruled, StringComparison.Ordinal);
        Assert.Contains("The rules in force on this line are the person's, named beside each count.", ruled, StringComparison.Ordinal);
        Assert.DoesNotContain("engine", ruled, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("count:answers-alone=2 (exact)", false)]
    [InlineData("count:answers-alone=2", false)]
    [InlineData("count:answers-alone", false)]
    [InlineData("  count:failures=1 (exact), read before answering", false)]
    [InlineData("count:moves (unknown)", false)]
    [InlineData("count:answers-alone=3 (exact)", true)]
    [InlineData("count:answers-alone=2 (at-least)", true)]
    [InlineData("count:tries=1 (exact)", true)]
    [InlineData("count:", true)]
    [InlineData("the quest's line 2", false)]
    public void CONTEXTDRIVE1i_a_count_source_names_a_count_handed_or_none(string source, bool none) =>
        Assert.Equal(none, CountTexts.NamesNone(source, Handed));

    [Fact]
    public void CONTEXTDRIVE1i_a_count_is_cited_as_the_print_names_it_and_none_handed_names_none()
    {
        Assert.Equal(["count:answers-alone=2 (exact)", "count:failures=1 (exact)", "count:quiet=0 (at-least)", "count:moves (unknown)"], Handed.Select(CountTexts.Cite));
        Assert.All(Handed, count => Assert.False(CountTexts.NamesNone(CountTexts.Cite(count), Handed)));
        Assert.True(CountTexts.NamesNone("count:answers-alone=2 (exact)", null));
        Assert.False(CountTexts.NamesNone("D201 point 3", null));
    }
}
