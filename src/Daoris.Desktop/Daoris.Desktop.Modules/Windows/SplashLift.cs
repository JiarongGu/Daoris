using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// When the main window's splash comes down (SPLASHDATA1, D56; D22's SHEN3 note): once the page says its first screen
/// has its data, over the kit's <c>CLOSE_SPLASH</c> (<c>WindowCommandOptions.CloseSplash</c>, Shenora 0.21), or at
/// <see cref="Bound"/>, whichever comes first.
/// </summary>
/// <remarks>
/// <para>It came down as the page's browser was made, before the bundle had loaded, so the person saw the page's empty
/// frame fill in. The page knows when its screen has what it shows; the window does not, so the page says it.</para>
///
/// <para><b>Never held forever.</b> A page that never says (a screen whose data never comes, a bundle without the call)
/// is lifted at the bound, and the machine log says which lifted it and after how long (<c>splash.lifted</c>, a warning
/// at the bound), so a start that waited is a line a person can find, not a window that seemed slow.</para>
///
/// <para>It lives in this half, not in the window, so it can be tested at all: the window is WinForms and has no test
/// project by construction.</para>
/// </remarks>
public sealed class SplashLift
{
    /// <summary>
    /// The longest the splash waits on the page once the page's browser is made: a local page's first screen takes a
    /// second or two, and past this the person is better served by the screen as it is than by a bar that keeps moving.
    /// </summary>
    public static TimeSpan Bound { get; } = TimeSpan.FromSeconds(10);

    private readonly TaskCompletionSource _said = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Stopwatch _up = Stopwatch.StartNew();
    private readonly StartStages? _stages;

    /// <param name="stages">The start's timer (STARTFAST1), told the page's word and the lift; none by default.</param>
    public SplashLift(StartStages? stages = null) => _stages = stages;

    /// <summary>The page's word: its first screen has its data. From any thread, any number of times.</summary>
    public void PageSaid()
    {
        _stages?.Mark(StartStages.Page);
        _said.TrySetResult();
    }

    /// <summary>
    /// Wait for the page's word or <paramref name="bound"/>, whichever comes first, and write which lifted the splash
    /// into <paramref name="log"/>, with how long the splash had been up. True when the page said.
    /// </summary>
    public async Task<bool> WaitAsync(MachineLog log, TimeSpan bound)
    {
        using var stop = new CancellationTokenSource();
        var first = await Task.WhenAny(_said.Task, Task.Delay(bound, stop.Token)).ConfigureAwait(false);
        await stop.CancelAsync().ConfigureAwait(false);

        var said = first == _said.Task;
        if (said) log.Info("splash.lifted", ("by", "page"), ("ms", _up.ElapsedMilliseconds));
        else log.Warn("splash.lifted", ("by", "bound"), ("ms", _up.ElapsedMilliseconds));
        _stages?.Lifted(said ? "page" : "bound");
        return said;
    }
}
