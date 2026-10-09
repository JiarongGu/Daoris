namespace Daoris.Driver.Host;

/// <summary>
/// Where a piece of work stands in its workflow, from a terminal (WORKFLOW1c2, D157, D50): <c>daoris-driver workflow run</c>, the
/// twin of the session's <i>Workflow</i> view. The words are the library's (<see cref="WorkflowRunCommand"/>); this wires the real
/// world: the service, the home and the person's choices. It writes nothing: the host routes it before the machine log opens,
/// since that open prunes old files. <c>workflow keep</c> (WORKFLOW1f, <see cref="WorkflowKeepCommand"/>) keeps a kind's workflow for
/// a session's work on its run, and writes nothing to the log either.
/// </summary>
internal static class WorkflowConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args is ["keep", ..]) return await KeepAsync(args).ConfigureAwait(false);

        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (WorkflowRunCommand.Read(args, out var problem) is not { } asked)
        {
            Console.Error.WriteLine($"workflow: {problem}");
            Console.Error.WriteLine(WorkflowRunCommand.Usage);
            return 2;
        }

        // Its own catch, since it runs before the host's: a missing home or service, or a driver file that does not read
        // (CONFIGREAD1), is exit 2 and a sentence.
        try
        {
            using var service = ServiceClient.FromEnvironment();
            var sources = await WorkflowRunCommand.SourcesAsync(service, DriverConfig.ResolvePath()).ConfigureAwait(false);
            return await WorkflowRunCommand.RunAsync(asked, sources, Console.Out).ConfigureAwait(false);
        }
        catch (HttpRequestException error)
        {
            // The registry is read before the reader's own reads, which say a service that did not answer for themselves.
            Console.Error.WriteLine($"workflow: could not reach the service — {error.Message}");
            return 2;
        }
        catch (Exception error) when (error is DriverException or System.Text.Json.JsonException or IOException)
        {
            Console.Error.WriteLine($"workflow: {error.Message}");
            return 2;
        }
    }

    /// <summary><c>workflow keep &lt;session&gt; ["…"]</c>: the person's <i>Keep</i>, through the library's press.</summary>
    private static async Task<int> KeepAsync(string[] args)
    {
        if (WorkflowKeepCommand.Read(args, out var problem) is not { } asked)
        {
            Console.Error.WriteLine($"workflow: {problem}");
            Console.Error.WriteLine(WorkflowKeepCommand.Usage);
            return 2;
        }

        try
        {
            using var service = ServiceClient.FromEnvironment();
            return await WorkflowKeepCommand.RunAsync(asked, DriverConfig.HomeOf(DriverConfig.ResolvePath()), service, Console.Out).ConfigureAwait(false);
        }
        catch (HttpRequestException error)
        {
            Console.Error.WriteLine($"workflow: could not reach the service — {error.Message}");
            return 2;
        }
        catch (Exception error) when (error is DriverException or System.Text.Json.JsonException or IOException)
        {
            Console.Error.WriteLine($"workflow: {error.Message}");
            return 2;
        }
    }
}
