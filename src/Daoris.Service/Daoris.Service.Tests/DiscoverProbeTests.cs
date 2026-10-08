using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Daoris.Service.Tests;

/// <summary>
/// The protocol's discovery probe at the connector (MCPDISCOVER1): a client of the 2026-07-28 revision opens
/// with <c>server/discover</c> (SEP-2575), and this server, which predates the revision, answers method not
/// found, which is how the client learns to fall back to <c>initialize</c>. That answer is the protocol's; the
/// two warnings the SDK wrote beside it into the machine log at every connector's start were not, since a
/// warning there is something a person should look at (<c>docs/2026-09-30-machine-log-design.md</c> §4).
/// </summary>
/// <remarks>
/// Each test runs the host's own logging (<see cref="McpHostLogging"/>) and a server over in-memory streams,
/// sends one request and reads its answer; the machine log is read once the host has stopped.
/// </remarks>
public sealed class DiscoverProbeTests : IDisposable
{
    private const string NotAvailable = "Method '{0}' is not available.";
    private const int MethodNotFound = -32601;

    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-probe-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Capture _capture = new();

    public DiscoverProbeTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_discover_probe_is_answered_method_not_found_and_leaves_no_warning_in_the_machine_log()
    {
        var answer = await AskAsync(DiscoverProbe.Method);

        Assert.Equal(1, (int)answer["id"]!);
        Assert.Equal(MethodNotFound, (int)answer["error"]!["code"]!);
        Assert.Equal(string.Format(NotAvailable, "server/discover"), (string)answer["error"]!["message"]!);
        Assert.DoesNotContain(Lines(), line => line.Contains("\"level\":\"warn\"") || line.Contains("\"level\":\"error\""));
        Assert.DoesNotContain(Lines(), line => line.Contains("server/discover"));

        // Said once, at debug, below every provider's floor: there for a person who lowers it, and nowhere else.
        var said = Assert.Single(_capture.Lines, line => line.Message.Contains("server/discover"));
        Assert.Equal(LogLevel.Debug, said.Level);
        Assert.Equal(typeof(DiscoverProbe).FullName, said.Category);
    }

    [Fact]
    public async Task Another_method_with_no_handler_still_warns_in_the_machine_log()
    {
        var answer = await AskAsync("example/unknown");

        Assert.Equal(MethodNotFound, (int)answer["error"]!["code"]!);
        Assert.Equal(string.Format(NotAvailable, "example/unknown"), (string)answer["error"]!["message"]!);
        var lines = Lines();
        Assert.Contains(lines, line => line.Contains("\"level\":\"warn\",\"event\":\"log\"")
            && line.Contains("\"category\":\"ModelContextProtocol.Server.McpServer\"")
            && line.Contains("received request for method 'example/unknown', but no handler is available"));
        Assert.DoesNotContain(_capture.Lines, line => line.Category == typeof(DiscoverProbe).FullName);
    }

    /// <summary>
    /// What the owner's install showed, and why the filter exists: the SDK in use has no handler of its own for
    /// the probe, so it answers it the same way and warns twice.
    /// </summary>
    /// <remarks>
    /// When an SDK answers the probe itself (a later line registers <c>server/discover</c> and offers the new
    /// revision), this fails. The upgrade that makes it fail removes <see cref="DiscoverProbe"/>, which would
    /// otherwise keep refusing the revision the SDK now speaks, and decides which revision the connector speaks.
    /// </remarks>
    [Fact]
    public async Task Without_the_filter_the_SDK_in_use_gives_the_same_answer_and_two_warnings()
    {
        var answer = await AskAsync(DiscoverProbe.Method, answered: false);

        Assert.Equal(MethodNotFound, (int)answer["error"]!["code"]!);
        Assert.Equal(string.Format(NotAvailable, "server/discover"), (string)answer["error"]!["message"]!);
        var warned = Lines().Where(line => line.Contains("\"level\":\"warn\"") && line.Contains("server/discover")).ToArray();
        Assert.Equal(2, warned.Length);
        Assert.Contains(warned, line => line.Contains("received request for method 'server/discover', but no handler is available"));
        Assert.Contains(warned, line => line.Contains("ModelContextProtocol.McpProtocolException: Method 'server/discover' is not available."));
    }

    /// <summary>The host's own composition calls both, so what the tests above hold is what a session's connector does.</summary>
    [Fact]
    public void The_host_answers_the_probe_and_logs_through_its_own_logging()
    {
        var program = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Mcp", "Program.cs"));

        Assert.Contains(".AnswerDiscoverProbe()", program);
        Assert.Contains("McpHostLogging.Use(builder.Logging, log);", program);
    }

    private async Task<JsonObject> AskAsync(string method, bool answered = true)
    {
        var toServer = new Pipe();
        var fromServer = new Pipe();
        using var log = new MachineLog(_home, "mcp");

        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        McpHostLogging.Use(builder.Logging, log);
        builder.Logging.AddProvider(_capture);
        builder.Logging.AddFilter<Capture>(null, LogLevel.Debug);
        var server = builder.Services
            .AddMcpServer(options => options.ServerInfo = new() { Name = "example-knowledge", Version = "0.0.1" })
            .WithStreamServerTransport(toServer.Reader.AsStream(), fromServer.Writer.AsStream());
        if (answered) server.AnswerDiscoverProbe();

        string? line;
        using (var host = builder.Build())
        {
            await host.StartAsync();
            var request = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = method, ["params"] = new JsonObject() };
            await toServer.Writer.WriteAsync(Encoding.UTF8.GetBytes(request.ToJsonString() + "\n"));
            using var reader = new StreamReader(fromServer.Reader.AsStream());
            line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await toServer.Writer.CompleteAsync();
            await host.StopAsync();
        }

        Assert.NotNull(line);
        return JsonNode.Parse(line)!.AsObject();
    }

    private string[] Lines()
    {
        var folder = Path.Combine(_home, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return Directory.GetFiles(folder).SelectMany(path =>
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }).ToArray();
    }

    /// <summary>Walk up to the workspace root — the tests run from `bin/Debug/net10.0`.</summary>
    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    /// <summary>Every line from debug up, beside the host's own providers, to see what is said below their floor.</summary>
    private sealed class Capture : ILoggerProvider
    {
        public ConcurrentQueue<(string Category, LogLevel Level, string Message)> Lines { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Writer(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class Writer(Capture capture, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                capture.Lines.Enqueue((category, logLevel, formatter(state, exception)));
        }
    }
}
