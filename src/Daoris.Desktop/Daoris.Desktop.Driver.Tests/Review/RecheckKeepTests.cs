using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A check asked again after a fix commit keeps each gate the run's attempt before passed at the same base and declaration, where no path
/// changed between the tree that attempt judged and the one judged now reaches it; the rest run. A gate's newest word is what counts: a failure
/// keeps nothing, and a gate kept, not run or ended says nothing, so the word before it is read (as <see cref="GatesPassed"/> reads a line).
/// </summary>
public sealed class RecheckKeepTests : IDisposable
{
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "recheck-keep", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_folder);

    private const string Onto = "1111111111111111111111111111111111111111";
    private const string Declaration = "dddddddddddddddddddddddddddddddddddddddd";

    private static readonly DeclaredGate Lint = new("lint", "node lint.mjs", null, false);
    private static readonly DeclaredGate Web = new("web-types", "npm run typecheck:web", null, false) { Paths = ["src/web/"] };
    private static readonly DeclaredGate Driver = new("censuses", "dotnet test", null, false) { Paths = ["src/driver/"] };

    /// <summary>A gate with no paths is reached by every change; one with paths by a changed path under them.</summary>
    private static bool Reaches(IReadOnlyCollection<string> paths, DeclaredGate gate) => GateReach.Reaches(gate, paths);

    private static VerifyResult Attempt(string invocation, string judged, params (string Gate, string Verdict)[] gates) =>
        new(invocation, judged, VerifyVerdicts.Failed, null, [.. gates.Select(each => new VerifyGate(each.Gate, each.Verdict))])
        {
            Judged = judged, Onto = Onto, Declaration = Declaration,
        };

    private static async Task<IReadOnlyList<RecheckKeep.Kept>> KeptAsync(IReadOnlyList<VerifyResult> earlier, IReadOnlyCollection<string>? changed, string onto = Onto)
    {
        var asked = new List<string>();
        var kept = await RecheckKeep.OfAsync(earlier, onto, Declaration, [Lint, Web, Driver], from =>
        {
            asked.Add(from);
            return Task.FromResult(changed);
        }, Reaches);
        Assert.True(asked.Distinct().Count() == asked.Count, "each earlier tree is compared once");
        return kept;
    }

    [Fact]
    public async Task A_fix_touching_one_lane_keeps_the_gates_it_does_not_reach_and_names_the_tree_each_passed_at()
    {
        var earlier = new[] { Attempt("i-a", "aaaa", ("lint", RunnerVerdicts.Pass), ("web-types", RunnerVerdicts.Pass), ("censuses", RunnerVerdicts.Fail)) };

        var kept = await KeptAsync(earlier, ["src/driver/Fix.cs"]);

        Assert.Equal([new RecheckKeep.Kept("web-types", "aaaa")], kept);
    }

    [Fact]
    public async Task A_check_of_the_same_tree_again_keeps_every_gate_that_passed_even_one_every_change_reaches()
    {
        var earlier = new[] { Attempt("i-a", "aaaa", ("lint", RunnerVerdicts.Pass), ("web-types", RunnerVerdicts.Pass), ("censuses", RunnerVerdicts.Pass)) };

        var kept = await KeptAsync(earlier, []);

        Assert.Equal(["lint", "web-types", "censuses"], kept.Select(each => each.Gate));
    }

    [Fact]
    public async Task A_gate_kept_or_not_run_in_the_newest_attempt_is_kept_from_the_pass_before_it_and_a_newest_failure_keeps_nothing()
    {
        var earlier = new[]
        {
            Attempt("i-c", "cccc", ("lint", RunnerVerdicts.Pass), ("web-types", VerifyGate.Kept), ("censuses", RunnerVerdicts.NotRun)),
            Attempt("i-b", "bbbb", ("lint", RunnerVerdicts.Pass), ("web-types", RunnerVerdicts.Pass), ("censuses", RunnerVerdicts.Fail)),
            Attempt("i-a", "aaaa", ("censuses", RunnerVerdicts.Pass)),
        };

        var kept = await KeptAsync(earlier, ["docs/a.md"]);

        Assert.Equal([new RecheckKeep.Kept("web-types", "bbbb")], kept);
    }

    [Fact]
    public async Task Nothing_is_kept_from_another_base_another_declaration_an_attempt_that_never_said_its_base_or_where_git_cannot_compare()
    {
        var passed = Attempt("i-a", "aaaa", ("web-types", RunnerVerdicts.Pass));

        Assert.Empty(await KeptAsync([passed], ["docs/a.md"], onto: "2222222222222222222222222222222222222222"));
        Assert.Empty(await KeptAsync([passed with { Declaration = "eeee" }], ["docs/a.md"]));
        Assert.Empty(await KeptAsync([passed with { Onto = null }], ["docs/a.md"]));
        Assert.Empty(await KeptAsync([passed with { Judged = null }], ["docs/a.md"]));
        Assert.Empty(await KeptAsync([passed], null));
    }

    [Fact]
    public void The_words_name_each_kept_gate_and_the_earlier_tree_it_stands_on()
    {
        var words = RecheckKeep.Words([new("web-types", "aaaa111"), new("censuses", "aaaa111"), new("lint", "bbbb222")], Onto, "main");

        Assert.Equal(
            "Kept from this run's checks before, on the same base (`main` at 1111111), as no path changed since reaches them: web-types, censuses (passed at aaaa111); lint (passed at bbbb222).",
            words);
        Assert.Null(RecheckKeep.Words([], Onto, "main"));
    }

    [Fact]
    public void A_result_keeps_the_tree_it_judged_and_its_base_and_reads_them_back_and_an_older_result_reads_with_neither()
    {
        var result = new VerifyResult("i-a", "work", VerifyVerdicts.Reached, null, [new VerifyGate("lint", RunnerVerdicts.Pass)]) { Judged = "merged", Onto = Onto };

        var read = VerifyResult.Read(Write(result));

        Assert.Equal(("merged", Onto), (read!.Judged, read.Onto));
        var older = Write(result with { Judged = null, Onto = null });
        Assert.Equal((null, null), (VerifyResult.Read(older)!.Judged, VerifyResult.Read(older)!.Onto));
    }

    [Fact]
    public void An_attempts_progress_carries_its_passed_gates_only_for_the_same_work_base_and_declaration()
    {
        GateProgress.Write(_folder, "work", Onto, Declaration,
            [new VerifyGate("lint", RunnerVerdicts.Pass, 0) { WallSeconds = 12.5, Log = "gate-01-lint.log" }, new VerifyGate("web-types", RunnerVerdicts.Fail, 2)]);

        var carried = GateProgress.Read(_folder, "work", Onto, Declaration);

        Assert.Equal(["lint"], carried.Keys);
        Assert.Equal((RunnerVerdicts.Pass, 12.5, "gate-01-lint.log"), (carried["lint"].Verdict, carried["lint"].WallSeconds, carried["lint"].Log));
        Assert.Empty(GateProgress.Read(_folder, "other work", Onto, Declaration));
        Assert.Empty(GateProgress.Read(_folder, "work", "2222222222222222222222222222222222222222", Declaration));
        Assert.Empty(GateProgress.Read(_folder, "work", Onto, "eeee"));
        File.WriteAllText(Path.Combine(_folder, GateProgress.File), "{ not json");
        Assert.Empty(GateProgress.Read(_folder, "work", Onto, Declaration));
    }

    private string Write(VerifyResult result)
    {
        var path = Path.Combine(_folder, VerifyFiles.Result);
        result.Write(path);
        return path;
    }
}
