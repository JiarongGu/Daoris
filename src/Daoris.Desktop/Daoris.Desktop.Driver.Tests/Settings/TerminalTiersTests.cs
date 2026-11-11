using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>terminal</c>, the tier of a person's press from a terminal no Daoris session owns, at its file: <c>ask</c> unless the file names
/// <c>act</c>, never widened by a value it does not know; written only while <c>act</c>; any other tier refused. The verb's words are
/// <c>fixtures/driver-choices-text.json</c>'s rows.
/// </summary>
public sealed class TerminalTiersTests
{
    [Theory]
    [InlineData("""{ "terminal": "act" }""", TerminalTiers.Act)]
    [InlineData("""{ "terminal": "ask" }""", TerminalTiers.Ask)]
    [InlineData("""{ "terminal": "ACT" }""", TerminalTiers.Ask)]
    [InlineData("""{ "terminal": "always" }""", TerminalTiers.Ask)]
    [InlineData("""{ "terminal": true }""", TerminalTiers.Ask)]
    [InlineData("{}", TerminalTiers.Ask)]
    public void The_file_is_ask_unless_it_names_act(string json, string tier)
    {
        Assert.Equal(tier, DriverConfig.Parse(json).Terminal);
    }

    [Fact]
    public void It_is_written_only_while_act_and_any_other_tier_is_refused()
    {
        Assert.Equal(TerminalTiers.Ask, DriverConfig.Empty.Terminal);
        Assert.DoesNotContain($"\"{TerminalTiers.File}\"", DriverConfig.Empty.ToJson());
        var act = DriverConfig.Empty.WithTerminal(TerminalTiers.Act);
        Assert.Contains("\"terminal\": \"act\"", act.ToJson());
        Assert.Equal(TerminalTiers.Act, DriverConfig.Parse(act.ToJson()).Terminal);
        Assert.DoesNotContain($"\"{TerminalTiers.File}\"", act.WithTerminal(TerminalTiers.Ask).ToJson());

        Assert.Equal(TerminalTiers.Refusal, Assert.Throws<DriverException>(() => DriverConfig.Empty.WithTerminal("always")).Message);
    }
}
