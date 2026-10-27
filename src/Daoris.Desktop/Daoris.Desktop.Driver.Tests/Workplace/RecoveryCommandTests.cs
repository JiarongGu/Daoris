using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The words of <c>daoris-driver work recovery drop</c> (RECOVERYDROP1): what it takes, and what it refuses in a sentence.</summary>
public sealed class RecoveryCommandTests
{
    private const string Ref = "refs/daoris/discarded/daoris/s-1";

    [Fact]
    public void Refs_with_their_repository_are_one_ask()
    {
        var ask = RecoveryCommand.Read([Ref, "--repository", "engine", Ref], out var problem);

        Assert.Equal("", problem);
        Assert.Equal([Ref], ask!.Refs);
        Assert.Equal((false, "engine", false), (ask.Unheld, ask.Repository, ask.Plan));
    }

    [Fact]
    public void Unheld_takes_a_repository_or_every_one_and_plan_only_lists()
    {
        var all = RecoveryCommand.Read(["--unheld"], out _)!;
        var one = RecoveryCommand.Read(["--unheld", "--repository", "engine", "--plan"], out _)!;

        Assert.True(all.Unheld);
        Assert.Null(all.Repository);
        Assert.False(all.Plan);
        Assert.Equal(("engine", true), (one.Repository, one.Plan));
    }

    [Theory]
    [InlineData(new string[] { }, "name the recovery refs to delete, or `--unheld` for every one nothing holds.")]
    [InlineData(new[] { "--unheld", Ref, "--repository", "engine" }, "name refs or `--unheld`, not both.")]
    [InlineData(new[] { Ref }, "a recovery ref is a repository's own: say which with `--repository <name>`.")]
    [InlineData(new[] { "--unheld", "--repository" }, "`--repository` takes a repository's name.")]
    [InlineData(new[] { "--unheld", "--yes" }, "`--yes` is not one of this verb's words.")]
    public void Words_it_does_not_understand_are_said_in_a_sentence(string[] args, string said)
    {
        Assert.Null(RecoveryCommand.Read(args, out var problem));
        Assert.Equal(said, problem);
        Assert.StartsWith("usage: daoris-driver work recovery drop", RecoveryCommand.Usage);
    }

    [Theory]
    [InlineData("work", "state", false)]
    [InlineData("work", "recovery", true)]
    public void The_verb_holds_the_homes_lease_only_where_it_deletes(string verb, string next, bool holds)
    {
        Assert.Equal(holds, VerbLeases.Holds([verb, next, "drop", "--unheld"]));
        Assert.False(VerbLeases.Holds([verb, next, "drop", "--unheld", "--plan"]));
    }
}
