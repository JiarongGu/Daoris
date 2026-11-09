using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The git half of the run-time censuses (the design's §46, §62), judged against a test's set of names, since no declaration's name is
/// banned before row b: red on §46's planted reads and on a door's start naming a declaration by path, green on the store's own read
/// by a declaration's id and on the door's exclusion.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class DeclarationGitCensusTests
{
    private const string Id = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";
    private static readonly string[] Names = [DeclarationNames.Gates];

    [Fact]
    public void The_names_judged_are_the_repository_declarations_the_banned_list_holds()
    {
        Assert.Equal([DeclarationNames.Gates], DeclarationGitJudge.Judged(["F:Daoris.Driver.DeclarationNames.Gates", "F:Daoris.Driver.HomeEntry.DriverConfig"]).ToArray());
    }

    [Theory]
    [InlineData("outside", new[] { "cat-file", "--batch", Id }, "a start outside the store names the declaration " + Id + " by id: git cat-file --batch " + Id)]
    [InlineData("outside", new[] { "cat-file", "-p", Id }, "a start outside the store names the declaration " + Id + " by id: git cat-file -p " + Id)]
    [InlineData("outside", new[] { "show", "HEAD:daoris.gates.json" }, "a start outside the store names daoris.gates.json by path: git show HEAD:daoris.gates.json")]
    [InlineData("door", new[] { "ls-tree", "HEAD", "--", "daoris.gates.json" }, "a door's start names daoris.gates.json by path: git ls-tree HEAD -- daoris.gates.json")]
    public void A_planted_read_of_a_judged_declaration_turns_the_census_red(string asker, string[] command, string said)
    {
        DeclarationAsker? scope = asker == "door" ? DeclarationAsker.Door : null;

        Assert.Equal([said], DeclarationGitJudge.Problems([(command, scope)], Names, [Id]));
    }

    [Fact]
    public void The_stores_own_read_by_id_and_the_doors_exclusion_are_green()
    {
        IReadOnlyList<string> byId = ["cat-file", "--batch", Id];
        IReadOnlyList<string> search = ["grep", "-I", "-F", "-e", "gates", "HEAD", "--", .. DeclarationNames.Excluded];

        Assert.Empty(DeclarationGitJudge.Problems([(byId, DeclarationAsker.Store), (search, DeclarationAsker.Door), (byId, DeclarationAsker.Door)], Names, [Id]));
    }
}
