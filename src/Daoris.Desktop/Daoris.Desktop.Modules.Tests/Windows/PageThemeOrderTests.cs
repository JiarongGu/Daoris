namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A page's theme that lands between the OS's change and its post to the window is the one worn: the OS's change decides on the
/// window's thread, after the page's word is in. The window's thread is a queue the test drains by hand, so the ordering is
/// proved over the post and no window is opened.
/// </summary>
public sealed class PageThemeOrderTests
{
    private readonly Queue<Action> _thread = new();
    private readonly List<bool> _painted = [];
    private bool _osDark;

    private PageThemeOrder Order() => new(() => _osDark, _thread.Enqueue, _painted.Add);

    private void Drain()
    {
        while (_thread.Count > 0) _thread.Dequeue()();
    }

    [Fact]
    public void A_page_that_speaks_between_the_os_change_and_its_post_is_not_overwritten()
    {
        var order = Order();
        _osDark = true;
        order.SystemChanged();
        order.PageSaid(dark: false);
        Drain();

        Assert.Equal([false], _painted);
        Assert.False(order.Current);
    }

    [Fact]
    public void Before_the_page_speaks_the_os_change_is_painted()
    {
        var order = Order();
        _osDark = true;
        order.SystemChanged();
        Drain();

        Assert.Equal([true], _painted);
        Assert.True(order.Current);
    }

    [Fact]
    public void After_the_page_spoke_the_os_change_paints_nothing()
    {
        var order = Order();
        order.PageSaid(dark: true);
        _osDark = false;
        order.SystemChanged();
        Drain();

        Assert.Equal([true], _painted);
    }
}
