using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A build of this branch, staged beside an install shaped like the one that refused 0.0.40 (2026-11-27), judged by the probe that install
/// runs (<see cref="HomeProbeAt0039"/>) and by this build's own (<see cref="HomeProbe"/>), each starting this build's driver on its copy.
/// The home holds what that install's does: accounts under <c>profiles</c>, sessions, a database above the copy's bound, the check
/// broker's seat, and a check's work tree whose dependencies are the dependency store's read-only hard links. 0.0.39's reading of a home
/// is this build's (<c>HomeProofReading</c> reads what it read at e82e2e5116, its accounts a part of their own).
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class HomeProbeUpgradeProcessTests : IDisposable
{
    private readonly string _install = Path.Combine(TestHomes.Root, "daoris-upgrade-" + Guid.NewGuid().ToString("N")[..8]);

    public HomeProbeUpgradeProcessTests()
    {
        Put("INSTALLED.md", "# Daoris — installed desktop\n");
        Put("Daoris.exe", "the launcher");
        Put("app/daoris-driver/daoris-driver.exe", "the running build");
        foreach (var (agent, account) in new[] { ("claude-code", "acct-a"), ("claude-code", "acct-b"), ("codex", "acct-c") })
        {
            Put($"profiles/agents/{agent}/{account}/.credentials.json", "{}");
        }

        Put("cache/toolchain/node/node.exe", "a tool");
        Put("logs/2026-11-27.desktop.jsonl", "{}\n");
        Put("run/home.lease", "");
        Put("work/trees/ws/repo/tree/src/a.cs", "class A {}");
        Put("data/accounts.json", "{}");
        Put("data/desktop.db", "a store");
        using (var large = File.Create(Path.Combine(Home, "knowledge.db"))) large.SetLength(HomeCopy.MaxFile + 1);
        Put("data/sessions/4f0c2a91.events.jsonl", "");
        Put("data/sessions/4f0c2a91.log", "a session's log");
        Put("data/checks/broker.lock", "");
        Put("data/checks/04e0ba77.log", "a check's log");
        var store = Put("work/deps/k1/node_modules/pkg/index.js", "module.exports = 1;");
        File.SetAttributes(store, FileAttributes.ReadOnly);
        const string slot = "data/workflows/lines/trees/slot-1";
        Put(slot + "/.git", "gitdir: elsewhere/.git/worktrees/slot-1\n");
        Put(slot + "/src/b.cs", "class B {}");
        Put(slot + "/node_modules/.vitest-cache/c0302f", "{}");
        var linked = Path.Combine(_install, slot, "node_modules", "pkg", "index.js");
        Directory.CreateDirectory(Path.GetDirectoryName(linked)!);
        Assert.True(FileLinks.Make(linked, store), "the volume links");
    }

    private string Home => Path.Combine(_install, "data");

    public void Dispose()
    {
        if (!Directory.Exists(_install)) return;
        foreach (var file in new DirectoryInfo(_install).EnumerateFiles("*", SearchOption.AllDirectories)) file.IsReadOnly = false;
        Directory.Delete(_install, recursive: true);
    }

    private string Put(string relative, string text)
    {
        var path = Path.Combine(_install, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void A_build_of_this_branch_passes_the_probe_0_0_39_runs_and_only_a_held_seat_made_that_probe_refuse()
    {
        StageDriver();

        // No check holds a file of the home: 0.0.39 makes its copy, and this build reads it as the running build reads the home.
        Assert.Null(HomeProbeAt0039.Refused(_install, Home, "0.0.41"));
        Assert.Null(HomeProbe.Refused(_install, Home, "0.0.41"));

        // A check waits or runs, so the broker holds its seat: 0.0.39 cannot make its copy, which refused 0.0.40; this build's probe can.
        using (new FileStream(Path.Combine(Home, "checks", "broker.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var refused = HomeProbeAt0039.Refused(_install, Home, "0.0.41");
            Assert.Equal(HomeProbe.Code, refused?.Code);
            Assert.Contains("could not be made", refused?.Sentence);
            Assert.Contains("broker.lock", refused?.Sentence);
            Assert.Null(HomeProbe.Refused(_install, Home, "0.0.41"));
        }
    }

    /// <summary>The host's build as the staged build's driver, in the folder a staged build keeps it.</summary>
    private void StageDriver()
    {
        var from = Path.GetDirectoryName(HostBuild.Dll())!;
        var to = Path.Combine(StagedBuild.StagedOf(_install), StagedBuild.AppFolder, "daoris-driver");
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
