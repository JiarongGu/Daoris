using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>The doors the look's passes reach the driver through: its context and settables, its stages' parts, and the starts' run.</summary>
internal sealed class LookDoors(Driver driver) : ILandingWiring, IPluginWiring, ISetupWiring, ILookStagesWiring, IStartsWiring
{
    private LoopContext Context => driver.Context;

    string ILookWiring.Home => Context.Home;

    DriverConfig ILookWiring.Config => Context.Config;

    ServiceClient ILookWiring.Service => Context.Service;

    RemoteSyncSet? ILookWiring.Sync => driver.Sync;

    RunningSessions ILookWiring.Runs => Context.Runs;

    SessionProcesses ILookWiring.Processes => Context.Processes;

    LookSaid ILookWiring.Lines => driver.Lines;

    bool ILookWiring.Beside => Context.Beside;

    Func<string, Noted, bool>? ILookWiring.Stops => driver.Stops;

    HarnessRoster ILookWiring.Harnesses => Context.Harnesses;

    QuestionSupervisorDecisions ILookWiring.Supervisor => Context.Supervisor;

    GoOnMarks ILookWiring.Marks => Context.Marks;

    SessionEvents ILookWiring.Events => Context.Events;

    LookStages ILookWiring.Stages => driver.Stages;

    MachineLog? ILookWiring.Log => driver.Log;

    IReadOnlyList<string> ILookWiring.LookIn(LookSaid said) => driver.LookIn(said);

    LoopContext ILookStagesWiring.Context => Context;

    ReviewDesk? ILookStagesWiring.Reviews => driver.Reviews;

    DriverHandOvers ILookStagesWiring.HandOvers => driver.HandOvers;

    OpinionGateLook ILookStagesWiring.Gate => driver.Gate;

    DriverHelpers ILookStagesWiring.Helpers => driver.Helpers;

    DriverPartners ILookStagesWiring.Partners => driver.Partners;

    DriverBrainstorms ILookStagesWiring.Brainstorms => driver.Brainstorms;

    DriverMeetings ILookStagesWiring.Meetings => driver.Meetings;

    DriverIntakes ILookStagesWiring.Intakes => driver.Intakes;

    LookSync ILookStagesWiring.SyncPass => driver.LookPasses.Of<LookSync>();

    SessionOutput? ILandingWiring.Output => Context.Output;

    HookSet? ILandingWiring.Hooks => Context.Hooks;

    ProviderRegistry IPluginWiring.Built => driver.Built;

    HookSet? IPluginWiring.Hooks => Context.Hooks;

    void IPluginWiring.Adopt(PluginCatalog catalog)
    {
        Context.Adapters = driver.Built.WithPlugins(catalog);
        Context.Servers = catalog.Servers;
        Context.Catalog = catalog;
        Context.Harnesses.Use(Context.Adapters);
        // The accounts are one set (D197): the wiring folded at the come-up, and again at a next look a held lock refused.
        Context.Harnesses.Pool(driver.Log);
    }

    Func<IWorkspaceSetupWorld>? ISetupWiring.SetupPlans => driver.SetupPlans;

    // Set-up refuses a door nobody knows like the window and the terminal; the set-up pass says the refusal and tries the next look.
    bool ISetupWiring.Unadopted() => BuildDoor.Drives(Context.Home, Context.Config, Context.Adapters);

    Func<CancellationToken, Task>? IStartsWiring.Wanted => driver.Wanted;

    Func<Consideration, Action, CancellationToken, Task<StartRun>>? IStartsWiring.Runner => driver.Runner;

    Task<StartRun> IStartsWiring.RunAsync(
        Consideration start, IReadOnlyList<RepoView> registry, List<TrustHold> untrusted, Action onOpened, FreshTree fresh, CancellationToken ct,
        string? movedOff, (string Agent, string Model)? movedOffModel, string? movedFrom) =>
        driver.RunAsync(start, registry, untrusted, onOpened, fresh, ct, movedOff, movedOffModel, movedFrom);

    Task<StartRun> IStartsWiring.RunIntakeAsync(AskView ask, List<TrustHold> untrusted, Action onOpened, CancellationToken ct) =>
        driver.Intakes.RunIntakeAsync(ask, untrusted, onOpened, ct);

    RoleCatalog? IStartsWiring.CatalogueForReport() => driver.Walk.CatalogueForReport();

    ConcurrentDictionary<string, string> IStartsWiring.LimitResumed => driver.LimitResumed;
}
