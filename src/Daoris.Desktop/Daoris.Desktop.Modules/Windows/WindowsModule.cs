using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// Opening a window on this machine — the shell's half, since a page cannot open one for itself.
/// </summary>
/// <remarks>
/// <para><b>What this is for</b> (D55 §b): the list of running things should be available while the
/// main window is in Manage, on another screen. That is a <b>monitor</b> window; and one session,
/// detached, is the same idea aimed at a single conversation.</para>
///
/// <para><b>They are routes into the same bundle</b>, not a second frontend — the same bytes a
/// browser gets, with one query parameter saying which window this page is. So a monitor costs the
/// components the Work frame already has, and a change to a session row lands in both windows at
/// once because there is only one of it.</para>
///
/// <para><b>The name is the whole contract, and it is checked here.</b> It becomes the URL the new
/// window navigates to AND the filename its geometry is remembered in, so an unchecked name would be
/// a page choosing where a window points and what the shell overwrites. Only the names this build
/// knows are opened (the monitor, a session, and another main window, ONEINSTANCE1); anything else
/// is refused with a sentence rather than sanitised into something adjacent.</para>
///
/// <para><b>There is no CLOSE route, deliberately.</b> Secondary windows keep their native frame
/// (D55 §b: a monitor is a utility, and OS chrome is what a utility should wear), so the close
/// button is already there and is the one every application has taught people to use. A second door
/// onto it would be a route that exists to be symmetrical.</para>
/// </remarks>
public sealed class WindowsModule(
    IEventBus events, ISecondaryWindows windows, PlatformAddress platform,
    // Daoris's own browser (D78): a window the person opens to sign in and to watch. Null where the
    // host carries none, which is only a test that does not care.
    Daoris.Driver.IInAppBrowser? browser = null,
    // Each main window's taskbar button and title (NOTIFY1b, NOTIFY2c). Null where there is no main window to badge.
    ShowWaiting? badge = null,
    // The workspace each main window's title wears (MAINWINDOWS3b). Null where there is no window to title.
    ShowWorkspace? workspace = null,
    // Which workspace each main window holds, from which a notification's click picks its window (NOTIFY2c). Null where none is kept.
    MainWindowScopes? scopes = null,
    // The words the shell's notifier says each notification in, as a main window's page handed them (NOTIFY2d). Null where none is kept.
    NoticeWords? notices = null)
    : ModuleBase(events: events)
{
    public override string ModuleName => "DAORIS.WINDOWS";

    /// <summary>
    /// The order of the count each main window wears, as its own page told it (SESSBADGE1), by the window's name (NOTIFY2c): each page
    /// orders by its own clock, so one order kept for all refused one window's count for being told before another's. And the lock.
    /// </summary>
    private readonly Lock _badgeGate = new();
    private readonly Dictionary<string, long> _badgeOrders = new(StringComparer.Ordinal);

    protected override Task<object?> RouteMessageAsync(
        IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
    {
        switch (request.Type)
        {
            // Not a route into the bundle like the others: the browser shows pages that are not
            // Daoris's, in a process of its own that holds no bridge (D78 §3.1, D85). Opening it
            // again brings it forward, as every window here does. With a `url`, a link on the page is
            // opened there (BRW7) — only a web page, by the favorites' rule, since whatever it opens
            // is a page agents can drive.
            case "OPEN_BROWSER":
            {
                if (PayloadHelper.GetOptionalValue<string>(request.Payload, "url") is { } url)
                {
                    var address = BrowserFavorites.Address(url)
                        ?? throw Refusals.Because(
                            Refusals.BrowserLinkNotAPage,
                            $"`{url}` is not a web page, so Daoris's browser will not open it.",
                            ("address", url));
                    browser?.Open(address);
                }
                else
                {
                    browser?.Show();
                }

                return Task.FromResult<object?>(State(opened: browser is not null));
            }

            // A window's page telling its own frame the theme it is in (WINDOW2), so its native caption
            // follows the viewer's choice. By name, the same checked name it was opened with.
            case "SET_THEME":
            {
                var name = Known(PayloadHelper.GetRequiredValue<string>(request.Payload, "name"));
                var dark = PayloadHelper.GetRequiredValue<bool>(request.Payload, "dark");
                return Task.FromResult<object?>(new { Applied = windows.SetTheme(name, dark) });
            }

            // A main window's page telling its own frame how much waits on the person in its workspace (NOTIFY1b, NOTIFY2c): its
            // *What needs you*, worn as its taskbar button's overlay and in its title. By name as SET_TITLE is, no name the first
            // main window's; a utility window wears none. A count below none is none, and blank words are none.
            case "SET_BADGE":
            {
                var window = PayloadHelper.GetOptionalValue<string>(request.Payload, "name") is { } named ? Known(named) : MainWindows.First;
                var count = Math.Max(0, PayloadHelper.GetRequiredValue<int>(request.Payload, "count"));
                var description = PayloadHelper.GetOptionalValue<string>(request.Payload, "description");
                if (!MainWindows.IsMain(window)) return Task.FromResult<object?>(new { Applied = false });
                // SESSBADGE1: the page tells each count without waiting, so two may arrive here in either order and the window
                // posts each to its thread as it comes: the title once kept 5 over a band reading 4. Each carries the order its page
                // told it in, kept per window, and one told before the count that window wears is never worn over it; a page that
                // tells none is worn.
                var order = PayloadHelper.GetOptionalValue<long?>(request.Payload, "order");
                lock (_badgeGate)
                {
                    if (order is { } told)
                    {
                        if (_badgeOrders.TryGetValue(window, out var worn) && told <= worn) return Task.FromResult<object?>(new { Applied = false });
                        _badgeOrders[window] = told;
                    }

                    badge?.Invoke(window, count, count > 0 && !string.IsNullOrWhiteSpace(description) ? description : null);
                }

                return Task.FromResult<object?>(new { Applied = badge is not null });
            }

            // A main window's page handing the shell's notifier the notices' words in its language (NOTIFY2d, the notification design
            // §3.3), at its load and at each change of language, the way SET_BADGE takes its description: the notifier has no
            // catalogue, and a balloon in English over a window in 中文 was the defect. `{ words: { key: words } }`, kept whole as told.
            case "SET_NOTICE_WORDS":
            {
                var words = request.Payload is { } payload && payload.ValueKind == System.Text.Json.JsonValueKind.Object
                    && payload.TryGetProperty("words", out var told) ? told : default;
                notices?.Told(words);
                return Task.FromResult<object?>(new { Applied = notices is not null });
            }

            // A main window's page telling its frame its own title (MAINWINDOWS3b), so two windows on two workspaces read apart
            // in the taskbar and Alt-Tab. Only the workspace is taken from it, by `WaitingBadge.Workspace`; the frame keeps the
            // application's name. No name is the first main window, whose page is not opened under one.
            case "SET_TITLE":
            {
                var name = PayloadHelper.GetOptionalValue<string>(request.Payload, "name") is { } named ? Known(named) : null;
                var title = PayloadHelper.GetOptionalValue<string>(request.Payload, "title");
                workspace?.Invoke(name, WaitingBadge.Workspace(title));
                // NOTIFY2c: and the workspace the window holds, as data beside the title, for the registry a notification's click
                // reads; never the title's words. None is every workspace.
                scopes?.Told(name ?? MainWindows.First, PayloadHelper.GetOptionalValue<string>(request.Payload, "workspace"));
                return Task.FromResult<object?>(new { Applied = workspace is not null });
            }

            case "OPEN":
            {
                var name = Known(PayloadHelper.GetRequiredValue<string>(request.Payload, "name"));

                // False is not a failure: one window per name is the framework's contract, and the
                // second press of "open the monitor" brings the monitor forward. That is what a
                // person means by pressing it again.
                // Its page on the engine's app origin, told where the host is (D92).
                var opened = windows.Open(name, DesktopPage.PathFor(platform.Url, name));
                return Task.FromResult<object?>(State(opened));
            }

            default:
                throw UnknownType(request);
        }
    }

    /// <summary>A name this build opens, or the refusal that says which names those are.</summary>
    private static string Known(string name) => SecondaryWindow.IsKnown(name)
        ? name
        : throw Refusals.Because(
            Refusals.WindowUnknown,
            $"`{name}` is not a window this build opens. The windows are `monitor`, "
            + "`session:<id>` for one session detached, and `main:<n>` for another main window.",
            ("name", name));

    /// <summary>
    /// What is open right now — asked of the shell at call time rather than cached, because the
    /// person can close one of these with its own close button and no page would hear about it.
    /// </summary>
    private object State(bool opened) => new
    {
        Opened = opened,
        Windows = windows.Opened,
    };
}
