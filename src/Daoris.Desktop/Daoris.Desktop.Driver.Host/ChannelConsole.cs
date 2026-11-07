namespace Daoris.Driver.Host;

/// <summary>
/// An ask's channel from a terminal:<c>daoris ask channel &lt;id&gt; [--to &lt;repository&gt;] [--re
/// &lt;message&gt;] [--ordinary] ["…"]</c>. The words and the doors are the library's (<see cref="ChannelCommand"/>); this wires the local host's
/// service and the exit codes. The ask page's <i>Channel</i> is the other door.
/// </summary>
internal static class ChannelConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        // An agent outside Daoris joins, posts, reads and leaves as the member it is admitted as.
        if (ChannelMemberVerb.Asks(args)) return await MemberAsync(args).ConfigureAwait(false);

        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (ChannelCommand.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"ask: {problem}");
            Console.Error.WriteLine($"usage: daoris {ChannelCommand.Usage}");
            return 2;
        }

        try
        {
            using var service = ServiceClient.FromEnvironment();
            // A post goes through the running Daoris where one answers, confirmed in its window.
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

    private static async Task<int> MemberAsync(string[] args)
    {
        if (ChannelMemberVerb.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"ask: {problem}");
            Console.Error.WriteLine($"usage: daoris {ChannelMemberVerb.Usage}");
            return 2;
        }

        try
        {
            using var service = ServiceClient.FromEnvironment();
            return await ChannelMemberVerb.RunAsync(ask, service, Console.Out, InstanceHost.World("ask")).ConfigureAwait(false);
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
