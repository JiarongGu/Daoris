using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A check's <c>gates</c> is the heavy gates, the names it lists, or the word <c>reached</c>: the fast gates its change reaches, after the hand-back reading.</summary>
public sealed class CheckReachedOptionTests
{
    private static NodeDeclaration Check(string? gates) =>
        new("check", NodeKinds.Check, 1, NodeReach.Performed)
        {
            Options = gates is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["gates"] = gates },
        };

    [Theory]
    [InlineData("\"reached\"", true)]
    [InlineData("\"heavy\"", false)]
    [InlineData("\"web-types\"", false)]
    [InlineData("[\"reached\"]", false)]
    [InlineData("[\"web-types\",\"comments\"]", false)]
    [InlineData(null, false)]
    public void Only_the_word_reached_alone_is_the_reached_choice_and_a_gate_so_named_is_asked_in_a_list(string? gates, bool reached)
    {
        Assert.Equal(reached, CheckNode.ReachedChoice(Check(gates)));
    }

    [Fact]
    public void The_reached_choice_names_no_gate_to_run_and_a_list_with_the_word_still_names_the_gate()
    {
        Assert.Null(CheckNode.GatesOf(Check("\"reached\"")));
        Assert.Equal(["reached"], CheckNode.GatesOf(Check("[\"reached\"]")));
    }
}
