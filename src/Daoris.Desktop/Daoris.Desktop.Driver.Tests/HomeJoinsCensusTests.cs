using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// No writer of the desktop's code joins a path onto the home by hand: every place under the home is an entry of
/// <c>HomeEntry.Table</c>, asked by <c>DaorisHome.Entry</c>, so a place a writer adds is a row (three twins, <c>HomeHistory</c>,
/// <c>home-places.json</c>) before it is written, and the install lists no stray of its own making. Code alone is read.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class HomeJoinsCensusTests
{
    private static readonly string[] Projects =
        [SourceFiles.DriverProject, "Daoris.Desktop.Modules", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.App"];

    /// <summary>A join whose first part is the home (a variable of that name, the folder found from it or the program's named home) and whose next is a literal.</summary>
    private static readonly Regex Joins = new(
        @"\bPath\.(?:Combine|Join)\(\s*(?:DaorisHome\.(?:FolderOf|Resolve)\([^()]*\)|ProgramHome\.Named|(?:\w+\.)*_?[hH]ome(?:Folder)?)\s*,",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// The joins in <paramref name="text"/>: found in its code (a comment's or a literal's words are blanked, offsets kept), and counted where
    /// the next part is a string literal, which the blanked code no longer shows, so the text's own character after the comma says.
    /// </summary>
    internal static int Joining(string text) =>
        Joins.Matches(StructureCheck.CodeOf(text)).Count(join =>
        {
            var next = join.Index + join.Length;
            while (next < text.Length && char.IsWhiteSpace(text[next])) next++;
            return next < text.Length && text[next] is '"' or '$' or '@';
        });

    [Fact]
    public void No_writer_joins_a_literal_onto_the_home_but_by_an_entry_of_the_table()
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var project in Projects)
        {
            var folder = SourceFiles.Project(project);
            foreach (var path in CensusScope.Judged(typeof(HomeJoinsCensusTests), SourceFiles.All(project, "*.cs")))
            {
                if (Joining(SourceTexts.Of(path)) > 0) found.Add(project + "/" + Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/'));
            }
        }

        Assert.True(found.Count == 0,
            "joins a place onto the home by hand, so no entry of the home's table names it and the install lists it as a stray: add a `HomeEntry` row "
            + $"(all three twins, HomeHistory, home-places.json) and ask `DaorisHome.Entry` for it: {string.Join(", ", found)}");
    }

    [Fact]
    public void The_census_sees_a_join_onto_the_home_and_not_one_onto_another_folder_or_a_word()
    {
        Assert.Equal(1, Joining("var p = Path.Combine(home, \"memo\", \"x.json\");"));
        Assert.Equal(1, Joining("var p = Path.Combine(DaorisHome.FolderOf(home), \"memo\");"));
        Assert.Equal(1, Joining("var p = Path.Join(world.Home, \"memo\");"));
        Assert.Equal(1, Joining("var p = Path.Combine(ProgramHome.Named, \"memo\");"));
        Assert.Equal(0, Joining("var p = DaorisHome.Entry(home, HomeEntry.Memo, \"git-objects.json\");"));
        Assert.Equal(0, Joining("var p = Path.Combine(folder, \"memo\");"));
        Assert.Equal(0, Joining("// Path.Combine(home, \"memo\")\nvar s = \"Path.Combine(home, \\\"memo\\\")\";"));
    }
}
