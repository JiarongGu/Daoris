using Microsoft.Extensions.DependencyInjection;
using Shenora.Core.Shell;

namespace Daoris.Desktop;

/// <summary>
/// The hand-over of a later launch to <see cref="SecondLaunch"/> (ONEINSTANCE1, D171 point 2), as the application gives it to
/// the kit's single-instance guard (<c>WindowsHostOptions.SingleInstance</c>): the options themselves, so what the kit runs is
/// what the tests run (ONEINSTANCE2).
/// </summary>
/// <remarks>
/// <para><b>Nothing silent</b> (ONEINSTANCE2). A later launch of the installed Daoris opened no window and wrote no line, and the
/// hand-over had been a discarded task: a resolution that threw went into the kit's callback, whose report went to a logger the
/// container never made, and a window that failed faulted a task nobody watched. So the launch's arrival is written before
/// anything is resolved or awaited (<c>window.launch</c>), and whatever the hand-over throws is written where it happened (the
/// <c>error</c> event, <see cref="Where"/>), never thrown into the kit's callback.</para>
///
/// <para>Called on the window's thread, after the kit has brought the first main window forward; the hand-over runs on it up
/// to its first wait, so <see cref="SecondLaunch.OpenAsync"/> still reads that bringing forward before anything else
/// (MAINWINDOWS2).</para>
/// </remarks>
public static class LaterLaunch
{
    /// <summary>The <c>where</c> of the <c>error</c> event a failed hand-over writes.</summary>
    public const string Where = "a later launch";

    /// <summary>
    /// The kit's single-instance options with the hand-over as its <see cref="SingleInstanceHostOptions.OnActivated"/>: each
    /// later launch is written as it arrives, then handed to the container's <see cref="SecondLaunch"/>.
    /// </summary>
    /// <param name="log">The shell's machine log.</param>
    public static SingleInstanceHostOptions Options(Daoris.Driver.MachineLog log) => new()
    {
        // The observation is discarded, never the hand-over: it completes once the hand-over has, and never faults.
        OnActivated = (running, launch) =>
            _ = HandOver(launch.Arguments, () => running.Services.GetRequiredService<SecondLaunch>(), log),
    };

    /// <summary>
    /// Write the launch's arrival, then hand it to the <see cref="SecondLaunch"/> <paramref name="resolve"/> answers, writing
    /// anything that throws on the way.
    /// </summary>
    /// <returns>The hand-over's observation, which never faults.</returns>
    public static Task HandOver(IReadOnlyList<string> arguments, Func<SecondLaunch> resolve, Daoris.Driver.MachineLog log)
    {
        // How many arguments it carried, never them: a native launcher's flags name this machine's folders.
        log.Info("window.launch", ("arguments", arguments.Count));
        return log.Observe(OpenAsync(), Where);

        // Async, so a resolution that throws faults the task the log observes rather than the kit's callback, and synchronous
        // up to the hand-over's first wait.
        async Task OpenAsync() => await resolve().OpenAsync(arguments).ConfigureAwait(false);
    }
}
