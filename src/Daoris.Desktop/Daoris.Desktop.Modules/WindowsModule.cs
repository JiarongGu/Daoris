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
/// a page choosing where a window points and what the shell overwrites. Only the two names this
/// build knows are opened; anything else is refused with a sentence rather than sanitised into
/// something adjacent.</para>
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
    Daoris.Driver.IInAppBrowser? browser = null)
    : ModuleBase(events: events)
{
    public override string ModuleName => "DAORIS.WINDOWS";

    protected override Task<object?> RouteMessageAsync(
        IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
    {
        switch (request.Type)
        {
            // Not a route into the bundle like the others: the browser shows pages that are not
            // Daoris's, in a process of its own that holds no bridge (D78 §3.1, D85). Opening it
            // again brings it forward, as every window here does.
            case "OPEN_BROWSER":
            {
                browser?.Show();
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

            case "OPEN":
            {
                var name = Known(PayloadHelper.GetRequiredValue<string>(request.Payload, "name"));

                // False is not a failure: one window per name is the framework's contract, and the
                // second press of "open the monitor" brings the monitor forward. That is what a
                // person means by pressing it again.
                var opened = windows.Open(name, SecondaryWindow.Address(platform.Url, name));
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
            $"`{name}` is not a window this build opens. The windows are `monitor` and "
            + "`session:<id>` for one session detached.",
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
