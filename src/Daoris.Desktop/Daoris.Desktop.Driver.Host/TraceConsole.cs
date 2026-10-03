namespace Daoris.Driver.Host;

/// <summary>
/// One read from a commit, a session or a quest back to its ask, from a terminal (TRACE1, D143, D50): <c>daoris-driver
/// trace</c>. The words are the library's (<see cref="TraceCommand"/>); this wires the real world: the service, the home and
/// the person's choices. It writes nothing: the host routes it before the machine log opens, since that open prunes old files.
/// </summary>
internal static class TraceConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (TraceCommand.Read(args, out var problem) is not { } asked)
        {
            Console.Error.WriteLine($"trace: {problem}");
            Console.Error.WriteLine(TraceCommand.Usage);
            return 2;
        }

        // Its own catch, since it runs before the host's: a missing home or service is exit 2 and a sentence.
        try
        {
            var configPath = DriverConfig.ResolvePath();
            using var service = ServiceClient.FromEnvironment();
            return await TraceCommand.RunAsync(
                asked, new TraceSources(service, DriverConfig.HomeOf(configPath), DriverConfig.Load(configPath)), Console.Out).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or System.Text.Json.JsonException or IOException)
        {
            Console.Error.WriteLine($"trace: {error.Message}");
            return 2;
        }
    }
}
