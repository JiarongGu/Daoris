using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A download as an action the page starts and follows (TOOLS4, D121 §3.6): answered once it has started, its end news
/// after, one at a time for each tool, and stopped by the person — the shape <c>HARNESS_ACTION</c>'s install has, since
/// the bridge gives up after thirty seconds and a download outlives that (WSR7 a). The route that carries it to the
/// page is TOOLS7's.
/// </summary>
/// <remarks>Nothing here reaches a network: a host is a stand-in, and one of them stalls until it is stopped.</remarks>
public sealed class ToolActionsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-toolactions-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly byte[] Bytes = ToolInstallTests.Archive.Build("zip", "bin/gh.exe=gh", "");

    private string Home => Path.Combine(_root, "data");

    private string App => Path.Combine(_root, "app");

    private string Platform => ToolResources.Current ?? "win-x64";

    public ToolActionsTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(App);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The list built in beside the application, naming each version at its own address with the same bytes.</summary>
    private void BuiltIn(params string[] versions)
    {
        var listed = new JsonObject();
        foreach (var version in versions)
        {
            listed[version] = new JsonObject
            {
                ["files"] = new JsonObject
                {
                    [Platform] = new JsonObject
                    {
                        ["url"] = $"https://maker.example/gh-{version}.zip",
                        ["sha256"] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Bytes)),
                        ["size"] = Bytes.Length,
                        ["archive"] = "zip",
                        ["exe"] = "bin/gh.exe",
                    },
                },
            };
        }

        File.WriteAllText(
            ToolResources.BesideApplication(App),
            new JsonObject { ["schema"] = 1, ["tools"] = new JsonObject { ["gh"] = new JsonObject { ["versions"] = listed } } }.ToJsonString());
    }

    private sealed class Host(Func<string, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request.RequestUri!.AbsoluteUri));
    }

    private static HttpResponseMessage Served() => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Bytes) };

    /// <summary>A body that sends one byte, then waits until the reader is stopped.</summary>
    private sealed class Stalling : Stream
    {
        private bool _sent;

        public TaskCompletionSource Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_sent)
            {
                _sent = true;
                buffer.Span[0] = 0x50;
                return 1;
            }

            Waiting.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("read asynchronously");
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public async Task A_download_is_answered_once_it_has_started_and_the_persons_stop_ends_it_leaving_nothing()
    {
        BuiltIn("2.62.0");
        var body = new Stalling();
        var actions = new ToolActions(Home, App, new Host(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) }));
        var lines = new List<string>();

        var run = actions.Start("gh", "download", null, line => { lock (lines) lines.Add(line); });
        Assert.Equal(("gh", "download", "2.62.0"), (run.Tool, run.Action, run.Version));
        await body.Waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(run.Ended.IsCompleted, "a download is followed, never waited on");
        Assert.Same(run, actions.Running("gh"));

        var busy = Assert.Throws<ToolRefusal>(() => actions.Start("gh", "use", "2.62.0", _ => { }));
        Assert.Equal("busy", busy.Check);
        Assert.Contains("GitHub CLI's download is still running", busy.Message);

        Assert.True(actions.Cancel("gh"));
        var end = await run.Ended.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(end.Stopped);
        Assert.Equal(-1, end.ExitCode);
        Assert.Null(actions.Running("gh"));
        Assert.False(actions.Cancel("gh"), "nothing runs to stop");
        Assert.False(Directory.Exists(Path.Combine(Home, Tools.Folder, "gh")), "a stop leaves nothing under tools/gh");
        Assert.False(File.Exists(Path.Combine(Home, Tools.FileName)), "nothing switches");
    }

    [Fact]
    public async Task Use_downloads_then_switches_and_its_end_is_news_after_the_answer()
    {
        BuiltIn("2.62.0");
        var actions = new ToolActions(Home, App, new Host(_ => Served()));
        var lines = new List<string>();

        var run = actions.Start("gh", "use", null, line => { lock (lines) lines.Add(line); });
        var end = await run.Ended.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(new ToolEnd("gh", "use", "2.62.0", 0, null, null, false, null), end);
        Assert.Equal(
            Path.Combine(ToolInstall.VersionFolder(Home, "gh", "2.62.0"), Tools.Package, "bin", "gh.exe"),
            Tools.Resolve(Home, "gh", path: "").File);
        Assert.Contains(lines, line => line.Contains("its size and SHA-256 match the list", StringComparison.Ordinal));
        Assert.Null(actions.Running("gh"));
    }

    [Fact]
    public async Task Update_moves_a_managed_tool_to_the_newest_and_says_when_there_is_nothing_to_do()
    {
        BuiltIn("2.62.0");
        var actions = new ToolActions(Home, App, new Host(_ => Served()));
        await actions.Start("gh", "use", "2.62.0", _ => { }).Ended.WaitAsync(TimeSpan.FromSeconds(10));

        var nothing = await actions.Start("gh", "update", null, _ => { }).Ended.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, nothing.ExitCode);
        Assert.Equal("GitHub CLI 2.62.0 is the newest the lists name, and it is downloaded — nothing to do", nothing.Nothing);

        BuiltIn("2.62.0", "2.63.0");
        var moved = await actions.Start("gh", "update", null, _ => { }).Ended.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(("2.63.0", 0), (moved.Version, moved.ExitCode));
        Assert.Equal("2.63.0", Tools.Read(Home).Entries["gh"].Version);
    }

    [Fact]
    public async Task A_refusal_by_a_check_ends_the_action_with_its_check_and_switches_nothing()
    {
        BuiltIn("2.62.0");
        var actions = new ToolActions(Home, App, new Host(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes(new string('x', Bytes.Length))),
        }));

        var end = await actions.Start("gh", "use", "2.62.0", _ => { }).Ended.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal((1, "hash", false), (end.ExitCode, end.Check, end.Stopped));
        Assert.Contains("served bytes whose SHA-256 is", end.Problem);
        Assert.False(File.Exists(Path.Combine(Home, Tools.FileName)));
    }

    [Fact]
    public void A_plan_that_refuses_is_refused_before_anything_starts()
    {
        var actions = new ToolActions(Home, App, new Host(_ => throw new InvalidOperationException("nothing is fetched")));

        var unknown = Assert.Throws<ToolRefusal>(() => actions.Start("gh", "download", null, _ => { }));
        Assert.Equal("unknown", unknown.Check);
        var machine = Assert.Throws<ToolRefusal>(() => actions.Start("gh", "update", null, _ => { }));
        Assert.Equal("machine", machine.Check);
        Assert.Throws<DriverException>(() => actions.Start("gh", "install", null, _ => { }));
        Assert.Null(actions.Running("gh"));
    }

    [Fact]
    public async Task The_machine_log_names_a_tool_and_a_version_and_never_an_address()
    {
        BuiltIn("2.62.0");
        using var log = new MachineLog(Home, "driver");
        var actions = new ToolActions(Home, App, new Host(_ => Served()), log);
        await actions.Start("gh", "download", null, _ => { }).Ended.WaitAsync(TimeSpan.FromSeconds(10));
        log.Dispose();

        var text = string.Concat(Directory.EnumerateFiles(Path.Combine(Home, MachineLog.Folder)).Select(File.ReadAllText));
        Assert.Contains("\"tool.download.started\"", text);
        Assert.Contains("\"tool.download.verified\"", text);
        Assert.Contains("\"gh\"", text);
        Assert.Contains("\"2.62.0\"", text);
        Assert.DoesNotContain("maker.example", text);
    }
}
