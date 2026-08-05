using Daoris.Devkit;

namespace Daoris.Devkit.Tests;

public sealed class GateRunnerTests : IDisposable
{
    private readonly Fixture _fx = new("runner");

    public void Dispose() => _fx.Dispose();

    private sealed class StubGate(string name, GateResult result) : IGate
    {
        public string Name => name;

        public bool Ran { get; private set; }

        public GateResult Run(GateContext context)
        {
            Ran = true;
            return result;
        }
    }

    private sealed class ThrowingGate(string name) : IGate
    {
        public string Name => name;

        public GateResult Run(GateContext context) => throw new InvalidOperationException("boom");
    }

    private (RunReport Report, List<string> Log) Run(GateDeclaration declaration, params IGate[] gates)
    {
        var log = new List<string>();
        var report = new GateRunner(new GateContext(_fx.Path, declaration), gates).Run(log.Add);
        return (report, log);
    }

    /// <summary>
    /// Stopping early is the point. Running a twelve-minute build before discovering a staged secret
    /// wastes the twelve minutes and teaches people to start it and walk away.
    /// </summary>
    [Fact]
    public void A_failing_gate_stops_the_run_and_the_later_gates_never_execute()
    {
        var later = new StubGate("later", GateResult.Pass("later"));

        var (report, _) = Run(
            new GateDeclaration(),
            new StubGate("first", GateResult.Fail("first", "no")),
            later);

        Assert.False(report.Passed);
        Assert.False(later.Ran);
        Assert.Single(report.Results);
    }

    /// <summary>
    /// "8 passed" when three had nothing to check reads as coverage and is not. The distinction has to
    /// survive into the report, or it may as well not exist.
    /// </summary>
    [Fact]
    public void A_skipped_gate_is_reported_as_skipped_rather_than_as_a_pass()
    {
        var (report, log) = Run(new GateDeclaration(), new StubGate("v", GateResult.Skip("v", "nothing declared")));

        Assert.True(report.Passed);
        Assert.True(report.Results.Single().Skipped);
        Assert.Contains(log, line => line.Contains("skipped"));
    }

    /// <summary>
    /// Off because someone wrote it down is a decision; off because nobody wired it up is an accident,
    /// and from the outside they look identical unless the run says so every time.
    /// </summary>
    [Fact]
    public void A_disabled_gate_does_not_run_and_the_run_says_so()
    {
        var gate = new StubGate("sensitive", GateResult.Fail("sensitive", "would have failed"));
        var declaration = new GateDeclaration
        {
            Disabled = new HashSet<string>(["sensitive"], StringComparer.OrdinalIgnoreCase),
        };

        var (report, log) = Run(declaration, gate);

        Assert.True(report.Passed);
        Assert.False(gate.Ran);
        Assert.Contains(log, line => line.Contains("disabled") && line.Contains("sensitive"));
    }

    /// <summary>
    /// One gate blowing up must not discard the results already collected — those are the useful part,
    /// and an unhandled exception trades them for a stack trace.
    /// </summary>
    [Fact]
    public void A_gate_that_throws_becomes_a_failed_gate_rather_than_a_crash()
    {
        var (report, _) = Run(
            new GateDeclaration(),
            new StubGate("first", GateResult.Pass("first")),
            new ThrowingGate("second"));

        Assert.False(report.Passed);
        Assert.Equal(2, report.Results.Count);
        Assert.Contains("boom", report.Results[1].Detail);
    }

    [Fact]
    public void Declared_gates_run_after_the_universal_ones()
    {
        var declaration = new GateDeclaration
        {
            Gates = [new DeclaredGate("say", OperatingSystem.IsWindows() ? "echo hi" : "echo hi")],
        };

        var (report, _) = Run(declaration, new StubGate("universal", GateResult.Pass("universal")));

        Assert.True(report.Passed);
        Assert.Equal(["universal", "say"], report.Results.Select(r => r.Name));
    }

    [Fact]
    public void A_declared_gate_that_exits_nonzero_fails_the_run()
    {
        var declaration = new GateDeclaration { Gates = [new DeclaredGate("nope", "exit 3")] };

        var (report, _) = Run(declaration);

        Assert.False(report.Passed);
        Assert.Contains("exited 3", report.Results.Single().Detail);
    }
}
