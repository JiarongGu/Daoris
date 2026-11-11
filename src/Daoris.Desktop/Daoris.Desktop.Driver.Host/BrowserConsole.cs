namespace Daoris.Driver.Host;

/// <summary>
/// <c>daoris browser favorite|extensions|use|links</c>: the typed operations Settings' browser section calls, pressed where Daoris
/// runs (<see cref="BrowserPresses"/>) and said by <see cref="BrowserWords"/>.
/// </summary>
internal static class BrowserConsole
{
    private const string Usage =
        "usage: daoris browser favorite list  ·  daoris browser favorite add <address> [--title \"…\"]  ·  daoris browser favorite remove <address>\n"
        + "       daoris browser extensions [offer|refuse]  ·  daoris browser use [daoris|edge]  ·  daoris browser links [system|daoris]";

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var home = DaorisHome.Resolve() ?? throw new DriverException(DaorisHome.Sentence);
            var instance = InstanceHost.World("browser");
            switch (args)
            {
                case ["favorite", "list"]:
                    return RemoteConsole.Say(BrowserWords.Favorites(home, BrowserFavorites.Read(home)));
                case ["favorite", "add", var address, .. var flags] when !address.StartsWith('-'):
                    var title = Array.IndexOf(flags, "--title") is var at && at >= 0 && at + 1 < flags.Length ? flags[at + 1] : null;
                    return await BrowserPresses.PressFavoriteAsync(home, "add", address, title, Console.Out, Console.Error, instance).ConfigureAwait(false);
                case ["favorite", "remove", var address] when !address.StartsWith('-'):
                    return await BrowserPresses.PressFavoriteAsync(home, "remove", address, null, Console.Out, Console.Error, instance).ConfigureAwait(false);
                case [var area] when area is "extensions" or "use" or "links":
                    return RemoteConsole.Say(BrowserWords.Setting(area, BrowserSettings.Read(home)));
                case [var area, var value] when area is "extensions" or "use" or "links":
                    return await BrowserPresses.PressSettingAsync(home, area, value, Console.Out, Console.Error, instance).ConfigureAwait(false);
                default:
                    Console.Error.WriteLine(Usage);
                    return 2;
            }
        }
        catch (HomeFileRefusal refused)
        {
            return RemoteConsole.Say(FileSaid.Refused(refused.Message));
        }
        catch (DriverException failed)
        {
            return RemoteConsole.Say(FileSaid.Refused(failed.Message));
        }
    }
}
