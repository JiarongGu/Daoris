using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// What the driver bridge's domains share beyond the loop (PART10d, D166): the event bus, the file manager and the file picker
/// the host hands <see cref="DriverModule"/>, a standing choice written to <c>driver.json</c>, and the wire the configured
/// adapter's sessions run on. A domain's handler class is handed this where it uses more than the loop, and the loop alone
/// where it does not.
/// </summary>
/// <param name="events">
/// The bus the module answers on, held as well because a domain raises news of its own (D49 §3): a conversation ending is
/// news the page wants without asking.
/// </param>
/// <param name="openFolder">
/// The file manager, for a plugin's and a session's Open folder (PLUGUI1e, SESSUX1d): the window kit's launcher. Null where the
/// host carries none: then nothing opens, and the answer says so.
/// </param>
/// <param name="pickFile">
/// The system's file picker, for a tool's *Browse…* (TOOLS7): the window's dialog. Null where the host carries none, and the
/// answer says so.
/// </param>
public sealed class DriverContext(IEventBus events, DriverLoop loop, OpenFolder? openFolder, PickFile? pickFile)
{
    public IEventBus Events { get; } = events;

    public DriverLoop Loop { get; } = loop;

    public OpenFolder? OpenFolder { get; } = openFolder;

    public PickFile? PickFile { get; } = pickFile;

    /// <summary>
    /// The one harness action running on this machine and the path that starts it (PART10f): Agents' routes and Ask Daoris's
    /// doors share this instance, so they share its one slot.
    /// </summary>
    public AgentActions Actions { get; } = new(events, loop);

    /// <summary>
    /// The reads a page may stop (GITAPP1a-iii, the git-app design P1.6): the module's router opens each shared read's <c>read</c> id
    /// here, and the trees domain's <c>READ_LEAVE</c> cancels one.
    /// </summary>
    public ReadLeaves Reads { get; } = new();

    /// <summary>
    /// The folder holding the install's offers — Daoris's own example plugins, none installed until a press (PLUG9 d, D103).
    /// Null finds them beside the running application, else beside the home; a test names its own, since every test's home
    /// shares one parent. Set by <see cref="DriverModule"/>'s initializer after the handlers are built (PART10e), so a domain
    /// reads <see cref="OffersFolder"/> at each request, never a value copied at build.
    /// </summary>
    public string? Offers { get; set; }

    /// <summary>The offers' folder as it stands now: the one named, else beside the application or the home.</summary>
    public string OffersFolder => Offers ?? PluginOffers.FolderFor(Loop.Home, AppContext.BaseDirectory);

    /// <summary>
    /// The world a set-up press reads (LAYOUT8), over the service a request was handed: null is the real one, which reads each
    /// line with git and starts <c>node</c> and <c>daoris</c> for their versions, as the terminal's press does; a test names a
    /// stand-in, since the fast half starts no process (MOD8). Set by <see cref="DriverModule"/>'s initializer, as
    /// <see cref="Offers"/> is, so a domain reads it at each request.
    /// </summary>
    public Func<ServiceClient, ISetupWorld>? SetupWorlds { get; set; }

    /// <summary>The set-up press's world over <paramref name="service"/>: the one named, else the real one under the home.</summary>
    public ISetupWorld SetupWorldOf(ServiceClient service) => SetupWorlds?.Invoke(service) ?? new SetupWorld(service, Loop.Home);

    /// <summary>
    /// The owner's mark's and a role's act's reads (STEWARDROLE1a-ii, D209 §5.4): a session's quest, an ask's quests, a quest's repository,
    /// a workspace's repositories and a quest's records, from the service the loop holds; a test names a stand-in. Set by
    /// <see cref="DriverModule"/>'s initializer, as <see cref="Offers"/> is.
    /// </summary>
    public IOwnerScopes? OwnerScopes { get; set; }

    /// <summary>The reads the owner's mark makes: the ones named, else the loop's service.</summary>
    public IOwnerScopes OwnerScopesOf() => OwnerScopes ?? (_ownerScopes ??= new ServiceOwnerScopes(() => Loop.Service));

    private IOwnerScopes? _ownerScopes;

    /// <summary>
    /// What the steward's acts read and change (STEWARDROLE1b, D209 §3.1): the bridge's own loop and service; a test names a stand-in,
    /// so no act starts a process. Set by <see cref="DriverModule"/>'s initializer, as <see cref="OwnerScopes"/> is.
    /// </summary>
    public IStewardWorld? StewardWorld { get; set; }

    /// <summary>The steward's acts' world: the one named, else the bridge's own.</summary>
    public IStewardWorld StewardWorldOf() => StewardWorld ?? (_stewardWorld ??= new DriverStewardWorld(this));

    private IStewardWorld? _stewardWorld;

    /// <summary>
    /// What This Daoris's read is handed (WIRING1b), changed: null is the window's own; a test hands the account's and the
    /// machine's variables of its own and sizes of its own, so it reads nothing of the machine it runs on. Set by
    /// <see cref="DriverModule"/>'s initializer, as <see cref="SetupWorlds"/> is.
    /// </summary>
    public Func<WiringWorld, WiringWorld>? WiringWorlds { get; set; }

    /// <summary>
    /// What *Free up space…* is handed (KEEP1e), changed: null is the window's own; a test hands a browser host and a clock of its own, so it
    /// clears nothing of the machine it runs on. Set by <see cref="DriverModule"/>'s initializer, as <see cref="WiringWorlds"/> is.
    /// </summary>
    public Func<SpaceFreeWorld, SpaceFreeWorld>? SpaceFreeWorlds { get; set; }

    /// <summary>
    /// This window's wiring world: its process, its account and machine, and what only it knows, from the loop and its
    /// supervisor. 🔴 No status door is asked: the supervisor's last probe is the service's answer (the second opinion's point 3).
    /// </summary>
    public WiringWorld WiringWorldOf()
    {
        var loop = Loop;
        var account = WiringWorld.Stored(EnvironmentVariableTarget.User);
        var (tool, toolFrom, toolWhy) = ToolHomeLeftovers.OwnHome(
            Environment.GetEnvironmentVariable, account, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), loop.Home);
        var world = new WiringWorld
        {
            ToolHome = tool,
            ToolHomeFrom = toolFrom,
            ToolHomeWhy = toolWhy,
            Home = loop.Home,
            BaseDirectory = AppContext.BaseDirectory,
            Process = Environment.GetEnvironmentVariable,
            Account = account,
            Machine = WiringWorld.Stored(EnvironmentVariableTarget.Machine),
            Window = new WindowWiring
            {
                Held = loop.Host.Held,
                HomeAccount = loop.HomeAccount,
                HomeSetByDaoris = loop.HomeSetByDaoris,
                HomeNotice = loop.HomeNotice,
                HostNotice = loop.HostNotice,
                ServiceUrl = loop.ServiceUrl,
                Status = loop.Host.Last,
                Proved = loop.Host.Proved,
                Kit = SystemWiring.VersionOf(typeof(Shenora.Core.Ipc.IpcRequest).Assembly),
                Program = SystemWiring.VersionOf(System.Reflection.Assembly.GetEntryAssembly()),
                Looked = loop.Look.At,
                Ready = loop.Service is not null,
                Draining = loop.Draining?.Invoke(),
                ProcessId = Environment.ProcessId,
            },
        };
        return WiringWorlds?.Invoke(world) ?? world;
    }

    /// <summary>
    /// A standing choice: an edit to <c>driver.json</c> under the home and a nudge, because the file is the truth and the loop
    /// re-reads it every tick, the same file the terminal's verbs edit (D50).
    /// </summary>
    public void Change(Func<DriverConfig, DriverConfig> change)
    {
        change(DriverConfig.Load(Loop.ConfigPath)).Save(Loop.ConfigPath);
        Loop.Nudge();
    }

    /// <summary>The configured adapter's wire, as the loop plans by it (D70); the pipe, the stricter door, when it names none this build has.</summary>
    public SessionWire Door(DriverConfig config) => DoorOf(Loop, config);

    /// <summary>
    /// <see cref="Door"/> for an owner with a loop and no context: the groups' pushed slice takes the look <c>SESSION_GROUPS</c> takes
    /// (PUSH1c3a), so both read one door.
    /// </summary>
    public static SessionWire DoorOf(DriverLoop loop, DriverConfig config)
    {
        try
        {
            return loop.Harnesses.Adapters.Resolve(config.Adapter).Wire;
        }
        catch (DriverException)
        {
            return SessionWire.Pipe;
        }
    }
}
