using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris ask</c>'s words read before anything is sent: <c>--help</c> asks for the usage alone, an unknown flag is a problem and never
/// the ask's words, and each refusal spells its verb as the usage does.
/// </summary>
public sealed class AskArgumentsTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Help_anywhere_asks_for_the_usage_and_nothing_else(string help)
    {
        Assert.True(AskArguments.Read(["fix", "the", "stall", help], out var problem)!.Help);
        Assert.Null(problem);
        // `ask publish --help`, which the command tree hands on as `--publish --help`.
        Assert.True(AskArguments.Read(["--publish", help], out _)!.Help);
    }

    [Fact]
    public void An_unknown_flag_is_a_problem_never_the_asks_words()
    {
        Assert.Null(AskArguments.Read(["--frobnicate", "fix it"], out var problem));
        Assert.Contains("`--frobnicate`", problem);
    }

    [Fact]
    public void A_flag_where_a_value_belongs_is_a_problem()
    {
        Assert.Null(AskArguments.Read(["--to", "--workspace", "games", "fix it"], out var problem));
        Assert.Contains("--to needs a value", problem);
    }

    [Fact]
    public void A_publish_without_its_receiver_is_refused_as_the_usage_spells_it()
    {
        Assert.Null(AskArguments.Read(["--publish", "a1"], out var problem));
        Assert.Contains("`ask publish <id>` needs `--to <repo>`", problem);
        Assert.Contains(AskArguments.Usage, line => line.Contains("ask publish <id> --to <repo>", StringComparison.Ordinal));
    }

    [Fact]
    public void A_new_ask_reads_its_words_and_what_it_carries()
    {
        var read = AskArguments.Read(
            ["--workspace", "games", "--to", "engine", "--short", "Stall", "--review", "on", "--url", "https://example.test/1", "- fix", "the stall"],
            out var problem)!;

        Assert.Null(problem);
        Assert.Equal(("games", "engine", "Stall", "on"), (read.Workspace, read.To, read.Short, read.Review));
        Assert.Equal(["https://example.test/1"], read.Links);
        Assert.Equal("- fix the stall", read.Sentence);
    }
}
