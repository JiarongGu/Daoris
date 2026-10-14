using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A Daoris route runs on the window's thread until its first wait, and never after it (UIFREEZE1): the dispatcher's
/// middleware (the kit's <c>UseMessageDispatcher</c>, after <see cref="UiStallWatch"/>) that hands a route no
/// synchronization context, so each of its awaits goes on on the thread pool.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> The bridge dispatches each page request on the window's thread, and the kit's pipeline keeps the
/// caller's context, so a route's every await came back to that thread. A sweep's look, once its service answer came back
/// there, ran its whole git fan-out from it, and a process start waits on .NET's one process-start lock: the window was
/// frozen for minutes while 51 threads queued on the lock behind a slow <c>CreateProcess</c>.</para>
///
/// <para><b>The synchronous part stays where it was.</b> The context is taken away only for the call, on the same thread,
/// and put back before the call returns: so the route's part before its first wait still runs inside the stall watch's
/// call, timed and named, and the kit's pipeline outside this one still resumes on the window's thread to answer the page.</para>
///
/// <para><b>Daoris's modules only.</b> The kit's own modules (the window's commands, mapped late from the form) move the
/// form and are resumed on its thread by design; and a Daoris route that needs the window hands its work there itself
/// (the folder and file pickers <c>Invoke</c>, a secondary window is <c>BeginInvoke</c>d by its host).</para>
/// </remarks>
public static class OffTheWindow
{
    /// <summary>The prefix every module of Daoris's own is named under.</summary>
    public const string Prefix = "DAORIS.";

    /// <summary>The middleware: a Daoris route's call runs with no synchronization context, which is put back after it.</summary>
    public static MessageMiddleware Middleware => (request, next, _) =>
    {
        var window = SynchronizationContext.Current;
        if (window is null || !Leaves(request.Module)) return next();

        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            // Not awaited here: only the call is run without the window's context; the answer goes back through the kit's
            // pipeline, which awaited this call under that context.
            return next();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(window);
        }
    };

    /// <summary>Whether a request to <paramref name="module"/> goes on off the window's thread: a module of Daoris's own.</summary>
    public static bool Leaves(string? module) =>
        module is not null && module.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) && module.Length > Prefix.Length;
}
