using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

[Collection("knowledge review port")]
public sealed class KnowledgeWorkplacePortsTests
{
    [Fact]
    public void Workplace_installs_the_existing_readers_and_words_before_Loop_exists()
    {
        var read = KnowledgeWorkplace.Current;
        Assert.IsType<KnowledgeWorkplaceReads>(read);
        Assert.Equal(TreeGuard.Matcher, read.TreeGuardMatcher);
        Assert.Equal(TreeGuard.EditMatcher, read.TreeGuardEditMatcher);
        Assert.Equal(new[] { LandingRules.PersonAccepted, LandingRules.AutoAccepted, LandingRules.HandNote(new TreeHand(true, "")).Text! }, read.AcceptanceNotes);
        Assert.Equal(LandedBranches.FileName, read.LandingsFileName);
        Assert.Equal(AutoLandings.FileName, read.AutoLandingsFileName);
        Assert.Equal(CanonicalLine.Judged(DriverConfig.Empty, "app", "default", null, null),
            read.JudgedLine(DriverConfig.Empty, "app", "default", null, null));
        Assert.Equal(LandingRules.Choose(DriverConfig.Empty, "app", "default"), read.ChooseLanding(DriverConfig.Empty, "app", "default"));
        var state = new PullRequestState("completed");
        Assert.Equal(PullRequestWords.Row(state, null, null, null), read.PullRequestRow(state, null));
        Assert.Equal(HistoryCommand.Count(2, "file"), read.HistoryCount(2, "file"));
        Assert.Equal(HistoryCommand.Size(1024), read.HistorySize(1024));
        Assert.Equal(HistoryCommand.Copies(2), read.HistoryCopies(2));
        Assert.Equal(HistoryCommand.Join(["one", null, "two"]), read.HistoryJoin(["one", null, "two"]));
    }

    [Fact]
    public void An_uninstalled_workplace_reader_throws_and_the_test_restores_it()
    {
        var reader = KnowledgeWorkplace.Current;
        try
        {
            KnowledgeWorkplace.Current = null!;
            Assert.Throws<InvalidOperationException>(() => KnowledgeWorkplace.Current);
        }
        finally
        {
            KnowledgeWorkplace.Current = reader;
        }
        Assert.Same(reader, KnowledgeWorkplace.Current);
    }
}
