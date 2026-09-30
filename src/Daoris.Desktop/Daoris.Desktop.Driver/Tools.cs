using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

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
/// <para>🔴 <b>A TWIN of the CLI's <c>tools.ts</c>.</b> The two share no code — the FILE is the contract —
/// and each carries the same tables (<c>ToolsTests</c> here, <c>tools.test.ts</c> there), row for row. A
/// rule changed here is changed there, in the same commit:</para>
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
/// TOOLS5's.</para>
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
        var path = Path.Combine(home, FileName);
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

    /// <summary>An address that is no resource location, refused on a write (rule 5); null for one that is.</summary>
    public static string? LocationProblem(string address)
    {
        var refused = $"`{address}` is not a resource location — an address is https://, or http:// to this machine "
            + $"({Loopback[0]}, {Loopback[1]} or {Loopback[2]})";
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)) return refused;
        if (uri.Scheme == Uri.UriSchemeHttps) return null;
        return uri.Scheme == Uri.UriSchemeHttp && Loopback.Contains(uri.Host, StringComparer.Ordinal) ? null : refused;
    }

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
    public static void UseSystem(string home, string tool) => SetWay(home, tool, "system", null);

    /// <summary>A file the person names, by its whole path (rule 6); one that is not there is refused.</summary>
    /// <exception cref="DriverException">An undeclared tool, a path that is not whole or holds no file, or a file
    /// this build cannot read; nothing is written.</exception>
    public static void UseFile(string home, string tool, string file)
    {
        if (Find(tool) is null) throw new DriverException(Undeclared(tool));
        if (!IsWholePath(file)) throw new DriverException($"`{file}` is not a whole path — name the executable by its whole path.");
        if (!File.Exists(file)) throw new DriverException($"no file at {file} — a tool is a file that is there.");
        SetWay(home, tool, "file", file);
    }

    /// <summary>
    /// Set one tool's way (rule 6): the other ways' fields go, and every key the writer has no field for stays,
    /// on the entry and on the file. Refused over a file it could not read, and over a <c>tools</c> that is no
    /// object.
    /// </summary>
    private static void SetWay(string home, string id, string way, string? file)
    {
        if (Find(id) is null) throw new DriverException(Undeclared(id));
        var path = Path.Combine(home, FileName);
        var (root, problem) = Load(path);
        if (problem is not null) throw new DriverException($"{problem}. Fix it, or delete it to start from nothing — nothing was written.");

        root ??= new JsonObject();
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
        if (file is not null) entry["file"] = file;

        Directory.CreateDirectory(home);
        AtomicFile.WriteText(path, root.ToJsonString(Written) + "\n");
    }

    /// <summary>A downloaded version's executable, or why there is none (§3.6's layout).</summary>
    private static (string? File, string? Problem) ManagedFile(string home, ToolDeclaration tool, string version)
    {
        var folder = Path.Combine(home, Folder, tool.Id, version);
        var back = $"— {Never}. `daoris tool use {tool.Id} system` runs the one on PATH";
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
        var parts = exe.Split('/');
        if (exe.IndexOfAny(['\\', ':']) >= 0 || parts.Any(part => part is "" or "." or ".."))
        {
            return Unread($"{record}: its `exe` is not a relative path inside the package");
        }

        var file = Path.Combine([folder, Package, .. parts]);
        return File.Exists(file)
            ? (file, null)
            : (null, $"{tool.Name} is managed at {version}, and its record names {file}, and there is no file there {back}");
    }

    /// <summary>How a way reads in a sentence.</summary>
    private static string Words(ToolWay way) => way switch
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
            if (!ExactVersion.IsMatch(version)) return (null, $"`{version}` is not an exact version — one to four numbers, like 2.51.0");
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

    private static string Undeclared(string id) =>
        $"`{id}` is not a tool this build runs — one of: {string.Join(", ", Declared.Select(tool => tool.Id))}. "
        + "A tool is added in the code, never by a file.";

    /// <summary>A home file as an object, or why it is not one. Absent is null and no problem.</summary>
    private static (JsonObject? Root, string? Problem) Load(string path)
    {
        if (!File.Exists(path)) return (null, null);

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(File.ReadAllText(path));
        }
        catch (JsonException error)
        {
            return (null, $"{path} is not readable JSON ({error.Message})");
        }

        return parsed is JsonObject root ? (root, null) : (null, $"{path} is not a JSON object");
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    /// <summary>Two-space indent, LF, and text left as text: the CLI's <c>JSON.stringify(value, null, 2)</c>.</summary>
    private static readonly JsonSerializerOptions Written = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
