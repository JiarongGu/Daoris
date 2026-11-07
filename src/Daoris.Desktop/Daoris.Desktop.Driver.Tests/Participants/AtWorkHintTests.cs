using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The at-work answer's hint, drawn where the attended part's newest generation started at least
/// two subagents and the repository's routing standing is not open; the standing is the judge's, asked only where the hint can be drawn.
/// </summary>
public sealed class AtWorkHintTests
{
    private const string First = "a000000000000001";
    private const string Second = "a000000000000002";
    private const string Issuer = "4242 638000000000000000";
    private static readonly DateTimeOffset T0 = new(2026, 11, 2, 8, 30, 0, TimeSpan.Zero);

    private static string Moment(int second) =>
        T0.AddSeconds(second).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static string Start(string proc, int second) => ChildBindings.StartLine(proc, Issuer, T0.AddSeconds(second))!;

    private static string Child(string agent, string proc, int second) =>
        new JsonObject { ["at"] = Moment(second), ["by"] = "stream", ["event"] = "started", ["agent"] = agent, ["proc"] = proc }.ToJsonString();

    private static LineageReading Read(params string[] lines) => LineageReading.Of(ChildBindings.Fold(lines, null, (_, _) => TokenReading.Watched));

    private static AtWorkAnswer Compose(LineageReading reading, Func<string, string>? standing, string? repository = "game", List<string>? asked = null)
    {
        var routing = new AtWorkRouting([], _ => null, _ => null, _ => 0, new SliceHealth(true))
        {
            StandingOf = standing is null ? null : repo =>
            {
                asked?.Add(repo);
                return standing(repo);
            },
        };
        var held = AtWork.Compose(
            "s1", null, id => id == "s1" ? new AtWorkRecord("s1", "working") { Repository = repository } : null, _ => reading,
            new SliceHealth(true), new SliceHealth(true), routing);
        return Assert.IsType<AtWorkAnswer>(held.Answer);
    }

    [Theory]
    [InlineData(RoutingStanding.NotDriven)]
    [InlineData(RoutingStanding.NoLanes)]
    [InlineData(RoutingStanding.OneAtATime)]
    public void Two_subagents_in_one_generation_draw_the_standing_unless_it_is_open(string standing)
    {
        var two = Read(Start(First, 0), Child("c1", First, 1), Child("c2", First, 2));

        Assert.Equal(standing, Compose(two, _ => standing).Hint);
        Assert.Null(Compose(two, _ => RoutingStanding.Open).Hint);
    }

    [Fact]
    public void One_subagent_draws_none_and_the_standing_is_not_asked()
    {
        var asked = new List<string>();

        Assert.Null(Compose(Read(Start(First, 0), Child("c1", First, 1)), _ => RoutingStanding.NoLanes, asked: asked).Hint);
        Assert.Empty(asked);
    }

    [Fact]
    public void Subagents_of_an_earlier_process_are_not_counted_against_the_newest_generation()
    {
        var restarted = Read(Start(First, 0), Child("c1", First, 1), Child("c2", First, 2), Start(Second, 10), Child("c3", Second, 11));

        Assert.Null(Compose(restarted, _ => RoutingStanding.NoLanes).Hint);
        Assert.Equal(RoutingStanding.NoLanes, Compose(
            Read(Start(First, 0), Child("c1", First, 1), Start(Second, 10), Child("c2", Second, 11), Child("c3", Second, 12)), _ => RoutingStanding.NoLanes).Hint);
    }

    [Theory]
    [InlineData(AtWorkRouting.Unheard)]
    [InlineData(AtWorkRouting.Unnamed)]
    public void A_standing_not_judged_draws_no_hint(string word)
    {
        var two = Read(Start(First, 0), Child("c1", First, 1), Child("c2", First, 2));

        Assert.Null(Compose(two, _ => word).Hint);
    }

    [Fact]
    public void No_repository_or_no_judge_or_no_readable_lineage_draws_none()
    {
        var two = Read(Start(First, 0), Child("c1", First, 1), Child("c2", First, 2));

        Assert.Null(Compose(two, _ => RoutingStanding.NoLanes, repository: null).Hint);
        Assert.Null(Compose(two, standing: null).Hint);
        Assert.Null(Compose(LineageReading.Failed("io"), _ => RoutingStanding.NoLanes).Hint);
    }
}
