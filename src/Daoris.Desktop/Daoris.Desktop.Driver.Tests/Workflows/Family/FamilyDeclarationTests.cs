using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The family file and a consumer's lock as the driver reads them: the same shapes the doctrine tool's check reads, plus each edge's lag
/// bound. A consumer's edges to a producer are the members that use it from a higher layer and are not denied.
/// </summary>
public sealed class FamilyDeclarationTests
{
    private const string File = """
        {
          "layers": [["core"], ["web", "api"], ["site"]],
          "denied": [{ "consumer": "site", "producer": "core", "why": "reach it through api" }],
          "edges": [{ "consumer": "web", "producer": "core", "lag": "3d" }, { "consumer": "api", "producer": "core", "lag": "12h" }]
        }
        """;

    private static IReadOnlyList<string> Uses(string member) => member switch
    {
        "web" => ["core"],
        "api" => ["core", "api", "CORE"],
        "site" => ["core", "api"],
        "core" => ["web"],
        _ => [],
    };

    [Fact]
    public void The_file_is_read_by_its_layers_denied_pairs_and_lag_bounds()
    {
        var family = FamilyDeclaration.Read(File)!;

        Assert.Equal(["core", "web", "api", "site"], family.Members);
        Assert.Equal(TimeSpan.FromDays(3), family.LagOf(new PinEdge("web", "core")));
        Assert.Equal(TimeSpan.FromHours(12), family.LagOf(new PinEdge("api", "core")));
        Assert.Null(family.LagOf(new PinEdge("site", "api")));
    }

    [Fact]
    public void A_producer_s_consumers_are_the_members_that_use_it_from_a_higher_layer_and_are_not_denied()
    {
        var family = FamilyDeclaration.Read(File)!;

        Assert.Equal([new PinEdge("web", "core"), new PinEdge("api", "core")], family.ConsumersOf("core", Uses));
        Assert.Equal([new PinEdge("site", "api")], family.ConsumersOf("api", Uses));
        Assert.Empty(family.ConsumersOf("site", Uses));
    }

    [Fact]
    public void An_edge_to_an_equal_or_higher_layer_is_no_fan_out_the_check_refuses_it_instead()
    {
        var family = FamilyDeclaration.Read(File)!;

        Assert.DoesNotContain(new PinEdge("core", "web"), family.ConsumersOf("web", Uses));
    }

    [Theory]
    [InlineData("""{ "layers": [] }""")]
    [InlineData("""{ "layers": [["a"], "b"] }""")]
    [InlineData("""{ "layers": [["a"]], "denied": [{ "consumer": "a" }] }""")]
    [InlineData("""{ "layers": [["a"]], "edges": [{ "consumer": "a", "producer": "b", "lag": "soon" }] }""")]
    [InlineData("""{ "layers": [["a"], ["a"]] }""")]
    [InlineData("not json")]
    public void A_file_of_the_wrong_shape_is_no_family_and_is_never_read_as_one(string text)
    {
        Assert.Null(FamilyDeclaration.Read(text));
    }

    [Theory]
    [InlineData("3d", 72)]
    [InlineData("12h", 12)]
    [InlineData("1h", 1)]
    [InlineData("999999h", 999999)]
    public void A_lag_is_a_count_of_days_or_hours(string text, int hours)
    {
        Assert.Equal(TimeSpan.FromHours(hours), FamilyLag.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0d")]
    [InlineData("3")]
    [InlineData("d3")]
    [InlineData("3w")]
    [InlineData("-1d")]
    [InlineData("03d")]
    [InlineData("1000000h")]
    [InlineData("99999999d")]
    public void Anything_else_is_no_lag(string text)
    {
        Assert.Null(FamilyLag.Parse(text));
    }

    [Fact]
    public void A_lock_gives_the_commit_pinned_of_each_producer_and_a_wrong_one_gives_none()
    {
        var pins = FamilyLock.Read("""{ "pins": [{ "producer": "Core", "commit": "abc" }] }""")!;

        Assert.Equal("abc", pins["core"]);
        Assert.Empty(FamilyLock.Read("""{ "pins": [] }""")!);
        Assert.Null(FamilyLock.Read("""{ "pins": [{ "producer": "core" }] }"""));
        Assert.Null(FamilyLock.Read("[]"));
    }
}
