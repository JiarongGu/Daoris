using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// HOSTSTART1: a host whose start threw left nothing of why in its machine log. Its entry point is async, so the log it
/// opened was closed as the exception left the entry point, before the runtime raised it as unhandled, and the
/// <c>error</c> line written for an unhandled exception was dropped on a closed log. A start that throws now writes one
/// <c>error</c> line at <c>start</c>. The real executable in a process of its own, since only a real process raises an
/// exception as unhandled: the in-process host hands it to the test instead.
/// </summary>
public sealed class StartFailureTests : IDisposable
{
    private readonly string _scratch = Path.Combine(
        Path.GetTempPath(), "daoris-hoststart1-" + Guid.NewGuid().ToString("N")[..8]);

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
    /// A store that cannot be opened ends the start before the host is built, where nothing else writes to the log: the
    /// index's folder is named under a file.
    /// </summary>
    [Fact]
    public void A_host_whose_store_cannot_open_writes_why_to_its_log_before_it_exits()
    {
        Directory.CreateDirectory(_scratch);
        var file = Path.Combine(_scratch, "a-file");
        File.WriteAllText(file, "not a folder");

        using var host = RealHost.Launch(_scratch, new Dictionary<string, string?>
        {
            [ServiceOptions.DatabaseVariable] = Path.Combine(file, "index", "knowledge.db"),
        });

        var failed = Failed(host);

        Assert.Equal(typeof(IOException).FullName, failed.GetProperty("type").GetString());
        Assert.Contains(typeof(IOException).FullName!, host.Said);
    }

    /// <summary>A port another program holds ends the start inside the server's own start, before the host serves.</summary>
    [Fact]
    public void A_host_whose_port_is_held_writes_why_to_its_log_before_it_exits()
    {
        var holder = new TcpListener(IPAddress.Loopback, 0);
        holder.Start();
        try
        {
            var port = ((IPEndPoint)holder.LocalEndpoint).Port;
            using var host = RealHost.Launch(_scratch, new Dictionary<string, string?>
            {
                ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}",
            });

            var failed = Failed(host);

            Assert.Equal(typeof(IOException).FullName, failed.GetProperty("type").GetString());
            Assert.Contains($"127.0.0.1:{port}", failed.GetProperty("message").GetString());
        }
        finally
        {
            holder.Stop();
        }
    }

    /// <summary>
    /// Waits for <paramref name="host"/> to exit, which it must, unanswered; then its log's one <c>error</c> line, at
    /// <c>start</c>, with the exception's type, message and stack, and the runtime ending the process.
    /// </summary>
    private static JsonElement Failed(RealHost host)
    {
        Assert.True(host.Process.WaitForExit(60_000), "the host did not exit");
        Assert.NotEqual(0, host.Process.ExitCode);

        var errors = host.LogLines()
            .Select(line => JsonDocument.Parse(line).RootElement)
            .Where(line => line.GetProperty("event").GetString() == "error")
            .ToList();
        var error = Assert.Single(errors);
        Assert.Equal("error", error.GetProperty("level").GetString());
        var data = error.GetProperty("data");
        Assert.Equal("start", data.GetProperty("where").GetString());
        Assert.True(data.GetProperty("terminating").GetBoolean());
        Assert.False(string.IsNullOrEmpty(data.GetProperty("message").GetString()));
        Assert.Contains(data.GetProperty("type").GetString()!, data.GetProperty("stack").GetString());
        return data;
    }
}
