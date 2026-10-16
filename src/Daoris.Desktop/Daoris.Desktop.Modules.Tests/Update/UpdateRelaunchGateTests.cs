using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// UPDATERELAUNCH1 (UIFREEZE1, D139 §5): *Update now* relaunches through the start gate, off the caller's thread. The
/// update's <c>SET</c> looked, applied and started the launcher synchronously, in the route's synchronous part, which runs
/// on the window's thread.
/// </summary>
public sealed class UpdateRelaunchGateTests : Bridge
{
    // True on the thread making the request, while it makes it: a start seen there was made on the caller's thread.
    [ThreadStatic]
    private static bool t_calling;

    private readonly string _install;

    private readonly List<(ProcessStartInfo Start, bool OnCaller)> _seen = [];

    private int _closed;

    public UpdateRelaunchGateTests()
    {
        _install = Path.Combine(Home, "install");
        Write("INSTALLED.md", "# Daoris — installed desktop\n");
        Write("Daoris.exe", "old launcher");
        Write("app/Daoris.Desktop.exe", "old application");
    }

    private string DataHome => Path.Combine(_install, "data");

    private static Task<IpcResponse> Calling(Func<Task<IpcResponse>> call)
    {
        t_calling = true;
        try
        {
            return call();
        }
        finally
        {
            t_calling = false;
        }
    }

    /// <summary>
    /// The application's own relaunch, <see cref="UpdateLauncher"/>, its start made by the gate's stand-in
    /// (<see cref="ProcessStarts.Within"/>), which answers with a process never started: nothing starts.
    /// </summary>
    [Fact]
    public async Task Update_now_relaunches_off_the_callers_thread()
    {
        Stage("b1");
        using var updater = new InstallUpdater(
            _install, DataHome, MachineLog.None, () => new UpdateWork(1, 0),
            relaunch: ct => UpdateLauncher.StartAsync(_install, 4242, ct),
            close: () => _closed++, bus: Bus, pid: 1);
        using var within = ProcessStarts.Within(info =>
        {
            lock (_seen) _seen.Add((info, t_calling));
            return new Process();
        });

        var answer = await Calling(() => AskAsync(new UpdateModule(updater), "SET", new { mode = "now" }));

        Assert.True(answer.Success, answer.Error?.Message);
        var (start, onCaller) = Assert.Single(_seen);
        Assert.False(onCaller, "the launcher was started on the caller's thread");
        Assert.Equal(Path.Combine(_install, StagedBuild.Launcher), start.FileName);
        Assert.Equal(["--update", "--after", "4242"], start.ArgumentList);
        Assert.True(start.UseShellExecute);
        Assert.Equal(UpdateStates.Applying, updater.State.State);
        Assert.Equal(1, _closed);
    }

    /// <summary>A build staged as the publish stages one, consistent with its manifest, so it passes the check.</summary>
    private void Stage(string id)
    {
        var staged = Path.Combine(_install, "update", "staged");
        Write("update/staged/INSTALLED.md", "# Daoris — installed desktop\nnew\n");
        Write("update/staged/Daoris.exe", "new launcher");
        Write("update/staged/app/Daoris.Desktop.exe", "new application");
        Write("update/staged/app/Daoris.Desktop.App.dll", "new library");
        var files = new JsonArray();
        foreach (var full in Directory.EnumerateFiles(staged, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            files.Add(new JsonObject
            {
                ["path"] = Path.GetRelativePath(staged, full).Replace(Path.DirectorySeparatorChar, '/'),
                ["size"] = new FileInfo(full).Length,
                ["sha256"] = StagedBuild.Sha256Of(full),
            });
        }

        File.WriteAllText(Path.Combine(staged, "build.json"), new JsonObject
        {
            ["schema"] = 1, ["id"] = id, ["version"] = "0.0.1", ["commit"] = "abc1234", ["at"] = "2026-10-03T12:00:00Z", ["files"] = files,
        }.ToJsonString());
    }

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_install, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
