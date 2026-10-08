using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// KSCHEMA1, at the HTTP host's start: a store a newer Daoris wrote is not opened, so the host serves no route at
/// all, its knowledge routes included, and says why in the refusal's sentence rather than a stack. The store is left
/// as it was, so a newer host already serving it keeps answering.
/// </summary>
public sealed class NewerStoreStartTests : IDisposable
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

    private string Database => Path.Combine(_scratch, "index", "knowledge.db");

    [Fact]
    public async Task The_real_host_over_a_newer_store_does_not_start_and_says_why_in_a_sentence()
    {
        var ahead = await NewerStoreAsync();

        var (exit, said) = await RunAsync();

        // The entry point leaves it unhandled, so the runtime reports it: the report is the refusal's sentence, with no
        // stack after it (the exception's own ToString). The machine log keeps nothing of it, since the entry point's log
        // is closed as the exception leaves it, before the runtime raises it.
        Assert.NotEqual(0, exit);
        Assert.Contains("belongs to a newer Daoris", said);
        Assert.Contains($"its schema is version {ahead}, and this build's is {ahead - 1}", said);
        Assert.Contains("Update the Daoris in", said);
        Assert.DoesNotContain("   at ", said);
        Assert.DoesNotContain(nameof(NewerStoreException), said);
        Assert.Equal(ahead, Version(Database));
    }

    /// <summary>The key console opens the same store, and refuses it the same way: exit 2 and the sentence alone.</summary>
    [Fact]
    public async Task The_key_console_over_a_newer_store_says_why_and_exits_2()
    {
        var ahead = await NewerStoreAsync();

        var (exit, said) = await RunAsync("keys", "list");

        Assert.Equal(2, exit);
        Assert.StartsWith(
            $"'{Path.GetFullPath(Database)}' belongs to a newer Daoris: its schema is version {ahead}, and this build's is {ahead - 1}. ",
            said.Trim());
        Assert.Single(said.Trim().Split('\n'));
        Assert.Equal(ahead, Version(Database));
    }

    /// <summary>A store this build wrote, then stamped one version past it, as only a newer build would leave it.</summary>
    private async Task<int> NewerStoreAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Database)!);
        await using (await SqliteKnowledgeStore.OpenAsync(Database))
        {
        }

        var ahead = Version(Database) + 1;
        Stamp(Database, ahead);
        return ahead;
    }

    /// <summary>
    /// The host's own build, beside this test's, over a machine of its own, every variable it reads set or cleared;
    /// what it said on both streams once it exited. A host that serves instead is killed at the minute, and fails.
    /// </summary>
    private async Task<(int Exit, string Said)> RunAsync(params string[] args)
    {
        var home = Path.Combine(_scratch, "home");
        var repositories = Path.Combine(_scratch, "repositories");
        Directory.CreateDirectory(home);
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
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "daoris-knowledge-http.dll"));
        foreach (var arg in args) start.ArgumentList.Add(arg);
        foreach (var (name, value) in new Dictionary<string, string?>
                 {
                     [Daoris.Knowledge.Http.InputEndStop.Variable] = null,
                     [DaorisHome.Variable] = home,
                     [ServiceOptions.DatabaseVariable] = Database,
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
                     ["ASPNETCORE_URLS"] = url,
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
            Assert.Fail("the host served over a newer store instead of refusing it");
        }

        process.WaitForExit();
        return (process.ExitCode, (await said) + (await printed));
    }

    private static void Stamp(string database, int version)
    {
        SqliteConnection.ClearAllPools();
        using (var connection = new SqliteConnection($"Data Source={database};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA user_version = {version};";
            command.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();
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

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
