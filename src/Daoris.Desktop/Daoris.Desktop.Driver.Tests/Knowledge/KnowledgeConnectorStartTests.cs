using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 CONNECTOR2, whole: the host started with the entry's variables ONLY, as Codex starts it, answers `initialize`;
/// the same start without the home exits 2 and answers nothing (the reproduction, pinned). Runs the development build's
/// host, so it needs `Daoris.Service.Mcp` built; it is skipped-by-failure otherwise, with the path in the message.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class KnowledgeConnectorStartTests : IDisposable
{
    private readonly string _root = Path.Combine(
        WorkspaceRoot.Folder, "_fixtures", "connector-start-" + Guid.NewGuid().ToString("N")[..8]);

    /// <remarks>
    /// The host holds its store (`knowledge.db`) until its process has gone, so a delete made the moment it is killed met
    /// the file still open in the full run (FLAKE1, 2026-10-12). The test waits for the exit; a straggling handle still
    /// gets a few tries here, and then the folder is left: the next run's fresh name never collides with it.
    /// </remarks>
    public void Dispose()
    {
        GitSeed.Discard(_root);
    }

    private static string? DevelopmentHost() =>
        KnowledgeConnector.Candidates(null, null, WorkspaceRoot.Folder)
            .Where(path => path.Contains(Path.Combine("Daoris.Service.Mcp", "bin")))
            .FirstOrDefault(File.Exists);

    private static ProcessStartInfo Start(string command, IEnumerable<KeyValuePair<string, string>> variables)
    {
        var info = new ProcessStartInfo(command)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = WorkspaceRoot.Folder,
        };
        info.Environment.Clear();
        foreach (var (name, value) in variables) info.Environment[name] = value;
        return info;
    }

    private const string Initialize =
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"0"}}}""";

    [Fact]
    public async Task The_host_answers_initialize_under_the_entrys_own_variables_alone()
    {
        var host = DevelopmentHost() ?? throw new InvalidOperationException("build src/Daoris.Service/Daoris.Service.Mcp first");
        var home = Directory.CreateDirectory(Path.Combine(_root, "home")).FullName;
        var entry = KnowledgeConnector.Offer(
            host, home, WorkspaceRoot.Folder, new Dictionary<string, string?>());
        Assert.NotNull(entry);

        using var process = Process.Start(Start(entry.Command, entry.Environment))!;
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await process.StandardInput.WriteLineAsync(Initialize);
        await process.StandardInput.FlushAsync();
        var answer = await process.StandardOutput.ReadLineAsync(limit.Token);
        try
        {
            Assert.NotNull(answer);
            Assert.Contains("\"id\":1", answer);
            Assert.Contains("protocolVersion", answer);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            // A kill only asks: the store is let go when the process has gone, and the folder is removed after it.
            using var gone = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(gone.Token);
        }
    }

    [Fact]
    public async Task The_host_started_without_the_home_exits_2_and_answers_nothing()
    {
        var host = DevelopmentHost() ?? throw new InvalidOperationException("build src/Daoris.Service/Daoris.Service.Mcp first");

        using var process = Process.Start(Start(host, []))!;
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var said = await process.StandardError.ReadToEndAsync(limit.Token);
        await process.WaitForExitAsync(limit.Token);

        Assert.Equal(2, process.ExitCode);
        Assert.Contains("DAORIS_HOME", said);
    }
}
