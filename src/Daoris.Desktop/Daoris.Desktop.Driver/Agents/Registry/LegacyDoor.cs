using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// A contract door presented to today's readers as an adapter (AGENTPROVIDER1 row b; the agent-provider design §11): its transport as
/// the wire, its postures and abilities, its spawn, and its program, account facts and words as the <see cref="HarnessToolchain"/>
/// every reader asks, so a provider written on the contract runs through the driver as the classes did and a provider added later
/// touches no adapter file. It is the one place that turns the contract's records back into the toolchain; row t takes it away with the
/// readers that need it.
/// </summary>
/// <remarks>
/// <para>A door that states no native dialect hands its readers no <see cref="IStreamMapper"/>, so its output is read by the plain
/// capture with no retry, as <see cref="StubAdapter"/> is. A protocol door's <c>Resumes</c> is eligibility to negotiate, which today's
/// default is (<c>Wire == Acp</c>); a door stating the ability absent reads as that default.</para>
/// <para>The facts are read the way <see cref="ProviderRegistration"/> grouped them: a door that gives its provider its id and is keyed
/// by it declares through its provider; any other through its own record and what it is lent.</para>
/// </remarks>
internal sealed class LegacyDoor : ISessionAdapter
{
    private readonly Provider _provider;
    private readonly AgentDoor _door;
    private readonly Lazy<HarnessToolchain?> _toolchain;

    public LegacyDoor(Provider provider, AgentDoor door)
    {
        _provider = provider;
        _door = door;
        _toolchain = new(Declare);
    }

    /// <summary>The adapters of every door of these providers, by name, as <see cref="AdapterSet"/> carries them.</summary>
    public static IEnumerable<KeyValuePair<string, ISessionAdapter>> Over(IEnumerable<Provider> providers) =>
        providers.SelectMany(provider => provider.Doors.Select(door =>
            KeyValuePair.Create(door.Id.Value, (ISessionAdapter)new LegacyDoor(provider, door))));

    public Provider Provider => _provider;

    public AgentDoor Door => _door;

    public string Name => _door.Id.Value;

    public SessionWire Wire => _door.Transport == Transport.Protocol ? SessionWire.Acp : SessionWire.Pipe;

    public string? AcpPosture => _door.Postures.Ordinary;

    public string? FullPosture => _door.Postures.Full;

    public string? AskingPosture => _door.Postures.Asking;

    public bool Interactive => _door.Abilities.Interactive.IsKnown && _door.Abilities.Interactive.Required;

    public bool TakesSettings => _door.Abilities.TakesSettings.IsKnown && _door.Abilities.TakesSettings.Required;

    /// <summary>A resume the door states, else the transport's default: a protocol door's eligibility, a pipe door's none.</summary>
    public bool Resumes => _door.Abilities.Resumes.IsKnown ? _door.Abilities.Resumes.Required : _door.Transport == Transport.Protocol;

    public HarnessToolchain? Toolchain => _toolchain.Value;

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => Spawn().Start(target, command);

    public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
        Interactive
            ? Spawn().Chat(target, command)
            : throw new DriverException(
                $"the `{Name}` adapter cannot hold a conversation — it spawns an agent that takes its "
                + "target once and runs to completion. Chat with an adapter that declares `interactive`.");

    public ProcessStartInfo? PrepareResume(SessionTarget target, IReadOnlyList<string>? command, string conversation, string prompt) =>
        Spawn().Resume(target, command, conversation, prompt);

    public IReadOnlyDictionary<string, string> ToolsUpFront => Spawn().ToolsUpFront;

    public void HandServers(ProcessStartInfo info, string configFile) => Spawn().HandServers(info, configFile);

    public void HandSettings(ProcessStartInfo info, string settingsFile) => Spawn().HandSettings(info, settingsFile);

    public void HandPlugins(ProcessStartInfo info, IReadOnlyList<string> plugins) => Spawn().HandPlugins(info, plugins);

    public IStreamMapper? StructuredOutput() => Native?.Mapper();

    public string FrameMessage(string text) => Native?.FrameMessage(text) ?? text;

    public string? FrameInterrupt() => Native?.FrameInterrupt();

    private INativeDialect? Native => _door.Dialect.IsKnown ? _door.Dialect.Required as INativeDialect : null;

    private ISpawn Spawn() =>
        _door.Spawn.IsKnown ? _door.Spawn.Required : throw new DriverException($"the `{Name}` door declares no spawn.");

    private HarnessToolchain? Declare()
    {
        var owning = Ids.Same(_provider.Id.Value, _door.Id.Value);
        var keyedItself = owning && _door.AccountOf is null;
        var program = owning ? _provider.Program : _door.Program;
        if (!program.IsKnown) return null;

        var facts = program.Required;
        var pool = keyedItself ? _provider.Accounts.Required.Facts : _door.Lent.Accounts;
        var tables = keyedItself ? (_provider.Tables.IsKnown ? _provider.Tables.Required : TablesPart.None) : _door.Lent.Tables;
        var usage = keyedItself ? (_provider.Usage.IsKnown ? _provider.Usage.Required : UsagePart.None) : _door.Lent.Usage;
        var errors = keyedItself ? tables.ErrorWords : _door.Tables.ErrorWords;
        var identity = owning ? (_provider.Identity.Product, _provider.Identity.Maker, _provider.Identity.Mark) : (_door.Identity.Product, _door.Identity.Maker, (string?)null);

        return new HarnessToolchain(
            Binary: facts.Binary,
            VersionArguments: facts.VersionArguments,
            ProfileVariable: _door.Home.Variable,
            Install: facts.Install,
            UpdateArguments: facts.UpdateArguments,
            LoginArguments: pool.LoginArguments,
            LoginCheck: pool.Login,
            Package: facts.Package,
            AccountOf: _door.AccountOf?.Value,
            ProfileMustExist: _door.Home.MustExist,
            TrustFile: _door.Home.TrustFile,
            ProbeByPresence: facts.ProbeByPresence,
            PinnedEnvironment: facts.PinnedEnvironment,
            Product: identity.Item1,
            Maker: identity.Item2,
            KeyVariable: pool.KeyVariable,
            Refused: tables.Refused,
            Channel: facts.Channel,
            SettingsFile: pool.SettingsFile,
            Limits: tables.Limits,
            WeekFixed: pool.WeekFixed,
            Windows: tables.Windows,
            SignIn: tables.SignIn,
            Usage: usage.Question is null ? null : usage.Question as UsageQuestion
                ?? new UsageQuestion(usage.Question.Harness, usage.Question.Command, usage.Question.Recorded, usage.Question.Kind),
            Mark: identity.Item3,
            RunsInOwnHome: _door.Home.OwnHome,
            TurnReport: _door.Tables.TurnReport,
            DeviceLoginArguments: pool.DeviceLoginArguments,
            LoginPorts: pool.LoginPorts,
            ErrorWords: errors);
    }
}
