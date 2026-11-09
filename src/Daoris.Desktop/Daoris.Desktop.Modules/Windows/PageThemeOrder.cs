namespace Daoris.Desktop;

/// <summary>
/// Which theme a secondary window wears when the OS's changes and when its page speaks: the page's word, once said, is never undone
/// by the OS. The OS's change arrives on the system-events thread and the page's on the window's own, so the page's word is looked
/// at on the window's thread, after the post, where it is ordered with the page's own: a look made before the post would let a
/// <c>SET_THEME</c> landing between the look and the post be overwritten by the OS's theme.
/// </summary>
/// <param name="startsDark">The theme to wear until the page says its own, asked each time.</param>
/// <param name="post">The window's own non-blocking post to its thread.</param>
/// <param name="paint">Paints a theme on the window; called on the window's thread alone.</param>
public sealed class PageThemeOrder(Func<bool> startsDark, Action<Action> post, Action<bool> paint)
{
    private bool _said;
    private bool _dark;

    /// <summary>The theme to wear now: the page's where it has spoken, otherwise the one it starts at. On the window's thread.</summary>
    public bool Current => _said ? _dark : startsDark();

    /// <summary>The page's theme, painted. On the window's thread.</summary>
    public void PageSaid(bool dark)
    {
        _said = true;
        _dark = dark;
        paint(dark);
    }

    /// <summary>The OS's theme changed: from any thread, painted on the window's thread unless the page has said by then.</summary>
    public void SystemChanged() => post(() =>
    {
        if (!_said) paint(startsDark());
    });
}
