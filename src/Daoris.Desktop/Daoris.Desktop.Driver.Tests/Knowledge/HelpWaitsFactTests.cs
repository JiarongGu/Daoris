using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>Ask Daoris's fact of what each line waits on: which of the loop's wakes move it, and how the room's reading takes it.</summary>
public sealed class HelpWaitsFactTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-19T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static HelpMachineWhole Whole(params RepoView[] repositories) => new(new Snapshot([], repositories, []), [], []);

    /// <summary>
    /// Woken by a session's records, a quest or a word moving, and by no ask, channel post, ref or configuration; the room's read says it
    /// not read until it is judged, and as judged once it is.
    /// </summary>
    [Fact]
    public void COMMS1i_what_each_line_waits_on_is_woken_by_the_records_a_quest_or_a_word_and_read_as_judged()
    {
        var facts = MachineFactsSnapshot.Empty.With(HelpFacts.Machine, HelpFacts.Whole, new JudgedFact(Whole(new RepoView("site", Adopted: true, Root: "checkouts/site")), At));

        foreach (var family in new[] { "sessions", "quests", "words" })
        {
            Assert.Contains((HelpFacts.Exchanges, HelpFacts.Whole), HelpFacts.Woken(ChangeTopics.Ledger, family, facts));
        }

        foreach (var (topic, subject) in new[] { (ChangeTopics.Ledger, "asks"), (ChangeTopics.Ledger, "channel"), (ChangeTopics.Refs, "site"), (ChangeTopics.Config, "driver.json") })
        {
            Assert.DoesNotContain((HelpFacts.Exchanges, HelpFacts.Whole), HelpFacts.Woken(topic, subject, facts));
        }

        var notYet = HelpFacts.Read(facts, DriverConfig.Empty)!;
        Assert.Null(notYet.Waits);
        Assert.True(notYet.NotReadYet.Waits);
        var waits = new HelpWaits([new HelpWait("s1", "site", "default", "helper h7 · stub · asked")], []);
        var judged = HelpFacts.Read(facts.With(HelpFacts.Exchanges, HelpFacts.Whole, new JudgedFact(waits, At)), DriverConfig.Empty)!;
        Assert.Same(waits, judged.Waits);
        Assert.False(judged.NotReadYet.Waits);
    }
}
