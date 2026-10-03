namespace Daoris.Driver.Host;

/// <summary>
/// The branch list from a terminal (GIT1c, D147 §3.3, D50): <c>daoris-driver git branches</c>. The words are the library's
/// (<see cref="GitBranchesCommand"/>); this wires the real world: the registry's checkouts, the session records, the home
/// and the person's choices. It writes nothing: the host routes it before the machine log opens, since that open prunes old
/// files.
/// </summary>
internal static class GitConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (GitBranchesCommand.Read(args, out var problem) is not { } asked)
        {
            Console.Error.WriteLine($"git: {problem}");
            Console.Error.WriteLine(GitBranchesCommand.Usage);
            return 2;
        }

        // Its own catch, since it runs before the host's: a missing home or service is exit 2 and a sentence.
        try
        {
            var configPath = DriverConfig.ResolvePath();
            using var service = ServiceClient.FromEnvironment();
            var registry = await service.RegistryAsync().ConfigureAwait(false);

            // The records only name session branches: a list without them still lists every branch, and says why once.
            IReadOnlyList<SessionRecord>? sessions = null;
            string? unread = null;
            try
            {
                sessions = await service.SessionRecordsAsync().ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
            {
                unread = error.Message;
            }

            var sources = new GitBranchesSources(registry, DriverConfig.Load(configPath), DriverConfig.HomeOf(configPath))
            {
                Sessions = sessions, SessionsUnread = unread,
            };
            return await GitBranchesCommand.RunAsync(asked, sources, Console.Out).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException)
        {
            Console.Error.WriteLine($"git: {error.Message}");
            return 2;
        }
    }
}
