namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The keepers census over every shipping desktop project (the design's §23, §47): no field keeps an answer of the store but a
/// listed holder, said with the one flow it lives for. Shown red on round 3's planted keepers and green on a field holding a version.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class DeclarationKeepersCensusTests
{
    private const string What = "keeps an answer of the store";

    /// <summary>Each holder, by its file and field, with the flow that ends what it keeps; c1 lists <c>WatchRun._config</c> and <c>LimitWaits._due</c>.</summary>
    private static readonly Dictionary<string, string> Holders = new(StringComparer.Ordinal)
    {
    };

    private const string Memo = "Daoris.Desktop.Driver/Planted/Memo.cs";
    private const string Shelf = "Daoris.Desktop.Driver/Planted/Shelf.cs";

    private static readonly (string Path, string Text)[] Planted =
    [
        (Memo, """
            using System.Collections.Concurrent;
            using System.Text.RegularExpressions;
            using Daoris.Driver;
            namespace Planted;
            internal sealed record Effective(DeclarationVersion Config);
            internal sealed class Memo
            {
                private sealed record Kept(IReadOnlyList<Regex> Declared);
                private readonly Dictionary<string, Kept> _kept = new();
                private object? _view;
                private string? _hash;
                private ContentKey? _key;
                private Effective? _effective;

                internal void Look(string path)
                {
                    var answer = RepositoryDeclarations.OnDisk(path);
                    _kept[path] = new Kept(Patterns.Of(answer));
                    _view = RepositoryDeclarations.Parse(answer, RuleFileKind.One);
                    _hash ??= Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(answer.ToString())));
                    _key = (answer as DeclarationAnswer.Present)?.Content?.Key;
                    _effective = new Effective(answer.Version);
                    Shelf.Put(path, answer);
                }
            }
            """),
        ("Daoris.Desktop.Driver/Planted/Patterns.cs", """
            using System.Text.RegularExpressions;
            using Daoris.Driver;
            namespace Planted;
            internal static class Patterns
            {
                internal static IReadOnlyList<Regex> Of(DeclarationAnswer answer) => [new Regex(answer.ToString())];
            }
            """),
        (Shelf, """
            using System.Collections.Concurrent;
            namespace Planted;
            internal static class Shelf
            {
                private static readonly ConcurrentDictionary<string, object> s_kept = new();

                internal static void Put(string key, object value) => s_kept[key] = value;
            }
            """),
    ];

    private static readonly Lazy<SortedSet<string>> Found = new(() =>
        new(DesktopCompilations.Each.SelectMany(DeclarationKeepers.Kept), StringComparer.Ordinal));

    [Fact]
    public void No_field_of_a_shipping_project_keeps_an_answer_of_the_store_but_a_listed_holder()
    {
        var problems = SourceCensus.Problems(Found.Value, Holders.Keys, What);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void Each_holder_left_out_turns_the_census_red_naming_it()
    {
        Assert.Empty(SourceCensus.Unproved(Found.Value, Holders.Keys, What));
    }

    [Fact]
    public void Round_threes_planted_keepers_each_turn_the_census_red_and_a_field_holding_a_version_does_not()
    {
        var compilation = DesktopCompilations.Compile("Daoris.Desktop.Driver.Tests", Planted, driverBuilt: true);

        var kept = DeclarationKeepers.Kept(compilation);

        Assert.Equal([$"{Memo} _hash", $"{Memo} _kept", $"{Memo} _view", $"{Shelf} s_kept"], kept);
        Assert.Equal(
            [$"{Memo} _hash {What} and is not listed", $"{Memo} _kept {What} and is not listed", $"{Memo} _view {What} and is not listed", $"{Shelf} s_kept {What} and is not listed"],
            SourceCensus.Problems(kept, Holders.Keys, What));
    }
}
