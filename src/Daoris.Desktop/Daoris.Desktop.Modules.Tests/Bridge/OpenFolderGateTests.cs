using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The file manager is opened through the start gate, never on the caller's thread (PROCSTART2, UIFREEZE1): the window kit's
/// launcher starts the file manager's process, and the plugins' and the log's Open folder called it in the route's
/// synchronous part, which runs on the window's thread.
/// </summary>
/// <remarks>The launcher is a stand-in that notes the thread it was called on: no process starts.</remarks>
public sealed class OpenFolderGateTests : DriverModuleBridge
{
    // True on the thread making the request, while it makes it: an open seen there was made on the caller's thread.
    [ThreadStatic]
    private static bool t_calling;

    private readonly List<bool> _onCaller = [];

    private void Open(string _)
    {
        lock (_onCaller) _onCaller.Add(t_calling);
    }

    private static Task<Shenora.Core.Ipc.IpcResponse> Calling(Func<Task<Shenora.Core.Ipc.IpcResponse>> call)
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

    [Fact]
    public async Task A_plugins_folder_opens_off_the_callers_thread()
    {
        var module = new DriverModule(Bus, Loop(), Open);

        var answer = await Calling(() => AskAsync(module, "PLUGIN_OPEN_FOLDER", new { which = "plugins" }));

        Assert.True(answer.Success, answer.Error?.Message);
        Assert.False(Assert.Single(_onCaller), "the file manager was opened on the caller's thread");
    }

    [Fact]
    public async Task The_logs_folder_opens_off_the_callers_thread()
    {
        using var log = new MachineLog(Home, "desktop", () => DateTimeOffset.UtcNow);
        var module = new LogModule(log, Open, () => DateTimeOffset.UtcNow);

        var answer = await Calling(() => AskAsync(module, "OPEN_FOLDER"));

        Assert.True(answer.Success, answer.Error?.Message);
        Assert.False(Assert.Single(_onCaller), "the file manager was opened on the caller's thread");
    }
}
