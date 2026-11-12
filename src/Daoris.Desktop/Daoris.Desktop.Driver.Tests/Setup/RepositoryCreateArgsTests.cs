using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The words a creation is typed as: the inverse of <see cref="RepositoryCreateCommand.Parse"/>, which the Confirm card prints as the command
/// the press is, so what the person reads is what a terminal would take and ask the same of.
/// </summary>
public sealed class RepositoryCreateArgsTests
{
    private static readonly string Folder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "somewhere"));

    [Fact]
    public void A_creation_is_typed_as_its_name_and_the_words_it_asks()
    {
        var ask = new CreateAsk("ledger", "keeps the books") { Workspace = "aurora", Line = "trunk", Work = "Build the first screen." };

        Assert.Equal(
            ["ledger", "--workspace", "aurora", "--line", "trunk", "--purpose", "keeps the books", "--work", "Build the first screen."],
            RepositoryCreateCommand.ArgsOf(ask));
    }

    [Fact]
    public void Only_what_differs_from_the_defaults_is_typed()
    {
        Assert.Equal(["notes", "--kind", "folder", "--purpose", "loose files"], RepositoryCreateCommand.ArgsOf(new CreateAsk("notes", "loose files") { Kind = PlaceKind.Folder }));
        Assert.Equal(["ledger", "--purpose", "why"], RepositoryCreateCommand.ArgsOf(new CreateAsk("ledger", "why")));
    }

    /// <summary>What a card prints, a terminal reads back as the same ask.</summary>
    [Fact]
    public void What_is_typed_is_read_back_as_the_same_ask()
    {
        var ask = new CreateAsk("ledger", "keeps the books") { Kind = PlaceKind.Folder, Workspace = "aurora", Under = Folder, Line = "trunk" };

        var (words, problem) = RepositoryCreateCommand.Parse(RepositoryCreateCommand.ArgsOf(ask), Folder);

        Assert.Null(problem);
        Assert.Equal(ask, words!.Ask);
        Assert.False(words.Plan);
    }
}
