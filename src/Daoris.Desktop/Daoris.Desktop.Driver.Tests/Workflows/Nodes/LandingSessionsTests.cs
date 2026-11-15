using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A landing supervisor's decision is kept against the newest session of the step's run's work: a chain's quests serve its run, and the quests an
/// ask gave a repository serve that ask's delivery run there, so the ask's one review is answerable by an appointed supervisor (D296).
/// </summary>
public sealed class LandingSessionsTests
{
    private static readonly QuestView First = new("q1", "ask #a1", "engine", "Part one", "", "Done") { Request = "a1/p1/1" };

    private static readonly QuestView Next = new("q2", "daoris", "engine", "Its next step", "", "Done") { Parent = "q1" };

    private static readonly QuestView Elsewhere = new("q3", "ask #a1", "site", "Part two", "", "Done") { Request = "a1/p2/1" };

    private static readonly QuestView Unasked = new("q4", "daoris", "engine", "Alone", "", "Done");

    private static readonly IReadOnlyList<QuestView> Quests = [First, Next, Elsewhere, Unasked];

    /// <summary>A teammate's record is keyed <c>origin/id</c>.</summary>
    private static PriorSession Session(string id, string quest, string repository, bool teammate = false) =>
        new(teammate ? "origin/" + id : id, null, "completed", null, repository) { Quest = quest };

    private static LandingStep Step(string run, string repository) => new(run, "review", 1, repository, ["accept"]);

    [Fact]
    public void A_chains_quests_serve_its_run_and_an_asks_quests_serve_its_delivery_run_in_their_repository()
    {
        var serves = LandingSessions.Serves(Quests);
        var delivery = AskRunKey.Of("a1", "engine")!;

        Assert.True(serves("q2", "q1"));
        Assert.True(serves("q1", delivery));
        Assert.False(serves("q3", delivery));
        Assert.True(serves("q3", AskRunKey.Of("a1", "site")!));
        Assert.False(serves("q4", delivery));
        Assert.False(serves("q1", AskRunKey.Of("a2", "engine")!));
    }

    [Fact]
    public void An_asks_delivery_step_is_kept_against_the_newest_session_of_the_asks_work_in_its_repository_never_a_teammates()
    {
        var records = new[]
        {
            Session("s1", "q1", "engine"), Session("s4", "q4", "engine"), Session("s3", "q3", "site"), Session("s9", "q1", "engine", teammate: true),
        };

        var found = LandingSessions.Of(records, Step(AskRunKey.Of("a1", "engine")!, "engine"), LandingSessions.Serves(Quests));

        Assert.Equal("s1", found?.Session);
        Assert.Null(LandingSessions.Of(records, Step(AskRunKey.Of("a2", "engine")!, "engine"), LandingSessions.Serves(Quests)));
    }

    [Fact]
    public void Where_the_quests_are_not_read_a_step_is_served_by_its_own_quests_session_alone()
    {
        var serves = LandingSessions.Serves(null);

        Assert.True(serves("q1", "q1"));
        Assert.False(serves("q2", "q1"));
        Assert.Equal("s2", LandingSessions.Of([Session("s2", "q1", "engine")], Step("q1", "engine"), serves)?.Session);
    }
}
