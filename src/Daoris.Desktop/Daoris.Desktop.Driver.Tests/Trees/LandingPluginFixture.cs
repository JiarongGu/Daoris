using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a landing plugin's tests share (WSR4, D100): a home under the scratch folder, a repository whose <c>origin</c> is a bare one
/// beside it, a branch rule naming the plugin, and the plugin itself, faked in process or a real one that pushes. Two classes take
/// it (PROC1f): <see cref="LandingPluginTests"/>, four at once with the git-only classes, and <see cref="LandingPluginPatienceTests"/>,
/// one at a time.
/// </summary>
/// <remarks>
/// 🔴 <b>Nothing here reaches a network.</b> `origin` is a bare repository under the scratch folder,
/// and the plugin that pushes pushes there.
/// </remarks>
public abstract class LandingPluginFixture : IDisposable
{
    protected const string Id = "example.lands";

    protected LandingPluginFixture()
    {
        Scratch = Path.Combine(RepoRoot(), "_fixtures", "landing-plugin", Guid.NewGuid().ToString("N")[..8]);
        Home = Path.Combine(Scratch, "daoris-home");
        Directory.CreateDirectory(Home);
    }

    protected string Scratch { get; }

    protected string Home { get; }

    public void Dispose()
    {
        // A real plugin's process stands in its folder a moment after it is told to go (FLAKE1).
        for (var attempt = 0; Directory.Exists(Scratch); attempt++)
        {
            try
            {
                Directory.Delete(Scratch, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 20) return;
                Thread.Sleep(250);
            }
        }

        GC.SuppressFinalize(this);
    }

    // ——— a plugin, faked

    private sealed class FakeLander(Func<object, PluginLanding> land) : IHookChannel
    {
        public IReadOnlyList<string> Points => [HookPoints.Land];
        public bool Alive => true;
        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);
        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;
        public Task<PluginLanding> LandAsync(object payload, CancellationToken ct) => Task.FromResult(land(payload));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    protected static PluginLanding Answer(bool pushed, string? pullRequest, string message) => new(Id, pushed, pullRequest, message);

    protected LandingPlugins Plugins(Func<object, PluginLanding> land) =>
        new(Home, start: (_, _, _) => Task.FromResult<IHookChannel>(new FakeLander(land)));

    protected void InstallManifest() => Install("""{ "id": "example.lands", "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": ["work/land"] } }""");

    /// <summary>
    /// A plugin that answers the way a real one does: it records what it was told and whether the
    /// branch was already there, pushes to origin (a bare repository beside the test), and answers.
    /// </summary>
    protected void InstallRealPlugin(bool silent = false)
    {
        InstallManifest();
        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.Plugins,Id, "land.mjs"), $$"""
            import { spawnSync } from 'node:child_process';
            import { mkdirSync, writeFileSync } from 'node:fs';
            import { join } from 'node:path';
            import { createInterface } from 'node:readline';
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const silent = {{(silent ? "true" : "false")}};
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: frame.params.points } });
              } else if (frame.method === 'hook/work/land') {
                if (silent) continue;
                const p = frame.params;
                const existed = spawnSync('git', ['rev-parse', '--verify', '--quiet', 'refs/heads/' + p.branch], { cwd: p.root }).status === 0;
                const data = process.env.DAORIS_PLUGIN_DATA;
                mkdirSync(data, { recursive: true });
                writeFileSync(join(data, 'landed.json'), JSON.stringify({ branchExisted: existed, frame: p }));
                const pushed = spawnSync('git', ['push', '--quiet', '-u', 'origin', p.branch], { cwd: p.root }).status === 0;
                send({ jsonrpc: '2.0', id: frame.id, result: { pushed, pullRequest: 'https://example.test/example-org/engine/pull/7', message: 'pushed it to origin and opened the pull request.' } });
              } else if (frame.method === 'shutdown') {
                process.exit(0);
              }
            }
            """);
    }

    private void Install(string manifest)
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Plugins,Id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), manifest);
    }

    protected void Rule(LandingRule rule, bool workspace = false) =>
        (workspace ? DriverConfig.Empty.WithWorkspaceLanding("aurora", rule) : DriverConfig.Empty.WithLanding("engine", rule))
            .Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));

    /// <summary>A repository whose `origin` is a bare repository beside it — somewhere a push lands without leaving the machine.</summary>
    protected async Task<(string Root, string Origin)> RepositoryWithOriginAsync(string name)
    {
        var origin = Path.Combine(Scratch, $"{name}-origin.git");
        Directory.CreateDirectory(origin);
        await GitAsync(origin, "init", "--quiet", "--bare", "-b", "main");

        var root = Path.Combine(Scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await CommitAsync(root, "README.md", $"# {name}", "first");
        await GitAsync(root, "remote", "add", "origin", origin);
        return (root, origin);
    }

    protected static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }

    protected static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return stdout;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
