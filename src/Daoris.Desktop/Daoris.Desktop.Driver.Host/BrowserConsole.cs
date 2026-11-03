namespace Daoris.Driver.Host;

/// <summary>
/// Daoris's browser from a terminal (ONEWAY1ai-i; the oneway design §6 X3): <c>daoris browser favorite|extensions|use|links</c>, on the
/// typed operations Settings' browser section calls (<see cref="BrowserFavorites"/>, <see cref="BrowserSettings"/>), said by
/// <see cref="BrowserWords"/>. The files are the home's; the browser reads them each time it starts.
/// </summary>
internal static class BrowserConsole
{
    private const string Usage =
        "usage: daoris browser favorite list  ·  daoris browser favorite add <address> [--title \"…\"]  ·  daoris browser favorite remove <address>\n"
        + "       daoris browser extensions [offer|refuse]  ·  daoris browser use [daoris|edge]  ·  daoris browser links [system|daoris]";

    public static int Run(string[] args)
    {
        try
        {
            var home = DaorisHome.Resolve() ?? throw new DriverException(DaorisHome.Sentence);
            switch (args)
            {
                case ["favorite", "list"]:
                    return RemoteConsole.Say(BrowserWords.Favorites(home, BrowserFavorites.Read(home)));
                case ["favorite", "add", var address, .. var flags] when !address.StartsWith('-'):
                    var title = Array.IndexOf(flags, "--title") is var at && at >= 0 && at + 1 < flags.Length ? flags[at + 1] : null;
                    Console.WriteLine(BrowserWords.Kept(BrowserFavorites.Add(home, address, title)));
                    return 0;
                case ["favorite", "remove", var address] when !address.StartsWith('-'):
                    Console.WriteLine(BrowserWords.Dropped(address, BrowserFavorites.Remove(home, address)));
                    return 0;
                case [var area] when area is "extensions" or "use" or "links":
                    return RemoteConsole.Say(BrowserWords.Setting(area, BrowserSettings.Read(home)));
                case [var area, var value] when area is "extensions" or "use" or "links":
                    switch (area)
                    {
                        case "extensions": BrowserSettings.SetExtensions(home, value); break;
                        case "use": BrowserSettings.SetBrowser(home, value); break;
                        default: BrowserSettings.SetLinks(home, value); break;
                    }

                    Console.WriteLine(BrowserWords.Set(area, value));
                    return 0;
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
