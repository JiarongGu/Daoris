namespace Daoris.Driver;

/// <summary>
/// The codes a tool's operation refuses in (PUSH1f2c, D194 point 4, the alone-path design §1.3): the page's catalogue keys, which the
/// shell's <c>Refusals</c> declares as these, and the driver's verbatim refusal for every other. One table, read by the routes that
/// answer the window (through the module's one mapping) and by <c>daoris-driver tool</c> acting alone, so a refusal is one line both
/// ways.
/// </summary>
public static class ToolCodes
{
    /// <summary>A delete of a version that is not downloaded; a git compared at one.</summary>
    public const string NotDownloaded = "TOOL_NOT_DOWNLOADED";

    /// <summary>A tool named as a file that is not a whole path, or holds no file: it is never written.</summary>
    public const string FileMissing = "TOOL_FILE_MISSING";

    /// <summary>A tool named as a file that did not start, or started and answered no version.</summary>
    public const string FileNoVersion = "TOOL_FILE_NO_VERSION";

    /// <summary>A resource location that is no address Daoris reads from.</summary>
    public const string LocationRefused = "TOOL_LOCATION_REFUSED";

    /// <summary>A delete of the version a tool runs.</summary>
    public const string InUse = "TOOL_IN_USE";

    /// <summary>A delete the system refused, since something still holds a file in that version's folder.</summary>
    public const string Held = "TOOL_HELD";

    /// <summary>The shell's <c>DRIVER_REFUSED</c>: any other refusal, said in its own sentence.</summary>
    public const string Refused = ChoiceCodes.Refused;

    /// <summary>Each check an operation refuses by, and the code it is said in.</summary>
    public static IReadOnlyDictionary<string, string> Table { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ToolRefusal.Missing] = NotDownloaded,
        [ToolRefusal.InUse] = InUse,
        [ToolRefusal.Held] = Held,
        [ToolRefusal.FileMissing] = FileMissing,
        [ToolRefusal.FileNoVersion] = FileNoVersion,
        [ToolRefusal.Location] = LocationRefused,
    };

    /// <summary>Whether a refusal is one an operation says in a code of its own.</summary>
    public static bool Coded(DriverException refused) => refused is ToolRefusal coded && Table.ContainsKey(coded.Check);

    /// <summary>The code a refusal is said in: its check's, else the verbatim refusal.</summary>
    public static string Of(DriverException refused) =>
        refused is ToolRefusal coded && Table.TryGetValue(coded.Check, out var code) ? code : Refused;
}

/// <summary>
/// The routes <c>daoris-driver tool</c> sends while Daoris runs (PUSH1f2c; RPC1f, the terminal RPC design §9), each the Tools place's
/// press it is the twin of, and the verbs no route takes, each with why it acts alone (D171's PUSH1f note, kept by D194 point 5): the
/// CLI's <c>TOOL_VERBS</c>, now this program's. Held to the census by <c>PipeCensusTests</c>.
/// </summary>
public static class ToolRoutes
{
    private static InstanceRoute Person(string route) => new(InstanceRoute.DriverModule, route, RouteClass.Person);

    /// <summary><c>tool use &lt;tool&gt; system|file</c>: the Tools place's way chosen.</summary>
    public static InstanceRoute Use { get; } = Person("TOOLS_USE");

    /// <summary><c>tool delete &lt;tool&gt; &lt;version&gt;</c>: a downloaded version removed.</summary>
    public static InstanceRoute Delete { get; } = Person("TOOLS_DELETE");

    /// <summary><c>tool locations add|remove &lt;address&gt;</c>: a resource location added or removed.</summary>
    public static InstanceRoute Location { get; } = Person("TOOLS_LOCATION");

    /// <summary><c>tool look</c>: the driver's own look, so open.</summary>
    public static InstanceRoute Look { get; } = new(InstanceRoute.DriverModule, "TOOLS_LOOK", RouteClass.Open);

    /// <summary>Each verb that changes the home, in the verb's words: the route it sends while Daoris runs, or null with why it acts alone.</summary>
    public static IReadOnlyList<(string Verb, InstanceRoute? Route, string Reason)> Verbs { get; } =
    [
        ("tool use <tool> system", Use, "the Tools place's way chosen"),
        ("tool use <tool> file <path>", Use, "the Tools place's way chosen; the file is asked its version before it is written, by the instance or here"),
        ("tool delete <tool> <version>", Delete, "a downloaded version removed"),
        ("tool locations add|remove <address>", Location, "a resource location added or removed"),
        ("tool look", Look, "the look for updates, whose copies the instance keeps"),
        ("tool use <tool> managed [<version>]", null,
            "alone: TOOLS_USE answers once the driver's download starts, and its end is an event the pipe forwards only to a subscription (RPC1g)"),
        ("tool download <tool> [<version>]", null, "alone: TOOLS_DOWNLOAD answers once the download starts, and its end is an event (RPC1g)"),
        ("tool update <tool>", null, "alone: no route moves a tool to the newest the lists name, and a managed use's end is an event (RPC1g)"),
    ];
}

/// <summary>
/// The tools' operations (PUSH1f2c, D194 point 4, the alone-path design §1.3): each the judgement a Tools route made inside its handler,
/// taken out over the facts it judges by, so the window's route and <c>daoris-driver tool</c> acting alone call one implementation. The
/// rest are already <see cref="Tools"/>' writers and <see cref="ToolInstall.Delete"/>.
/// </summary>
public static class ToolChoices
{
    /// <summary>
    /// A tool set to a file the person names (§4.1): one that is there, by its whole path, that starts and answers a version, since a
    /// program the person names runs as them on every call, so a wrong one is said now. The window's <c>TOOLS_USE</c> and the terminal
    /// alike.
    /// </summary>
    /// <param name="ask">The file's answer to its tool's version question: <see cref="ToolQuestions.VersionOfAsync"/>, a route's kept
    /// answers, or a test's.</param>
    /// <returns>The version the file answered.</returns>
    /// <exception cref="ToolRefusal"><see cref="ToolRefusal.FileMissing"/> or <see cref="ToolRefusal.FileNoVersion"/>; nothing is
    /// written.</exception>
    /// <exception cref="DriverException">A tool this build does not run, or a <c>tools.json</c> this build cannot write.</exception>
    public static async Task<string> UseFileAsync(
        string home, string tool, string file, Func<string, ToolDeclaration, CancellationToken, Task<(string? Version, string? Problem)>> ask,
        CancellationToken ct)
    {
        var declared = Tools.Find(tool) ?? throw new DriverException(Tools.Undeclared(tool));
        if (!Tools.IsWholePath(file) || !File.Exists(file))
        {
            throw new ToolRefusal(ToolRefusal.FileMissing, $"{declared.Name} is not set to `{file}`: name a file that is there by its whole path.",
                ("tool", declared.Name), ("file", file));
        }

        var (version, problem) = await ask(file, declared, ct).ConfigureAwait(false);
        if (version is null)
        {
            throw new ToolRefusal(ToolRefusal.FileNoVersion, $"{file} did not answer its version, so {declared.Name} was not set to it: {problem}",
                ("tool", declared.Name), ("file", file), ("reason", problem ?? ""));
        }

        Tools.UseFile(home, tool, file);
        return version;
    }

    /// <summary>A resource location added (§3.3), refused where it is no address Daoris reads from.</summary>
    /// <returns>False where it is already listed, and nothing is written.</returns>
    /// <exception cref="ToolRefusal"><see cref="ToolRefusal.Location"/>; nothing is written.</exception>
    public static bool AddLocation(string home, string address) =>
        Tools.LocationProblem(address) is { } problem
            ? throw new ToolRefusal(ToolRefusal.Location, problem, ("address", address))
            : Tools.AddLocation(home, address);
}
