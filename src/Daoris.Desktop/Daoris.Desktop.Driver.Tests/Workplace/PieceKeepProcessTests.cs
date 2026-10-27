using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1b (D199 points 2 and 3; the co-agent design §7.9, §2.4, Codex's reads 23 and 24): the keeper reads a partner's copy as
/// files and writes one commit onto the ask's base in the repository itself, with real git. The copy's hooks, filters, configuration
/// and identity, all rewritten by the partner, are never run or read; the partner's own commits never reach the repository; the same
/// inputs make the same commit, so a restart repeats it; the piece's ref is created once and never moved; and a piece ref is never
/// counted as a holder of a branch.
/// </summary>
/// <remarks>Real git, so the class is in the real-process half's git classes, as <see cref="HelperCaptureProcessTests"/> is.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PieceKeepProcessTests : IDisposable
{
    private static readonly DateTimeOffset Kept = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "piece-keep", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_scratch);

    /// <summary>A repository with a README, a source file and an ignore rule for logs, its identity set; and its base commit.</summary>
    private async Task<(string Root, string Base)> RepositoryAsync()
    {
        var seed = await GitSeed.SeedAsync("piece-keep", async folder =>
        {
            var root = Path.Combine(folder, "reports");
            Directory.CreateDirectory(Path.Combine(root, "src"));
            await GitAsync(root, "init", "--quiet", "-b", "main");
            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# reports\n");
            await File.WriteAllTextAsync(Path.Combine(root, "src", "report.ts"), "export const days = 6;\n");
            await File.WriteAllTextAsync(Path.Combine(root, "src", "gone.ts"), "export const gone = true;\n");
            await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "*.log\n");
            await GitAsync(root, "add", "-A");
            await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--quiet", "-m", "first");
        });
        var root = Path.Combine(_scratch, "reports");
        GitSeed.Copy(Path.Combine(seed, "reports"), root);
        await GitAsync(root, "config", "user.name", "Owner");
        await GitAsync(root, "config", "user.email", "owner@example.test");
        return (root, (await GitAsync(root, "rev-parse", "HEAD")).Trim());
    }

    /// <summary>The partner's copy: a clone with no remote at the base, as the piece's preparing makes it.</summary>
    private async Task<string> CopyAsync(string root, string @base)
    {
        var copy = Path.Combine(_scratch, "p-piece");
        Assert.Null(await PieceCopy.MakeAsync(root, copy, @base, CancellationToken.None));
        Assert.Equal("", (await GitAsync(copy, "remote")).Trim());
        return copy;
    }

    [Fact]
    public async Task The_copy_is_refused_at_a_base_the_repository_does_not_hold_with_no_fallback()
    {
        var (root, _) = await RepositoryAsync();

        var refused = await PieceCopy.MakeAsync(root, Path.Combine(_scratch, "p-none"), new string('1', 40), CancellationToken.None);

        Assert.Equal(PartnerWhy.BaseMissing, refused?.Code);
        Assert.False(Directory.Exists(Path.Combine(_scratch, "p-none")));
    }

    private static PartnerAsk Ask(string @base, params string[] paths) =>
        new("p1", "lead", "Make the window seven days.", paths, "stub-b", @base, [], PartnerAsk.ByPerson, Kept);

    private string Index() => Path.Combine(_scratch, "index-" + Guid.NewGuid().ToString("N")[..6]);

    [Fact]
    public async Task One_commit_onto_the_base_holds_the_files_the_partner_left_and_nothing_of_its_own_git()
    {
        var (root, @base) = await RepositoryAsync();
        var copy = await CopyAsync(root, @base);
        // The partner's work, and the partner's own commit of part of it with an identity of its own.
        await File.WriteAllTextAsync(Path.Combine(copy, "src", "report.ts"), "export const days = 7;\n");
        await GitAsync(copy, "-c", "user.email=partner@example.test", "-c", "user.name=Partner", "commit", "--quiet", "-am", "partner's own");
        await File.WriteAllTextAsync(Path.Combine(copy, "src", "window.ts"), "export const window = 'inclusive';\n");
        await File.WriteAllTextAsync(Path.Combine(copy, "debug.log"), "ignored\n");
        await File.WriteAllTextAsync(Path.Combine(copy, "secret.txt"), "written and deleted\n");
        File.Delete(Path.Combine(copy, "secret.txt"));
        File.Delete(Path.Combine(copy, "src", "gone.ts"));
        // The copy's git rewritten: a hook that would mark its run, a filter that would rewrite every byte, a signing demand.
        var marker = Path.Combine(_scratch, "hook-ran");
        Directory.CreateDirectory(Path.Combine(copy, ".git", "hooks"));
        await File.WriteAllTextAsync(Path.Combine(copy, ".git", "hooks", "pre-commit"), $"#!/bin/sh\necho ran > '{marker.Replace('\\', '/')}'\n");
        await File.WriteAllTextAsync(Path.Combine(copy, ".gitattributes"), "*.ts filter=evil\n");
        await GitAsync(copy, "config", "filter.evil.clean", "echo EVIL");
        await GitAsync(copy, "config", "commit.gpgSign", "true");
        await GitAsync(copy, "config", "user.name", "Impostor");
        var partnerHead = (await GitAsync(copy, "rev-parse", "HEAD")).Trim();

        var kept = await PieceKeeper.KeepAsync(root, copy, Ask(@base, "src"), Index(), Kept, "piece p1", CancellationToken.None);

        Assert.Equal(PartnerStates.Imported, kept.State);
        var commit = kept.Commit!;
        Assert.Equal(commit, (await GitAsync(root, "rev-parse", PieceRefs.Of("p1"))).Trim());
        // One commit, straight onto the base: the partner's own commit is not in its history, nor anywhere in the repository.
        Assert.Equal(@base, (await GitAsync(root, "rev-parse", commit + "^")).Trim());
        Assert.Equal("1", (await GitAsync(root, "rev-list", "--count", $"{@base}..{commit}")).Trim());
        Assert.NotEqual("commit", (await GitAsync(root, "cat-file", "-t", partnerHead)).Trim());
        // The bytes as the partner left them, with no filter; the ignored file left out, the deletion taken, the secret never seen.
        Assert.Equal("export const days = 7;\n", await GitAsync(root, "show", $"{commit}:src/report.ts"));
        Assert.Equal(
            [".gitattributes", ".gitignore", "README.md", "src/report.ts", "src/window.ts"],
            (await GitAsync(root, "ls-tree", "-r", "--name-only", commit)).Split('\n', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal));
        Assert.Contains(new PieceLeftOut("debug.log", PieceImport.WhyIgnored), kept.Judged!.LeftOut);
        // The repository's identity and the keeper's moment, never the copy's; no hook ran.
        Assert.Equal("Owner <owner@example.test> 1792670400", (await GitAsync(root, "log", "-1", "--format=%an <%ae> %at", commit)).Trim());
        Assert.False(File.Exists(marker));
        // Beyond the piece's paths: the ignore-rule files the partner added outside `src`.
        Assert.Equal([".gitattributes"], PieceImport.Beyond(kept.Judged.Changed.Select(pair => pair.Entry.Path).Concat(kept.Judged.Deleted), ["src"]));
    }

    [Fact]
    public async Task The_same_inputs_make_the_same_commit_and_the_ref_is_create_only()
    {
        var (root, @base) = await RepositoryAsync();
        var copy = await CopyAsync(root, @base);
        await File.WriteAllTextAsync(Path.Combine(copy, "src", "report.ts"), "export const days = 7;\n");

        var first = await PieceKeeper.KeepAsync(root, copy, Ask(@base), Index(), Kept, "piece p1", CancellationToken.None);
        // A restart between the ref and the state: keeping again finds its own commit standing, and is imported.
        var again = await PieceKeeper.KeepAsync(root, copy, Ask(@base), Index(), Kept, "piece p1", CancellationToken.None);
        Assert.Equal((PartnerStates.Imported, first.Commit), (again.State, again.Commit));

        // A ref at another commit is never moved: the piece is uncertain.
        await File.WriteAllTextAsync(Path.Combine(copy, "src", "report.ts"), "export const days = 8;\n");
        var moved = await PieceKeeper.KeepAsync(root, copy, Ask(@base), Index(), Kept, "piece p1", CancellationToken.None);
        Assert.Equal((PartnerStates.Uncertain, PartnerWhy.RefMoved), (moved.State, moved.Code));
        Assert.Equal(first.Commit, (await GitAsync(root, "rev-parse", PieceRefs.Of("p1"))).Trim());
    }

    [Fact]
    public async Task A_copy_with_no_work_imports_nothing_and_one_whose_work_is_all_refused_is_held()
    {
        var (root, @base) = await RepositoryAsync();
        var copy = await CopyAsync(root, @base);

        var none = await PieceKeeper.KeepAsync(root, copy, Ask(@base), Index(), Kept, "piece p1", CancellationToken.None);
        Assert.Equal((null, PartnerWhy.NoWork), (none.State, none.Code));

        Directory.CreateDirectory(Path.Combine(copy, "nested"));
        await GitAsync(Path.Combine(copy, "nested"), "init", "--quiet");
        await File.WriteAllTextAsync(Path.Combine(copy, "nested", "x.txt"), "x\n");
        var held = await PieceKeeper.KeepAsync(root, copy, Ask(@base), Index(), Kept, "piece p1", CancellationToken.None);
        Assert.Equal((PartnerStates.Held, PartnerWhy.ImportRefused), (held.State, held.Code));
        Assert.Equal([new PieceLeftOut("nested", PieceImport.WhyRepository)], held.Judged!.LeftOut);
        Assert.Equal("", (await GitAsync(root, "for-each-ref", PieceRefs.Prefix)).Trim());
    }

    [Fact]
    public async Task A_piece_ref_is_never_counted_as_a_holder_of_a_branch()
    {
        var (root, @base) = await RepositoryAsync();
        var copy = await CopyAsync(root, @base);
        await File.WriteAllTextAsync(Path.Combine(copy, "src", "report.ts"), "export const days = 7;\n");
        await GitAsync(root, "branch", "daoris/lead", @base);
        await GitAsync(root, "checkout", "--quiet", "--detach");
        await PieceKeeper.KeepAsync(root, copy, Ask(@base), Index(), Kept, "piece p1", CancellationToken.None);
        await GitAsync(root, "branch", "-D", "main");

        var holders = await ContentHolds.HoldersAsync(root, @base, "daoris/lead", CancellationToken.None);

        // The piece's commit holds the lead's base by ancestry, and is still no holder.
        Assert.NotNull(holders);
        Assert.DoesNotContain(holders!, pair => PieceRefs.IsPiece(pair.Ref));
        Assert.False(ContentHolds.AnyRef(PieceRefs.Of("p1")));
        Assert.False(ContentHolds.AnyRef(ContentHolds.DiscardedPrefix + "x"));
        Assert.True(ContentHolds.AnyRef("refs/heads/main"));
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
