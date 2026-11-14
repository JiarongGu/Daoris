using Daoris.Driver;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The gates a repository requires before its work is handed back: declared on the gate, told in every brief, and met by a verdict at the very commit.</summary>
public sealed class HandbackGatesTests : IDisposable
{
    private readonly HeavyFixture _fixture = new("handback-gates");
    private static readonly DateTimeOffset At = DateTimeOffset.UnixEpoch;
    private const string Declaration = """
        {"gates":[
          {"name":"reached","run":"node tools/reached-checks.mjs --tier fast","handback":"node tools/reached-checks.mjs --base main --tier fast"},
          {"name":"cli","run":"npm run verify:checks","handback":true},
          {"name":"whole","run":"dotnet test","heavy":true}
        ]}
        """;

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void This_repository_requires_the_reached_set_against_main_and_the_static_checks()
    {
        var required = HandbackGates.OfFolder(WorkspaceRoot.Folder);

        Assert.Equal(["node tools/reached-checks.mjs --base main --tier fast", "npm run verify:checks"], required.Select(gate => gate.Run).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(SessionChecks.Light)]
    [InlineData(SessionChecks.HeavyAtEnd)]
    public void Every_pipeline_tells_the_declared_commands_before_done_and_the_check_step_that_verifies_them(string? pipeline)
    {
        File.WriteAllText(Path.Combine(_fixture.Checkout, DeclarationNames.Gates), Declaration);
        var words = HeavyTold.Hand(_fixture.Checkout, null, "engine", pipeline, takesRules: true).Words;

        Assert.Contains("node tools/reached-checks.mjs --base main --tier fast", words);
        Assert.Contains("npm run verify:checks", words);
        Assert.DoesNotContain("dotnet test", words);
        Assert.Contains("Before done or ready", words);
        Assert.Contains("check step verifies the same commands at your tip while you are open", words);
    }

    [Fact]
    public void A_repository_requiring_nothing_tells_nothing()
    {
        File.WriteAllText(Path.Combine(_fixture.Checkout, DeclarationNames.Gates), """{"gates":[{"name":"a","run":"x"}]}""");

        Assert.Null(HandbackGates.Told(_fixture.Checkout));
    }

    [Fact]
    public void A_verdict_meets_a_gate_by_its_whole_command_or_by_its_name_only_where_its_own_run_is_the_requirement()
    {
        var gates = DeclaredGates.Read(Declaration);
        var reached = gates[0];
        var cli = gates[1];
        CheckAsk Ask(string? gate, params string[]? command) => new("a", "s", gate, command is { Length: > 0 } ? command : null, At);

        Assert.True(HandbackGates.Matches(reached, Ask(null, "node", "tools/reached-checks.mjs", "--base", "main", "--tier", "fast")));
        Assert.False(HandbackGates.Matches(reached, Ask(null, "node", "tools/reached-checks.mjs", "--base", "main", "--tier", "fast", "--classes", "Chosen")));
        Assert.False(HandbackGates.Matches(reached, Ask("reached")));
        Assert.True(HandbackGates.Matches(cli, Ask("cli")));
        Assert.True(HandbackGates.Matches(cli, Ask(null, "npm", "run", "verify:checks")));
        Assert.False(HandbackGates.Matches(cli, Ask("whole")));
    }
}
