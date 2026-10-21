namespace Daoris.Driver;

/// <summary>
/// A plugin's operation refusing by one named check (PUSH1f2f, D194 point 4): the check, its sentence, and the facts a page's catalogue
/// fills it with. <see cref="PluginCodes"/> gives each check the code the route and <c>daoris-driver plugin</c> both say it in.
/// </summary>
public sealed class PluginRefusal(string check, string message, params (string Key, string Value)[] facts) : DriverException(message)
{
    /// <summary>No plugin of the id is installed.</summary>
    public const string Unknown = "unknown";

    /// <summary>Something on the machine holds a file in the plugin's folder, so it was not moved: nothing was taken.</summary>
    public const string Busy = "busy";

    public string Check { get; } = check;

    public IReadOnlyList<(string Key, string Value)> Facts { get; } = facts;
}

/// <summary>
/// The library's one table from a plugin operation's check to its code (PUSH1f2f, the alone-path design §1.3): the modules'
/// <c>Refusals</c> read these constants, and <c>DriverModule</c> maps a <see cref="PluginRefusal"/> through <see cref="Of"/>, so the
/// window and the terminal say one refusal in one code.
/// </summary>
public static class PluginCodes
{
    /// <summary>A plugin not installed here.</summary>
    public const string Unknown = "PLUGIN_UNKNOWN";

    /// <summary>A remove the system refused, since something still holds a file in the plugin's folder.</summary>
    public const string Busy = "PLUGIN_BUSY";

    /// <summary>The shell's <c>DRIVER_REFUSED</c>: any other refusal, said in its own sentence.</summary>
    public const string Refused = ChoiceCodes.Refused;

    /// <summary>Each check an operation refuses by, and the code it is said in.</summary>
    public static IReadOnlyDictionary<string, string> Table { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [PluginRefusal.Unknown] = Unknown,
        [PluginRefusal.Busy] = Busy,
    };

    /// <summary>Whether a refusal is one an operation says in a code of its own.</summary>
    public static bool Coded(DriverException refused) => refused is PluginRefusal coded && Table.ContainsKey(coded.Check);

    /// <summary>The code a refusal is said in: its check's, else the verbatim refusal.</summary>
    public static string Of(DriverException refused) =>
        refused is PluginRefusal coded && Table.TryGetValue(coded.Check, out var code) ? code : Refused;
}

/// <summary>
/// The routes <c>daoris-driver plugin</c> sends while Daoris runs (PUSH1f2f, D194 point 5): the Plugins place's presses, code that runs
/// in the driver, so each is the person's; and the one verb no route takes, with why it acts alone (D171's PUSH1f note, kept by D194
/// point 5): the CLI's <c>PLUGIN_VERBS</c>, now this program's. Held to the census by <c>PipeCensusTests</c>.
/// </summary>
public static class PluginRoutes
{
    private static InstanceRoute Person(string route) => new(TreesRoutes.DriverModule, route, RouteClass.Person);

    /// <summary><c>plugin add &lt;folder&gt;</c>: the Plugins place's install from a folder, for a plugin not installed here.</summary>
    public static InstanceRoute Add { get; } = Person("PLUGIN_ADD");

    /// <summary><c>plugin add --offer &lt;id&gt;</c>: the Install beside one of the install's offers.</summary>
    public static InstanceRoute Install { get; } = Person("PLUGIN_INSTALL");

    /// <summary><c>plugin update &lt;id&gt; --yes</c>: the update's press, the person's with <c>apply: true</c>, the only form sent.</summary>
    public static InstanceRoute Update { get; } = Person("PLUGIN_UPDATE");

    /// <summary><c>plugin remove|enable|disable &lt;id&gt;</c>: the row's switch, and its remove.</summary>
    public static InstanceRoute Action { get; } = Person("PLUGIN_ACTION");

    /// <summary>Each verb that changes the home, in the verb's words: the route it sends while Daoris runs, or null with why it acts alone.</summary>
    public static IReadOnlyList<(string Verb, InstanceRoute? Route, string Reason)> Verbs { get; } =
    [
        ("plugin add <folder>", Add, "the Plugins place's install from a folder, for a plugin not installed here"),
        ("plugin add --offer <id>", Install, "the Install beside one of the install's offers, for one not installed here"),
        ("plugin update <id> --yes", Update, "the update's press, sent with `apply: true`; the plan without --yes is read here"),
        ("plugin remove|enable|disable <id>", Action,
            "the row's switch, and its remove, which stops the plugin's process before the folder goes"),
        ("plugin add … (installed already)", null,
            "alone: PLUGIN_ADD and PLUGIN_INSTALL add and never replace; a replace is the terminal's, on its word (D50)"),
    ];
}
