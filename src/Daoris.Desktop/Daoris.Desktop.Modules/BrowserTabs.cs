namespace Daoris.Desktop;

/// <summary>
/// The tabs of Daoris's own browser (BRW4), as a regular browser orders them: a tab the person asks
/// for goes last, a tab a page opens sits beside that page, either comes to the front, and closing the
/// one in front brings its right neighbour forward, or its left at the end.
/// </summary>
/// <remarks>
/// <para><b>Which tab an agent drives is the agent's.</b> Every tab is a CDP target of its own. Playwright
/// MCP, attached over the browser's endpoint, takes the first page it finds as current, and moves when
/// it opens or selects one (read from its bundle, 0.0.82). Daoris does not choose for it. The window
/// says which tab is in front, and BRW8 says who is driving.</para>
///
/// <para>Ids only: the window holds the pages. Kept apart from WinForms so the order is tested here.</para>
/// </remarks>
public sealed class BrowserTabs
{
    private readonly List<int> _order = [];
    private int _next;

    /// <summary>The tabs, left to right.</summary>
    public IReadOnlyList<int> Order => _order;

    /// <summary>The tab in front, or null when there are none.</summary>
    public int? Front { get; private set; }

    /// <summary>
    /// A new tab, in front: last when the person asked for it, beside <paramref name="opener"/> when a
    /// page opened it.
    /// </summary>
    public int Open(int? opener = null)
    {
        var id = ++_next;
        var beside = opener is { } page ? _order.IndexOf(page) : -1;
        if (beside >= 0) _order.Insert(beside + 1, id);
        else _order.Add(id);
        Front = id;
        return id;
    }

    /// <summary>Bring a tab to the front. One that is not there changes nothing.</summary>
    public void Bring(int id)
    {
        if (_order.Contains(id)) Front = id;
    }

    /// <summary>
    /// Close a tab. False when it was the last, because the window goes with it, as in any browser.
    /// One that is not there changes nothing: a page's close and the person's can race.
    /// </summary>
    public bool Close(int id)
    {
        var at = _order.IndexOf(id);
        if (at < 0) return _order.Count > 0;

        _order.RemoveAt(at);
        if (_order.Count == 0)
        {
            Front = null;
            return false;
        }

        if (Front == id) Front = _order[Math.Min(at, _order.Count - 1)];
        return true;
    }

    /// <summary>The next tab to the right (+1) or left (-1), going round, brought to the front.</summary>
    public int? Step(int delta)
    {
        if (Front is not { } front || _order.Count == 0) return Front;
        var at = (_order.IndexOf(front) + delta % _order.Count + _order.Count) % _order.Count;
        Front = _order[at];
        return Front;
    }

    /// <summary>A tab's name: its page's title, else its address's host, else a new tab.</summary>
    public static string Name(string? title, string? source)
    {
        if (!string.IsNullOrWhiteSpace(title)) return title.Trim();
        return Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.Host.Length > 0
            ? uri.Host
            : "New tab";
    }
}
