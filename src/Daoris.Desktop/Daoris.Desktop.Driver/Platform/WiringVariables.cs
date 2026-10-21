namespace Daoris.Driver;

/// <summary>Who reads a variable (WIRING1b, the this-Daoris design §3.5): what the row says it reaches.</summary>
public static class WiringReaders
{
    public const string Desktop = "desktop";
    /// <summary>The driver's library: the window's loop and the terminal's verbs.</summary>
    public const string DriverLibrary = "driver";
    public const string Cli = "cli";
    public const string Service = "service";
}

/// <summary>What a variable holds, which says how its value may be shown (design §5).</summary>
public enum WiringValue
{
    /// <summary>A path on this machine: shown as it is, since the page is the desktop's alone (D47 §4).</summary>
    Path,

    /// <summary>An address: shown without its credentials, its query or its fragment, and not at all where it does not parse.</summary>
    Address,

    /// <summary>A word or a number: shown as it is.</summary>
    Word,

    /// <summary>A key: <c>set</c>, and a minted deployment key's audit prefix at most; never its value.</summary>
    Key,
}

/// <summary>A variable Daoris reads, as the page's §3.5 row shows it.</summary>
/// <param name="Name">The variable.</param>
/// <param name="Value">How its value may be shown.</param>
/// <param name="Instead">What is used where it is not set, in words a test can hold, or null where nothing stands in.</param>
/// <param name="ReadBy">The programs that read it (<see cref="WiringReaders"/>).</param>
public sealed record WiringVariable(string Name, WiringValue Value, string? Instead, params string[] ReadBy)
{
    /// <summary>Read by the service alone: an adopted service's value of it is not this window's to say (the second opinion).</summary>
    public bool ServiceOnly => ReadBy is [WiringReaders.Service];
}

/// <summary>
/// Every variable Daoris reads, from one list (WIRING1b, the this-Daoris design §3.5): those a person may set, which the page
/// shows, and those set aside with the reason they are not a person's setting. <c>SystemWiringTests</c> holds the list against
/// a search of the sources, so a variable a change adds is shown or set aside by the same change.
/// </summary>
/// <remarks>
/// 🔴 The search reads every source but this one, so a name listed here that no reader spells any more is found too; a variable
/// with a reader's constant is named through it, and the wiring reader names each through this list.
/// </remarks>
public static class WiringVariables
{
    // Each reader's own constant where it has one, so the list names what the code reads; the two the connector passes
    // through without a constant of their own are spelled here, and the home's too: this list builds no path under the home,
    // so the dev loop's scan of the files that do (`desktop-tool.test.ts`) has nothing to ask of it.
    public const string Home = "DAORIS_HOME";
    public const string ServiceUrl = ServiceClient.UrlVariable;
    public const string ServiceKey = ServiceClient.KeyVariable;
    public const string ConfigFile = DriverConfig.PathVariable;
    public const string HarnessFile = HarnessSettings.PathVariable;
    public const string RemotesFile = RemoteTarget.PathVariable;
    public const string KnowledgeDb = "DAORIS_KNOWLEDGE_DB";
    public const string KnowledgeRoot = "DAORIS_KNOWLEDGE_ROOT";
    public const string HttpHost = ServiceHostLocator.PathVariable;
    public const string McpHost = KnowledgeConnector.PathVariable;
    public const string RemoteUrl = RemoteTarget.UrlVariable;
    public const string RemoteKey = RemoteTarget.KeyVariable;
    public const string RemoteWorkspace = RemoteTarget.WorkspaceVariable;

    /// <summary>The address the desktop uses where <see cref="ServiceUrl"/> is not set (the desktop's <c>Program.cs</c>).</summary>
    public const string DefaultServiceUrl = "http://localhost:5177";

    /// <summary>The variables a person may set, in the page's order: the home and its per-file overrides, the service, the hosts, the remote trio, the knowledge service's.</summary>
    public static IReadOnlyList<WiringVariable> Shown { get; } =
    [
        new(Home, WiringValue.Path, "install-data", WiringReaders.Desktop, WiringReaders.DriverLibrary, WiringReaders.Cli, WiringReaders.Service),
        new(ConfigFile, WiringValue.Path, "home", WiringReaders.Desktop, WiringReaders.DriverLibrary, WiringReaders.Cli),
        new(HarnessFile, WiringValue.Path, "home", WiringReaders.Desktop, WiringReaders.DriverLibrary, WiringReaders.Cli),
        new(RemotesFile, WiringValue.Path, "home", WiringReaders.Desktop, WiringReaders.DriverLibrary, WiringReaders.Cli, WiringReaders.Service),
        new(KnowledgeDb, WiringValue.Path, "home", WiringReaders.DriverLibrary, WiringReaders.Service),
        new(ServiceUrl, WiringValue.Address, DefaultServiceUrl, WiringReaders.Desktop, WiringReaders.DriverLibrary, WiringReaders.Cli),
        new(ServiceKey, WiringValue.Key, null, WiringReaders.Desktop, WiringReaders.DriverLibrary, WiringReaders.Cli),
        new(HttpHost, WiringValue.Path, "located", WiringReaders.Desktop),
        new(McpHost, WiringValue.Path, "located", WiringReaders.DriverLibrary),
        new(RemoteUrl, WiringValue.Address, "remotes-file", WiringReaders.Desktop, WiringReaders.DriverLibrary, WiringReaders.Cli, WiringReaders.Service),
        new(RemoteKey, WiringValue.Key, "remotes-file", WiringReaders.Desktop, WiringReaders.DriverLibrary, WiringReaders.Cli, WiringReaders.Service),
        new(RemoteWorkspace, WiringValue.Word, "default", WiringReaders.Desktop, WiringReaders.DriverLibrary, WiringReaders.Cli, WiringReaders.Service),
        new(KnowledgeRoot, WiringValue.Path, "registered", WiringReaders.Service),
        new("DAORIS_KNOWLEDGE_REPOSITORY", WiringValue.Path, null, WiringReaders.Service),
        new("DAORIS_KNOWLEDGE_DOCUMENTS", WiringValue.Word, null, WiringReaders.Service),
        new("DAORIS_KNOWLEDGE_INDEX", WiringValue.Word, null, WiringReaders.Service),
        new("DAORIS_EMBED_MODEL", WiringValue.Word, "lexical", WiringReaders.Service),
        new("DAORIS_EMBED_URL", WiringValue.Address, "http://localhost:11434", WiringReaders.Service),
        new("DAORIS_EMBED_WINDOW", WiringValue.Word, "2000", WiringReaders.Service),
        new("DAORIS_MODE", WiringValue.Word, "local", WiringReaders.Service),
        new("DAORIS_WORKSPACE", WiringValue.Word, "default", WiringReaders.Service),
        new("DAORIS_WEB_ORIGIN", WiringValue.Address, null, WiringReaders.Service),
    ];

    /// <summary>The names the sources spell that are no person's setting, each with why the page does not show it.</summary>
    public static IReadOnlyDictionary<string, string> SetAside { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DAORIS_TARGET"] = "handed to a session by the driver: its target",
        ["DAORIS_BRAINSTORM_ID"] = "handed to a brainstorm's pass by the driver: the brainstorm it serves",
        ["DAORIS_BRAINSTORM_TURN"] = "handed to a brainstorm's pass by the driver: its turn",
        ["DAORIS_BRAINSTORM_ANSWER"] = "handed to a brainstorm's pass by the driver: the file its result tool writes",
        ["DAORIS_BRAINSTORM_ROOM"] = "handed to a brainstorm pass's connector by the driver: the room its answer is judged against",
        ["DAORIS_QUEST_ATTACHMENTS"] = "handed to a session by the driver: its quest's kept files",
        ["DAORIS_SESSION_ID"] = "handed to a session or its connector by the driver",
        ["DAORIS_QUEST_ID"] = "handed to a session or its connector by the driver",
        ["DAORIS_QUEST_TITLE"] = "handed to a plugin's hook by the driver",
        ["DAORIS_QUEST_BODY"] = "handed to a plugin's hook by the driver",
        ["DAORIS_QUEST_ASKER"] = "handed to a plugin's hook by the driver",
        ["DAORIS_ASK_ID"] = "handed to an intake's connector by the driver",
        ["DAORIS_HEAVY_RUN"] = "handed to a heavy run's gates by the driver: the slot their run holds, so a run inside it never waits on it",
        ["DAORIS_SESSION_WORKSPACE"] = "handed to Ask Daoris's connector by the driver: the workspace its conversation is bound to",
        [HeavyRun.RepositoryVariable] = "handed to a session and a plugin's hook by the driver: the repository it works for, which a heavy run reads this machine's heavy gates under",
        ["DAORIS_PLUGIN_ID"] = "handed to a plugin's process by the driver",
        ["DAORIS_PLUGIN_FOLDER"] = "handed to a plugin's process by the driver",
        ["DAORIS_PLUGIN_DATA"] = "handed to a plugin's process by the driver",
        [PluginCycles.World] = "handed to a plugin's process by the kit's trial and cycle alone (FABRIC-PERFORM1e): the stand-in world it answers from in its platform's place",
        ["DAORIS_RULES_HOME"] = "handed to a session's connector by the driver: the home its rule proposals go to",
        ["DAORIS_STOP_ON_INPUT_END"] = "handed to the service host the desktop starts: its stop",
        ["DAORIS_PERSON_KEY_ON_INPUT"] = "handed to the service host the desktop starts: the person key's line",
        ["DAORIS_DEVTOOLS_PORT"] = "a development run's instruments only",
        [InstanceDoor.Variable] = "the dev loop's or a rehearsal's own instance, named for a workspace build, which reads no address file",
        ["DAORIS_DRIVER_PROGRAM"] = "the dev loop's or a rehearsal's own build of the driver's program, which a checkout's `daoris` hands its management families to (D194); an install's finds its own",
        ["DAORIS_WEB_WORKERS"] = "the web tests' worker count",
        ["DAORIS_MEASURED_PORT"] = "the web's measured check's port, where a checkout's own is not wanted",
        ["DAORIS_CANON"] = "the doctrine tool's tests' fixture canon",
        ["DAORIS_STUB_CONFIG_DIR"] = "the stub agent's tests' folder",
        ["DAORIS_OWN_TOOLS"] = "a constant's name, not a variable",
    };

    /// <summary>
    /// Whether a source, by its path under <c>src/</c>, is one the census reads: code outside the tests, the stories, the
    /// end-to-end specs and the build's output, and not this list itself.
    /// </summary>
    public static bool Searched(string relative)
    {
        var path = relative.Replace('\\', '/');
        var extension = System.IO.Path.GetExtension(path);
        if (extension is not (".cs" or ".ts" or ".tsx" or ".mjs")) return false;
        if (path.EndsWith("/WiringVariables.cs", StringComparison.Ordinal)) return false;
        if (path.Contains(".test.", StringComparison.Ordinal) || path.Contains(".stories.", StringComparison.Ordinal)) return false;
        var parts = path.Split('/');
        return !parts.Any(part => part.EndsWith(".Tests", StringComparison.Ordinal)
            || part is "test" or "tests" or "e2e" or "bin" or "obj" or "node_modules" or "dist" or "wwwroot" or "fixtures");
    }
}
