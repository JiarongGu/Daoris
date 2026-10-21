using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A download staged and published apart (PUSH1f2c, D194 point 3, the alone-path design §1.4): it stages under its own process's name
/// while it waits on the network, holding nothing, and becomes the version by one move once published. Two runs of one version never
/// meet in a staging; one publishes and the other keeps it; the person's stop takes the staging with it; and a staging whose process is
/// gone is swept at the next run's start. Nothing here reaches a network: a stand-in answers as a host would.
/// </summary>
public sealed class ToolDownloadsTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "tool-downloads", Guid.NewGuid().ToString("N")[..8]);

    public ToolDownloadsTests() => Directory.CreateDirectory(Home);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Home => Path.Combine(_root, "data");

    private string Tool => DaorisHome.Entry(Home, HomeEntry.ToolVersions, "gh");

    private static readonly byte[] Bytes = ToolInstallTests.Archive.Build("zip", "bin/gh.exe=gh", "");

    private static OfferedVersion Offered(string version) => new(
        version, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Bytes)), Bytes.Length, "zip", "bin/gh.exe", ["bin"],
        [$"https://maker.example/gh-{version}.zip"], [ToolResources.BuiltIn]);

    /// <summary>A host that answers the archive at every address, or waits for the person's stop once asked.</summary>
    private sealed class Host(bool hold = false) : HttpMessageHandler
    {
        public TaskCompletionSource Asked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.TrySetResult();
            if (hold) await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(Bytes) };
        }
    }

    private IReadOnlyList<string> Under() =>
        Directory.Exists(Tool) ? [.. Directory.EnumerateDirectories(Tool).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)] : [];

    [Fact]
    public async Task A_run_stages_under_its_process_s_name_and_its_publication_is_the_version()
    {
        var lines = new List<string>();
        var staged = await ToolInstall.StageAsync(Home, "gh", Offered("2.62.0"), "win-x64", lines.Add, CancellationToken.None, new Host(),
            process: 1001, running: _ => true);

        Assert.NotNull(staged);
        Assert.Equal(["2.62.0.part-1001"], Under());
        Assert.Empty(ToolInstall.Downloaded(Home, "gh"));

        var (exe, published) = ToolInstall.Publish(staged, lines.Add);
        Assert.True(published);
        Assert.Equal(Path.Combine(ToolInstall.VersionFolder(Home, "gh", "2.62.0"), Tools.Package, "bin", "gh.exe"), exe);
        Assert.Equal(["2.62.0"], Under());
        Assert.Equal(["2.62.0"], ToolInstall.Downloaded(Home, "gh"));
        Assert.Contains(lines, line => line.StartsWith("  unpacked into ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Two_runs_of_one_version_each_stage_their_own_and_the_second_keeps_the_first_s()
    {
        var first = await ToolInstall.StageAsync(Home, "gh", Offered("2.62.0"), "win-x64", _ => { }, CancellationToken.None, new Host(),
            process: 1001, running: _ => true);
        var second = await ToolInstall.StageAsync(Home, "gh", Offered("2.62.0"), "win-x64", _ => { }, CancellationToken.None, new Host(),
            process: 1002, running: _ => true);
        Assert.Equal(["2.62.0.part-1001", "2.62.0.part-1002"], Under());

        Assert.True(ToolInstall.Publish(first!, _ => { }).Published);
        var record = File.ReadAllText(Path.Combine(ToolInstall.VersionFolder(Home, "gh", "2.62.0"), Tools.Record));

        var lines = new List<string>();
        var (exe, published) = ToolInstall.Publish(second!, lines.Add);
        Assert.False(published);
        Assert.Equal(Path.Combine(ToolInstall.VersionFolder(Home, "gh", "2.62.0"), Tools.Package, "bin", "gh.exe"), exe);
        Assert.Equal(["2.62.0"], Under());
        Assert.Equal(record, File.ReadAllText(Path.Combine(ToolInstall.VersionFolder(Home, "gh", "2.62.0"), Tools.Record)));
        Assert.Contains(lines, line => line.Contains("another run downloaded GitHub CLI 2.62.0 meanwhile", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_run_stopped_midway_takes_its_staging_with_it()
    {
        using var stop = new CancellationTokenSource();
        var host = new Host(hold: true);
        var run = ToolInstall.StageAsync(Home, "gh", Offered("2.62.0"), "win-x64", _ => { }, stop.Token, host, process: 1001, running: _ => true);

        await host.Asked.Task.WaitAsync(TimeSpan.FromMinutes(2));
        Assert.Equal(["2.62.0.part-1001"], Under());
        await stop.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Empty(Under());
    }

    [Fact]
    public async Task A_staging_whose_process_is_gone_is_swept_at_the_next_run_s_start_and_a_live_one_is_kept()
    {
        Directory.CreateDirectory(Path.Combine(Tool, "2.61.0.part-4242"));
        Directory.CreateDirectory(Path.Combine(Tool, "2.63.0.part-1002"));
        Directory.CreateDirectory(Path.Combine(Tool, "2.60.0.part"));

        var staged = await ToolInstall.StageAsync(Home, "gh", Offered("2.62.0"), "win-x64", _ => { }, CancellationToken.None, new Host(),
            process: 1001, running: process => process is 1001 or 1002);

        // The dead process's goes; the live one's stays; an older build's bare staging of another version waits for that version.
        Assert.Equal(["2.60.0.part", "2.62.0.part-1001", "2.63.0.part-1002"], Under());
        ToolInstall.Discard(staged!);
        Assert.Equal(["2.60.0.part", "2.63.0.part-1002"], Under());
    }

    [Fact]
    public async Task A_staging_is_never_a_version_a_reader_takes()
    {
        await ToolInstall.StageAsync(Home, "gh", Offered("2.62.0"), "win-x64", _ => { }, CancellationToken.None, new Host(),
            process: 1001, running: _ => true);

        Assert.Empty(ToolInstall.Downloaded(Home, "gh"));
        Assert.Equal("missing", Assert.Throws<ToolRefusal>(() => ToolInstall.Delete(Home, "gh", "2.62.0")).Check);
        Assert.Equal("version", Assert.Throws<ToolRefusal>(() => ToolInstall.Delete(Home, "gh", "2.62.0.part-1001")).Check);
    }
}
