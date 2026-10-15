namespace Daoris.Driver;

/// <summary>
/// One declared point: its kind and how long the driver waits there (the kit's words), and whether the process that
/// runs now listens on it, or null where no process runs to say.
/// </summary>
public sealed record PagePoint(string Name, string? Kind, long? WaitMs, bool? Listening);

/// <summary>A plugin's hook as written: its command, `${plugin}` left as written, and the points it declares.</summary>
public sealed record PageHook(IReadOnlyList<string> Command, IReadOnlyList<string> Points);

/// <summary>An agent a plugin declares, as written: its way in is the protocol door, and its posture null where its wire names none (ACP3).</summary>
public sealed record PageAgent(
    string Name, IReadOnlyList<string> Command, string WayIn, string? Posture, string? ProfileVariable, string? Package,
    IReadOnlyList<string>? Install);

/// <summary>
/// A server a plugin hands, as written: its command, and its environment variables by NAME only
/// (D119 §4.1), since a value may be a key. Whether it drives Daoris's browser, which is withheld where no shell
/// answers (D78).
/// </summary>
public sealed record PageServer(string Name, IReadOnlyList<string> Command, IReadOnlyList<string> Environment, bool DrivesBrowser)
{
    /// <summary>Who it is handed to (HELPPLUG1a, D170 point 3): <c>sessions</c>, <c>help</c> (Ask Daoris) or both.</summary>
    public IReadOnlyList<string> To { get; init; } = [PluginServerTo.Sessions];

    /// <summary>The tools of it Ask Daoris may call, as the plugin declares them.</summary>
    public IReadOnlyList<string> Allow { get; init; } = [];
}

/// <summary>A landing rule that names the plugin: a repository's or a workspace's, and its pattern.</summary>
public sealed record PageRule(string Scope, string Name, string? Pattern);

/// <summary>A landing plugin's rules, and why it cannot land work here now, or null when it can (<see cref="LandingRules.PluginProblem"/>).</summary>
public sealed record PageLanding(IReadOnlyList<PageRule> Rules, string? Problem);

/// <summary>
/// Where it came from (D103): <c>folder</c>, <c>offer</c>, <c>none</c> or <c>unread</c>; whether an update
/// <c>waits</c> or it is <c>current</c> (null where it cannot be told), what would change, and an update's refusal.
/// </summary>
public sealed record PageSource(
    string Kind, string? Folder, string? Offer, string? Problem, string? Update, IReadOnlyList<PluginChange> Changes, string? Refusal);

/// <summary>What a plugin keeps: its data folder, whether it exists, its files and bytes counted up to a bound, and its newest change.</summary>
/// <param name="More">Whether the count stopped at its bound: the files and bytes are then at least this many.</param>
public sealed record PageData(string Folder, bool Exists, int Files, long Bytes, bool More, DateTimeOffset? Changed)
{
    /// <summary>
    /// Whether a file is a test as <c>node --test</c> finds them by default: <c>*.test.*</c>, <c>*-test.*</c>,
    /// <c>*_test.*</c>, <c>test-*.*</c>, <c>test.*</c>, and any script under a <c>test</c> folder, in JavaScript.
    /// </summary>
    public static bool IsTest(string name, bool underTest)
    {
        var extension = Path.GetExtension(name);
        if (extension is not (".js" or ".mjs" or ".cjs")) return false;
        if (underTest) return true;
        var stem = Path.GetFileNameWithoutExtension(name);
        return stem == "test" || stem.StartsWith("test-", StringComparison.Ordinal)
            || stem.EndsWith(".test", StringComparison.Ordinal) || stem.EndsWith("-test", StringComparison.Ordinal)
            || stem.EndsWith("_test", StringComparison.Ordinal);
    }
}

/// <summary>What <see cref="PluginPage.Read"/> answers for one installed plugin.</summary>
/// <param name="Taken">Whether the driver takes what it declares: false for a refused plugin, whose manifest is shown as written.</param>
/// <param name="HealthFrom"><c>loop</c> where the loop's own record answered, <c>log</c> where the machine log's last word did.</param>
/// <param name="Tests">The test files its install folder carries, relative, as <c>node --test</c> would find them.</param>
public sealed record PluginPageRead(
    string Id, string Name, string Version, string Description, bool Enabled, string? Problem, bool Taken, string Folder,
    DateTimeOffset? InstalledAt, PluginHealthState Health, string HealthFrom, PageHook? Hook, IReadOnlyList<PagePoint> Points,
    IReadOnlyList<PageAgent> Agents, IReadOnlyList<PageServer> Servers, PageLanding? Landing, PageSource Source, PageData Data,
    IReadOnlyList<string> Tests);
