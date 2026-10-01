using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>One download, as a list names it (§3.2).</summary>
/// <param name="Sha256">Lower case.</param>
/// <param name="Exe">The executable inside the unpacked archive, a relative <c>/</c> path.</param>
/// <param name="Paths">The folders a child's <c>PATH</c> takes first; <c>.</c> is the archive's root.</param>
public sealed record ToolFile(string Url, string Sha256, long Size, string Archive, string Exe, IReadOnlyList<string> Paths);

/// <summary>A tool's licence, shown before a download.</summary>
public sealed record ToolLicence(string Id, string? Url);

/// <summary>What one list says of one tool.</summary>
/// <param name="Versions">Version → platform → file: only what reads, each in ordinal order.</param>
public sealed record ResourceTool(
    string? Source, ToolLicence? Licence, IReadOnlyDictionary<string, IReadOnlyDictionary<string, ToolFile>> Versions);

/// <summary>One list as read.</summary>
/// <param name="Origin">How sentences name it: an address, or <see cref="ToolResources.BuiltIn"/>.</param>
/// <param name="Integrity">What vouches for it (§3.5): <c>built in</c>, <c>this machine</c>, or the host; null for text read on its own.</param>
/// <param name="Path">The file read; null for text.</param>
/// <param name="Sha256">The sha256 of the bytes read, as the screen shows it; null when nothing was read.</param>
/// <param name="Tools">The declared tools it names, in ordinal order.</param>
/// <param name="Unknown">The ids it names that this build does not run: never offered.</param>
/// <param name="Notes">What it skips, and why.</param>
/// <param name="Problem">Why nothing in it is read.</param>
public sealed record ResourceList(
    string Origin, string? Integrity, string? Path, bool Exists, string? Sha256, IReadOnlyDictionary<string, ResourceTool> Tools,
    IReadOnlyList<string> Unknown, IReadOnlyList<string> Notes, string? Problem);

/// <summary>One version of one tool for one platform, offered: one download, from every address that names it.</summary>
/// <param name="Urls">Every address, in read order, once: the person's first, then the maker's.</param>
/// <param name="Lists">Every list that names it, in read order.</param>
public sealed record OfferedVersion(
    string Version, string Sha256, long Size, string Archive, string Exe, IReadOnlyList<string> Paths, IReadOnlyList<string> Urls,
    IReadOnlyList<string> Lists);

/// <summary>A version two lists disagree on, and so refused.</summary>
/// <param name="Field">The first field they disagree on.</param>
/// <param name="Lists">The two lists, in read order.</param>
public sealed record RefusedVersion(string Version, string Field, IReadOnlyList<string> Lists, string Problem);

/// <summary>One tool, merged across every list read.</summary>
/// <param name="Lists">Every list naming the tool, in read order.</param>
/// <param name="Versions">Newest first.</param>
/// <param name="Refused">Newest first.</param>
/// <param name="Newest">The highest offered version by number; never a refused one.</param>
public sealed record MergedTool(
    string Tool, string? Source, string? SourceFrom, ToolLicence? Licence, string? LicenceFrom, IReadOnlyList<string> Lists,
    IReadOnlyList<OfferedVersion> Versions, IReadOnlyList<RefusedVersion> Refused, string? Newest);

/// <summary>A tool a list names that this build does not run, with the lists that name it.</summary>
public sealed record UnknownTool(string Tool, IReadOnlyList<string> Lists);

/// <summary>Every list, merged for one platform.</summary>
/// <param name="Tools">Every declared tool, in the declared order.</param>
/// <param name="Notes">Every list's problem and notes in read order, then each refusal.</param>
public sealed record MergedResources(
    string? Platform, IReadOnlyList<MergedTool> Tools, IReadOnlyList<UnknownTool> Unknown, IReadOnlyList<string> Notes);

/// <summary>
/// <c>resources.json</c>: where each version of a tool downloads from, and the merge of every list read (TOOLS3,
/// D121; <c>docs/2026-10-01-tools-design.md</c> §3.1–§3.5). One list is built into the install, at
/// <c>app/resources.json</c> beside the application; more are resource locations the person lists in
/// <c>tools.json</c>, each fetched on their press and kept under <c>&lt;home&gt;/tools/locations/</c>. This reads the
/// copies and the list built in, and merges them. Fetching is TOOLS4's.
/// </summary>
/// <remarks>
/// <para>🔴 <b>A TWIN of the CLI's <c>resources.ts</c>.</b> The two share no code — the FILE is the contract — and
/// each carries the same tables (<c>ToolResourcesTests</c> here, <c>resources.test.ts</c> there), row for row. A
/// rule changed here is changed there, in the same commit:</para>
/// <list type="number">
/// <item><c>schema</c> is read first: anything but 1 refuses the whole list, and so does a <c>tools</c> that is no
/// object.</item>
/// <item>A tool this build does not declare is named and never offered.</item>
/// <item>A version is one to four numbers; a platform is one of the table's; a version names its <c>files</c>.</item>
/// <item>A file names its <c>url</c> (https://, or http:// to this machine), <c>sha256</c> (64 hex, kept lower
/// case), <c>size</c> (whole bytes above 0), <c>archive</c> (zip or tar.gz) and <c>exe</c> (a relative <c>/</c> path
/// inside the archive); <c>paths</c> defaults to the folder <c>exe</c> is in, <c>.</c> for the root.</item>
/// <item>What does not read is skipped and said, never guessed at.</item>
/// </list>
/// <para>Then the merge (§3.4): the person's locations in order, then the list built in. One tool, version and
/// platform is one download, whoever names it: lists that disagree refuse that version, naming both; lists that
/// agree under other addresses are mirrors, tried in read order; the versions are the union, and the newest is the
/// highest by number, never by any list's word.</para>
/// <para>It reads files and answers "what is offered". Fetching a location is TOOLS4's.</para>
/// </remarks>
public static class ToolResources
{
    /// <summary>The list's name, built in and fetched alike. The CLI's <c>RESOURCES_FILE</c>.</summary>
    public const string FileName = "resources.json";

    /// <summary>
    /// Where an install carries the list built in, from its root: beside the application, in <c>app/</c>. Twin: the
    /// CLI's <c>BUILT_IN_LAYOUT</c> and the publish's <c>RESOURCES</c>; <c>desktop-publish.test.ts</c> reads all three.
    /// </summary>
    public static readonly IReadOnlyList<string> Layout = ["app", "resources.json"];

    /// <summary>Where each location's fetched copy is kept, under the home. The CLI's <c>LOCATIONS_FOLDER</c>.</summary>
    public static readonly IReadOnlyList<string> LocationsFolder = [Tools.Folder, "locations"];

    /// <summary>The one schema this build reads. The CLI's <c>SCHEMA</c>.</summary>
    public const int Schema = 1;

    /// <summary>The archives either reader unpacks; a third is a reviewed change to both (§3.2).</summary>
    public static readonly IReadOnlyList<string> Archives = ["zip", "tar.gz"];

    /// <summary>How the list built in names itself in a sentence. The CLI's <c>BUILT_IN</c>.</summary>
    public const string BuiltIn = "the built-in list";

    /// <summary>
    /// The platforms a list may name — .NET runtime identifiers — and .NET's names for each (§3.2). 🔴 The CLI's
    /// table holds the same ids in the same order, with Node's names; the tests hold both spellings in one table.
    /// </summary>
    private static readonly (string Id, OSPlatform Os, Architecture Arch)[] PlatformTable =
    [
        ("win-x64", OSPlatform.Windows, Architecture.X64),
        ("win-arm64", OSPlatform.Windows, Architecture.Arm64),
        ("linux-x64", OSPlatform.Linux, Architecture.X64),
        ("linux-arm64", OSPlatform.Linux, Architecture.Arm64),
        ("osx-x64", OSPlatform.OSX, Architecture.X64),
        ("osx-arm64", OSPlatform.OSX, Architecture.Arm64),
    ];

    public static readonly IReadOnlyList<string> Platforms = [.. PlatformTable.Select(row => row.Id)];

    private const string AddressRule = "https://, or http:// to this machine";

    private static readonly Regex Hash = new(@"^[0-9A-Fa-f]{64}\z", RegexOptions.CultureInvariant);

    /// <summary>The fields one download is, whoever names it (§3.4 rule 2), in the order a refusal names the first that differs.</summary>
    private static readonly string[] FileFields = ["sha256", "size", "archive", "exe", "paths"];

    /// <summary>The platform an operating system and architecture name, or null for one no list may name.</summary>
    public static string? PlatformFor(OSPlatform os, Architecture arch) =>
        PlatformTable.FirstOrDefault(row => row.Os == os && row.Arch == arch).Id;

    /// <summary>This process's platform: the architecture it runs as, as the CLI asks of Node's <c>process.arch</c>.</summary>
    public static string? Current => PlatformFor(
        OperatingSystem.IsWindows() ? OSPlatform.Windows
            : OperatingSystem.IsLinux() ? OSPlatform.Linux
            : OperatingSystem.IsMacOS() ? OSPlatform.OSX
            : OSPlatform.Create("OTHER"),
        RuntimeInformation.ProcessArchitecture);

    /// <summary>
    /// Compare two versions by number: each part as a whole number, a missing part as 0, then the spelling, so two
    /// spellings of one number still sort one way. The CLI's <c>compareVersions</c>.
    /// </summary>
    public static int CompareVersions(string a, string b)
    {
        var left = a.Split('.');
        var right = b.Split('.');
        for (var at = 0; at < Math.Max(left.Length, right.Length); at++)
        {
            var x = Number(at < left.Length ? left[at] : "0");
            var y = Number(at < right.Length ? right[at] : "0");
            if (x.Length != y.Length) return x.Length - y.Length;
            var order = string.CompareOrdinal(x, y);
            if (order != 0) return Math.Sign(order);
        }

        return Math.Sign(string.CompareOrdinal(a, b));

        static string Number(string part) => part.TrimStart('0') is { Length: > 0 } trimmed ? trimmed : "0";
    }

    /// <summary>A list's text (rules 1–5), as the list named <paramref name="origin"/>. A byte-order mark is not part of it.</summary>
    public static ResourceList Parse(string text, string origin)
    {
        var tools = new SortedDictionary<string, ResourceTool>(StringComparer.Ordinal);
        var unknown = new List<string>();
        var notes = new List<string>();
        ResourceList Read(string? problem) => new(origin, null, null, true, null, tools, unknown, notes, problem);

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(text.StartsWith('﻿') ? text[1..] : text);
        }
        catch (JsonException error)
        {
            return Read($"{origin} is not readable JSON ({error.Message})");
        }

        if (parsed is not JsonObject root) return Read($"{origin} is not a JSON object");

        // Rule 1: the schema, before anything else.
        if (root["schema"] is not { } schema) return Read($"{origin} names no `schema`: nothing in it is read");
        if (!IsSchema(schema))
        {
            return Read($"{origin} is schema {schema.ToJsonString()}, and this build reads schema {Schema}: nothing in it is read, "
                + "and a newer Daoris may read it");
        }

        if (root["tools"] is not { } toolsNode) return Read(null);
        if (toolsNode is not JsonObject held) return Read($"{origin}'s `tools` is not an object: nothing in it is read");

        foreach (var (id, entry) in Ordinal(held))
        {
            // Rule 2: a list offers versions of the tools this build declares, and can never add one.
            if (Tools.Find(id) is null)
            {
                unknown.Add(id);
                notes.Add($"{origin} names `{id}`, which is not a tool this build runs: nothing of it is offered");
                continue;
            }

            if (entry is not JsonObject tool)
            {
                notes.Add($"{origin}: `{id}` is not an object, and nothing of it is offered");
                continue;
            }

            tools[id] = ReadTool(origin, id, tool, notes);
        }

        return Read(null);
    }

    /// <summary>What vouches for a list from an address (§3.5): this machine, or the host the person chose to trust.</summary>
    public static string IntegrityOf(string address)
    {
        var uri = new Uri(address);
        if (Tools.IsLoopback(uri.Host)) return "this machine";
        return uri.IsDefaultPort ? uri.IdnHost : $"{uri.IdnHost}:{uri.Port}";
    }

    /// <summary>
    /// A list on disk, hashed as its bytes and read as its text. Absent is <c>Exists: false</c> and no problem: the
    /// caller says what absence means.
    /// </summary>
    public static ResourceList ReadFile(string path, string origin)
    {
        var integrity = origin == BuiltIn ? "built in" : IntegrityOf(origin);
        if (!File.Exists(path))
        {
            return new ResourceList(
                origin, integrity, path, false, null, new SortedDictionary<string, ResourceTool>(StringComparer.Ordinal), [], [], null);
        }

        var bytes = File.ReadAllBytes(path);
        return Parse(Encoding.UTF8.GetString(bytes), origin) with
        {
            Integrity = integrity,
            Path = path,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
        };
    }

    /// <summary>The list built in beside the home: its sibling <c>app/resources.json</c>, which an install makes one folder with the application's.</summary>
    public static string BesideHome(string home)
    {
        var full = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(home));
        return System.IO.Path.Combine([System.IO.Path.GetDirectoryName(full) ?? full, .. Layout]);
    }

    /// <summary>The list built in beside a running application, whose folder is the install's <c>app/</c>.</summary>
    public static string BesideApplication(string baseDirectory) =>
        System.IO.Path.Combine(System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(baseDirectory)), Layout[^1]);

    /// <summary>The list built in an application reads: beside it where it has one, as a build and an install do, else beside the home.</summary>
    public static string BuiltInFor(string home, string baseDirectory) =>
        File.Exists(BesideApplication(baseDirectory)) ? BesideApplication(baseDirectory) : BesideHome(home);

    /// <summary>Where a location's fetched copy is kept: named by the sha256 of its address as written.</summary>
    public static string LocationCopy(string home, string address) =>
        System.IO.Path.Combine([home, .. LocationsFolder, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(address))) + ".json"]);

    /// <summary>
    /// Every list, in read order (§3.3): the person's locations top to bottom, each from its fetched copy, then the
    /// list built in, always last. A location never fetched, and a home with no install beside it, are lists that
    /// name nothing and say why.
    /// </summary>
    /// <param name="baseDirectory">The running application's folder, where the list built in is first looked for.</param>
    /// <param name="read">The home's <c>tools.json</c>, as read; null reads it now.</param>
    public static IReadOnlyList<ResourceList> ReadLists(string home, string baseDirectory, ToolsRead? read = null)
    {
        read ??= Tools.Read(home);
        var lists = new List<ResourceList>();
        foreach (var address in read.Locations)
        {
            var list = ReadFile(LocationCopy(home, address), address);
            lists.Add(list.Exists ? list : list with { Notes = [.. list.Notes, $"{address} has not been fetched yet, so it names nothing"] });
        }

        var builtIn = ReadFile(BuiltInFor(home, baseDirectory), BuiltIn);
        lists.Add(builtIn.Exists
            ? builtIn
            : builtIn with { Notes = [.. builtIn.Notes, $"no list is built in beside this home ({builtIn.Path}), so only the locations are read"] });
        return lists;
    }

    /// <summary>
    /// Merge the lists, read in order, for one platform (§3.4). A list with a problem offers nothing; its problem is
    /// carried into the notes, as each list's notes are.
    /// </summary>
    public static MergedResources Merge(IReadOnlyList<ResourceList> lists, string? platform)
    {
        var notes = new List<string>();
        foreach (var list in lists)
        {
            if (list.Problem is not null) notes.Add(list.Problem);
            notes.AddRange(list.Notes);
        }

        var unknown = new List<(string Tool, List<string> Lists)>();
        foreach (var list in lists)
        {
            foreach (var id in list.Unknown)
            {
                var at = unknown.FindIndex(entry => entry.Tool == id);
                if (at >= 0) unknown[at].Lists.Add(list.Origin);
                else unknown.Add((id, [list.Origin]));
            }
        }

        var refusals = new List<string>();
        var tools = new List<MergedTool>();
        foreach (var declared in Tools.Declared)
        {
            string? source = null, sourceFrom = null, licenceFrom = null;
            ToolLicence? licence = null;
            var named = new List<string>();
            var offered = new Dictionary<string, (ToolFile File, List<string> Urls, List<string> Lists)>(StringComparer.Ordinal);
            var refused = new Dictionary<string, RefusedVersion>(StringComparer.Ordinal);

            foreach (var list in lists)
            {
                if (!list.Tools.TryGetValue(declared.Id, out var tool)) continue;
                named.Add(list.Origin);
                // Rule 7: the first list that names them, in read order.
                if (source is null && tool.Source is not null) (source, sourceFrom) = (tool.Source, list.Origin);
                if (licence is null && tool.Licence is not null) (licence, licenceFrom) = (tool.Licence, list.Origin);
                if (platform is null) continue;

                foreach (var (version, files) in tool.Versions)
                {
                    if (!files.TryGetValue(platform, out var file) || refused.ContainsKey(version)) continue;
                    if (!offered.TryGetValue(version, out var held))
                    {
                        offered[version] = (file, [file.Url], [list.Origin]);
                        continue;
                    }

                    // Rules 2 and 3: one download, whoever names it; a disagreement refuses this version, naming both.
                    if (FileFields.FirstOrDefault(field => FieldText(held.File, field) != FieldText(file, field)) is { } differs)
                    {
                        var first = held.Lists[0];
                        refused[version] = new RefusedVersion(
                            version, differs, [first, list.Origin],
                            $"{declared.Name} {version} for {platform} is refused: {first} names its {differs} as "
                            + $"{FieldText(held.File, differs)}, and {list.Origin} as {FieldText(file, differs)}. Two lists that disagree on one "
                            + "download refuse it, and nothing is fetched until one of them changes");
                        offered.Remove(version);
                        continue;
                    }

                    // Rule 4: the same bytes under another address are a mirror, tried in read order.
                    if (!held.Urls.Contains(file.Url, StringComparer.Ordinal)) held.Urls.Add(file.Url);
                    if (!held.Lists.Contains(list.Origin, StringComparer.Ordinal)) held.Lists.Add(list.Origin);
                }
            }

            // Rules 5 and 6: the union, newest first by number; a refused version is never the newest.
            var newestFirst = Comparer<string>.Create((a, b) => CompareVersions(b, a));
            var versions = offered
                .Select(pair => new OfferedVersion(
                    pair.Key, pair.Value.File.Sha256, pair.Value.File.Size, pair.Value.File.Archive, pair.Value.File.Exe,
                    [.. pair.Value.File.Paths], pair.Value.Urls, pair.Value.Lists))
                .OrderBy(version => version.Version, newestFirst)
                .ToList();
            var refusedList = refused.Values.OrderBy(entry => entry.Version, newestFirst).ToList();
            refusals.AddRange(refusedList.Select(entry => entry.Problem));
            tools.Add(new MergedTool(
                declared.Id, source, sourceFrom, licence, licenceFrom, named, versions, refusedList, versions.FirstOrDefault()?.Version));
        }

        return new MergedResources(
            platform, tools, [.. unknown.Select(entry => new UnknownTool(entry.Tool, entry.Lists))], [.. notes, .. refusals]);
    }

    /// <summary>A tool's entry (rules 3–5): what it offers, with each thing skipped said.</summary>
    private static ResourceTool ReadTool(string origin, string id, JsonObject entry, List<string> notes)
    {
        string? source = null;
        ToolLicence? licence = null;
        var versions = new SortedDictionary<string, IReadOnlyDictionary<string, ToolFile>>(StringComparer.Ordinal);

        if (entry["source"] is { } sourceNode)
        {
            if (Text(sourceNode) is { } text && Tools.IsAddress(text)) source = text;
            else notes.Add($"{origin}: `{id}`'s `source` is not {AddressRule}, and is not shown");
        }

        if (entry["licence"] is { } licenceNode)
        {
            if (licenceNode is not JsonObject named || Text(named["id"]) is not { Length: > 0 } licenceId)
            {
                notes.Add($"{origin}: `{id}`'s `licence` names no `id`, and is not shown");
            }
            else if (named["url"] is { } urlNode && (Text(urlNode) is not { } url || !Tools.IsAddress(url)))
            {
                notes.Add($"{origin}: `{id}`'s licence `url` is not {AddressRule}, and is not shown");
                licence = new ToolLicence(licenceId, null);
            }
            else
            {
                licence = new ToolLicence(licenceId, Text(named["url"]));
            }
        }

        if (entry["versions"] is not { } versionsNode) return new ResourceTool(source, licence, versions);
        if (versionsNode is not JsonObject held)
        {
            notes.Add($"{origin}: `{id}`'s `versions` is not an object, and nothing of it is offered");
            return new ResourceTool(source, licence, versions);
        }

        foreach (var (version, value) in Ordinal(held))
        {
            if (!Tools.IsExactVersion(version))
            {
                notes.Add($"{origin}: `{id}` `{version}` is not an exact version — one to four numbers — and is skipped");
                continue;
            }

            if (value is not JsonObject named || named["files"] is not JsonObject platforms)
            {
                notes.Add($"{origin}: `{id}` {version} has no `files` object, and is skipped");
                continue;
            }

            var files = new SortedDictionary<string, ToolFile>(StringComparer.Ordinal);
            foreach (var (platform, fileNode) in Ordinal(platforms))
            {
                if (!Platforms.Contains(platform, StringComparer.Ordinal))
                {
                    notes.Add($"{origin}: `{id}` {version} names `{platform}`, which is not a platform this build knows, and is skipped");
                    continue;
                }

                var (file, why) = JudgeFile(fileNode);
                if (file is null) notes.Add($"{origin}: `{id}` {version} for {platform} is skipped — {why}");
                else files[platform] = file;
            }

            if (files.Count > 0) versions[version] = files;
        }

        return new ResourceTool(source, licence, versions);
    }

    /// <summary>A file of the list, judged (rule 4): what it downloads, or why it is skipped.</summary>
    private static (ToolFile? File, string? Why) JudgeFile(JsonNode? value)
    {
        if (value is not JsonObject file) return (null, "it is not an object");
        if (Text(file["url"]) is not { } url) return (null, "it names no `url`");
        if (!Tools.IsAddress(url)) return (null, $"`url` is not {AddressRule}");
        if (Text(file["sha256"]) is not { } sha256 || !Hash.IsMatch(sha256)) return (null, "`sha256` is not 64 hex digits");
        if (WholeBytes(file["size"]) is not { } size) return (null, "`size` is not a whole number of bytes above 0");
        if (Text(file["archive"]) is not { } archive || !Archives.Contains(archive, StringComparer.Ordinal)) return (null, "`archive` is not zip or tar.gz");
        if (Text(file["exe"]) is not { } exe) return (null, "it names no `exe`");
        if (!Tools.IsInside(exe)) return (null, "`exe` is not a relative path inside the archive");

        IReadOnlyList<string> paths;
        if (file["paths"] is not { } pathsNode)
        {
            var at = exe.LastIndexOf('/');
            paths = [at < 0 ? "." : exe[..at]];
        }
        else if (pathsNode is JsonArray named && named.Count > 0
            && named.Select(Text).All(path => path is not null && (path == "." || Tools.IsInside(path))))
        {
            paths = [.. named.Select(path => Text(path)!)];
        }
        else
        {
            return (null, "`paths` is not a list of folders inside the archive");
        }

        return (new ToolFile(url, sha256.ToLowerInvariant(), size, archive, exe, paths), null);
    }

    /// <summary>How a field of one download reads in a refusal, and how two are compared.</summary>
    private static string FieldText(ToolFile file, string field) => field switch
    {
        "sha256" => file.Sha256,
        "size" => file.Size.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "archive" => file.Archive,
        "exe" => file.Exe,
        _ => $"[{string.Join(", ", file.Paths)}]",
    };

    /// <summary>Schema 1, as JSON's number: <c>1</c> and <c>1.0</c> alike, as Node reads them.</summary>
    private static bool IsSchema(JsonNode node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.Number && value.TryGetValue<decimal>(out var number) && number == Schema;

    /// <summary>A whole number of bytes above 0, within what Node reads exactly (<c>Number.isSafeInteger</c>).</summary>
    private static long? WholeBytes(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.Number && value.TryGetValue<decimal>(out var number)
        && number == decimal.Truncate(number) && number > 0 && number <= 9007199254740991m
            ? (long)number
            : null;

    /// <summary>An object's members in ordinal order: JSON's own order is not one both runtimes keep.</summary>
    private static IEnumerable<KeyValuePair<string, JsonNode?>> Ordinal(JsonObject value) =>
        value.OrderBy(pair => pair.Key, StringComparer.Ordinal);

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
