using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>How a tool is run (D121 §2.2): the system's, from <c>PATH</c>; managed; or a file the person names.</summary>
public enum ToolWay
{
    System,
    Managed,
    File,
}

/// <summary>A program Daoris starts, or hands to a session, that is neither an agent (D57) nor Daoris's own (§2.1).</summary>
/// <param name="Id">How the file and the doors name it.</param>
/// <param name="Name">Its product's name, as a person knows it.</param>
/// <param name="Answers">The names it answers for on a child's <c>PATH</c>; the first is the program itself.</param>
/// <param name="Version">The arguments that ask its version.</param>
/// <param name="Settings">The settings it may carry (§2.5): git's allow-list, and none for the rest.</param>
public sealed record ToolDeclaration(
    string Id, string Name, IReadOnlyList<string> Answers, IReadOnlyList<string> Version, IReadOnlyList<string> Settings);

/// <summary>One tool's entry as read: its way, or why it is refused.</summary>
/// <param name="Way">Null when the entry, or the file, could not say one.</param>
/// <param name="Problem">Why the entry is refused: its tool is never run another way. Null when it reads.</param>
public sealed record ToolEntry(ToolWay? Way, string? Version, string? File, string? Problem);

/// <summary><c>tools.json</c> as it stands.</summary>
/// <param name="Entries">Every declared tool, in the declared order.</param>
/// <param name="Unknown">The ids the file names that this build does not declare: kept, never applied (rule 3).</param>
/// <param name="Git">The settings Daoris's git carries (rule 4).</param>
/// <param name="Locations">The resource locations, in order (rule 5).</param>
/// <param name="Notes">What the file keeps without applying, or skips, and why.</param>
/// <param name="Problem">Why the whole file does not read; every tool is then refused.</param>
public sealed record ToolsRead(
    string Path, bool Exists, IReadOnlyDictionary<string, ToolEntry> Entries, IReadOnlyList<string> Unknown,
    IReadOnlyDictionary<string, string> Git, IReadOnlyList<string> Locations, IReadOnlyList<string> Notes, string? Problem);

/// <summary>Which file a tool is, or why none.</summary>
/// <param name="File">The file that starts; null for a refusal, and for a system tool <c>PATH</c> does not find.</param>
/// <param name="Refused">
/// True when the way set cannot run: it never falls back to <c>PATH</c>. False with a problem is a system tool
/// <c>PATH</c> does not find, which is said, and whose callers keep today's behaviour (§2.3).
/// </param>
public sealed record ToolResolution(string Tool, ToolWay? Way, string? Version, string? File, bool Refused, string? Problem);

/// <summary>
/// The programs Daoris runs beside its agents, and which file each one is (TOOLS2, D121;
/// <c>docs/2026-10-01-tools-design.md</c> §2.1–§2.3). The way lives in <c>$DAORIS_HOME/tools.json</c>, one
/// file for both doors (D50), and absent means the system's.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The file's one reader and writer.</b> The FILE is the contract, and <c>ToolsTests</c> carries its tables. The CLI's
/// <c>tools.ts</c> was a twin of the reading and the resolution, held to the same rows, until its last reader left (PUSH1f2f, D194);
/// its writers had left with <c>daoris tool</c>, which is <c>daoris-driver tool</c> (PUSH1f2c).</para>
/// <list type="number">
/// <item>No file, no entry, or <c>"use": "system"</c> is the system's.</item>
/// <item><c>managed</c> needs an exact version of one to four numbers; <c>file</c> needs a whole path. An
/// entry that names another way's field, or lacks its own, is refused whole: its tool is never run another
/// way.</item>
/// <item>A tool id this build does not declare is kept as written and never applied.</item>
/// <item><c>git</c> holds only keys on the allow-list: refused on a write; on a read kept, not applied, said.</item>
/// <item><c>locations</c> holds only https://, or http:// to this machine: refused on a write; skipped and
/// said.</item>
/// <item>Setting one way clears the others, and a writer keeps what it has no field for.</item>
/// </list>
/// <para>Then the resolution: the way set decides which file starts. A managed version nobody downloaded, or
/// a named file that is gone, refuses and <b>never</b> falls back to <c>PATH</c> (D57's pin rule, read for a
/// tool). It reads and writes files and answers "which program"; handing that answer to every start is
/// the Children section's at the end of this file (TOOLS5).</para>
/// </remarks>
public static class Tools
{
    /// <summary>The file under the home. The CLI's <c>TOOLS_FILE</c>.</summary>
    public const string FileName = "tools.json";

    /// <summary>Where managed versions live: <c>&lt;home&gt;/tools/&lt;tool&gt;/&lt;version&gt;/</c>. The CLI's <c>TOOLS_FOLDER</c>.</summary>
    public const string Folder = "tools";

    /// <summary>
    /// A downloaded version's record, in its version folder: finding it is the proof the download verified
    /// (§3.6). TOOLS4 writes it; this reads its <c>exe</c>, which names the executable inside
    /// <see cref="Package"/>, where the archive was unpacked whole. The CLI's <c>TOOL_RECORD</c>.
    /// </summary>
    public const string Record = "tool.json";

    /// <summary>The folder a version's archive is unpacked into. The CLI's <c>TOOL_PACKAGE</c>.</summary>
    public const string Package = "package";

    /// <summary>
    /// The tools this build runs, in the order a child's <c>PATH</c> takes them (§2.4). 🔴 Declared in code on
    /// both sides (<c>TOOLS</c> in <c>tools.ts</c> is the other copy), never read from a file: a list may
    /// offer versions of these, and can never make Daoris run a program its code does not name.
    /// </summary>
    public static readonly IReadOnlyList<ToolDeclaration> Declared =
    [
        new("git", "Git", ["git"], ["--version"], ["core.sshCommand"]),
        new("node", "Node.js", ["node", "npm", "npx"], ["--version"], []),
        new("pwsh", "PowerShell", ["pwsh"], ["--version"], []),
        new("gh", "GitHub CLI", ["gh"], ["--version"], []),
        new("az", "Azure CLI", ["az"], ["version"], []),
    ];

    /// <summary>What Daoris's git may carry (§2.5): one key, the one WSR7 measured the need for.</summary>
    public static IReadOnlyList<string> GitSettings => Find("git")!.Settings;

    /// <summary>This machine, named: the hosts an http:// location may have (rule 5).</summary>
    private static readonly string[] Loopback = ["localhost", "127.0.0.1", "[::1]"];

    private static readonly string[] Ways = ["system", "managed", "file"];

    /// <summary>One to four numbers (§3.2), ASCII digits only; <c>\z</c>, since .NET's <c>$</c> passes a final newline.</summary>
    private static readonly Regex ExactVersion = new(@"^[0-9]+(?:\.[0-9]+){0,3}\z", RegexOptions.CultureInvariant);

    private const string Never = "it never falls back to PATH";

    /// <summary>A declared tool by its id, or null.</summary>
    public static ToolDeclaration? Find(string id) => Declared.FirstOrDefault(tool => tool.Id == id);

    /// <summary>
    /// Whether a path is whole: fully qualified, so it does not depend on where a process stands. The CLI's
    /// <c>isWholePath</c> spells this rule for itself.
    /// </summary>
    public static bool IsWholePath(string path) => Path.IsPathFullyQualified(path);

    /// <summary>
    /// The file as it stands (rules 1–5). Reading creates nothing, and a file that does not read refuses every
    /// tool — never read as empty, which would run <c>PATH</c>'s program in place of the one chosen.
    /// </summary>
    public static ToolsRead Read(string home)
    {
        var path = DaorisHome.Entry(home, HomeEntry.Tools);
        var exists = File.Exists(path);
        var entries = new Dictionary<string, ToolEntry>(StringComparer.Ordinal);
        var unknown = new List<string>();
        var git = new Dictionary<string, string>(StringComparer.Ordinal);
        var locations = new List<string>();
        var notes = new List<string>();

        var (root, problem) = Load(path);
        var tools = root?["tools"];
        var fileProblem = problem ?? (root is not null && tools is not null and not JsonObject ? $"{path}'s `tools` is not an object" : null);
        if (fileProblem is not null)
        {
            var refused = $"{fileProblem}, and every tool is refused, never run another way: fix it, or delete it to run every "
                + "tool from PATH";
            foreach (var tool in Declared) entries[tool.Id] = new ToolEntry(null, null, null, refused);
            return new ToolsRead(path, exists, entries, unknown, git, locations, notes, refused);
        }

        var held = tools as JsonObject ?? new JsonObject();
        foreach (var tool in Declared)
        {
            if (held[tool.Id] is not { } value)
            {
                entries[tool.Id] = new ToolEntry(ToolWay.System, null, null, null);
                continue;
            }

            var (entry, why) = Judge(value);
            entries[tool.Id] = entry ?? new ToolEntry(
                null, null, null,
                $"the entry for `{tool.Id}` in {path} does not read ({why}), and {tool.Name} is never run another way: "
                + $"fix the entry, or `daoris tool use {tool.Id} system` writes a new one");
        }

        // Rule 3: an id a newer build may know.
        foreach (var (id, _) in held)
        {
            if (Find(id) is not null) continue;
            unknown.Add(id);
            notes.Add($"`{id}` in `tools` is not a tool this build runs: it is kept as written, and never applied");
        }

        // Rule 4: what git carries.
        if (root?["git"] is { } gitNode)
        {
            if (gitNode is not JsonObject settings)
            {
                notes.Add("`git` is not an object: it is kept, and nothing in it is applied");
            }
            else
            {
                foreach (var (key, setting) in settings)
                {
                    if (!GitSettings.Contains(key, StringComparer.Ordinal))
                    {
                        notes.Add($"`{key}` in `git` is not a setting Daoris's git carries: it is kept, and not applied");
                    }
                    else if (Text(setting) is not { } text)
                    {
                        notes.Add($"`{key}` in `git` is not text: it is kept, and not applied");
                    }
                    else
                    {
                        git[key] = text;
                    }
                }
            }
        }

        // Rule 5: where versions come from.
        if (root?["locations"] is { } locationsNode)
        {
            if (locationsNode is not JsonArray addresses)
            {
                notes.Add("`locations` is not a list: it is kept, and no location is read");
            }
            else
            {
                foreach (var item in addresses)
                {
                    if (Text(item) is not { } address) notes.Add("an entry in `locations` is not text, and is skipped");
                    else if (LocationProblem(address) is not null)
                    {
                        notes.Add($"`{address}` in `locations` is skipped: an address is https://, or http:// to this machine");
                    }
                    else locations.Add(address);
                }
            }
        }

        return new ToolsRead(path, exists, entries, unknown, git, locations, notes, null);
    }

    /// <summary>A key off git's allow-list, refused on a write (rule 4); null for a key on it.</summary>
    public static string? GitKeyProblem(string key) =>
        GitSettings.Contains(key, StringComparer.Ordinal)
            ? null
            : $"`{key}` is not a setting Daoris's git carries — the one it carries is {string.Join(", ", GitSettings)}";

    /// <summary>Whether a host, as a URL names it, is this machine. The CLI's <c>isLoopbackHost</c>.</summary>
    public static bool IsLoopback(string host) => Loopback.Contains(host, StringComparer.Ordinal);

    /// <summary>
    /// Whether an address is one Daoris reads from: https://, or http:// to this machine. Rule 5's for a location,
    /// and a resource list's for a download (§3.2, TOOLS3). The CLI's <c>isAddress</c>.
    /// </summary>
    public static bool IsAddress(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)) return false;
        if (uri.Scheme == Uri.UriSchemeHttps) return true;
        return uri.Scheme == Uri.UriSchemeHttp && IsLoopback(uri.Host);
    }

    /// <summary>An address that is no resource location, refused on a write (rule 5); null for one that is.</summary>
    public static string? LocationProblem(string address) =>
        IsAddress(address)
            ? null
            : $"`{address}` is not a resource location — an address is https://, or http:// to this machine "
              + $"({Loopback[0]}, {Loopback[1]} or {Loopback[2]})";

    /// <summary>
    /// Whether a path names something inside a folder: relative, in <c>/</c> form, with no empty, <c>.</c> or
    /// <c>..</c> part, and no <c>\</c> or drive. A record's <c>exe</c> (§3.6) and a list's <c>exe</c> and
    /// <c>paths</c> (§3.2). The CLI's <c>isInsidePath</c>.
    /// </summary>
    public static bool IsInside(string path) => SafeNames.IsRepositoryPath(path);

    /// <summary>An exact version: one to four numbers (§3.2), in <c>tools.json</c> and in a resource list alike.</summary>
    public static bool IsExactVersion(string version) => ExactVersion.IsMatch(version);

    /// <summary>Which file a tool is, read from the home's file now.</summary>
    /// <param name="path">The <c>PATH</c> a system tool is found on; null for this process's own.</param>
    public static ToolResolution Resolve(string home, string tool, string? path = null) => Resolve(Read(home), home, tool, path);

    /// <summary>
    /// Which file a tool is, from a read of the file (§2.3). The way set decides: a named file, a downloaded
    /// version, or <c>PATH</c> by PATHEXT — the one resolver for a bare name, <see cref="CommandPresence"/>,
    /// with <c>startable</c>.
    /// </summary>
    /// <exception cref="DriverException">A tool this build does not declare.</exception>
    public static ToolResolution Resolve(ToolsRead read, string home, string tool, string? path = null)
    {
        var declared = Find(tool) ?? throw new DriverException(Undeclared(tool));
        var entry = read.Entries[tool];

        if (entry.Problem is not null) return new ToolResolution(tool, entry.Way, entry.Version, null, true, entry.Problem);

        if (entry.Way == ToolWay.File)
        {
            return File.Exists(entry.File)
                ? new ToolResolution(tool, entry.Way, null, entry.File, false, null)
                : new ToolResolution(tool, entry.Way, null, null, true,
                    $"{declared.Name} runs the file {entry.File}, and there is no file there — {Never}. "
                    + $"`daoris tool use {tool} file <path>` names another, and `daoris tool use {tool} system` runs the one on PATH");
        }

        if (entry.Way == ToolWay.Managed)
        {
            var (file, problem) = ManagedFile(home, declared, entry.Version!);
            return new ToolResolution(tool, entry.Way, entry.Version, file, file is null, problem);
        }

        var found = CommandPresence.Resolve(declared.Answers[0], path, startable: true);
        return found is not null
            ? new ToolResolution(tool, entry.Way, null, found, false, null)
            : new ToolResolution(tool, entry.Way, null, null, false,
                $"`{declared.Answers[0]}` is not on this machine's PATH. A tool is run as the system's, managed, or from a "
                + $"file you name: `daoris tool use {tool} file <path>` names one");
    }

    /// <summary>The system's: the one <c>PATH</c> finds, as before (rule 6).</summary>
    /// <exception cref="DriverException">An undeclared tool, or a file this build cannot read; nothing is written.</exception>
    public static void UseSystem(string home, string tool) => SetWay(home, tool, "system", null, null, read: null);

    /// <summary><see cref="UseSystem(string, string)"/>, with what happens once the file is read: a test's other writer.</summary>
    internal static void UseSystem(string home, string tool, Action? read) => SetWay(home, tool, "system", null, null, read);

    /// <summary>A file the person names, by its whole path (rule 6); one that is not there is refused.</summary>
    /// <exception cref="DriverException">An undeclared tool, a path that is not whole or holds no file, or a file
    /// this build cannot read; nothing is written.</exception>
    public static void UseFile(string home, string tool, string file)
    {
        if (Find(tool) is null) throw new DriverException(Undeclared(tool));
        if (!IsWholePath(file)) throw new DriverException($"`{file}` is not a whole path — name the executable by its whole path.");
        if (!File.Exists(file)) throw new DriverException($"no file at {file} — a tool is a file that is there.");
        SetWay(home, tool, "file", null, file, read: null);
    }

    /// <summary>
    /// Managed at one exact version (TOOLS4), written only once that version is downloaded: a pin nobody installed
    /// refuses every start (D57), so it is never written ahead of its download. <see cref="ToolInstall"/> downloads,
    /// then writes. The CLI's <c>useManaged</c>.
    /// </summary>
    /// <exception cref="DriverException">An undeclared tool, a version that is not exact or not downloaded, or a file
    /// this build cannot read; nothing is written.</exception>
    public static void UseManaged(string home, string tool, string version)
    {
        var declared = Find(tool) ?? throw new DriverException(Undeclared(tool));
        if (!IsExactVersion(version))
        {
            throw new DriverException($"`{version}` is not an exact version — one to four numbers, like 2.51.0 — so nothing was written.");
        }
        if (ManagedFile(home, declared, version).File is null)
        {
            throw new DriverException(
                $"{declared.Name} {version} is not downloaded ({DaorisHome.Entry(home, HomeEntry.ToolVersions, tool, version)}), so nothing was written: a "
                + $"managed version nobody downloaded would refuse every start. `daoris tool use {tool} managed {version}` downloads it, "
                + "then uses it");
        }

        SetWay(home, tool, "managed", version, null, read: null);
    }

    /// <summary>
    /// A downloaded version's executable, or why there is none (§3.6's layout): its record, naming a file that is
    /// there. Finding it is the proof the download verified. The CLI's <c>managedExecutable</c>.
    /// </summary>
    /// <exception cref="DriverException">A tool this build does not declare.</exception>
    public static (string? File, string? Problem) ManagedExecutable(string home, string tool, string version) =>
        ManagedFile(home, Find(tool) ?? throw new DriverException(Undeclared(tool)), version);

    /// <summary>
    /// Add a resource location at the end, after the person's others and before the list built in (§3.3). The CLI's
    /// <c>addLocation</c>.
    /// </summary>
    /// <returns>False when it is already listed, and nothing is written.</returns>
    /// <exception cref="DriverException">An address that is no resource location, or a file whose <c>locations</c>
    /// this build cannot read; nothing is written.</exception>
    public static bool AddLocation(string home, string address) => AddLocation(home, address, read: null);

    /// <summary><see cref="AddLocation(string, string)"/>, with what happens once the file is read: a test's other writer.</summary>
    internal static bool AddLocation(string home, string address, Action? read)
    {
        if (LocationProblem(address) is { } problem) throw new DriverException($"{problem} — nothing was written.");
        using var locked = KeepLock.Take(DaorisHome.Entry(home, HomeEntry.Tools));
        var (path, root, locations, understood) = WritableLocations(home);
        read?.Invoke();
        if (locations.Any(each => Text(each) == address)) return false;
        locations.Add(address);
        Keep(path, understood, root);
        return true;
    }

    /// <summary>
    /// Remove a resource location (§3.3): its versions are no longer offered, and a version already downloaded stays.
    /// Every other entry stays as written, one that is not an address included. The CLI's <c>removeLocation</c>.
    /// </summary>
    /// <returns>False when it was not listed, and nothing is written.</returns>
    public static bool RemoveLocation(string home, string address) => RemoveLocation(home, address, read: null);

    /// <summary><see cref="RemoveLocation(string, string)"/>, with what happens once the file is read: a test's other writer.</summary>
    internal static bool RemoveLocation(string home, string address, Action? read)
    {
        if (!File.Exists(DaorisHome.Entry(home, HomeEntry.Tools))) return false;
        using var locked = KeepLock.Take(DaorisHome.Entry(home, HomeEntry.Tools));
        var (path, root, locations, understood) = WritableLocations(home);
        read?.Invoke();
        var removed = false;
        for (var at = locations.Count - 1; at >= 0; at--)
        {
            if (Text(locations[at]) != address) continue;
            locations.RemoveAt(at);
            removed = true;
        }

        if (!removed) return false;
        Keep(path, understood, root);
        return true;
    }

    /// <summary>
    /// The file and its <c>locations</c> to write over, with the file as read, or the refusal that leaves it as it is.
    /// </summary>
    private static (string Path, JsonObject Root, JsonArray Locations, string? Read) WritableLocations(string home)
    {
        var (path, root, read) = Writable(home);
        if (root["locations"] is { } held && held is not JsonArray)
        {
            throw new DriverException($"{path}'s `locations` is not a list, so nothing was written — fix it, or delete the key to start from nothing.");
        }

        if (root["locations"] is not JsonArray locations)
        {
            locations = [];
            root["locations"] = locations;
        }

        return (path, root, locations, read);
    }

    /// <summary>
    /// The file as an object to edit, and as it read, written by this writer (null where there was none); or the refusal that
    /// leaves it as it is.
    /// </summary>
    private static (string Path, JsonObject Root, string? Read) Writable(string home)
    {
        var path = DaorisHome.Entry(home, HomeEntry.Tools);
        var (root, problem) = Load(path);
        if (problem is not null) throw new DriverException($"{problem}. Fix it, or delete it to start from nothing — nothing was written.");
        return root is null ? (path, new JsonObject(), null) : (path, root, JsonFile.Serialize(root));
    }

    /// <summary>
    /// The edit from <paramref name="read"/> to <paramref name="root"/> made onto the file as it stands (KEEPALL1c,
    /// <see cref="JsonKeeping.Save"/>): its line ends kept, a key written twice refused, no edit nothing written. Written from the
    /// nodes, it wrote LF over a file in CRLF and edited a file holding a key twice.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Its writer holds the file's <see cref="KeepLock"/> from its read to here</b> (PUSH1f2c, D194 point 3, the alone-path
    /// design §1.4): each writer decides its edit from the file as it read it (a location already listed, the list it adds to), so a
    /// change another writer made between that read and this write was lost where the edit wrote <c>locations</c> whole. A terminal's
    /// change beside a running window's is the case: the lease keeps them apart only while no instance runs.
    /// </remarks>
    private static void Keep(string path, string? read, JsonObject root) =>
        JsonKeeping.SaveHolding(path, read is not null, read ?? "{}\n", JsonFile.Serialize(root));

    /// <summary>
    /// Set one tool's way (rule 6): the other ways' fields go, and every key the writer has no field for stays,
    /// on the entry and on the file. Refused over a file it could not read, and over a <c>tools</c> that is no
    /// object.
    /// </summary>
    private static void SetWay(string home, string id, string way, string? version, string? file, Action? read)
    {
        if (Find(id) is null) throw new DriverException(Undeclared(id));
        using var locked = KeepLock.Take(DaorisHome.Entry(home, HomeEntry.Tools));
        var (path, root, understood) = Writable(home);
        read?.Invoke();
        if (root["tools"] is { } held && held is not JsonObject)
        {
            throw new DriverException(
                $"{path}'s `tools` is not an object, so nothing was written — fix it, or delete the file to start from nothing.");
        }

        if (root["tools"] is not JsonObject tools)
        {
            tools = new JsonObject();
            root["tools"] = tools;
        }

        if (tools[id] is not JsonObject entry)
        {
            entry = new JsonObject();
            tools[id] = entry;
        }

        entry.Remove("version");
        entry.Remove("file");
        entry["use"] = way;
        if (version is not null) entry["version"] = version;
        if (file is not null) entry["file"] = file;
        Keep(path, understood, root);
    }

    /// <summary>A downloaded version's executable, or why there is none (§3.6's layout).</summary>
    private static (string? File, string? Problem) ManagedFile(string home, ToolDeclaration tool, string version)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.ToolVersions, tool.Id, version);
        var back = $"— {Never}. `daoris tool use {tool.Id} managed {version}` downloads it, and `daoris tool use {tool.Id} system` "
            + "runs the one on PATH";
        var record = Path.Combine(folder, Record);
        if (!File.Exists(record))
        {
            return (null, $"{tool.Name} is managed at {version}, and that version is not downloaded ({folder}) {back}");
        }

        (string? File, string? Problem) Unread(string why) =>
            (null, $"{tool.Name} is managed at {version}, and its record does not read ({why}) {back}");

        var (value, problem) = Load(record);
        if (problem is not null) return Unread(problem);
        if (Text(value!["exe"]) is not { Length: > 0 } exe) return Unread($"{record}: it names no `exe`");
        if (!IsInside(exe)) return Unread($"{record}: its `exe` is not a relative path inside the package");

        var file = Path.Combine([folder, Package, .. exe.Split('/')]);
        return File.Exists(file)
            ? (file, null)
            : (null, $"{tool.Name} is managed at {version}, and its record names {file}, and there is no file there {back}");
    }

    /// <summary>How a way reads in a sentence.</summary>
    internal static string Words(ToolWay way) => way switch
    {
        ToolWay.System => "the system's",
        ToolWay.Managed => "managed",
        _ => "a file",
    };

    /// <summary>One entry of <c>tools</c>, judged (rule 2): its way, or the reason it is refused.</summary>
    private static (ToolEntry? Entry, string? Why) Judge(JsonNode value)
    {
        if (value is not JsonObject entry) return (null, "it is not an object");
        if (Text(entry["use"]) is not { } use) return (null, "it names no way — `use` is system, managed or file");
        if (!Ways.Contains(use, StringComparer.Ordinal)) return (null, $"`use` is `{use}`, not system, managed or file");
        var way = use switch { "managed" => ToolWay.Managed, "file" => ToolWay.File, _ => ToolWay.System };

        // Another way's field makes the entry two ways, and a tool holds exactly one (§2.3).
        string[] others = way switch { ToolWay.System => ["version", "file"], ToolWay.Managed => ["file"], _ => ["version"] };
        if (others.FirstOrDefault(key => entry[key] is not null) is { } other)
        {
            return (null, $"it is {Words(way)}, and names a {other} too — a tool is run one way");
        }

        if (way == ToolWay.Managed)
        {
            if (entry["version"] is null) return (null, "managed needs a `version`, one to four numbers");
            if (Text(entry["version"]) is not { } version) return (null, "its `version` is not text");
            if (!IsExactVersion(version)) return (null, $"`{version}` is not an exact version — one to four numbers, like 2.51.0");
            return (new ToolEntry(way, version, null, null), null);
        }

        if (way == ToolWay.File)
        {
            if (entry["file"] is null) return (null, "a file needs its `file`, a whole path");
            if (Text(entry["file"]) is not { } file) return (null, "its `file` is not text");
            if (!IsWholePath(file)) return (null, $"`{file}` is not a whole path");
            return (new ToolEntry(way, null, file, null), null);
        }

        return (new ToolEntry(way, null, null, null), null);
    }

    internal static string Undeclared(string id) =>
        $"`{id}` is not a tool this build runs — one of: {string.Join(", ", Declared.Select(tool => tool.Id))}. "
        + "A tool is added in the code, never by a file.";

    /// <summary>
    /// A home file as an object, or why it is not one. Absent is null and no problem. A key written twice reads as the CLI's
    /// <c>JSON.parse</c> reads it (<see cref="JsonKeeping.Once"/>), where a node threw at the first look; an edit then refuses it.
    /// </summary>
    private static (JsonObject? Root, string? Problem) Load(string path)
    {
        if (!File.Exists(path)) return (null, null);

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(JsonKeeping.Once(AtomicFile.ReadText(path)));
        }
        catch (JsonException error)
        {
            return (null, $"{path} is not readable JSON ({error.Message})");
        }

        return parsed is JsonObject root ? (root, null) : (null, $"{path} is not a JSON object");
    }

    // ---- Children (TOOLS5, D121; docs/2026-10-01-tools-design.md §2.4) ----
    // One answer for Daoris and every child: the file a command's first word starts, and the environment every process
    // the driver and the modules start is handed. A TWIN of the CLI's tools.ts (toolFolders, childPath, childEnvironment,
    // resolveCommand): ToolsChildrenTests here and tools-children.test.ts there hold the same tables, row for row.
    // - A child's PATH is the folders of each tool that is managed or a named file, in the declared order, then the PATH
    //   it inherited. With every tool the system's the environment is the inherited one exactly: no variable is set.
    // - A command's first word that a tool answers for is that tool's: its own name is the resolved file; another it
    //   answers for (npm, npx) is found beside it, by PATHEXT, and never on PATH when the tool is managed or a file.
    // - Read from the file at each start, and never written onto this process's own environment: a variable rewritten
    //   while sessions start is the trap the modules' serialized tests already know (§2.4).
    // - An install's app/bin/ beside the home comes first of all (WSSETUP3, D124 §1.3): the doctrine tool's launchers,
    //   so every child finds daoris by its bare name. A home with no install beside it builds what it built before.
    // GIT_CONFIG_GLOBAL, the other variable §2.4 names, is TOOLS6's: it joins ChildEnvironment with the git file it names.

    /// <summary>The variable a child's <c>PATH</c> is. Windows reads it in any case, and a start's environment there is case-blind.</summary>
    public const string PathVariable = CommandPresence.PathVariable;

    /// <summary>Where an install carries the launchers, from its root: the home's, which owns the layout (<see cref="DaorisHome.InstallBinLayout"/>).</summary>
    public static IReadOnlyList<string> InstallBinLayout => DaorisHome.InstallBinLayout;

    /// <summary>The launchers' folder beside the home, where it is a folder: the home's answer (<see cref="DaorisHome.InstallBin"/>).</summary>
    public static string? InstallBin(string home) => DaorisHome.InstallBin(home);

    /// <summary>
    /// The folders one tool puts first on a child's <c>PATH</c> (§2.4): a managed version's, or a named file's; none
    /// for the system's, and none for a way that refuses.
    /// </summary>
    /// <exception cref="DriverException">A tool this build does not declare.</exception>
    public static IReadOnlyList<string> Folders(ToolsRead read, string home, string tool)
    {
        var declared = Find(tool) ?? throw new DriverException(Undeclared(tool));
        var entry = read.Entries[tool];
        if (entry.Problem is not null) return [];

        if (entry.Way == ToolWay.File) return File.Exists(entry.File) ? [Path.GetDirectoryName(entry.File)!] : [];
        if (entry.Way != ToolWay.Managed) return [];

        var (file, _) = ManagedFile(home, declared, entry.Version!);
        if (file is null) return [];

        var version = DaorisHome.Entry(home, HomeEntry.ToolVersions, tool, entry.Version!);
        var package = Path.Combine(version, Package);
        return [.. RecordPaths(Path.Combine(version, Record)).Select(path => path == "." ? package : Path.Combine([package, .. path.Split('/')]))];
    }

    /// <summary>
    /// The <c>PATH</c> a child of Daoris starts with (§2.4): the install's launchers beside the home where there is one
    /// (<see cref="InstallBin"/>), then every managed or named tool's folders, in the declared order, then
    /// <paramref name="inherited"/>. Null when nothing puts a folder there, so the child's is the inherited one exactly.
    /// An empty inherited <c>PATH</c> adds no empty folder.
    /// </summary>
    public static string? ChildPath(ToolsRead read, string home, string? inherited)
    {
        List<string> folders = [.. InstallBin(home) is { } bin ? [bin] : Array.Empty<string>(), .. Declared.SelectMany(tool => Folders(read, home, tool.Id))];
        if (folders.Count == 0) return null;
        if (!string.IsNullOrEmpty(inherited)) folders.Add(inherited);
        return string.Join(Path.PathSeparator, folders);
    }

    /// <summary>
    /// What a child's environment takes over the one it inherited (§2.4): <see cref="PathVariable"/> when the install or
    /// a tool puts a folder first, and nothing when there is no install beside the home and every tool is the system's.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ChildEnvironment(ToolsRead read, string home, string? inheritedPath)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ChildPath(read, home, inheritedPath) is { } path) variables[PathVariable] = path;
        return variables;
    }

    /// <summary>
    /// Hand a start the tools' environment, read now from <c>$DAORIS_HOME</c>'s file (D63's one seam), over the
    /// <c>PATH</c> the start holds. A process with no home is handed the prompt variables alone.
    /// </summary>
    public static void Hand(ProcessStartInfo info) => Hand(info, DaorisHome.Resolve());

    /// <summary>
    /// Hand a start the tools' environment, read now from <paramref name="home"/>'s file; null hands the prompt variables alone
    /// (<see cref="GitWiring"/>), which need no home.
    /// </summary>
    public static void Hand(ProcessStartInfo info, string? home)
    {
        if (home is null)
        {
            GitWiring.Hand(info);
            return;
        }

        Hand(info, Read(home), home);
    }

    /// <summary>
    /// Hand a start the tools' environment from a read the caller already holds, with the prompt variables and the doors every
    /// child takes (GITAUTH1a and GITAUTH1b, <see cref="GitWiring"/>): never the terminal's, which builds its block from
    /// <see cref="ChildEnvironment"/>.
    /// </summary>
    public static void Hand(ProcessStartInfo info, ToolsRead read, string home)
    {
        GitWiring.Hand(info, home);
        var inherited = info.Environment.TryGetValue(PathVariable, out var path) ? path : null;
        foreach (var (name, value) in ChildEnvironment(read, home, inherited)) info.Environment[name] = value;
    }

    /// <summary>
    /// Which file starts for a command's first word (§2.4), or null when no tool answers for it and the caller's own
    /// resolver decides. The tool's own name is <see cref="Resolve(ToolsRead, string, string, string?)"/>'s answer;
    /// another name it answers for is found beside the tool's file, by PATHEXT, and a managed or named tool never
    /// lends <c>PATH</c>'s.
    /// </summary>
    /// <param name="path">The <c>PATH</c> the system's tool is found on; null for this process's own.</param>
    public static ToolResolution? ResolveCommand(ToolsRead read, string home, string name, string? path = null)
    {
        var declared = Declared.FirstOrDefault(tool => tool.Answers.Contains(name, StringComparer.Ordinal));
        if (declared is null) return null;

        var resolution = Resolve(read, home, declared.Id, path);
        if (name == declared.Answers[0] || resolution.Refused) return resolution;

        if (resolution.Way == ToolWay.System)
        {
            var found = CommandPresence.Resolve(name, path, startable: true);
            return found is not null
                ? resolution with { File = found, Problem = null }
                : resolution with
                {
                    File = null,
                    Problem = $"`{name}` is not on this machine's PATH. {declared.Name} is run as the system's, managed, or from a "
                        + $"file you name: `daoris tool use {declared.Id} file <path>` names one",
                };
        }

        var folders = Folders(read, home, declared.Id);
        var beside = CommandPresence.Resolve(name, string.Join(Path.PathSeparator, folders), startable: true);
        return beside is not null
            ? resolution with { File = beside }
            : resolution with
            {
                File = null,
                Refused = true,
                Problem = $"`{name}` is not beside {declared.Name}'s own file ({resolution.File}) — {Never}. "
                    + $"`daoris tool use {declared.Id} system` runs the one on PATH",
            };
    }

    /// <summary>
    /// A downloaded version's folders for <c>PATH</c>, from its record: its <c>paths</c> when they are a list of
    /// folders inside the package, else the folder its <c>exe</c> is in (§3.2's default, which TOOLS4 writes out).
    /// </summary>
    private static IReadOnlyList<string> RecordPaths(string record)
    {
        var (value, _) = Load(record);
        if (value?["paths"] is JsonArray named && named.Count > 0
            && named.Select(Text).All(path => path is not null && (path == "." || IsInside(path))))
        {
            return [.. named.Select(path => Text(path)!)];
        }

        var exe = Text(value?["exe"]) ?? "";
        var at = exe.LastIndexOf('/');
        return [at < 0 ? "." : exe[..at]];
    }
}
