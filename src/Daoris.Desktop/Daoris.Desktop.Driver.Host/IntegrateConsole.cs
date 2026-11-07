namespace Daoris.Driver.Host;

/// <summary>
/// A repository's integration from a terminal:<c>daoris integrate &lt;repository&gt;</c> reads the record. The words are the
/// library's (<see cref="IntegrateCommand"/>); an act is the owner's card in the running window, pressed over the pipe and waited on.
/// </summary>
internal static class IntegrateConsole
{
    public static async Task<int> RunAsync(string[] args, CancellationToken closing)
    {
        if (IntegrateCommand.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"integrate: {problem}");
            Console.Error.WriteLine(IntegrateCommand.Usage);
            return 2;
        }

        if (ask.Plan is { } branch) return await PlanAsync(ask.Repository, branch, closing).ConfigureAwait(false);
        if (ask.Act is not null)
        {
            try
            {
                return await IntegrateCommand.PressAsync(ask, InstanceHost.World(DaorisHome.Resolve(), "integrate"), closing).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (closing.IsCancellationRequested)
            {
                await Console.Error.WriteLineAsync(InstanceDoor.StoppedAtCtrlC).ConfigureAwait(false);
                return 2;
            }
        }

        if (DaorisHome.Resolve() is not { } home)
        {
            Console.Error.WriteLine("integrate: the integration is kept under Daoris's home, and none is named here: run the installed daoris, or name the home with DAORIS_HOME.");
            return 2;
        }

        foreach (var line in IntegrateCommand.Said(IntegrationLooks.Of(new IntegrationStore(home), ask.Repository))) Console.Out.WriteLine(line);
        return 0;
    }

    /// <summary>
    /// <c>--plan &lt;branch&gt;</c>: the repository's registered checkout read of the service, its line the rule's or the repository's, and the branch
    /// merged onto it in git's objects alone, quarantined under the home's trees and removed after.
    /// </summary>
    private static async Task<int> PlanAsync(string repository, string branch, CancellationToken closing)
    {
        try
        {
            var configPath = DriverConfig.ResolvePath();
            var home = DriverConfig.HomeOf(configPath);
            var config = DriverConfig.Load(configPath);
            using var service = ServiceClient.FromEnvironment();
            var row = (await service.RegistryAsync(closing).ConfigureAwait(false))
                .FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(each.Root) && Directory.Exists(each.Root));
            if (row is null)
            {
                Console.Error.WriteLine($"integrate: `{repository}` has no checkout here, so there is nothing to preview.");
                return 1;
            }

            var line = config.LinePolicyOf(repository, row.Workspace)?.Resolved().Line
                       ?? (await CanonicalLine.ResolveAsync(row.Root!, repository, row.Workspace, config, closing).ConfigureAwait(false)).Branch;
            if (line is null)
            {
                Console.Error.WriteLine($"integrate: `{repository}` has no line here (none is set and git names none), so there is nothing to preview onto.");
                return 1;
            }

            var scratch = Path.Combine(DaorisHome.Entry(home, HomeEntry.Trees), "integration-plans");
            var plan = await IntegrationPlans.PlanAsync(repository, row.Root!, line, branch, scratch, closing).ConfigureAwait(false);
            foreach (var said in IntegrationPlans.Said(plan)) Console.Out.WriteLine(said);
            return plan.Problem is null ? 0 : 1;
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException)
        {
            Console.Error.WriteLine($"integrate: {error.Message}");
            return 2;
        }
    }
}
