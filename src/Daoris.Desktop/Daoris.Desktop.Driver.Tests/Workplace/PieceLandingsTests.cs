using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A partner's record lands nothing at any landing door, in one sentence naming the
/// lead's merge of the frozen ref. A run's <c>land</c> refuses it before it reads the tree, says so once, and the verb's words are the same
/// sentence.
/// </summary>
/// <remarks>No git runs: the refusal comes before the landing reads the tree.</remarks>
public sealed class PieceLandingsTests : ReviewLandingFixture
{
    private const string Copy = "X:/daoris/trees/aurora/engine/p-pc000001";

    private LandPerformer Performer() =>
        new(_home, _world, new SessionTrees(_home), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), _lines.Add) { HoldsLease = () => true };

    private static LandStart Start(string run) =>
        new(RunUnits.IdOf(run), run, "landing", "branch:engine/p-pc000001", ["pt1"], "engine", "aurora", OpinionStandIn.Tip, "digest", null);

    private void Partner() =>
        _world.Records.Add(new SessionRecord("pt1", "engine", "completed") { Tree = Copy, Serves = "lead1", Piece = "pc000001", Created = DateTimeOffset.UtcNow });

    [Theory]
    [InlineData("chat-pt1")]
    [InlineData("q1")]
    public async Task A_runs_land_refuses_a_partners_record_in_its_sentence_once(string run)
    {
        Partner();
        if (run == "q1") _world.Records[0] = _world.Records[0] with { Quest = "q1" };
        var performer = Performer();

        var said = await performer.PerformAsync(Start(run));

        Assert.NotNull(said);
        Assert.Contains("session `pt1` is a partner writing piece `pc000001` for session `lead1`, and lands nothing", said);
        Assert.Contains($"git merge --no-ff {PieceRefs.Of("pc000001")}", said);
        Assert.Null(await performer.PerformAsync(Start(run)));
    }

    [Fact]
    public void The_verbs_words_are_the_doors_one_sentence_and_a_helpers_record_has_none()
    {
        Partner();
        _world.Records.Add(new SessionRecord("hp1", "engine", "completed") { Tree = Copy, Serves = "lead1" });

        var refused = TreesWords.PartnerLands(_world.Records, "pt1")!;

        Assert.Equal($"trees: {PieceLandings.Refusal(_world.Records[0])}", Assert.Single(refused.Error));
        Assert.Null(PieceLandings.Refusal(_world.Records[1]));
        Assert.Null(TreesWords.PartnerLands(_world.Records, "hp1"));
    }
}
