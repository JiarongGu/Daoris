namespace Daoris.Devkit.Tests;

/// <summary>The doctrine gate passes on the CLI's exit code, and says what the CLI said when it could not run.</summary>
public sealed class DoctrineGateTests : IDisposable
{
    private readonly Fixture _fx = new("doctrine-gate");

    public void Dispose() => _fx.Dispose();

    private GateResult RunWith(int exit, string said)
    {
        _fx.Write("daoris.json", "{}");
        var declaration = new GateDeclaration
        {
            Doctrine = new DoctrineOptions("node", ["-e", $"console.log('{said}'); process.exit({exit})"]),
        };
        return new DoctrineGate().Run(new GateContext(_fx.Path, declaration));
    }

    /// <summary>
    /// The CLI says every message, a tool error's included, on stdout: a failure that named only stderr
    /// reported "could not run" with nothing after it.
    /// </summary>
    [Fact]
    public void A_tool_error_names_what_the_cli_said_on_its_stdout()
    {
        var result = RunWith(2, "daoris: daoris.lock is not valid JSON");

        Assert.False(result.Passed);
        Assert.Contains("daoris.lock is not valid JSON", result.Detail);
    }

    [Fact]
    public void A_policy_failure_carries_the_drift_the_cli_listed()
    {
        var result = RunWith(1, "drifted rules/x.md");

        Assert.False(result.Passed);
        Assert.Contains("drifted rules/x.md", result.Detail);
    }
}
