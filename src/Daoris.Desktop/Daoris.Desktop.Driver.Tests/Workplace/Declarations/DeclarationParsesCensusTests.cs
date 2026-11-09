using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The parses census by declared types (the design's §33, §50): every kind and view the driver declares reaches immutable concrete
/// types alone, and each version holds ids, numbers and strings. Shown red on a planted list behind a read-only interface and on an array.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class DeclarationParsesCensusTests
{
    [Fact]
    public void Every_kind_and_view_the_driver_declares_reaches_immutable_types_alone()
    {
        var kinds = DeclarationParses.Declared(typeof(RepositoryDeclarations).Assembly).ToList();

        Assert.Contains(typeof(RuleFileKind), kinds);
        var problems = DeclarationParses.Problems(kinds);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void A_planted_list_behind_a_read_only_interface_and_an_array_each_turn_the_census_red()
    {
        Assert.Equal(
            ["ListedParse.Paths is IReadOnlyList`1, which admits a mutable value", "ListedParse.Rules is String[], which admits a mutable value"],
            DeclarationParses.Problems([typeof(ListedKind)]));
    }

    [Fact]
    public void A_planted_version_holding_an_answer_turns_the_census_red()
    {
        Assert.Equal(["ContentKey.Answer holds DeclarationAnswer, which no version may: ids, numbers and strings alone"],
            DeclarationParses.Problems([typeof(VersionedKind)]));
    }

    private sealed record ListedParse(IReadOnlyList<string> Paths, string[] Rules);

    private sealed class ListedKind : IDeclarationKind<ListedParse>
    {
        public ListedParse Parse(DeclarationBytes content) => new([], []);

        public bool Problem(ListedParse parsed) => false;

        public long Weigh(ListedParse parsed) => 0;
    }

    private sealed record ContentKey(string Hex, DeclarationAnswer Answer);

    private sealed record VersionedParse(ContentKey Key);

    private sealed class VersionedKind : IDeclarationKind<VersionedParse>
    {
        public VersionedParse Parse(DeclarationBytes content) => new(new("", DeclarationAnswer.Absent.One));

        public bool Problem(VersionedParse parsed) => false;

        public long Weigh(VersionedParse parsed) => 0;
    }
}
