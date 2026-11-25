using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The proof a staged build owes before it is swapped in (<see cref="HomeProbe"/>): it starts against a copy of the install's home, the
/// source is read and never written, and the swap is refused, with the reason, unless every account the running build lists is still
/// listed, the sessions and runs read, the workflows bind, and the home names no stray. The staged build's own driver is the one that
/// reads the copy; these tests hand it as a function, and the process half runs the real driver (<c>HomeProofProcessTests</c>).
/// </summary>
public sealed class HomeProbeTests : IDisposable
{
    private readonly string _install = Path.Combine(TestHomes.Root, "daoris-probe-" + Guid.NewGuid().ToString("N")[..8]);

    public HomeProbeTests()
    {
        Directory.CreateDirectory(Path.Combine(_install, "data"));
        File.WriteAllText(Path.Combine(_install, "INSTALLED.md"), "# Daoris — installed desktop\n");
        Directory.CreateDirectory(StagedBuild.StagedOf(_install));
    }

    private string Home => Path.Combine(_install, "data");

    public void Dispose()
    {
        if (Directory.Exists(_install)) Directory.Delete(_install, recursive: true);
    }

    private void Put(string relative, string text = "")
    {
        var path = Path.Combine(_install, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private void Account(string agent, string id) => Directory.CreateDirectory(Path.Combine(_install, "profiles", "agents", agent, id));

    /// <summary>A staged build that reads as the build asking does: the same reader, over the home it is handed.</summary>
    private static (int Exit, string Output) SameReader(string staged, string copy) => (0, HomeProof.Say(HomeProof.Read(copy)));

    private List<string> Snapshot() =>
        [.. Directory.EnumerateFileSystemEntries(_install, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.Combine(_install, "update"), StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(_install, path) + (File.Exists(path) ? ":" + new FileInfo(path).Length + ":" + File.GetLastWriteTimeUtc(path).Ticks : "/"))
            .Order(StringComparer.Ordinal)];

    [Fact]
    public void A_build_that_starts_clean_on_the_copy_is_not_refused_and_the_source_is_left_as_found()
    {
        Account("claude-code", "acct-a");
        Account("claude-code", "acct-b");
        Put("data/sessions/s-1.events.jsonl", "");
        string? seen = null;
        var before = Snapshot();

        var refusal = HomeProbe.Refused(_install, Home, "0.0.9", (staged, copy) =>
        {
            seen = copy;
            Assert.True(File.Exists(Path.Combine(copy, "sessions", "s-1.events.jsonl")), "the copy holds the home's files");
            Assert.True(Directory.Exists(Path.Combine(DaorisHome.FolderOf(copy), "profiles", "agents", "claude-code", "acct-a")));
            return SameReader(staged, copy);
        });

        Assert.Null(refusal);
        Assert.NotNull(seen);
        Assert.NotEqual(Path.GetFullPath(Home), Path.GetFullPath(seen!));
        Assert.Equal(before, Snapshot());
        Assert.False(Directory.Exists(Path.Combine(_install, StagedBuild.Folder, HomeProbe.Scratch)) && Directory.EnumerateFileSystemEntries(Path.Combine(_install, StagedBuild.Folder, HomeProbe.Scratch)).Any(),
            "the scratch copy is gone once the proof is");
    }

    [Fact]
    public void A_build_that_lists_fewer_accounts_than_the_running_one_is_refused_naming_the_account()
    {
        Account("claude-code", "acct-a");
        Account("claude-code", "acct-b");

        var refusal = HomeProbe.Refused(_install, Home, "0.0.9", (_, copy) =>
        {
            var report = HomeProof.Read(copy);
            return (0, HomeProof.Say(report with { Accounts = new Dictionary<string, IReadOnlyList<string>> { ["claude-code"] = ["acct-a"] } }));
        });

        Assert.Equal(HomeProbe.Code, refusal?.Code);
        Assert.Contains("claude-code/acct-b", refusal?.Sentence);
        Assert.Contains("keeps running", refusal?.Sentence);
    }

    [Fact]
    public void A_build_that_reads_a_stray_in_the_copy_is_refused_naming_it()
    {
        // An install holding accounts at a place the table no longer names: the reading the account fix lacked.
        Put("data/harnesses/claude-code/old-acct/state.json", "{}");

        var refusal = HomeProbe.Refused(_install, Home, "0.0.9", SameReader);

        Assert.Equal(HomeProbe.Code, refusal?.Code);
        Assert.Contains("data/harnesses", refusal?.Sentence);
    }

    [Fact]
    public void A_build_that_gives_no_report_or_fails_is_refused_with_what_it_said()
    {
        var silent = HomeProbe.Refused(_install, Home, "0.0.9", (_, _) => (2, "usage: daoris home purge ..."));
        var garbled = HomeProbe.Refused(_install, Home, "0.0.9", (_, _) => (0, "not a report"));

        Assert.Equal(HomeProbe.Code, silent?.Code);
        Assert.Contains("usage: daoris home purge", silent?.Sentence);
        Assert.Equal(HomeProbe.Code, garbled?.Code);
        Assert.Contains("no readable report", garbled?.Sentence);
    }

    [Fact]
    public void A_staged_build_with_no_driver_to_run_is_refused_by_the_real_start()
    {
        var refusal = HomeProbe.Refused(_install, Home, "0.0.9");

        Assert.Equal(HomeProbe.Code, refusal?.Code);
        Assert.Contains("carries no program", refusal?.Sentence);
        Assert.Contains("keeps running", refusal?.Sentence);
    }

    [Fact]
    public void A_copy_an_earlier_proof_left_is_removed_and_one_a_proof_still_holds_is_kept()
    {
        var scratch = Path.Combine(_install, StagedBuild.Folder, HomeProbe.Scratch);
        Directory.CreateDirectory(Path.Combine(scratch, "abandoned", "data"));
        Directory.CreateDirectory(Path.Combine(scratch, "running", "data"));
        string? during = null;

        using (new FileStream(Path.Combine(scratch, "running.live"), FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            HomeProbe.Refused(_install, Home, "0.0.9", (_, _) =>
            {
                during = string.Join("|", new[] { "abandoned", "running" }.Select(name => Directory.Exists(Path.Combine(scratch, name))));
                return (2, "");
            });
        }

        Assert.Equal("False|True", during);
    }

    [Fact]
    public void The_copy_names_what_the_home_names_and_no_more()
    {
        Put("data/sessions/s-1.events.jsonl", "abc");
        Put("cache/bin/tool.exe", "binary");
        Put("work/trees/ws/repo/tree/deep/file.txt", "deep");
        string? seen = null;

        HomeProbe.Refused(_install, Home, "0.0.9", (_, copy) =>
        {
            var folder = DaorisHome.FolderOf(copy);
            seen = string.Join("|", new[] { "cache/bin/tool.exe", "work/trees/ws/repo/tree" }.Select(path => File.Exists(Path.Combine(folder, path)) || Directory.Exists(Path.Combine(folder, path))));
            Assert.False(File.Exists(Path.Combine(folder, "work/trees/ws/repo/tree/deep/file.txt")), "a tree's files are not copied");
            Assert.Equal(0, new FileInfo(Path.Combine(folder, "cache/bin/tool.exe")).Length);
            Assert.Equal("abc", File.ReadAllText(Path.Combine(copy, "sessions", "s-1.events.jsonl")));
            return SameReader(null!, copy);
        });

        Assert.Equal("True|True", seen);
    }
}
