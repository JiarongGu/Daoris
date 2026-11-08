namespace Daoris.Desktop;

/// <summary>A window's own thread, posted to from any other: <see cref="WindowPost"/>'s one guarded post, spelled for a control.</summary>
internal static class ControlPost
{
    /// <summary>
    /// Run <paramref name="work"/> on <paramref name="control"/>'s thread later, never blocking: false where it has no window to run it on, or its
    /// handle went before the post.
    /// </summary>
    public static bool Post(this Control control, Action work) =>
        WindowPost.Run(() => !control.IsDisposed && control.IsHandleCreated, post => control.BeginInvoke(post), work);
}
