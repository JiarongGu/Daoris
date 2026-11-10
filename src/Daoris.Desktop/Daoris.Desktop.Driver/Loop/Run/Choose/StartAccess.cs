namespace Daoris.Driver;

/// <summary>
/// The access setup the start runs under, resolved once from the file this look read, so its record keeps the one it started under.
/// Its permission prompts name the harness's own mode; a harness with none is held here, before any tree grows or any record opens,
/// never run in its own mode under that label. A resume goes on under the same.
/// </summary>
[RunStage("access", RunPhase.Choose, "home")]
internal sealed class StartAccess(IRunWiring wiring) : IRunStage
{
    public ValueTask<StartRun?> RunAsync(RunRound run, CancellationToken ct)
    {
        var access = AccessRules.Resolve(wiring.Context.Config, run.Quest.To, run.Start.Workspace);
        DrivenAccess permissions;
        try
        {
            permissions = DrivenAccess.For(access, run.Quest.To, wiring.Context.Adapters.Door(run.State.Agent)) with { Workspace = run.Start.Workspace };
        }
        catch (DriverException error)
        {
            return ValueTask.FromResult<StartRun?>(GoneAgentHold.Held(wiring, run) ?? run.Hold(error.Message));
        }

        if (permissions.Refusal is { } unsupported) return ValueTask.FromResult<StartRun?>(run.Hold(unsupported));
        run.State.Access = access;
        run.State.Permissions = permissions;
        return ValueTask.FromResult<StartRun?>(null);
    }
}
