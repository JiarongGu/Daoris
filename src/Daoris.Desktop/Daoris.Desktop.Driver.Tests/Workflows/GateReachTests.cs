using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A check with <c>reach: paths</c> runs the gates its batch's changed paths reach, for any repository, by the
/// <c>paths</c> each gate carries in the accepted declaration, plus every gate still owed; the others are named as kept.
/// </summary>
public sealed class GateReachTests : IDisposable
{
    private const string Declaration = """
        {"gates":[
          {"name":"web","run":"npm test","paths":["src/web/","package.json"]},
          {"name":"driver","run":"dotnet test","paths":["src/driver/"]},
          {"name":"docs","run":"node docs.mjs","paths":["docs/"]},
          {"name":"universal","run":"node scan.mjs"}
        ]}
        """;

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "gate-reach", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private static IReadOnlyList<DeclaredGate> Declared => DeclaredGates.Read(Declaration);

    [Fact]
    public void A_gate_reads_the_paths_the_declaration_gives_it_and_one_with_none_is_reached_by_every_change()
    {
        Assert.Equal(["src/web/", "package.json"], Declared[0].Paths);
        Assert.Null(Declared[3].Paths);
        Assert.True(GateReach.Reaches(Declared[3], ["anything/at/all.txt"]));
        Assert.True(GateReach.Reaches(Declared[0], ["src/web/App.tsx"]));
        Assert.True(GateReach.Reaches(Declared[0], ["package.json"]));
        Assert.False(GateReach.Reaches(Declared[0], ["src/webhook/x.cs", "package.json.bak"]));
    }

    [Fact]
    public void A_batch_whose_paths_reach_two_gates_runs_those_two_and_names_the_rest_as_kept()
    {
        var passed = Declared.Select(gate => gate.Name).ToList();

        var plan = GateReach.Of(Declared, ["src/web/App.tsx", "docs/README.md"], passed);

        Assert.Equal(["web", "docs", "universal"], plan.Run.Select(gate => gate.Name));
        Assert.Equal(["driver"], plan.Kept.Select(gate => gate.Name));
    }

    /// <summary>
    /// This repository's own declaration places its gates by paths, as its merge tool's reach table does: a batch that changes the page alone keeps
    /// the suites no page source reaches, one that changes the devkit alone runs the devkit's and keeps the page's, and the gates that read the
    /// whole tree (the CLI's verify, the universal checks, the maps) carry no paths, so every batch runs them.
    /// </summary>
    [Fact]
    public void This_repository_s_gates_declare_the_paths_that_reach_them()
    {
        var declared = DeclaredGates.Read(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "daoris.gates.json")));
        string[] Run(params string[] changed) =>
            [.. GateReach.Of(declared, changed, [.. declared.Select(gate => gate.Name)]).Run.Select(gate => gate.Name)];

        Assert.Equal(["universal", "cli", "code-map", "test-map"], declared.Where(gate => gate.Paths is null).Select(gate => gate.Name));
        var page = Run("src/Daoris.Web/src/workflow/said.ts");
        Assert.Contains("web-types", page);
        Assert.Contains("web-unit", page);
        Assert.DoesNotContain("devkit", page);
        Assert.DoesNotContain("driver-process", page);
        Assert.DoesNotContain("deployment", page);
        var devkit = Run("src/Daoris.Devkit/Daoris.Devkit.Core/GateDeclaration.cs");
        Assert.Contains("devkit", devkit);
        Assert.DoesNotContain("web-types", devkit);
        Assert.DoesNotContain("driver", devkit);
        Assert.Equal(declared.Select(gate => gate.Name), Run("daoris.gates.json"));
    }

    [Fact]
    public void A_gate_that_has_not_passed_is_owed_and_runs_whatever_the_batch_changed()
    {
        var plan = GateReach.Of(Declared, ["docs/README.md"], ["web", "universal"]);

        Assert.Equal(["driver", "docs", "universal"], plan.Run.Select(gate => gate.Name));
        Assert.Equal(["web"], plan.Kept.Select(gate => gate.Name));
    }

    [Fact]
    public void The_gates_a_line_has_passed_are_read_from_its_runs_newest_evidence_first()
    {
        Directory.CreateDirectory(_home);
        var at = new DateTimeOffset(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);
        VerifyRun Bound(string id, int minutes) => new(id, "engine", "default", "checkout", "verify", 1, "d", new string('a', 40), WorkflowLevels.RepositoryLanded, at.AddMinutes(minutes))
        {
            Line = "main",
        };

        var older = Bound("v-0000000000000001", 0);
        var newer = Bound("v-0000000000000002", 10);
        var now = Bound("v-0000000000000003", 20);
        foreach (var run in new[] { older, newer, now }) VerifyRuns.Bind(_home, run);
        Write(older, "attempt-1", new VerifyGate("web", "pass"), new VerifyGate("driver", "pass"), new VerifyGate("docs", "pass"));
        // The newer run failed `web` and never reached `driver`; it kept `docs`, which says nothing of it.
        Write(newer, "attempt-1", new VerifyGate("web", "fail"), new VerifyGate("driver", "not-run"), new VerifyGate("docs", VerifyGate.Kept));

        var passed = GatesPassed.Of(_home, now);

        Assert.Equal(["docs", "driver"], passed.Order(StringComparer.Ordinal));
        Assert.DoesNotContain("web", passed);
    }

    private void Write(VerifyRun run, string invocation, params VerifyGate[] gates) =>
        new VerifyResult(invocation, run.Commit, VerifyVerdicts.Reached, null, gates).Write(VerifyFiles.ResultOf(_home, run.Run, invocation));
}
