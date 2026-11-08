namespace Daoris.Desktop;

/// <summary>
/// The one post to a window's own thread from another: the shell's windows are posted to with <c>BeginInvoke</c>, never a blocking
/// <c>Invoke</c> (a driver loop thread waiting on a window that waits on the loop is a deadlock), from the bus's, a timer's and the
/// IPC thread, each of which may meet a window that has closed.
/// </summary>
/// <remarks>
/// The look and the post are two steps, and the handle can go between them: the framework then throws
/// <see cref="InvalidOperationException"/>, which on a timer's thread nobody observes. It is answered here as a false, as for a
/// window already gone at the look. Only that exception: any other is the caller's defect, said as one.
/// </remarks>
public static class WindowPost
{
    /// <summary>
    /// Hand <paramref name="work"/> to the window through <paramref name="post"/> where <paramref name="alive"/> says it can take it:
    /// false where it cannot, or where its handle went before the post.
    /// </summary>
    /// <param name="alive">The window is not disposed and its handle is created.</param>
    /// <param name="post">The window's own non-blocking <c>BeginInvoke</c>.</param>
    public static bool Run(Func<bool> alive, Action<Action> post, Action work)
    {
        if (!alive()) return false;
        try
        {
            post(work);
            return true;
        }
        catch (InvalidOperationException)
        {
            // The handle went between the look and the post.
            return false;
        }
    }
}
