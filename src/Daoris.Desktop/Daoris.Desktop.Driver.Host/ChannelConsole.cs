namespace Daoris.Driver.Host;

/// <summary>
/// An ask's channel from a terminal (SESSONE2c, D167, D50): <c>daoris-driver ask channel &lt;id&gt; [--to &lt;repository&gt;] [--re
/// &lt;message&gt;] [--ordinary] ["…"]</c>. The words and the doors are the library's (<see cref="ChannelCommand"/>); this wires the local host's
/// service and the exit codes. The ask page's <i>Channel</i> is the other door.
/// </summary>
internal static class ChannelConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (ChannelCommand.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"ask: {problem}");
            Console.Error.WriteLine($"usage: daoris-driver {ChannelCommand.Usage}");
            return 2;
        }

        try
        {
            using var service = ServiceClient.FromEnvironment();
            // RPC1e: a post goes through the running Daoris where one answers, confirmed in its window.
            return await ChannelCommand.RunAsync(ask, service, Console.Out, InstanceHost.World("ask")).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            Console.Error.WriteLine($"ask: {error.Message}");
            return 2;
        }
        catch (HttpRequestException error)
        {
            Console.Error.WriteLine($"ask: could not reach the service — {error.Message}");
            return 2;
        }
    }
}
