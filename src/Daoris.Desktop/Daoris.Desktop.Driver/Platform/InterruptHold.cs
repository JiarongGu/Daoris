namespace Daoris.Driver;

/// <summary>A Ctrl+C as a handler sees it: <see cref="Cancel"/> set keeps this process from ending on the press.</summary>
public sealed class InterruptPress
{
    public bool Cancel { get; set; }
}

/// <summary>
/// Ctrl+C held off this process for as long as a child it waits for runs: the press reaches the child too, which ends its command, and
/// this process waits for that end rather than ending first. The hold is taken before the child starts and let go when it ends, so a
/// press outside that span ends this process as it would have.
/// </summary>
public static class InterruptHold
{
    /// <param name="subscribe">
    /// Hands a handler to the signal and returns what removes it: the console's own where none is given, a test's stand-in otherwise,
    /// so the signal can be raised without a real console.
    /// </param>
    public static IDisposable Take(Func<Action<InterruptPress>, IDisposable>? subscribe = null) =>
        (subscribe ?? OnConsole)(press => press.Cancel = true);

    private static IDisposable OnConsole(Action<InterruptPress> handler)
    {
        ConsoleCancelEventHandler onPress = (_, press) =>
        {
            var held = new InterruptPress { Cancel = press.Cancel };
            handler(held);
            press.Cancel = held.Cancel;
        };
        Console.CancelKeyPress += onPress;
        return new Release(() => Console.CancelKeyPress -= onPress);
    }

    private sealed class Release(Action release) : IDisposable
    {
        public void Dispose() => release();
    }
}
