namespace Daoris.Driver.Host;

/// <summary>
/// The room measurement from a terminal (BRAINSTORM1c, D176 point 2, D53): <c>daoris-driver brainstorm measure &lt;agent&gt; [--run]</c>.
/// Without <c>--run</c> it walks the agent's accounts and says which it would take, starting nothing; with it, one probe pass runs on that
/// account, which is a spend, and the evidence is kept. Daoris starts it only on this press, and runs it in this process, which is the one
/// holding the loop's records for the run's length: it spawns the agent as the look's passes do, through the same walk, posture and connector.
/// </summary>
internal static class BrainstormMeasureConsole
{
    public const string Usage =
        "usage: daoris-driver brainstorm measure <agent> [--run]\n"
        + "  Without --run: the account the walk would take for <agent>, and nothing started.\n"
        + "  With --run: ONE pass (one turn, up to 10 minutes) on that account, which spends it; its evidence is kept under\n"
        + "  brainstorms/measure-<agent>/. Run it on a scratch home whose accounts are the ones you name for it.";

    public static async Task<int> RunAsync(string[] args)
    {
        if (args is not [var agent, .. var rest] || rest.Any(each => each != "--run") || rest.Length > 1 || !System.Text.RegularExpressions.Regex.IsMatch(BrainstormFiles.MeasureId(agent), "^[A-Za-z0-9-]{1,32}$"))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var spend = rest.Length == 1;
        var configPath = DriverConfig.ResolvePath();
        var home = DriverConfig.HomeOf(configPath);
        using var service = ServiceClient.FromEnvironment();
        var driver = new Daoris.Driver.Driver(service, DriverConfig.Load(configPath), AdapterSet.Built(), home, usage: new SessionUsage(home));
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, press) =>
        {
            press.Cancel = true;
            stop.Cancel();
        };
        RoomMeasure measure;
        try
        {
            measure = await driver.MeasureRoomAsync(agent, spend, stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("daoris-driver: stopped; nothing is kept of it.");
            return 1;
        }

        if (measure.Verdict == RoomVerdicts.NotRun)
        {
            Console.Out.WriteLine(measure.Code == "dry-run"
                ? $"A measurement of `{agent}` would run on {(measure.Account is { } account ? $"account `{account}`" : "its own sign-in")}. Nothing was started: add --run to spend one pass on it."
                : $"A measurement of `{agent}` could not run here ({measure.Code}): nothing was started and nothing was spent.");
            return measure.Code == "dry-run" ? 0 : 1;
        }

        Console.Out.Write(BrainstormRoomProbe.Markdown(measure));
        return measure.Verdict == RoomVerdicts.Kept ? 0 : 1;
    }
}
