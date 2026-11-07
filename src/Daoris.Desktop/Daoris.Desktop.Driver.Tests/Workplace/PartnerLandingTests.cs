using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1b2 (D199; the co-agent design §2.3 step 10, §2.4, §8.3's <c>PartnerLandingTests</c>), with real git: the lead's merge of the
/// frozen commit reads as taken in, recorded with the lead's commit; a cherry-pick of it is not taken in; a lead whose work completed
/// without it leaves the piece not taken in, its ref still standing through <c>gc</c>; and the person's <i>Discard…</i> removes the ref
/// at the commit it froze and the copy, and keeps a ref that has moved.
/// </summary>
/// <remarks>Real git, so the class is in the real-process half's git classes, as <see cref="PieceKeepProcessTests"/> is.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PartnerLandingTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-22T10:00:00Z");

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "partner-landing", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _home;

    public PartnerLandingTests()
    {
        _home = Path.Combine(_scratch, "home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose() => GitSeed.Discard(_scratch);

    private PartnerFiles Files => new(_home);

    /// <summary>The lead's repository and tree in one, on <c>main</c> with a piece's commit frozen at its ref, the piece handed.</summary>
    private async Task<(string Root, string Piece)> HandedAsync(string state = PartnerStates.Handed)
    {
        var seed = await GitSeed.SeedAsync("partner-landing", async folder =>
        {
            var root = Path.Combine(folder, "reports");
            Directory.CreateDirectory(Path.Combine(root, "src"));
            await GitAsync(root, "init", "--quiet", "-b", "main");
            await File.WriteAllTextAsync(Path.Combine(root, "src", "report.ts"), "export const days = 6;\n");
            await GitAsync(root, "add", "-A");
            await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--quiet", "-m", "first");
        });
        var root = Path.Combine(_scratch, "reports");
        GitSeed.Copy(Path.Combine(seed, "reports"), root);
        await GitAsync(root, "config", "user.name", "Owner");
        await GitAsync(root, "config", "user.email", "owner@example.test");
        var @base = (await GitAsync(root, "rev-parse", "HEAD")).Trim();

        await GitAsync(root, "checkout", "--quiet", "-b", "piece");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "report.ts"), "export const days = 7;\n");
        await GitAsync(root, "commit", "--quiet", "-am", "the piece");
        var piece = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "update-ref", PieceRefs.Of("p1"), piece);
        await GitAsync(root, "checkout", "--quiet", "main");
        await GitAsync(root, "branch", "-D", "piece");
        await File.WriteAllTextAsync(Path.Combine(root, "lead.txt"), "the lead's own work\n");
        await GitAsync(root, "add", "-A");
        await GitAsync(root, "commit", "--quiet", "-m", "lead");

        Files.WriteAsk(new PartnerAsk("p1", "s1", "Make the window seven days.", ["src"], "codex-acp", @base, [], PartnerAsk.ByPerson, At));
        Files.WriteState(new PartnerState("p1", state, null, At) { Agent = "codex-acp", Commit = piece, CopyRemoved = true });
        return (root, piece);
    }

    private Daoris.Driver.Driver Driver(string root, string leadState = "working") =>
        new(new OpinionStandIn().Session("s1", leadState, tree: root).Client(), DriverConfig.Empty with { Actors = StaffedRoles.On("claude-code-acp"), Cap = 3 },
            ProviderRegistry.Built(), _home)
        {
            PartnerTrees = (_, _) => Task.FromResult<string?>(root),
        };

    private async Task<List<string>> Look(Daoris.Driver.Driver driver)
    {
        var events = new List<string>();
        await driver.PiecesAsync(new Snapshot([], [], []) { Pieces = Files.Reservations() }, 0, events, CancellationToken.None);
        return events;
    }

    [Fact]
    public async Task The_lead_merging_the_frozen_commit_reads_as_taken_in_with_the_leads_commit_and_it_is_never_unset()
    {
        var (root, piece) = await HandedAsync();
        var driver = Driver(root);

        await Look(driver);
        Assert.Equal(PartnerStates.Handed, Files.ReadState("p1")!.State);

        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "take it in", PieceRefs.Of("p1"));
        var tip = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await Look(driver);

        var state = Files.ReadState("p1")!;
        Assert.Equal(PartnerStates.TakenIn, state.State);
        Assert.Equal(tip, state.TakenBy);
        Assert.Equal(piece, state.Commit);

        // A later rewrite of the lead's branch is the lead's own work: what was read stays.
        await GitAsync(root, "reset", "--quiet", "--hard", "HEAD~1");
        await Look(driver);
        Assert.Equal(PartnerStates.TakenIn, Files.ReadState("p1")!.State);
    }

    [Fact]
    public async Task A_cherry_pick_is_not_taking_it_in()
    {
        var (root, piece) = await HandedAsync();
        await GitAsync(root, "cherry-pick", piece);

        await Look(Driver(root));

        Assert.Equal(PartnerStates.Handed, Files.ReadState("p1")!.State);
    }

    [Fact]
    public async Task A_lead_that_completed_without_it_leaves_it_not_taken_in_and_its_ref_stands_through_gc()
    {
        var (root, piece) = await HandedAsync();

        await Look(Driver(root, leadState: "completed"));

        Assert.Equal(PartnerStates.NotTakenIn, Files.ReadState("p1")!.State);
        await GitAsync(root, "gc", "--quiet", "--prune=now");
        Assert.Equal(piece, (await GitAsync(root, "rev-parse", "--verify", PieceRefs.Of("p1"))).Trim());
    }

    /// <summary>
    /// A lead whose work landed and whose tree is gone: the look reads the repository's checkout from the registry, and the piece against
    /// the commit the lead's landing recorded.
    /// </summary>
    private Daoris.Driver.Driver Landed(string root, string tip)
    {
        new LandedBranches(_home).Record(new LandedBranch("reports", "default", "daoris/s1", "main", tip, "s1", "q1", "Work q1", At));
        var gone = Path.Combine(_scratch, "trees", "s1");
        return new(new OpinionStandIn().Session("s1", "completed", tree: gone).Registered("reports", root).Client(),
            DriverConfig.Empty with { Actors = StaffedRoles.On("claude-code-acp"), Cap = 3 }, ProviderRegistry.Built(), _home);
    }

    /// <summary>
    /// 🔴 The verdict at the landing, where the lead's tree is gone (the design §2.4): a piece the lead's landed commit does not hold is not
    /// taken in, its ref kept; one it holds is taken in, recorded with that commit. Read in the repository's own checkout, which the
    /// registry names, since the tree's checkout can no longer be asked.
    /// </summary>
    [Fact]
    public async Task At_the_landing_with_the_leads_tree_gone_the_piece_is_read_against_the_commit_that_landed()
    {
        var (root, piece) = await HandedAsync();
        var landedWithout = (await GitAsync(root, "rev-parse", "HEAD")).Trim();

        await Look(Landed(root, landedWithout));

        Assert.Equal(PartnerStates.NotTakenIn, Files.ReadState("p1")!.State);
        Assert.Equal(piece, (await GitAsync(root, "rev-parse", "--verify", PieceRefs.Of("p1"))).Trim());

        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "take it in", PieceRefs.Of("p1"));
        var landedWith = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await Look(Landed(root, landedWith));

        var state = Files.ReadState("p1")!;
        Assert.Equal((PartnerStates.TakenIn, landedWith), (state.State, state.TakenBy));
    }

    /// <summary>🔴 A piece not taken in at its lead's landing is discarded with the lead's tree gone: its ref is removed in the registry's checkout.</summary>
    [Fact]
    public async Task A_piece_not_taken_in_at_the_landing_is_discarded_with_the_leads_tree_gone()
    {
        var (root, _) = await HandedAsync(PartnerStates.NotTakenIn);
        Assert.True(PartnerPersonAct.Ask(_home, "p1", PartnerPersonActs.Discard, At).Acted);

        var said = await Look(Landed(root, (await GitAsync(root, "rev-parse", "HEAD")).Trim()));

        Assert.Equal(PartnerStates.Discarded, Files.ReadState("p1")!.State);
        Assert.Equal("", (await GitAsync(root, "for-each-ref", PieceRefs.Prefix)).Trim());
        Assert.Contains("is discarded", Assert.Single(said));
    }

    [Fact]
    public async Task Discard_removes_the_ref_at_the_commit_it_froze_and_the_copy_and_nothing_else()
    {
        var (root, _) = await HandedAsync();
        // Where the piece's copy is made: a copy anywhere else is no copy of a piece's, and is never removed.
        var copy = PartnerPass.CopyFor(_home, null, "reports", "p1");
        Directory.CreateDirectory(copy);
        File.WriteAllText(Path.Combine(copy, "left.txt"), "left");
        Files.WriteState(Files.ReadState("p1")! with { Copy = copy, CopyRemoved = false });

        Assert.True(PartnerPersonAct.Ask(_home, "p1", PartnerPersonActs.Discard, At).Acted);
        var said = await Look(Driver(root));

        Assert.Equal(PartnerStates.Discarded, Files.ReadState("p1")!.State);
        Assert.True(Files.ReadState("p1")!.CopyRemoved);
        Assert.False(Directory.Exists(copy));
        Assert.Equal("", (await GitAsync(root, "for-each-ref", PieceRefs.Prefix)).Trim());
        Assert.False(new PartnerActFiles(_home).Waits("p1"));
        Assert.Contains("is discarded", Assert.Single(said));
        Assert.Equal("main", (await GitAsync(root, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
    }

    [Fact]
    public async Task Discard_keeps_a_ref_that_is_no_longer_where_the_piece_froze_it()
    {
        var (root, _) = await HandedAsync();
        await GitAsync(root, "update-ref", PieceRefs.Of("p1"), "HEAD");
        Assert.True(PartnerPersonAct.Ask(_home, "p1", PartnerPersonActs.Discard, At).Acted);

        var said = await Look(Driver(root));

        Assert.Equal(PartnerStates.Handed, Files.ReadState("p1")!.State);
        Assert.Equal((await GitAsync(root, "rev-parse", "HEAD")).Trim(), (await GitAsync(root, "rev-parse", "--verify", PieceRefs.Of("p1"))).Trim());
        Assert.Contains("nothing was discarded", Assert.Single(said));
    }

    [Fact]
    public async Task A_taken_in_piece_takes_no_discard()
    {
        await HandedAsync(PartnerStates.TakenIn);

        var acted = PartnerPersonAct.Ask(_home, "p1", PartnerPersonActs.Discard, At);

        Assert.False(acted.Acted);
        Assert.Equal(PartnerPersonActs.Why.State, acted.Why);
        Assert.False(new PartnerActFiles(_home).Waits("p1"));
    }

    private static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync();
        return await stdout;
    }
}
