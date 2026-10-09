namespace Daoris.Driver.Host;

/// <summary>
/// The person's verdict on a review from a terminal (REVIEWENV1c, D154 point 8, D50): <c>daoris-driver quest review &lt;id&gt;
/// reviewed|not-yet|skip ["…"]</c>, and the chain's choice at the gate, <c>off|on|&lt;environment&gt;</c> (REVIEWENV1j). The words are
/// the library's (<see cref="QuestReviewCommand"/>); this wires the real world: the service, this machine's review rule, a <i>not
/// yet</i>'s words to the step's session through <c>sessions say</c>'s own path, and this host's machine log, where the verdict's
/// <c>review.verdict</c> line goes.
/// </summary>
internal static class QuestReviewConsole
{
    public static async Task<int> RunAsync(string[] args, MachineLog log)
    {
        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (QuestReviewCommand.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"daoris-driver: {problem}");
            Console.Error.WriteLine(QuestReviewCommand.Usage);
            return 2;
        }

        var configPath = DriverConfig.ResolvePath();
        var config = DriverConfig.Load(configPath);
        var home = DriverConfig.HomeOf(configPath);
        using var service = ServiceClient.FromEnvironment();
        var sessions = new SessionsWorld(service, home, config, SessionsConsole.Wire(config), log);

        return await QuestReviewCommand.RunAsync(ask, new QuestReviewWorld(service)
        {
            // The rule a set-up's choice is judged against (REVIEWENV1j): the service holds none.
            Config = config,
            // The session's next turn (design §3.4, D137): held at the door of the loop that runs it, or kept on its record.
            Say = (session, words, ct) =>
                SessionsCommand.RunAsync(new SessionsAsk("say") { Ids = [session], Text = words }, sessions, Console.Out, ct),
            Log = line => SessionLog.WriteLanding(log, line),
        }, Console.Out).ConfigureAwait(false);
    }
}
