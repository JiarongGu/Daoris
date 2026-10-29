using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1f part f2: the verdict port's wiring, and the caller of a repository's declared runner (the integration design §5.5, §5.9;
/// D192's runner contract): the environment it sets, on the runner's process alone, through the gate environment's composition; and how
/// the exit and the answer file are read. Files and pure functions, no process.
/// </summary>
public sealed class IntegrationRunnerCallTests
{
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";
    private const string Base = "89abcdef0123456789abcdef0123456789abcdef";
    private const string Blob = "fedcba9876543210fedcba9876543210fedcba98";

    private static readonly KeyValuePair<string, string>[] Installed =
    [
        new("PATH", "X:/daoris/bin"), new("SystemRoot", "X:/windows"), new("DAORIS_HOME", "X:/daoris/home"), new("DAORIS_ROLE_KEY", "secret"),
        new("daoris_session_id", "s-1"), new("GIT_DIR", "X:/daoris/other/.git"), new("MERGE_BRANCH_FULL", "1"), new("NODE_OPTIONS", "--trace"),
        new("KEEP_ME", "kept"), new("DAORIS_VERIFY_COMMIT", "an earlier call's"),
    ];

    private static RunnerCallAsk Ask(string mode, string commit = Tip) =>
        new(mode, commit, Base, "X:/daoris/scratch/declaration.json", Blob, "X:/daoris/tree", ["unit", "lint"]);

    private static Dictionary<string, string> Environment(RunnerCallAsk ask, IEnumerable<string>? pass = null, string? hold = null) =>
        RunnerCaller.EnvironmentOf(Installed, pass, hold, ask, "X:/daoris/scratch/answer.json").ToDictionary(each => each.Key, each => each.Value, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The runner is handed its own inputs and the gate environment composed from the install's: no Daoris name of the driver's, no git
    /// locating variable, no switch of the tool's, the build servers of the gate's own, and nothing of this process's environment but what
    /// the composition keeps.
    /// </summary>
    [Fact]
    public void The_runner_s_environment_is_the_composition_and_its_own_inputs_and_no_other_daoris_name()
    {
        var environment = Environment(Ask("run") with { Gates = ["unit"] });

        Assert.Equal(
            [
                "DAORIS_VERIFY_BASE", "DAORIS_VERIFY_COMMIT", "DAORIS_VERIFY_DECLARATION", "DAORIS_VERIFY_GATES", "DAORIS_VERIFY_MODE", "DAORIS_VERIFY_RESULT",
                "DAORIS_VERIFY_TREE",
            ],
            environment.Keys.Where(name => name.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal));
        Assert.Equal("X:/daoris/bin", environment["PATH"]);
        Assert.Equal("kept", environment["KEEP_ME"]);
        Assert.False(environment.ContainsKey("GIT_DIR"));
        Assert.False(environment.ContainsKey("MERGE_BRANCH_FULL"));
        Assert.Equal("1", environment["MSBUILDDISABLENODEREUSE"]);
        Assert.Equal(Tip, environment["DAORIS_VERIFY_COMMIT"]);
        Assert.Equal("run", environment["DAORIS_VERIFY_MODE"]);
        Assert.Equal(Base, environment["DAORIS_VERIFY_BASE"]);
        Assert.Equal("X:/daoris/scratch/declaration.json", environment["DAORIS_VERIFY_DECLARATION"]);
        Assert.Equal("X:/daoris/scratch/answer.json", environment["DAORIS_VERIFY_RESULT"]);
        Assert.Equal("X:/daoris/tree", environment["DAORIS_VERIFY_TREE"]);
        Assert.Equal("unit", environment["DAORIS_VERIFY_GATES"]);
    }

    /// <summary>The commit is the full lower-case id however the caller spelled it; the gates are a comma list for run and plan only, the failure's file for probe only.</summary>
    [Theory]
    [InlineData("run", true, false)]
    [InlineData("plan", true, false)]
    [InlineData("probe", false, true)]
    [InlineData("commits", false, false)]
    public void Each_mode_is_handed_only_the_inputs_it_takes(string mode, bool gates, bool failure)
    {
        var ask = Ask(mode, Tip.ToUpperInvariant()) with { Gates = ["unit", "lint"], FailureFile = "X:/daoris/scratch/failure.json" };

        var environment = Environment(ask);

        Assert.Equal(Tip, environment["DAORIS_VERIFY_COMMIT"]);
        Assert.Equal(gates, environment.ContainsKey("DAORIS_VERIFY_GATES"));
        if (gates) Assert.Equal("unit,lint", environment["DAORIS_VERIFY_GATES"]);
        Assert.Equal(failure, environment.ContainsKey("DAORIS_VERIFY_FAILURE"));
        if (failure) Assert.Equal("X:/daoris/scratch/failure.json", environment["DAORIS_VERIFY_FAILURE"]);
    }

    /// <summary>No gates named is the runner's own plan: the name is not set at all (an empty one is a refusal in the tool).</summary>
    [Fact]
    public void A_run_with_no_gates_named_sets_no_gates()
    {
        Assert.False(Environment(Ask("run")).ContainsKey("DAORIS_VERIFY_GATES"));
    }

    /// <summary>
    /// The admission's hold marker is the one Daoris name a gate gets on purpose, set where the call runs inside a hold; a name the declaration
    /// passes comes back, a Daoris one never does.
    /// </summary>
    [Fact]
    public void The_hold_marker_is_set_inside_a_hold_and_a_passed_name_comes_back_but_never_a_daoris_one()
    {
        var environment = Environment(Ask("probe"), pass: ["NODE_OPTIONS", "DAORIS_HOME", "NOT_THERE"], hold: "9-ab-01");

        Assert.Equal("9-ab-01", environment["DAORIS_ADMISSION_HOLD"]);
        Assert.Equal("--trace", environment["NODE_OPTIONS"]);
        Assert.False(environment.ContainsKey("DAORIS_HOME"));
        Assert.False(environment.ContainsKey("DAORIS_ROLE_KEY"));
        Assert.False(Environment(Ask("probe")).ContainsKey("DAORIS_ADMISSION_HOLD"));
    }

    private static RunnerAsk Asked(string mode, string commit = Tip) => new(commit, ["unit", "lint"]) { Mode = mode, Base = Base, Declaration = Blob, Gates = null };

    private static string Plan(string commit = Tip, string blob = Blob) =>
        $$"""{ "schema": 2, "mode": "plan", "commit": "{{commit}}", "base": "{{Base}}", "declaration": "{{blob}}", "run": [ { "name": "unit", "why": "x" } ], "kept": [ { "name": "lint", "why": "kept", "tree": "t", "standing": "exact" } ], "deferred": [] }""";

    /// <summary>Exit 75 is busy whatever was written; so is an answer that says it (D192).</summary>
    [Fact]
    public void Exit_75_and_busy_true_are_busy()
    {
        Assert.Equal(RunnerKind.Busy, RunnerCaller.Interpret(75, null, Asked("plan"), "X:/daoris/none.log").Kind);
        Assert.Equal(RunnerKind.Busy, RunnerCaller.Interpret(
            0, $$"""{ "schema": 2, "mode": "plan", "commit": "{{Tip}}", "base": "{{Base}}", "declaration": "{{Blob}}", "busy": true }""", Asked("plan"), "X:/daoris/none.log").Kind);
    }

    /// <summary>Exit 2 with no answer is a refusal, said in the log's last words; with the answer file there it is the answer that is read.</summary>
    [Fact]
    public void Exit_2_with_no_answer_is_a_refusal_in_the_log_s_words_and_never_a_pass()
    {
        var folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "runner-call", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        try
        {
            var log = Path.Combine(folder, "call.log");
            File.WriteAllText(log, "merge-branch: DAORIS_VERIFY_TREE stands at 0123456, not at DAORIS_VERIFY_COMMIT 89abcde\n\n");

            var read = RunnerCaller.Interpret(2, null, Asked("plan"), log);

            Assert.Equal(RunnerKind.Unknown, read.Kind);
            Assert.Equal("the runner refused (exit 2) and wrote no answer: merge-branch: DAORIS_VERIFY_TREE stands at 0123456, not at DAORIS_VERIFY_COMMIT 89abcde", read.Why);
            Assert.Equal(RunnerKind.Plan, RunnerCaller.Interpret(0, Plan(), Asked("plan"), log).Kind);
        }
        finally
        {
            GitSeed.Discard(folder);
        }
    }

    /// <summary>A missing answer, one for another commit, and one under another blob are unknown, never a pass.</summary>
    [Theory]
    [InlineData(1, null, "the runner wrote no answer")]
    [InlineData(0, "other-commit", "the runner's answer is for commit")]
    [InlineData(0, "other-blob", "the runner's answer is for the declaration")]
    public void An_answer_that_is_missing_or_for_another_commit_or_blob_is_unknown(int exit, string? which, string said)
    {
        var text = which switch
        {
            null => null,
            "other-commit" => Plan(commit: Base),
            _ => Plan(blob: Tip),
        };

        var read = RunnerCaller.Interpret(exit, text, Asked("plan"), "X:/daoris/none.log");

        Assert.Equal(RunnerKind.Unknown, read.Kind);
        Assert.Contains(said, read.Why, StringComparison.Ordinal);
    }

    /// <summary>The gates come from the accepted blob's text; a text that does not read, or a gate without a command, declares nothing.</summary>
    [Fact]
    public void The_declared_gates_are_read_from_the_blob_s_text()
    {
        var gates = DeclaredGates.Read("""
            { "gates": [ { "name": "build", "run": "dotnet build", "heavy": true }, { "name": "unit", "run": "node unit.mjs", "cwd": "src" },
                         { "name": "build", "run": "again" }, { "name": "none" }, 7 ] }
            """);

        Assert.Equal([("build", "dotnet build", null, true), ("unit", "node unit.mjs", "src", false)], gates.Select(gate => (gate.Name, gate.Run, gate.Cwd, gate.Heavy)));
        Assert.Empty(DeclaredGates.Read("{ not json"));
        Assert.Empty(DeclaredGates.Read(null));
        Assert.Empty(DeclaredGates.Read("""{ "gates": "no" }"""));
    }

    /// <summary>Review installs the port when the driver loads, as it installs the workplace review's.</summary>
    [Fact]
    public void Review_installs_the_verdict_port_when_the_driver_loads()
    {
        Assert.IsType<IntegrationVerdictOperations>(IntegrationVerdicts.Current);
    }

    /// <summary>Until the sightings' store is built the memory remembers nothing: no failure is a known red, and a sighting is told to no one.</summary>
    [Fact]
    public void The_memory_is_unwired_and_remembers_nothing()
    {
        var signature = new FailureSignature { Test = "Unit.Fails", Exception = "Error", Message = "boom" };

        Assert.Null(IntegrationMemory.Current.KnownRedFor("engine", signature));
        IntegrationMemory.Current.Sighted("engine", Tip, new JudgedFailure(FailureNames.Test, "unit", "Unit.Fails", signature), null);
    }

    /// <summary>A step is handed the environment composed for it and nothing of this process's own.</summary>
    [Fact]
    public void A_step_started_with_its_own_environment_holds_no_name_of_this_process_s()
    {
        System.Environment.SetEnvironmentVariable("INTEGRATION_RUNNER_CALL_PROBE", "leaks");
        try
        {
            var block = HeavyStep.EnvironmentBlock(
                new Dictionary<string, string> { ["ADDED"] = "1" }, new Dictionary<string, string> { ["PATH"] = "X:/daoris/bin", ["z"] = "2" });

            Assert.Equal("ADDED=1\0PATH=X:/daoris/bin\0z=2\0\0", block);
            Assert.Contains("INTEGRATION_RUNNER_CALL_PROBE=leaks", HeavyStep.EnvironmentBlock(new Dictionary<string, string>()), StringComparison.Ordinal);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("INTEGRATION_RUNNER_CALL_PROBE", null);
        }
    }
}
