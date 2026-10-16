using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The launcher an update closes the application for (UPDATE1, D139 §5): started at the install's root with
/// <c>--update --after &lt;this process&gt;</c>, it waits for this application to close, then swaps <c>app/</c>.
/// </summary>
/// <remarks>
/// <para><b>Through the start gate</b> (<see cref="ProcessStarts"/>, UPDATERELAUNCH1): *Update now*'s <c>SET</c> applies before
/// it answers, and the start it made in the route's synchronous part was a start on the window's thread.</para>
/// </remarks>
public static class UpdateLauncher
{
    /// <summary>
    /// Start the launcher at <paramref name="install"/> for <paramref name="pid"/>, once the start gate has a place. False when
    /// it is not there or would not start, and the application stays.
    /// </summary>
    public static async Task<bool> StartAsync(string install, int pid, CancellationToken ct)
    {
        var launcher = Path.Combine(install, StagedBuild.Launcher);
        if (!File.Exists(launcher)) return false;

        // Not the tools' environment (TOOLS5): the launcher is Daoris's own program, and it hands this process's environment,
        // its home included, to the application it starts, which hands every child the tools' environment there.
        // Through the shell (UPDATE1): a start that does not go through it hands the launcher every inheritable handle this
        // process holds, the engine's DevTools socket among them, and the launcher hands them on to the application it starts,
        // so a build started after a swap found its port held by the builds before it. The shell's start inherits none and
        // still hands this process's environment. CreateNoWindow is said as every start says it; the shell's start ignores it,
        // and the launcher is a windowed program with no console to show.
        var start = new ProcessStartInfo(launcher)
        {
            UseShellExecute = true,
            WorkingDirectory = install,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("--update");
        start.ArgumentList.Add("--after");
        start.ArgumentList.Add(pid.ToString(CultureInfo.InvariantCulture));
        try
        {
            using var started = await ProcessStarts.Shared.StartAsync(start, ct).ConfigureAwait(false);
            return started is not null;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
