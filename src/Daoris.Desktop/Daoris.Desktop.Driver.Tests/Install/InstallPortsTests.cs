using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The ports the install declares and the workplace answers (FOUNDATION1 mod-v): the git a child is handed and the clearing a downloaded
/// version is deleted through. The workplace installs both when the driver's assembly loads, so a start and a delete read and clear as they
/// always did; a test may put another in and puts it back.
/// </summary>
public sealed class InstallPortsTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "install-ports", Guid.NewGuid().ToString("N")[..8]);

    public InstallPortsTests() => Directory.CreateDirectory(Home);

    public void Dispose() => GitSeed.Discard(_root);

    private string Home => Path.Combine(_root, "data");

    [Fact]
    public void The_git_wiring_in_force_is_the_workplaces_and_hands_what_the_tools_seam_always_handed()
    {
        Assert.IsType<WorkingTreeGit>(ChildGit.Current);

        var through = new ProcessStartInfo("x");
        Tools.Hand(through, home: null);
        var direct = new ProcessStartInfo("x");
        GitWiring.Hand(direct);

        Assert.Equal(direct.Environment.OrderBy(pair => pair.Key), through.Environment.OrderBy(pair => pair.Key));
        Assert.All(GitWiring.Prompts, pair => Assert.Equal(pair.Value, through.Environment[pair.Key]));
    }

    [Fact]
    public void A_test_may_install_another_git_wiring_and_put_the_workplaces_back()
    {
        var workplaces = ChildGit.Current;
        var told = new List<string?>();
        try
        {
            ChildGit.Current = new StandGit(told);
            Tools.Hand(new ProcessStartInfo("x"), home: null);
            Tools.Hand(new ProcessStartInfo("x"), "home-a");
        }
        finally
        {
            ChildGit.Current = workplaces;
        }

        Assert.Equal([null, "home-a"], told);
        Assert.Same(workplaces, ChildGit.Current);
    }

    [Fact]
    public async Task The_clearing_in_force_is_the_workplaces_and_reads_no_session_where_none_runs()
    {
        Assert.IsType<DownloadClearings>(ToolDownloads.Current);

        // No process marker is kept under a home that has none: nothing runs, so nothing keeps a version.
        Assert.Same(LiveSessions.None, await ToolDownloads.Current.RunningAsync(Home, CancellationToken.None));
    }

    [Theory]
    [InlineData("used", "in-use", null)]
    [InlineData("running", "held", "delete it once that session has ended")]
    [InlineData("named", "held", "driver.json names a file inside it")]
    [InlineData("unread", "held", "cannot be read now (sessions)")]
    [InlineData(null, "held", "could not be moved aside")]
    public async Task A_delete_the_clearing_keeps_is_refused_in_the_installs_words(string? keeps, string check, string? said)
    {
        var folder = ToolInstall.VersionFolder(Home, "gh", "2.62.0");
        Directory.CreateDirectory(folder);
        var workplaces = ToolDownloads.Current;
        var stand = new StandDownloads(new ToolVersionCleared("kept", keeps, keeps switch
        {
            "running" => ["session:s1"],
            "named" => ["driver.json"],
            "unread" => ["sessions"],
            _ => [],
        }));
        try
        {
            ToolDownloads.Current = stand;
            var refused = await Assert.ThrowsAsync<ToolRefusal>(() =>
                ToolInstall.DeleteAsync(Home, "gh", "2.62.0", _ => Task.FromResult(LiveSessions.None), harnesses: "wiring.json", config: "driver.json"));

            Assert.Equal(check, refused.Check);
            if (said is not null) Assert.Contains(said, refused.Message);
        }
        finally
        {
            ToolDownloads.Current = workplaces;
        }

        // The install asked for exactly this version, handed the files that say what is used as it was told them.
        Assert.Equal(("gh", "2.62.0", "wiring.json", "driver.json"), stand.Asked);
        Assert.True(Directory.Exists(folder), "the version stays");
    }

    [Fact]
    public async Task A_delete_the_clearing_removes_answers_the_folder()
    {
        var folder = ToolInstall.VersionFolder(Home, "gh", "2.62.0");
        Directory.CreateDirectory(folder);
        var workplaces = ToolDownloads.Current;
        try
        {
            ToolDownloads.Current = new StandDownloads(new ToolVersionCleared("removed", null, []));
            Assert.Equal(folder, await ToolInstall.DeleteAsync(Home, "gh", "2.62.0", _ => Task.FromResult(LiveSessions.None)));
        }
        finally
        {
            ToolDownloads.Current = workplaces;
        }

        Assert.Same(workplaces, ToolDownloads.Current);
    }

    private sealed class StandGit(List<string?> told) : IChildGit
    {
        public void Hand(ProcessStartInfo info, string? home) => told.Add(home);
    }

    private sealed class StandDownloads(ToolVersionCleared answer) : IToolDownloads
    {
        public (string Tool, string Version, string Harnesses, string Config)? Asked { get; private set; }

        public Task<ToolVersionCleared> ClearAsync(
            string home, string tool, string version, string harnesses, string config, Func<CancellationToken, Task<LiveSessions>> sessions,
            MachineLog? log, Action<string, string>? move, CancellationToken ct)
        {
            Asked = (tool, version, harnesses, config);
            return Task.FromResult(answer);
        }

        public Task<LiveSessions> RunningAsync(string home, CancellationToken ct) => Task.FromResult(LiveSessions.None);
    }
}
