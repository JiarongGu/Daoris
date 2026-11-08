using Shenora.Windows;

namespace Daoris.Desktop;

/// <summary>
/// What Daoris decides of a window's taskbar button: which identity an install's windows wear, and the colours of the count on it.
/// The drawing, the shell calls and the writes through the window's property store are the kit's (<see cref="TaskbarOverlay"/>,
/// <see cref="Shenora.Windows.TaskbarIdentity"/>).
/// </summary>
internal static class TaskbarWear
{
    /// <summary>This process's identity, or null in a workspace build, which wears none.</summary>
    private static readonly TaskbarIdentity? Identity = TaskbarIdentity.For(AppContext.BaseDirectory);

    /// <summary>
    /// Have <paramref name="form"/> wear the install's identity for as long as it has a handle, so the taskbar groups it under the
    /// person's pinned Daoris and a pin made from it starts the launcher at the install's root rather than the application in
    /// <c>app/</c>. Nothing in a workspace build, whose window must never join the person's pin.
    /// </summary>
    public static void Wear(Form form)
    {
        if (Identity is not { } identity) return;
        new Shenora.Windows.TaskbarIdentity
        {
            AppId = identity.AppId,
            RelaunchCommand = identity.RelaunchCommand,
            RelaunchDisplayName = identity.RelaunchDisplayName,
            RelaunchIcon = identity.RelaunchIcon,
        }.Wear(form);
    }

    /// <summary>Draw the count in the palette's red, the page's theme's (<c>SET_THEME</c>): the close button's, as the platform's count is.</summary>
    public static TaskbarOverlay Wear(this TaskbarOverlay overlay, ChromePalette palette)
    {
        overlay.Fill = palette.CloseHover;
        overlay.Ink = palette.CloseGlyphHot;
        return overlay;
    }

    /// <summary>
    /// Show <paramref name="count"/> on the button as <see cref="WaitingBadge.Label"/> says it (a digit and a sign, <c>9+</c>), or
    /// nothing at none; <paramref name="description"/> is the screen reader's words.
    /// </summary>
    public static void ShowWaiting(this TaskbarOverlay overlay, int count, string? description) =>
        overlay.Show(WaitingBadge.Label(count), description);
}
