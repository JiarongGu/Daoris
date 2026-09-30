using System.Diagnostics;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using Daoris.Knowledge;
using Daoris.Knowledge.Http;
using Microsoft.Extensions.Hosting;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// LOG2a: the first real machine log held a host <c>app.started</c> for every start and no
/// <c>app.stopped</c> at all, because the shell ended the host it started by killing it. The shell now
/// starts it with its standard input redirected and <see cref="InputEndStop.Variable"/> set, and closes
/// that input to stop it: the host reads its input to the end, reading nothing from it, and then stops
/// as it would on Ctrl+C, so its lifetime runs and the log says so. A host nobody asked (a terminal's) is
/// as it was.
/// </summary>
public sealed class InputEndStopTests : IDisposable
{
    private readonly string _scratch = Path.Combine(
        Path.GetTempPath(), "daoris-log2a-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        for (var attempt = 0; attempt < 20 && Directory.Exists(_scratch); attempt++)
        {
            try
            {
                Directory.Delete(_scratch, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }

    /// <summary>
    /// The twin (<c>.claude/knowledge/twins.md</c>): the shell's <c>HostSupervisor.StopOnInputEnd</c>
    /// spells the same variable and sets it to <c>1</c>, and its tests hold this same table.
    /// </summary>
    [Theory]
    [InlineData("1", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("0", false)]
    [InlineData("true", false)]
    [InlineData(" 1", false)]
    public void Only_a_1_asks_for_the_stop(string? value, bool asks)
    {
        Assert.Equal("DAORIS_STOP_ON_INPUT_END", InputEndStop.Variable);
        Assert.Equal(asks, InputEndStop.Asks(value));
    }

    [Fact]
    public async Task A_host_asked_stops_once_its_input_ends()
    {
        var lifetime = new Lifetime();
        var input = new MemoryStream("whatever was written is read past, never read as anything"u8.ToArray());

        await InputEndStop.Watch(input, lifetime).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(1, lifetime.Stops);
        Assert.Equal(input.Length, input.Position);
    }

    /// <summary>The shape the shell makes: a pipe, which ends when its one writer closes it, and not before.</summary>
    [Fact]
    public async Task It_keeps_serving_until_the_writer_closes_the_pipe()
    {
        var lifetime = new Lifetime();
        var writer = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
        using var reader = new AnonymousPipeClientStream(PipeDirection.In, writer.ClientSafePipeHandle);

        var watching = InputEndStop.Watch(reader, lifetime);
        writer.Write("a byte or two"u8);
        writer.Flush();
        await Task.Delay(300);
        Assert.Equal(0, lifetime.Stops);
        Assert.False(watching.IsCompleted);

        writer.Dispose();
        await watching.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, lifetime.Stops);
    }

    /// <summary>A pipe whose writer died with its process breaks rather than ends, and its host is as alone.</summary>
    [Fact]
    public async Task An_input_that_breaks_has_ended_too()
    {
        var lifetime = new Lifetime();

        await InputEndStop.Watch(new Breaking(), lifetime).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(1, lifetime.Stops);
    }

    /// <summary>Unasked, nothing watches, and standard input is never so much as opened.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    public void A_host_not_asked_starts_no_watcher_and_never_opens_its_input(string? value)
    {
        var lifetime = new Lifetime();

        var watching = InputEndStop.WatchWhenAsked(
            value, () => throw new InvalidOperationException("the input was opened"), lifetime);

        Assert.Null(watching);
        Assert.Equal(0, lifetime.Stops);
    }

    [Fact]
    public async Task Asked_it_watches_the_input_it_is_handed()
    {
        var lifetime = new Lifetime();

        var watching = InputEndStop.WatchWhenAsked("1", () => new MemoryStream(), lifetime);

        Assert.NotNull(watching);
        await watching.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, lifetime.Stops);
    }

    /// <summary>
    /// The host itself, as the shell starts it: the real executable in a process of its own, over a
    /// machine of its own. Closing its input stops it cleanly — exit 0, and <c>app.stopped</c> in its
    /// log with the uptime — which is what a kill never let it write.
    /// </summary>
    [Fact]
    public async Task The_real_host_asked_stops_when_its_input_closes_and_writes_app_stopped()
    {
        using var host = await StartHostAsync(asked: true);

        host.Process.StandardInput.Close();

        Assert.True(host.Process.WaitForExit(15_000), "the host did not stop when its input closed");
        Assert.Equal(0, host.Process.ExitCode);
        var lines = HostLogLines();
        Assert.Contains(lines, line => line.Contains("\"event\":\"app.started\"", StringComparison.Ordinal));
        var stopped = Assert.Single(lines, line => line.Contains("\"event\":\"app.stopped\"", StringComparison.Ordinal));
        Assert.Contains("\"uptimeSeconds\":", stopped);
    }

    /// <summary>The same host from a terminal, not asked: its input closing is nothing to it.</summary>
    [Fact]
    public async Task The_real_host_not_asked_keeps_serving_when_its_input_closes()
    {
        using var host = await StartHostAsync(asked: false);

        host.Process.StandardInput.Close();

        Assert.False(host.Process.WaitForExit(2_000), "a host nobody asked stopped when its input closed");
        Assert.True(await AnswersAsync(host.Url));
        Assert.DoesNotContain(HostLogLines(), line => line.Contains("\"event\":\"app.stopped\"", StringComparison.Ordinal));
    }

    private string Home => Path.Combine(_scratch, "home");

    private IReadOnlyList<string> HostLogLines()
    {
        var folder = Path.Combine(Home, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return Directory.EnumerateFiles(folder, "*.host.jsonl")
            .SelectMany(path =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            })
            .ToList();
    }

    /// <summary>
    /// The host's own build, beside this test's, started as the shell starts it — input redirected — on a
    /// free loopback port, with every variable it reads set or cleared, and waited for until it answers.
    /// </summary>
    private async Task<HostProcess> StartHostAsync(bool asked)
    {
        var repositories = Path.Combine(_scratch, "repositories");
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(repositories);
        var url = $"http://127.0.0.1:{FreePort()}";

        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _scratch,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "daoris-knowledge-http.dll"));
        foreach (var (name, value) in new Dictionary<string, string?>
                 {
                     [InputEndStop.Variable] = asked ? "1" : null,
                     [DaorisHome.Variable] = Home,
                     [ServiceOptions.DatabaseVariable] = Path.Combine(_scratch, "index", "knowledge.db"),
                     [ServiceOptions.RootVariable] = repositories,
                     [ServiceOptions.ModelVariable] = null,
                     [ServiceOptions.UrlVariable] = null,
                     [Access.ModeVariable] = null,
                     [Access.WorkspaceVariable] = null,
                     [RemoteConfig.UrlVariable] = null,
                     [RemoteConfig.KeyVariable] = null,
                     [RemoteConfig.WorkspaceVariable] = null,
                     [RemoteConfig.PathVariable] = null,
                     [RuleProposalBox.HomeVariable] = null,
                     [IntakeScope.AskVariable] = null,
                     [IntakeScope.SessionVariable] = null,
                     ["DAORIS_WEB_ORIGIN"] = null,
                     ["ASPNETCORE_ENVIRONMENT"] = "Production",
                     ["ASPNETCORE_URLS"] = url,
                 })
        {
            if (value is null) start.Environment.Remove(name);
            else start.Environment[name] = value;
        }

        var host = new HostProcess(Process.Start(start)!, url);
        // Drained, so a host that writes more than a pipe holds is never blocked on its own output.
        host.Process.OutputDataReceived += (_, _) => { };
        host.Process.ErrorDataReceived += (_, _) => { };
        host.Process.BeginOutputReadLine();
        host.Process.BeginErrorReadLine();

        for (var attempt = 0; attempt < 150; attempt++)
        {
            if (await AnswersAsync(url)) return host;
            if (host.Process.HasExited)
            {
                var exit = host.Process.ExitCode;
                host.Dispose();
                throw new InvalidOperationException($"the host exited ({exit}) before it answered");
            }

            await Task.Delay(200);
        }

        host.Dispose();
        throw new TimeoutException($"the host never answered at {url}");
    }

    private static async Task<bool> AnswersAsync(string url)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            using var response = await client.GetAsync($"{url}/api/status");
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    /// <summary>A started host and the address it answers at; killed with its tree if a test leaves it running.</summary>
    private sealed class HostProcess(Process process, string url) : IDisposable
    {
        public Process Process { get; } = process;

        public string Url { get; } = url;

        public void Dispose()
        {
            try
            {
                if (!Process.HasExited) Process.Kill(entireProcessTree: true);
                Process.WaitForExit(5_000);
            }
            catch (InvalidOperationException)
            {
                // Already reaped.
            }

            Process.Dispose();
        }
    }

    /// <summary>What the host's lifetime is asked: how many times it was told to stop.</summary>
    private sealed class Lifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _stopping = new();
        private int _stops;

        public int Stops => Volatile.Read(ref _stops);

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
            Interlocked.Increment(ref _stops);
            _stopping.Cancel();
        }
    }

    /// <summary>An input whose writer went without closing it: the read fails rather than ends.</summary>
    private sealed class Breaking : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("The pipe has been ended.");

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
