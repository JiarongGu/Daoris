namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver workflow run --session|--quest|--ask &lt;id&gt;</c> (WORKFLOW1c2; D157's WORKFLOW1c2 note, the workflow design
/// §5.2, §7, D50): where a piece of work stands in the workflow it follows, the terminal's twin of the session's <i>Workflow</i>
/// view. It prints what <see cref="WorkflowRunReader"/> reads, the screen's read too, in <see cref="WorkflowRunWords"/>. In the
/// library, so its words are held by a test.
/// </summary>
/// <remarks>
/// <para><b>Reads only.</b> The reader writes nothing anywhere, and the host routes the verb before it opens its machine log,
/// whose open prunes old files, as it routes <c>trace</c>.</para>
///
/// <para>Exit codes are the family's: 0 a run printed · 1 none: the id names nothing here, or what no workflow runs for (a chat,
/// in the driver's sentence), or nothing of the work is published yet · 2 could not: the usage, or the service did not answer.</para>
/// </remarks>
public static class WorkflowRunCommand
{
    public const string Usage =
        """
        usage: daoris-driver workflow run --session <id> | --quest <id> | --ask <id>
               where a piece of work stands in the workflow it follows, as the session's Workflow view draws it: each step of
               its repository's workflow with its state and what that state says, the step the run stands at marked ●. A
               session's run is its quest's; an ask's, each repository its chains reach. Reads only.
        """;

    /// <summary>The flags that name whose run is read, each with its id.</summary>
    private static readonly string[] Flags = ["--session", "--quest", "--ask"];

    /// <summary>What the words ask, or null with what is wrong with them.</summary>
    /// <param name="args">The words after <c>workflow</c>.</param>
    public static WorkflowRunAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args is ["run", var flag, var id] && Flags.Contains(flag) && id.Trim().Length > 0)
        {
            return flag switch
            {
                "--session" => new WorkflowRunAsk(Session: id.Trim()),
                "--quest" => new WorkflowRunAsk(Quest: id.Trim()),
                _ => new WorkflowRunAsk(Ask: id.Trim()),
            };
        }

        problem = args is ["run", ..]
            ? "`workflow run` reads one run: name its session, its quest or its ask, one of them (`workflow run --quest <id>`)."
            : "`workflow` reads a run: `workflow run --session <id> | --quest <id> | --ask <id>`.";
        return null;
    }

    /// <summary>
    /// Where a run is read on this machine, as the screen's <c>WORKFLOW_RUN</c> reads it: the person's choices in
    /// <paramref name="configPath"/>, its home, the plugins beside it switched on and sound, and each repository's workspace as
    /// the service's registry holds it (one it does not hold is in none).
    /// </summary>
    public static async Task<WorkflowRunSources> SourcesAsync(ServiceClient service, string configPath, CancellationToken ct = default)
    {
        var home = DriverConfig.HomeOf(configPath);
        var config = DriverConfig.Load(configPath);
        var plugins = WorkflowCurrent.PluginsOf(PluginCatalog.Load(home, AdapterSet.Built().Names));
        var registry = await service.RegistryAsync(ct).ConfigureAwait(false);
        string? WorkspaceOf(string repository) => registry
            .FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Workspace;
        return new WorkflowRunSources(service, home, config, plugins, WorkspaceOf);
    }

    /// <summary>Read the runs (<see cref="WorkflowRunReader.ReadAsync"/>, the screen's read too) and print them in the driver's words.</summary>
    public static async Task<int> RunAsync(WorkflowRunAsk asked, WorkflowRunSources sources, TextWriter output, CancellationToken ct = default)
    {
        var read = await WorkflowRunReader.ReadAsync(asked, sources, ct).ConfigureAwait(false);
        if (read.Runs.Count == 0)
        {
            output.WriteLine($"workflow: {read.Problem ?? "nothing runs here yet: no quest of this work is published."}");
            return read.Unanswered ? 2 : 1;
        }

        output.Write(WorkflowRunWords.Say(read.Runs, asked.Session));
        return 0;
    }
}
