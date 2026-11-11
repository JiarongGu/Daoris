using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris browser favorite add|remove</c> and <c>use|links|extensions</c> as the person's presses of the pipe: the typed edit of the home's
/// browser files (<see cref="BrowserPresses"/>), run in this Daoris and confirmed in the window. The window's Settings → Browser is the other
/// door to the same files; its routes answer the screen's state, these the terminal's words.
/// </summary>
public sealed class BrowserPersonRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    [DriverRoute("BROWSER_FAVORITE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("BROWSER_FAVORITE", None = OwnerNone.MachineSetting)]
    [GroundRead("BROWSER_FAVORITE", nameof(PipeReads.BrowserFiles))]
    [NotAlready("BROWSER_FAVORITE", "keeping a page again, or dropping one not kept, is the verb's own answer")]
    [Publishes("BROWSER_FAVORITE", StateScopes.Browser)]
    private Task<object?> FavoriteAsync(BrowserFavoritePress press, CancellationToken cancellationToken) =>
        TerminalSaid.PressAsync(_loop, press.Said, (output, error) => Task.FromResult(BrowserPresses.Favorite(
            _loop.Home, press.Action.Trim(), press.Address.Trim(), string.IsNullOrWhiteSpace(press.Title) ? null : press.Title.Trim(), output, error)));

    [DriverRoute("BROWSER_SETTING", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("BROWSER_SETTING", None = OwnerNone.MachineSetting)]
    [GroundRead("BROWSER_SETTING", nameof(PipeReads.BrowserFiles))]
    [NotAlready("BROWSER_SETTING", "a setting written again is the same file, which the verb says as it does any")]
    [Publishes("BROWSER_SETTING", StateScopes.Browser)]
    private Task<object?> SettingAsync(BrowserSettingPress press, CancellationToken cancellationToken) =>
        TerminalSaid.PressAsync(_loop, press.Said, (output, error) => Task.FromResult(BrowserPresses.Setting(
            _loop.Home, press.Setting.Trim(), press.Value.Trim(), output, error)));
}
