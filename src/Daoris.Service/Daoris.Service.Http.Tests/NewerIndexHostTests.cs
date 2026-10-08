using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// KSCHEMA1, at the HTTP host: over an index a newer Daoris wrote, the host starts and serves what is not derived, and
/// every route over the index answers the refusal, 409 in the house's error shape, rather than HOSTLOG1's sentence for
/// an error nobody expected. The answer names no machine path, the index is left as it was, and the host says so once in
/// its log. 409 and not 503: nothing about it passes by waiting, so it is a refusal a caller shows, as every other 409
/// here is, never a busy host to try again.
/// </summary>
public sealed class NewerIndexHostTests : IDisposable
{
    private readonly string _scratch = Path.Combine(
        Path.GetTempPath(), "daoris-kschema1-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
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

    [Fact]
    public async Task The_host_over_a_newer_index_starts_and_its_index_routes_answer_the_refusal()
    {
        var ahead = 0;
        using var host = new DaorisHost(ServiceMode.Local, seed: repositories =>
            ahead = NewerIndex(Path.Combine(Path.GetDirectoryName(repositories)!, "index", "knowledge.db")));
        var sentence = host.Composed.IndexRefusal!.Message;

        Assert.Equal(200, (await host.GetAsync("/api/status")).Status);
        Assert.Equal(200, (await host.GetAsync("/api/registry")).Status);
        Assert.Equal(200, (await host.GetAsync("/api/quests")).Status);
        Assert.Equal(200, (await host.GetAsync("/api/sessions")).Status);

        foreach (var (method, path) in new[]
                 {
                     ("GET", "/api/search?q=lesson"),
                     ("GET", "/api/repositories"),
                     ("GET", "/api/entry?id=anything"),
                     ("GET", "/api/entries?repository=alpha"),
                     ("GET", "/api/convergence"),
                     ("POST", "/api/refresh"),
                     ("DELETE", "/api/registry/alpha"),
                 })
        {
            var answer = await host.SendAsync(method, path, DaorisHost.Loopback, json: method == "POST" ? "{}" : null);
            Assert.True(answer.Status == 409, $"{method} {path} answered {answer.Status}: {answer.Body}");
            Assert.Equal(sentence, answer.Error);
            Assert.Contains($"its schema is version {ahead}, and this build's is {ahead - 1}", answer.Error);
            Assert.DoesNotContain(host.Scratch, answer.Body, StringComparison.OrdinalIgnoreCase);
        }

        var lines = host.LogLines();
        var refused = Assert.Single(lines, line => line.Contains("\"event\":\"index.refused\"", StringComparison.Ordinal));
        Assert.Contains($"\"found\":{ahead}", refused);
        // A refusal is not an error nobody expected (HOSTLOG1): none is written as one, and none counts as failed.
        Assert.DoesNotContain(lines, line => line.Contains("\"event\":\"error\"", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("\"event\":\"request.failed\"", StringComparison.Ordinal));
        Assert.Equal(ahead, Version(host.Database));
    }

    /// <summary>The key console opens the same store: over a newer index it lists the keys as before.</summary>
    [Fact]
    public async Task The_key_console_over_a_newer_index_works_as_before()
    {
        var database = Path.Combine(_scratch, "index", "knowledge.db");
        var ahead = NewerIndex(database);

        var (exit, said) = await RunAsync(database, "keys", "list");

        Assert.Equal(0, exit);
        Assert.Contains("No keys minted", said);
        Assert.Equal(ahead, Version(database));
    }

    /// <summary>A store this build wrote with one entry, then stamped one version past it, as only a newer build leaves it.</summary>
    private static int NewerIndex(string database)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        var store = SqliteKnowledgeStore.OpenAsync(database).GetAwaiter().GetResult();
        try
        {
            store.ReplaceRepositoryAsync("alpha", [new KnowledgeEntry(
                "alpha", EntryKind.Decision, Provenance.Local, "A lesson", "What was learned.", "docs/DECISIONS.md", "A lesson")])
                .GetAwaiter().GetResult();
        }
        finally
        {
            store.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        var ahead = Version(database) + 1;
        SqliteConnection.ClearAllPools();
        using (var connection = new SqliteConnection($"Data Source={database};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA user_version = {ahead};";
            command.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();
        return ahead;
    }

    private static int Version(string database)
    {
        SqliteConnection.ClearAllPools();
        using var connection = new SqliteConnection($"Data Source={database};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    /// <summary>
    /// The host's own build, beside this test's, over a machine of its own, every variable it reads set or cleared;
    /// what it said on both streams once it exited. One that serves instead is killed at the minute, and fails.
    /// </summary>
    private async Task<(int Exit, string Said)> RunAsync(string database, params string[] args)
    {
        var home = Path.Combine(_scratch, "home");
        var repositories = Path.Combine(_scratch, "repositories");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(repositories);

        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _scratch,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "daoris-knowledge-http.dll"));
        foreach (var arg in args) start.ArgumentList.Add(arg);
        foreach (var (name, value) in new Dictionary<string, string?>
                 {
                     [Daoris.Knowledge.Http.InputEndStop.Variable] = null,
                     [DaorisHome.Variable] = home,
                     [ServiceOptions.DatabaseVariable] = database,
                     [ServiceOptions.RootVariable] = repositories,
                     [ServiceOptions.ModelVariable] = null,
                     [ServiceOptions.UrlVariable] = null,
                     [ServiceOptions.WindowVariable] = null,
                     [ServiceOptions.RepositoryVariable] = null,
                     [ServiceOptions.DocumentsVariable] = null,
                     [ServiceOptions.IndexVariable] = null,
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
                     ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{FreePort()}",
                 })
        {
            if (value is null) start.Environment.Remove(name);
            else start.Environment[name] = value;
        }

        using var process = Process.Start(start)!;
        var said = process.StandardError.ReadToEndAsync();
        var printed = process.StandardOutput.ReadToEndAsync();
        if (!process.WaitForExit(60_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
            Assert.Fail("the key console never ended");
        }

        process.WaitForExit();
        return (process.ExitCode, (await said) + (await printed));
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
