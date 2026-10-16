using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What a service's status door last said, as This Daoris shows it (WIRING1b, the this-Daoris design §3.2).</summary>
/// <param name="Answers">Whether a Daoris host answered.</param>
/// <param name="Asked">When it was asked.</param>
/// <param name="Why">Why it did not answer, a code: <c>no-answer</c>, <c>foreign</c>, <c>no-host</c>, <c>unstartable</c>, <c>exited</c>, <c>never-answered</c>.</param>
/// <param name="Tier">Its search tier, as it says it.</param>
/// <param name="Version">Its version, as it says it (<c>/api/status</c>'s <c>version</c>, WIRING1b); null from a host older than that.</param>
/// <param name="Home">The id of the home it serves (HOMEID1), as <see cref="RunningService.ReadStatus"/> reads it; null where it names none.</param>
/// <remarks>Discovery's own reading is <see cref="ServiceStatus"/>: this one keeps when it was asked, why not, and the version.</remarks>
public sealed record ServiceProbe(bool Answers, DateTimeOffset Asked, string? Why, string? Tier, string? Version, string? Home = null)
{
    /// <summary>The longest tier or version kept: a status is the service's words, and a host is not trusted to keep them short.</summary>
    private const int Longest = 200;

    /// <summary>A status body read as discovery reads it (<see cref="RunningService.ReadStatus"/>): a Daoris host's names its tier, any other is foreign.</summary>
    public static ServiceProbe Of(string body, DateTimeOffset asked)
    {
        if (RunningService.ReadStatus(body) is not { } read) return new(false, asked, "foreign", null, null);
        using var status = JsonDocument.Parse(body);
        return new(true, asked, null, Short(status.RootElement, "tier"), Short(status.RootElement, "version"), read.Home);
    }

    private static string? Short(JsonElement status, string name) =>
        status.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { } text
            ? text.Length > Longest ? text[..Longest] : text
            : null;

    /// <summary>
    /// Ask <paramref name="url"/>'s status door within two seconds and with redirects off, as a terminal finding the running
    /// service does (HOMEID1: where an address sends its asker is no answer).
    /// </summary>
    /// <param name="handler">A test's host; null asks over the network.</param>
    public static ServiceProbe Ask(string url, HttpMessageHandler? handler = null)
    {
        var asked = DateTimeOffset.UtcNow;
        using var http = handler is null
            ? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
            : new HttpClient(handler, disposeHandler: false);
        http.Timeout = TimeSpan.FromSeconds(2);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{url.TrimEnd('/')}/api/status");
            using var response = http.Send(request);
            if (!response.IsSuccessStatusCode) return new(false, asked, "foreign", null, null);
            using var reader = new StreamReader(response.Content.ReadAsStream());
            return Of(reader.ReadToEnd(), asked);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException
                                          or InvalidOperationException or NotSupportedException or UriFormatException)
        {
            return new(false, asked, "no-answer", null, null);
        }
    }
}

/// <summary>What only the window knows (design §6): a terminal's reader is handed none, and says <c>window</c> for each.</summary>
public sealed record WindowWiring
{
    public const string Hosted = "hosted";
    public const string Adopted = "adopted";
    public const string None = "none";

    /// <summary>Whether this window started its service, adopted one already running, or has none: <see cref="Hosted"/>, <see cref="Adopted"/>, <see cref="None"/>.</summary>
    public string Held { get; init; } = None;

    /// <summary>How the home stands to the account's <c>DAORIS_HOME</c> (LEFT2): <c>same</c>, <c>overridden</c>, <c>this-start</c>.</summary>
    public string? HomeAccount { get; init; }

    /// <summary>Whether this window set its own home (D63's establishing): the home variable's source is then Daoris.</summary>
    public bool HomeSetByDaoris { get; init; }

    /// <summary>What establishing the home said, verbatim, before it is redacted.</summary>
    public string? HomeNotice { get; init; }

    /// <summary>What the adopted host's page said, verbatim, before it is redacted.</summary>
    public string? HostNotice { get; init; }

    /// <summary>The address this window uses, as it was handed.</summary>
    public string? ServiceUrl { get; init; }

    /// <summary>The supervisor's last probe of that address; null where it has asked nothing.</summary>
    public ServiceProbe? Status { get; init; }

    /// <summary>Whether the host proved it holds this start's person key (PERSONDOOR1); null where nothing was asked.</summary>
    public bool? Proved { get; init; }

    /// <summary>The UI kit's version.</summary>
    public string? Kit { get; init; }

    /// <summary>This program's informational version, as the release workflow stamped it.</summary>
    public string? Program { get; init; }

    /// <summary>When the loop last looked; null before its first look.</summary>
    public DateTimeOffset? Looked { get; init; }

    /// <summary>Whether the loop's service is up (<c>STATE</c>'s <c>ready</c>).</summary>
    public bool Ready { get; init; }

    /// <summary>Whether an update drains the loop; null where no update is wired.</summary>
    public bool? Draining { get; init; }

    /// <summary>This window's process, which a holder of the home is compared to.</summary>
    public int ProcessId { get; init; }
}

/// <summary>
/// What the wiring read is handed (WIRING1b): every fact of the program that asks, each injectable, so a test reads no
/// machine's environment, account or home but its own.
/// </summary>
public sealed record WiringWorld
{
    /// <summary>The home this program runs on; null where it has none.</summary>
    public string? Home { get; init; }

    /// <summary>The folder the asking program runs from, which says whether an install carries it.</summary>
    public required string BaseDirectory { get; init; }

    /// <summary>This process's variables.</summary>
    public required Func<string, string?> Process { get; init; }

    /// <summary>The account's variables, which a new terminal gets; null where the account keeps none (off Windows).</summary>
    public Func<string, string?>? Account { get; init; }

    /// <summary>The machine's variables; null where the machine keeps none.</summary>
    public Func<string, string?>? Machine { get; init; }

    /// <summary>What only the window knows; null for a terminal.</summary>
    public WindowWiring? Window { get; init; }

    /// <summary>
    /// How a terminal asks an address's status door; null asks nothing. 🔴 The window is handed none: its service's status is its
    /// supervisor's last probe, and the page's open asks no network, starts no program and probes no git (the second opinion).
    /// </summary>
    public Func<string, ServiceProbe?>? AskStatus { get; init; }

    /// <summary>Whether the process an address file names still runs as the process it named.</summary>
    public Func<int, long, bool> Lives { get; init; } = RunningService.Lives;

    /// <summary>The sizes measured, kept for the process.</summary>
    public WiringSizes Sizes { get; init; } = WiringSizes.Shared;

    /// <summary>The person's own tool home (OWNSPACE2); null where none is named: a world made by hand reads no machine's, a test names its scratch one, and the real worlds are handed <see cref="ToolHomeLeftovers.OwnHome(WiringWorld)"/>.</summary>
    public string? ToolHome { get; init; }

    /// <summary>Where <see cref="ToolHome"/> was read from: <c>process</c>, <c>account</c> or <c>default</c> as the environment gave it, <c>named</c> where a caller named it.</summary>
    public string ToolHomeFrom { get; init; } = "named";

    /// <summary>Why no tool home is named though the environment named one: <c>relative</c> or <c>inside-daoris</c>; null otherwise.</summary>
    public string? ToolHomeWhy { get; init; }

    /// <summary>
    /// The working directories Daoris's own records hold (<see cref="ToolHomeLeftovers.Origins"/>), read by the window's route
    /// from the service's records; null where none was read, which lists the rooms with every folder kept <c>records-unread</c>.
    /// </summary>
    public IReadOnlyList<ToolOrigin>? ToolOrigins { get; init; }

    /// <summary>The asking terminal's own version, where it is the driver's verbs.</summary>
    public string? DriverVersion { get; init; }

    /// <summary>A terminal's world: this process, its account and machine, its status door, its own version.</summary>
    public static WiringWorld OfThisTerminal(string? home)
    {
        var account = Stored(EnvironmentVariableTarget.User);
        var (tool, from, toolWhy) = ToolHomeLeftovers.OwnHome(
            Environment.GetEnvironmentVariable, account, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), home);
        return new()
        {
            Home = home,
            BaseDirectory = AppContext.BaseDirectory,
            Process = Environment.GetEnvironmentVariable,
            Account = account,
            Machine = Stored(EnvironmentVariableTarget.Machine),
            AskStatus = url => ServiceProbe.Ask(url),
            DriverVersion = SystemWiring.VersionOf(Assembly.GetEntryAssembly()),
            ToolHome = tool,
            ToolHomeFrom = from,
            ToolHomeWhy = toolWhy,
        };
    }

    /// <summary>The account's or the machine's stored variables, on Windows, where they are kept; null elsewhere.</summary>
    public static Func<string, string?>? Stored(EnvironmentVariableTarget target) =>
        OperatingSystem.IsWindows() ? name => Environment.GetEnvironmentVariable(name, target) : null;
}

/// <summary>
/// The one reader This Daoris and <c>daoris-driver wiring</c> share (WIRING1b, the this-Daoris design §6): every wire of this
/// Daoris and where it came from, in the route's shape, every field added and never renamed.
/// </summary>
/// <remarks>
/// <para><b>A fact not read is its field's <c>null</c> with a sibling <c>…Why</c> code</b>, never an absent field; a code the
/// page words (<c>window</c>: only the window knows; <c>adopted</c>: an adopted service's own; <c>not-measured</c>, …).</para>
///
/// <para><b>Redacted here, before anything is serialized</b> (<see cref="WiringRedact"/>): a key is <c>set</c> and a minted
/// key's prefix at most, an address loses what may carry a credential, a notice is cut, and a last sweep takes every secret
/// the reader saw out of every string. No file's contents are answered, only whether it reads.</para>
///
/// <para><b>The layout as it runs</b> (the second opinion's point 4): each entry of D172's table where it is found, its old
/// place said with why it stays; the planned folders are named with whether they are there, never as installed fact.</para>
///
/// <para><b>Nothing slow on the opening path</b>: no git, no program started, no download, and no folder walked. Sizes are the
/// last measure <see cref="WiringSizes"/> kept, or <c>null</c> until one is asked.</para>
/// </remarks>
public static class SystemWiring
{
    public const int Schema = 1;

    /// <summary>The machine log's days (<see cref="MachineLog.KeptDays"/>).</summary>
    private const int LogDays = MachineLog.KeptDays;

    /// <summary>Each launcher an install's <c>app/bin/</c> carries (D124 §1.2, <c>desktop-publish</c>'s <c>CLI_LAUNCHERS</c> and <c>DRIVER_LAUNCHERS</c>).</summary>
    public static IReadOnlyList<string> Launchers { get; } = ["daoris", "daoris.cmd", "daoris-driver", "daoris-driver.cmd"];

    /// <summary>The settings files a person's choice lives in (design §3.4), each with the variable that overrides it and the door to where it is edited.</summary>
    private static readonly (HomeEntry Entry, string? Override, string Door)[] SettingsFiles =
    [
        (HomeEntry.DriverConfig, WiringVariables.ConfigFile, "driver"),
        (HomeEntry.Harnesses, WiringVariables.HarnessFile, "agents"),
        (HomeEntry.Accounts, null, "agents"),
        (HomeEntry.Permissions, null, "agents"),
        (HomeEntry.Tools, null, "tools"),
        (HomeEntry.PluginState, null, "plugins"),
        (HomeEntry.BrowserSettings, null, "browser"),
        (HomeEntry.Update, null, "driver"),
        (HomeEntry.Remotes, WiringVariables.RemotesFile, "workspace"),
        (HomeEntry.Keys, null, "agents"),
    ];

    /// <summary>The records (design §3.4's fold), the same way, with no door.</summary>
    private static readonly (HomeEntry Entry, string? Override)[] Records =
    [
        (HomeEntry.Usage, null), (HomeEntry.Landings, null), (HomeEntry.SessionBranches, null), (HomeEntry.Cooling, null),
        (HomeEntry.Windows, null), (HomeEntry.Reads, null), (HomeEntry.Sessions, null), (HomeEntry.Knowledge, WiringVariables.KnowledgeDb),
    ];

    /// <summary>Why an entry stays at its old place (D172 point 6): what names it by path, or that it moves at a start after an update.</summary>
    private static string Stays(HomeEntry entry) => entry.Name switch
    {
        "agent-homes" or "trees" or "help-room" or "intake" => "resumes",
        "session-files" or "quest-files" or "ask-files" => "kept",
        "bin" => "mcp",
        _ => "not-moved",
    };

    /// <summary>Read every wire, in the route's shape.</summary>
    public static JsonObject Read(WiringWorld world)
    {
        var secrets = Secrets(world);
        var reading = world.Home is { } measuredHome ? world.Sizes.Of(measuredHome) : null;
        var install = InstallOf(world.Home);
        var runsFrom = DaorisHome.InstallOf(world.BaseDirectory);
        var answer = new JsonObject
        {
            ["schema"] = Schema,
            ["reader"] = world.Window is null ? "terminal" : "window",
            ["measuring"] = world.Home is { } home && world.Sizes.Measuring(home),
            ["measured"] = reading is null ? null : Moments.Write(reading.Measured),
            ["partial"] = reading?.Partial ?? false,
            ["partialWhy"] = reading?.Why,
        };

        answer["folder"] = Folder(world, install, runsFrom, secrets);
        answer["folders"] = Folders(world, install, reading);
        answer["leftOver"] = LeftOver(world, reading);
        var (address, addressStatus) = Address(world);
        answer["service"] = Service(world, install, address, addressStatus);
        var (remotes, remotesWhy) = Remotes(world);
        answer["remotes"] = remotes;
        answer["remotesWhy"] = remotesWhy;
        answer["loop"] = Loop(world);
        answer["files"] = Files(world, reading, remotesWhy);
        answer["variables"] = Variables(world);
        answer["versions"] = Versions(world, install ?? runsFrom);
        answer["terminal"] = Terminal(world, install ?? runsFrom, address);
        answer["knowledge"] = Knowledge(world, reading);
        answer["logs"] = Logs(world, reading);
        answer["toolHome"] = ToolHome(world, reading);

        WiringRedact.Sweep(answer, secrets);
        return answer;
    }

    /// <summary>The answer as the terminal's <c>--json</c> prints it: the route's shape, indented, LF only.</summary>
    public static string Json(JsonObject wiring) => JsonFile.Serialize(wiring);

    /// <summary>The Daoris folder a home is in, as it runs: an install whose <c>data/</c> it is, or one it is the root of; null for any other.</summary>
    public static string? InstallOf(string? home)
    {
        if (home is null) return null;
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
        if (File.Exists(Path.Combine(full, DaorisHome.InstallMarker))) return full;
        return string.Equals(Path.GetFileName(full), DaorisHome.InstallData, StringComparison.OrdinalIgnoreCase)
            && Path.GetDirectoryName(full) is { } parent && File.Exists(Path.Combine(parent, DaorisHome.InstallMarker))
                ? parent
                : null;
    }

    /// <summary>The root a home's top folders are beside: its install's, or the home itself.</summary>
    public static string? RootOf(string? home) => InstallOf(home) ?? home;

    /// <summary>An assembly's informational version, as the release workflow stamped it.</summary>
    public static string? VersionOf(Assembly? assembly) =>
        assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? assembly?.GetName().Version?.ToString();

    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>Every secret this reader may meet: each key variable's value, here, in the account and on the machine, and each remote's key.</summary>
    private static List<string> Secrets(WiringWorld world)
    {
        var secrets = new List<string>();
        foreach (var variable in WiringVariables.Shown.Where(v => v.Value == WiringValue.Key))
        {
            foreach (var read in new[] { world.Process, world.Account, world.Machine })
            {
                if (read?.Invoke(variable.Name) is { Length: > 0 } value) secrets.Add(value.Trim());
            }
        }

        foreach (var remote in LoadRemotes(world).Map?.Values ?? []) secrets.Add(remote.Key);
        return secrets;
    }

    private static void Fact(JsonObject into, string name, JsonNode? value, string? why)
    {
        into[name] = value;
        into[name + "Why"] = value is null ? why : null;
    }

    private static JsonObject Folder(WiringWorld world, string? install, string? runsFrom, IReadOnlyCollection<string> secrets)
    {
        var folder = new JsonObject();
        Fact(folder, "path", world.Home, "no-home");
        folder["kind"] = world.Home is null ? null
            : runsFrom is null ? "development"
            : install is not null && SamePath(install, runsFrom) ? "install"
            : "named";

        var account = world.Account?.Invoke(WiringVariables.Home);
        var standing = world.Window?.HomeAccount
            ?? (world.Account is null || world.Home is null ? null : SamePath(world.Home, account) ? "same" : "this-start");
        Fact(folder, "account", standing, world.Account is null ? "no-account" : "no-home");
        var names = world.Account is null ? null
            : string.IsNullOrWhiteSpace(account) ? "none"
            : InstallOf(account) is { } named && !SamePath(named, account) ? "data"
            : "root";
        Fact(folder, "accountNames", names, "no-account");

        if (world.Window is { } window)
        {
            Fact(folder, "notice", WiringRedact.Text(window.HomeNotice, secrets), "none");
            Fact(folder, "hostNotice", WiringRedact.Text(window.HostNotice, secrets), "none");
        }
        else
        {
            Fact(folder, "notice", null, "window");
            Fact(folder, "hostNotice", null, "window");
        }

        return folder;
    }

    /// <summary>
    /// Each entry's place as it runs: each of its places that is there (D172 point 5), new before old; an entry known by the
    /// names it holds, where it holds one, as <c>DaorisHome</c> reads it.
    /// </summary>
    private static IReadOnlyList<string> Found(string home, HomeEntry entry) =>
        [.. DaorisHome.Places(home, entry).Where(place => entry.Holds.Length > 0
            ? entry.Holds.Any(name => Path.Exists(Path.Combine(place, name)))
            : Path.Exists(place))];

    private static JsonArray Folders(WiringWorld world, string? install, SizeReading? reading)
    {
        var rows = new JsonArray();
        var root = install ?? world.Home;
        foreach (var id in WiringSizes.TopFolders)
        {
            var row = new JsonObject { ["id"] = id };
            var category = Enum.TryParse<HomeCategory>(id, ignoreCase: true, out var parsed) ? parsed : (HomeCategory?)null;
            string? path = root is null ? null
                : category is null && install is null ? null
                : Path.Combine(root, id);
            Fact(row, "path", path, root is null ? "no-home" : "development");
            row["present"] = path is not null && Directory.Exists(path);

            var old = new JsonArray();
            long? bytes = reading is null || path is null && category is null ? null : reading.Folders.GetValueOrDefault(id);
            if (category is { } kind && world.Home is { } home)
            {
                foreach (var entry in HomeEntry.Table.Where(entry => entry.Category == kind))
                {
                    if (reading is not null) bytes += reading.Entries.GetValueOrDefault(entry.Name);
                    foreach (var place in Found(home, entry))
                    {
                        if (path is not null && Under(place, path)) continue;
                        old.Add(new JsonObject
                        {
                            ["entry"] = entry.Name,
                            ["path"] = place,
                            ["bytes"] = reading is null ? null : reading.Entries.GetValueOrDefault(entry.Name),
                            ["why"] = Stays(entry),
                        });
                    }
                }
            }

            Fact(row, "bytes", bytes, path is null && category is null ? "development" : world.Home is { } h && world.Sizes.Measuring(h) ? "measuring" : "not-measured");
            if (bytes is not null && reading is not null)
            {
                row["bytesWhy"] = reading.Partial ? reading.Why
                    : reading.Unreadable.Any(name => name == $"folder:{id}"
                        || (category is { } c && HomeEntry.Named(name)?.Category == c)) ? "unreadable" : null;
            }

            row["measured"] = reading is null ? null : Moments.Write(reading.Measured);
            row["old"] = old;
            rows.Add(row);
        }

        return rows;
    }

    private static JsonArray LeftOver(WiringWorld world, SizeReading? reading)
    {
        var rows = new JsonArray();
        if (world.Home is not { } home || !Directory.Exists(home)) return rows;
        // Each as the folder lists it: a name the table does not hold is no entry to ask for by name (HOMELAYOUT2's census).
        IEnumerable<(string Name, string Path)> found;
        try
        {
            found = [.. Directory.EnumerateFileSystemEntries(home).Select(path => (Path.GetFileName(path), path))];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return rows;
        }

        foreach (var (name, path) in found.Where(each => !WiringSizes.Held(each.Name)).OrderBy(each => each.Name, StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(new JsonObject
            {
                ["name"] = name,
                ["path"] = path,
                ["bytes"] = reading is null ? null : reading.LeftOver.GetValueOrDefault(name),
                ["why"] = WiringSizes.EarlierBuild.Contains(name) ? "earlier-build" : "not-daoris",
            });
        }

        return rows;
    }

    /// <summary>The address file read as a terminal reads it (D171, FINDDAORIS1): its entry, and the status that judged it.</summary>
    private static (JsonObject Address, ServiceProbe? Status) Address(WiringWorld world)
    {
        var address = new JsonObject();
        var entry = world.Home is { } home ? RunningAddress.Read(home) : null;
        address["present"] = entry is not null;
        address["url"] = WiringRedact.Address(entry?.Url);
        address["pid"] = entry?.Pid;
        address["since"] = entry is null ? null : Since(entry.Started);
        ServiceProbe? status = null;
        string? why;
        if (entry is null) why = "absent";
        else if (!world.Lives(entry.Pid, entry.Started)) why = "ended";
        else if (!RunningService.IsLoopback(entry.Url)) why = "not-loopback";
        else
        {
            status = world.Window is { } window && SameAddress(window.ServiceUrl, entry.Url) ? window.Status : world.AskStatus?.Invoke(entry.Url);
            // HOMEID1, as RunningService.Find judges it: only a host naming this home's id is taken, and none where it has no id.
            why = status is null ? "not-asked"
                : !status.Answers ? "not-answering"
                : HomeIdentity.Read(world.Home!) is not { } mine || status.Home != mine ? "another-home"
                : null;
        }

        address["taken"] = entry is null ? false : why is null ? true : why == "not-asked" ? null : false;
        address["why"] = why;
        address["raw"] = entry?.Url;
        return (address, status);
    }

    private static string? Since(long ticks)
    {
        try
        {
            return Moments.Write(new DateTimeOffset(ticks, TimeSpan.Zero));
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static JsonObject Service(WiringWorld world, string? install, JsonObject address, ServiceProbe? addressStatus)
    {
        var service = new JsonObject();
        var named = world.Process(WiringVariables.ServiceUrl);
        string? raw;
        string from;
        if (!string.IsNullOrWhiteSpace(named))
        {
            (raw, from) = (named.Trim(), "variable");
        }
        else if (world.Window is { } window)
        {
            (raw, from) = (window.ServiceUrl ?? WiringVariables.DefaultServiceUrl, "default");
        }
        else if (install is not null && DaorisHome.InstallOf(world.BaseDirectory) is not null && (bool?)address["taken"] == true)
        {
            // 🔴 Only a program an install carries takes the address file; a workspace build never does (ServiceHttp).
            (raw, from) = ((string?)address["raw"], "address-file");
        }
        else
        {
            (raw, from) = (null, "none");
        }

        address.Remove("raw");
        var url = WiringRedact.Address(raw);
        Fact(service, "url", url, raw is null ? "none" : "malformed");
        service["from"] = from;

        if (world.Window is { } held)
        {
            service["held"] = held.Held;
            service["heldWhy"] = null;
        }
        else
        {
            Fact(service, "held", null, "window");
        }

        var status = world.Window?.Status
            ?? (from == "address-file" ? addressStatus : raw is null ? null : world.AskStatus?.Invoke(raw));
        Fact(service, "answers", status?.Answers, "not-asked");
        if (status is { Answers: false }) service["answersWhy"] = status.Why;
        service["asked"] = status is null ? null : Moments.Write(status.Asked);
        Fact(service, "tier", status?.Tier, status is null ? "not-asked" : status.Answers ? "unsaid" : "not-answering");
        Fact(service, "version", status?.Version, status is null ? "not-asked" : status.Answers ? "unsaid" : "not-answering");
        Fact(service, "proved", world.Window?.Proved, world.Window is null ? "window" : "not-asked");

        var http = world.Process(WiringVariables.HttpHost);
        var host = ServiceHostLocator.Candidates(http, world.Home, world.BaseDirectory)
            .Select(candidate => candidate.Executable).FirstOrDefault(File.Exists);
        service["host"] = Located(host, http, world);
        var mcp = world.Process(WiringVariables.McpHost);
        service["connector"] = Located(KnowledgeConnector.Locate(mcp, world.Home, world.BaseDirectory), mcp, world);
        return service;
    }

    /// <summary>A host program found, and which of its places found it: the variable, beside the application, the home's <c>bin/</c>, or a development build.</summary>
    private static JsonObject Located(string? path, string? named, WiringWorld world)
    {
        var located = new JsonObject();
        Fact(located, "path", path, "not-found");
        located["from"] = path is null ? null
            : !string.IsNullOrWhiteSpace(named) && SamePath(path, named) ? "variable"
            : Under(path, world.BaseDirectory) ? "beside"
            : world.Home is { } home && Under(path, home) ? "home"
            : "workspace";
        return located;
    }

    private sealed record RemotesRead(IReadOnlyDictionary<string, RemoteTarget>? Map, string From, string? Why);

    private static RemotesRead LoadRemotes(WiringWorld world)
    {
        if (RemoteTarget.FromEnvironment(world.Process) is { } fromEnvironment) return new(fromEnvironment, "environment", null);
        var path = world.Process(WiringVariables.RemotesFile) is { Length: > 0 } named ? named
            : world.Home is { } home ? DaorisHome.Entry(home, HomeEntry.Remotes) : null;
        if (path is null || !File.Exists(path)) return new(new Dictionary<string, RemoteTarget>(), "file", null);
        try
        {
            var text = File.ReadAllText(path);
            try
            {
                using var _ = JsonDocument.Parse(text);
            }
            catch (JsonException)
            {
                return new(null, "file", "unparsed");
            }

            return new(RemoteTarget.Parse(text), "file", null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new(null, "file", "unreadable");
        }
    }

    private static (JsonArray Rows, string? Why) Remotes(WiringWorld world)
    {
        var read = LoadRemotes(world);
        var rows = new JsonArray();
        foreach (var (workspace, target) in (read.Map ?? new Dictionary<string, RemoteTarget>()).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var url = WiringRedact.Address(target.Url);
            rows.Add(new JsonObject
            {
                ["workspace"] = workspace,
                ["url"] = url,
                ["urlWhy"] = url is null ? "malformed" : null,
                ["keySet"] = !string.IsNullOrEmpty(target.Key),
                ["keyPrefix"] = WiringRedact.Prefix(target.Key),
                ["from"] = read.From,
            });
        }

        return (rows, read.Why);
    }

    private static JsonObject Loop(WiringWorld world)
    {
        var loop = new JsonObject();
        // Who holds the home's lease (HOMELEASE1): an instance for its life, or a verb, a migration or a swap for its act.
        var holder = world.Home is { } home ? HomeLease.HeldBy(home) : null;
        loop["holder"] = holder is null ? null : new JsonObject
        {
            ["kind"] = holder.Kind switch
            {
                LeaseKind.Desktop => "desktop",
                LeaseKind.Headless => "headless",
                LeaseKind.Verb => "verb",
                LeaseKind.Migration => "migration",
                LeaseKind.Swap => "swap",
                _ => "unknown",
            },
            ["instance"] = holder.IsInstance,
            // A verb's own words, its verb and never its arguments, as the lease keeps them.
            ["what"] = holder.What,
            ["pid"] = holder.Pid,
            ["since"] = Moments.Write(holder.Since),
            ["thisWindow"] = world.Window is { } window && holder.Kind == LeaseKind.Desktop && holder.Pid == window.ProcessId,
        };

        Fact(loop, "looked", world.Window?.Looked is { } looked ? Moments.Write(looked) : null, world.Window is null ? "window" : "starting");
        Fact(loop, "ready", world.Window?.Ready, "window");
        Fact(loop, "draining", world.Window?.Draining, world.Window is null ? "window" : "not-installed");

        var configPath = ConfigPath(world);
        DriverConfig? config = null;
        string? configWhy = configPath is null ? "no-home" : null;
        if (configPath is not null)
        {
            try
            {
                config = DriverConfig.Load(configPath);
            }
            catch (DriverConfigUnreadableException)
            {
                configWhy = "unparsed";
            }
        }

        Fact(loop, "holds", config?.Holds.Count, configWhy);
        Fact(loop, "pollSeconds", config?.PollSeconds, configWhy);
        return loop;
    }

    /// <summary>The person's choices as the one resolution finds them (CONFIGSEAM1): the override where set, else this home's file.</summary>
    private static string? ConfigPath(WiringWorld world) =>
        DriverConfig.FindPath(name => name == DaorisHome.Variable ? world.Home : world.Process(name));

    private static JsonArray Files(WiringWorld world, SizeReading? reading, string? remotesWhy)
    {
        var rows = new JsonArray();
        foreach (var (entry, overrides, door) in SettingsFiles) rows.Add(FileRow(world, reading, entry, overrides, door, record: false, remotesWhy));
        foreach (var (entry, overrides) in Records) rows.Add(FileRow(world, reading, entry, overrides, door: null, record: true, remotesWhy));
        return rows;
    }

    private static JsonObject FileRow(
        WiringWorld world, SizeReading? reading, HomeEntry entry, string? overrides, string? door, bool record, string? remotesWhy)
    {
        var named = overrides is null ? null : world.Process(overrides);
        string? path = null;
        string? pathWhy = world.Home is null ? "no-home" : null;
        if (entry == HomeEntry.DriverConfig) path = ConfigPath(world);
        else if (!string.IsNullOrWhiteSpace(named)) path = named.Trim();
        else if (world.Home is { } home)
        {
            try
            {
                path = DaorisHome.Entry(home, entry);
            }
            catch (HomeEntryTwiceException)
            {
                pathWhy = "twice";
            }
        }

        var isFile = path is not null && File.Exists(path);
        var present = isFile || (path is not null && Directory.Exists(path));
        var row = new JsonObject
        {
            ["id"] = entry.Name,
            ["folder"] = entry.Category.ToString().ToLowerInvariant(),
        };
        Fact(row, "path", path, pathWhy);
        row["present"] = present;

        long? bytes = null;
        if (isFile)
        {
            bytes = Length(path!);
            // A database's write-ahead log is the database's (design §3.8).
            if (path!.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) bytes += Length(path + "-wal") ?? 0;
        }
        else if (present && reading is not null)
        {
            bytes = reading.Entries.GetValueOrDefault(entry.Name);
        }

        Fact(row, "bytes", bytes, !present ? "absent" : "not-measured");
        row["written"] = present ? Written(path!) : null;

        bool? reads = null;
        string? readsWhy = null;
        if (record) readsWhy = "record";
        else if (!present) readsWhy = "absent";
        else if (entry == HomeEntry.Remotes) (reads, readsWhy) = (remotesWhy is null, remotesWhy);
        // 🔴 A sign-in's file is sized, never read (design §5): its folder holds keys.
        else if (entry.Category == HomeCategory.Profiles) readsWhy = "private";
        else (reads, readsWhy) = Parses(path!);
        row["reads"] = reads;
        row["readsWhy"] = readsWhy;

        row["overriddenBy"] = string.IsNullOrWhiteSpace(named) ? null : overrides;
        row["door"] = door;
        row["record"] = record;
        return row;
    }

    private static long? Length(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? Written(string path)
    {
        try
        {
            return Moments.Write(new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Whether a settings file parses, said as a code: what the parser said is never passed on, since it may quote the file.</summary>
    private static (bool? Reads, string? Why) Parses(string path)
    {
        if (!File.Exists(path)) return (null, "folder");
        try
        {
            using var stream = File.OpenRead(path);
            using var _ = JsonDocument.Parse(stream);
            return (true, null);
        }
        catch (JsonException)
        {
            return (false, "unparsed");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (false, "unreadable");
        }
    }

    private static JsonArray Variables(WiringWorld world)
    {
        var rows = new JsonArray();
        foreach (var variable in WiringVariables.Shown)
        {
            var here = Trimmed(world.Process(variable.Name));
            var account = Trimmed(world.Account?.Invoke(variable.Name));
            var machine = Trimmed(world.Machine?.Invoke(variable.Name));
            var row = new JsonObject { ["name"] = variable.Name, ["set"] = here is not null };

            string? valueWhy;
            string? value;
            string from;
            if (variable.ServiceOnly && world.Window is not { Held: WindowWiring.Hosted })
            {
                // 🔴 The second opinion's point 2: an adopted service's environment is its own, and a terminal knows no service's.
                (value, valueWhy, from) = (null, world.Window is null ? "window" : world.Window.Held == WindowWiring.Adopted ? "adopted" : "no-service", "unknown");
            }
            else
            {
                (value, valueWhy) = Shown(variable, here);
                from = here is null ? "not-set"
                    : variable.Name == WiringVariables.Home && world.Window is { HomeSetByDaoris: true } ? "set-by-daoris"
                    : here == account || here == machine ? "inherited"
                    : "this-start";
            }

            row["value"] = value;
            row["valueWhy"] = valueWhy;
            row["secret"] = variable.Value == WiringValue.Key;
            row["prefix"] = variable.Value == WiringValue.Key ? WiringRedact.Prefix(here) : null;
            row["from"] = from;
            row["account"] = Shown(variable, account).Value;
            row["accountSet"] = world.Account is null ? null : account is not null;
            row["machine"] = Shown(variable, machine).Value;
            row["machineSet"] = world.Machine is null ? null : machine is not null;
            row["instead"] = here is null ? variable.Instead : null;
            row["readBy"] = new JsonArray([.. variable.ReadBy.Select(reader => (JsonNode?)reader)]);
            rows.Add(row);
        }

        return rows;
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>A variable's value as it may be shown (design §5): a key never, an address cut, and why not where it is not.</summary>
    private static (string? Value, string? Why) Shown(WiringVariable variable, string? value) => value is null ? (null, "not-set") : variable.Value switch
    {
        WiringValue.Key => (null, "secret"),
        WiringValue.Address => WiringRedact.Address(value) is { } address ? (address, null) : (null, "malformed"),
        _ => (value, null),
    };

    private static JsonObject Versions(WiringWorld world, string? install)
    {
        var versions = new JsonObject();
        var program = world.Window?.Program;
        var plus = program?.IndexOf('+') ?? -1;
        Fact(versions, "program", plus > 0 ? program![..plus] : program, world.Window is null ? "window" : "unsaid");
        Fact(versions, "build", plus > 0 ? program![(plus + 1)..] : null, world.Window is null ? "window" : "unsaid");
        Fact(versions, "kit", world.Window?.Kit, "window");
        Fact(versions, "cli", install is null ? null : PackageVersion(Path.Combine(install, "app", "cli", "node_modules", "daoris", "package.json")), install is null ? "development" : "not-carried");
        var driver = world.DriverVersion
            ?? (install is null ? null : FileVersion(Path.Combine(install, "app", "daoris-driver", "daoris-driver.dll")));
        Fact(versions, "driver", driver, install is null ? "development" : "not-carried");
        var status = world.Window?.Status;
        Fact(versions, "service", status?.Version, status is null ? world.Window is null ? "window" : "not-asked" : "unsaid");
        var connector = KnowledgeConnector.Locate(world.Process(WiringVariables.McpHost), world.Home, world.BaseDirectory);
        Fact(versions, "connector", connector is null ? null : FileVersion(connector), connector is null ? "not-found" : "unsaid");
        versions["dotnet"] = Environment.Version.ToString();
        versions["dotnetWhy"] = null;
        // Node as Tools resolves it would start it to ask: Settings → Tools says it (design §3.6's door).
        Fact(versions, "node", null, "tools");
        return versions;
    }

    private static string? PackageVersion(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.String
                    ? version.GetString()
                    : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>A program's version from its file's version resource: read, never run.</summary>
    private static string? FileVersion(string path)
    {
        try
        {
            return File.Exists(path) && FileVersionInfo.GetVersionInfo(path).ProductVersion is { Length: > 0 } version ? version : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static JsonObject Terminal(WiringWorld world, string? install, JsonObject address)
    {
        var terminal = new JsonObject();
        var bin = install is null ? null : Path.Combine(install, "app", "bin");
        terminal["launchers"] = new JsonArray([.. (bin is null ? [] : Launchers).Select(name =>
            (JsonNode?)new JsonObject { ["name"] = name, ["present"] = File.Exists(Path.Combine(bin!, name)) })]);
        terminal["launchersWhy"] = bin is null ? "development" : null;

        var accountPath = Folders(world.Account?.Invoke("PATH"));
        var machinePath = Folders(world.Machine?.Invoke("PATH"));
        Fact(terminal, "onAccountPath", bin is null || world.Account is null ? null : accountPath.Any(folder => SamePath(folder, bin)),
            bin is null ? "development" : "no-account");
        Fact(terminal, "onMachinePath", bin is null || world.Machine is null ? null : machinePath.Any(folder => SamePath(folder, bin)),
            bin is null ? "development" : "no-machine");

        // What a new terminal runs (design §3.7): Windows hands a new process the machine's PATH, then the account's.
        var extensions = (world.Account?.Invoke("PATHEXT") ?? world.Machine?.Invoke("PATHEXT") ?? world.Process("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var order = machinePath.Concat(accountPath).ToArray();
        terminal["firstDaoris"] = First(order, "daoris", extensions, bin);
        terminal["firstDriver"] = First(order, "daoris-driver", extensions, bin);
        terminal["address"] = address;
        return terminal;
    }

    private static string[] Folders(string? path) =>
        path is null ? [] : [.. path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(folder => Environment.ExpandEnvironmentVariables(folder.Trim('"')))];

    private static JsonObject First(IEnumerable<string> folders, string name, string[] extensions, string? bin)
    {
        foreach (var folder in folders)
        {
            foreach (var candidate in extensions.Select(extension => name + extension.ToLowerInvariant()).Append(name))
            {
                string full;
                try
                {
                    full = Path.Combine(folder, candidate);
                }
                catch (ArgumentException)
                {
                    break;
                }

                if (!File.Exists(full)) continue;
                var ours = bin is not null && SamePath(folder, bin);
                return new JsonObject { ["is"] = ours ? "this" : "another", ["path"] = ours ? null : full };
            }
        }

        return new JsonObject { ["is"] = "none", ["path"] = null };
    }

    private static JsonObject Knowledge(WiringWorld world, SizeReading? reading)
    {
        var knowledge = new JsonObject();
        var named = Trimmed(world.Process(WiringVariables.KnowledgeDb));
        var path = named ?? (world.Home is { } home ? DaorisHome.Entry(home, HomeEntry.Knowledge) : null);
        Fact(knowledge, "path", path, "no-home");
        knowledge["from"] = named is not null ? "variable" : path is null ? null : "home";
        long? bytes = named is not null ? Length(named) is { } file ? file + (Length(named + "-wal") ?? 0) : null
            : reading?.Entries.GetValueOrDefault(HomeEntry.Knowledge.Name);
        Fact(knowledge, "bytes", bytes, path is not null && !File.Exists(path) ? "absent" : "not-measured");

        string? root = null;
        string? rootWhy;
        if (world.Window is { Held: WindowWiring.Hosted })
        {
            root = Trimmed(world.Process(WiringVariables.KnowledgeRoot));
            rootWhy = "not-set";
        }
        else rootWhy = world.Window is null ? "window" : world.Window.Held == WindowWiring.Adopted ? "adopted" : "no-service";

        Fact(knowledge, "familyRoot", root, rootWhy);
        return knowledge;
    }

    private static JsonObject Logs(WiringWorld world, SizeReading? reading)
    {
        var logs = new JsonObject();
        var path = world.Home is { } home ? DaorisHome.Entry(home, HomeEntry.Logs) : null;
        Fact(logs, "path", path, "no-home");
        Fact(logs, "bytes", reading?.Entries.GetValueOrDefault(HomeEntry.Logs.Name), "not-measured");
        logs["days"] = LogDays;
        string? newest = null;
        if (path is not null && Directory.Exists(path))
        {
            try
            {
                newest = new DirectoryInfo(path).EnumerateFiles().Select(file => file.LastWriteTimeUtc).DefaultIfEmpty().Max() is { } at
                    && at != default ? Moments.Write(new DateTimeOffset(at, TimeSpan.Zero)) : null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                newest = null;
            }
        }

        Fact(logs, "newest", newest, "absent");
        logs["engineLog"] = world.Home is { } h && File.Exists(Path.Combine(DaorisHome.Entry(h, HomeEntry.Window), "chromium", "cef.log"));
        return logs;
    }

    /// <summary>
    /// What Daoris's sessions left in the person's own tool home (OWNSPACE2, D175 point 3): the project folders its own working
    /// roots made, by name, each with its size and its newest file's time once measured. Never read inside a folder beyond that.
    /// </summary>
    private static JsonObject ToolHome(WiringWorld world, SizeReading? reading)
    {
        var (path, from, pathWhy) = ToolHomeLeftovers.ToolHomeOf(world);
        var listing = ToolHomeLeftovers.List(world);
        var folders = new JsonArray();
        foreach (var folder in listing.Folders)
        {
            // `by` is the session records that name it: why it is listed as Daoris's.
            var row = new JsonObject
            {
                ["name"] = folder.Name, ["path"] = folder.Path, ["source"] = folder.Source, ["keeps"] = folder.Keeps,
                ["by"] = new JsonArray([.. folder.By.Select(id => (JsonNode?)JsonValue.Create(id))]),
            };
            var measure = reading?.Tool?.GetValueOrDefault(folder.Name);
            Fact(row, "bytes", measure?.Bytes, world.Home is { } home && world.Sizes.Measuring(home) ? "measuring" : "not-measured");
            row["partial"] = measure?.Partial ?? false;
            Fact(row, "newest", measure?.Newest is { } at ? Moments.Write(at) : null, measure is null ? "not-measured" : "empty");
            folders.Add(row);
        }

        var tool = new JsonObject();
        Fact(tool, "path", path, pathWhy ?? "no-tool-home");
        tool["from"] = from;
        tool["present"] = ToolHomeLeftovers.ProjectsOf(world) is { } projects && Directory.Exists(projects);
        // Why nothing is listed where nothing can be (a link on the way, a home that is not the person's), and what was left out.
        tool["why"] = listing.Why;
        tool["caseInsensitive"] = listing.CaseInsensitive;
        tool["ambiguous"] = listing.Ambiguous;
        tool["uncertain"] = listing.Uncertain;
        tool["folders"] = folders;
        return tool;
    }

    // ---------------------------------------------------------------------------------------------------------------------

    private static string Full(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));

    private static StringComparison PathCase => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Two spellings of one folder, as <c>InstallHome</c> compares them.</summary>
    internal static bool SamePath(string a, string? b)
    {
        if (string.IsNullOrWhiteSpace(b)) return false;
        try
        {
            return string.Equals(Full(a), Full(b), PathCase);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool Under(string path, string folder)
    {
        try
        {
            var full = Full(path);
            var parent = Full(folder);
            return string.Equals(full, parent, PathCase)
                || full.StartsWith(parent + Path.DirectorySeparatorChar, PathCase);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool SameAddress(string? a, string b) =>
        a is not null && string.Equals(a.Trim().TrimEnd('/'), b.Trim().TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
}
