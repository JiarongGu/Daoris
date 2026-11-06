namespace Daoris.Driver;

/// <summary>
/// What a test states of a door's agent, flat and named, for <see cref="TestDoors"/> to lay into the provider's parts: its program
/// (<see cref="Program"/>), its accounts (<see cref="Pool"/>), its words (<see cref="Tables"/>), the home it runs in (<see cref="Home"/>)
/// and who makes it. A fixture's way of writing a registration short, never a second description of an agent: the registry reads
/// only the parts.
/// </summary>
internal sealed record TestAgent(
    IReadOnlyList<string> Binary,
    IReadOnlyList<string> VersionArguments,
    string? ProfileVariable = null,
    IReadOnlyList<string>? Install = null,
    IReadOnlyList<string>? UpdateArguments = null,
    IReadOnlyList<string>? LoginArguments = null,
    LoginQuestion? LoginCheck = null,
    string? Package = null,
    string? AccountOf = null,
    string? TrustFile = null,
    bool ProbeByPresence = false,
    IReadOnlyDictionary<string, string>? PinnedEnvironment = null,
    string? Product = null,
    string? Maker = null,
    string? KeyVariable = null,
    string? Refused = null,
    string? Channel = null,
    string? SettingsFile = null,
    LimitWords? Limits = null,
    bool WeekFixed = false,
    WindowWords? Windows = null,
    SignInWords? SignIn = null,
    IUsageRead? Usage = null,
    string? Mark = null,
    bool RunsInOwnHome = false,
    string? TurnReport = null,
    IReadOnlyList<string>? DeviceLoginArguments = null,
    IReadOnlyList<int>? LoginPorts = null,
    ErrorWords? ErrorWords = null)
{
    /// <summary>The program it runs.</summary>
    public ToolSpec Program => new(Binary, VersionArguments, Install, UpdateArguments, Package, Channel, PinnedEnvironment, ProbeByPresence);

    /// <summary>Its accounts' facts.</summary>
    public PoolFacts Pool => new(KeyVariable, LoginCheck, LoginArguments, DeviceLoginArguments, LoginPorts, SettingsFile, WeekFixed);

    /// <summary>The home its spawn hands an account's home in.</summary>
    public DoorHome Home => new(ProfileVariable, RunsInOwnHome, TrustFile);

    /// <summary>Its accounts' words, with its error words where the reader is its provider.</summary>
    public TablesPart Tables(bool withErrors) => new(Limits, Windows, SignIn, Refused, withErrors ? ErrorWords : null);

    /// <summary>The facts a shipped or declared agent states, written again as a test's own, for it to change some and register.</summary>
    public static TestAgent Of(AccountAgent agent) => new(
        agent.Program.Binary, agent.Program.VersionArguments, agent.Home.Variable, agent.Program.Install, agent.Program.UpdateArguments,
        agent.Accounts.LoginArguments, agent.Accounts.Login, agent.Program.Package, agent.AccountOf, agent.Home.TrustFile,
        agent.Program.ProbeByPresence, agent.Program.PinnedEnvironment, agent.Product, agent.Maker, agent.Accounts.KeyVariable,
        agent.Tables.Refused, agent.Program.Channel, agent.Accounts.SettingsFile, agent.Tables.Limits, agent.Accounts.WeekFixed,
        agent.Tables.Windows, agent.Tables.SignIn, agent.Usage, agent.Mark, agent.Home.OwnHome, agent.TurnReport,
        agent.Accounts.DeviceLoginArguments, agent.Accounts.LoginPorts, agent.Tables.ErrorWords);

    /// <summary>The agent where a probe or an account verb reads only its facts, not its name.</summary>
    public static implicit operator AccountAgent(TestAgent agent) => agent.Agent("a-test-agent");

    /// <summary>The agent as a roster, a probe and the account verbs read it, under <paramref name="name"/>.</summary>
    public AccountAgent Agent(string name) =>
        new(name, Program, Pool, Home, Product, Maker, Mark, Tables(withErrors: true), TurnReport, Usage, AccountOf);
}
