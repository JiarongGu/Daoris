using System.Text.Json;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>One door of a <c>provider.json</c>: its id, its kind (<c>native</c> or <c>protocol</c>) and, for a stand-in, the arguments that choose it.</summary>
internal sealed record DoorFile(string Id, string Kind, IReadOnlyList<string> Arguments);

/// <summary>
/// A provider's <c>provider.json</c> as read: its id, its stand-in flag, its program's entry under the folder's <c>Program/</c> (a stand-in's
/// alone), its doors, and the folder it sits in.
/// </summary>
internal sealed record ProviderFile(string Id, bool StandIn, string? Entry, IReadOnlyList<DoorFile> Doors, string Folder)
{
    /// <summary>The program as the driver's build carries it beside itself: <c>Agents/Providers/&lt;Folder&gt;/Program/&lt;entry&gt;</c> in the output.</summary>
    public string Built(string output) =>
        Path.Combine(output, "Agents", "Providers", Path.GetFileName(Folder), ProviderFiles.ProgramFolder, Entry ?? "");

    /// <summary>The command that runs a door's program from this test's build output: Node, the entry, the door's arguments, then the case's own.</summary>
    public IReadOnlyList<string> Command(string door, params string[] more) =>
        ["node", Built(AppContext.BaseDirectory), .. Doors.Single(each => each.Id == door).Arguments, .. more];
}

/// <summary>A <c>provider.json</c>'s read: the file, or null where it breaks the layout, and each break named.</summary>
internal sealed record ProviderFileRead(string Path, ProviderFile? File, IReadOnlyList<string> Problems);

/// <summary>
/// The one place a tool reads a provider's stand-in facts (AGENTPROVIDER1 row b, part 3; D213's row b part 3 note): a <c>provider.json</c> in
/// each provider's folder under <c>Agents/Providers/</c>, holding its id, its stand-in flag, its doors' ids and kinds, and, for a stand-in,
/// its program's layout: the entry under the folder's <c>Program/</c>, a Node module, and the arguments that choose each door. The family
/// rehearsal reads the same files (<c>tools/stand-ins.mjs</c>); <c>ProviderFilesTests</c> holds every file equal to <c>Providers.Built()</c>.
/// </summary>
internal static class ProviderFiles
{
    public const string FileName = "provider.json";

    /// <summary>The folder a provider's program sits in, carried beside the driver by the project's one glob.</summary>
    public const string ProgramFolder = "Program";

    public static readonly string[] Kinds = ["native", "protocol"];

    private static readonly string[] Fields = ["id", "standIn", "program", "doors"];
    private static readonly string[] ProgramFields = ["entry"];
    private static readonly string[] DoorFields = ["id", "kind", "arguments"];

    /// <summary>The providers' folder in the driver's source.</summary>
    public static string ProvidersFolder => Path.Combine(SourceFiles.Project(SourceFiles.DriverProject), "Agents", "Providers");

    /// <summary>Every provider folder's file, read, in the folders' order.</summary>
    public static IReadOnlyList<ProviderFileRead> All() =>
    [
        .. Directory.EnumerateDirectories(ProvidersFolder)
            .Order(StringComparer.Ordinal)
            .Select(folder => Path.Combine(folder, FileName))
            .Where(File.Exists)
            .Select(file => Parse(File.ReadAllText(file), file)),
    ];

    /// <summary>The file of a provider by its id; one that breaks the layout, or none, fails naming why.</summary>
    public static ProviderFile Of(string provider)
    {
        var read = All().Where(each => each.File is { } file && string.Equals(file.Id, provider, StringComparison.OrdinalIgnoreCase)).ToList();
        return read.Count == 1
            ? read[0].File!
            : throw new InvalidOperationException(
                $"provider '{provider}' has {read.Count} {FileName} files under {ConformancePending.Relative(ProvidersFolder)}: each provider written on the contract keeps one in its folder");
    }

    /// <summary>A file's text read by the one layout, each break of it a problem naming the file and the field.</summary>
    public static ProviderFileRead Parse(string text, string file)
    {
        var where = ConformancePending.Relative(file);
        var folder = Path.GetDirectoryName(file) ?? "";
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException error)
        {
            return new(file, null, [$"{where}: not JSON ({error.Message})"]);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return new(file, null, [$"{where}: the file is an object of {string.Join(", ", Fields)}"]);

            var problems = new List<string>();
            Extra(root, Fields, where, problems);
            var id = Text(root, "id");
            if (id is null) problems.Add($"{where}: 'id' is missing or blank");
            var standIn = root.TryGetProperty("standIn", out var flag) && flag.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? flag.GetBoolean()
                : (bool?)null;
            if (standIn is null) problems.Add($"{where}: 'standIn' is true or false");

            string? entry = null;
            if (root.TryGetProperty("program", out var program))
            {
                if (program.ValueKind != JsonValueKind.Object)
                {
                    problems.Add($"{where}: 'program' is an object of {string.Join(", ", ProgramFields)}");
                }
                else
                {
                    Extra(program, ProgramFields, $"{where}, program", problems);
                    entry = Text(program, "entry");
                    if (entry is null) problems.Add($"{where}: 'program.entry' is missing or blank");
                    else if (entry.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(entry))
                    {
                        problems.Add($"{where}: 'program.entry' is a path under the folder's {ProgramFolder}/, never outside it: {entry}");
                    }
                }
            }
            else if (standIn == true)
            {
                problems.Add($"{where}: a stand-in states its 'program', the entry under its folder's {ProgramFolder}/");
            }

            var doors = new List<DoorFile>();
            if (!root.TryGetProperty("doors", out var listed) || listed.ValueKind != JsonValueKind.Array || listed.GetArrayLength() == 0)
            {
                problems.Add($"{where}: 'doors' is an array of {string.Join(", ", DoorFields)}, one at least");
            }
            else
            {
                var at = 0;
                foreach (var door in listed.EnumerateArray())
                {
                    at++;
                    var on = $"{where}, door {at}";
                    if (door.ValueKind != JsonValueKind.Object)
                    {
                        problems.Add($"{on}: a door is an object of {string.Join(", ", DoorFields)}");
                        continue;
                    }

                    Extra(door, DoorFields, on, problems);
                    var doorId = Text(door, "id");
                    var kind = Text(door, "kind");
                    if (doorId is null) problems.Add($"{on}: 'id' is missing or blank");
                    if (kind is null || !Kinds.Contains(kind)) problems.Add($"{on}: 'kind' is one of {string.Join(", ", Kinds)}");
                    var arguments = Arguments(door, on, problems);
                    if (standIn == true && arguments is not { Count: > 0 })
                    {
                        problems.Add($"{on}: a stand-in's door states the 'arguments' that choose it in its program");
                    }

                    if (doorId is not null && kind is not null) doors.Add(new DoorFile(doorId, kind, arguments ?? []));
                }

                foreach (var twice in doors.GroupBy(door => door.Id, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
                {
                    problems.Add($"{where}: door '{twice.Key}' is listed twice");
                }

                if (standIn == true
                    && doors.Select(door => string.Join("\u0000", door.Arguments)).Distinct(StringComparer.Ordinal).Count() != doors.Count)
                {
                    problems.Add($"{where}: two doors state the same arguments, so the program cannot tell them apart");
                }
            }

            return problems.Count > 0 || id is null || standIn is null
                ? new(file, null, problems)
                : new(file, new ProviderFile(id, standIn.Value, entry, doors, folder), []);
        }
    }

    private static IReadOnlyList<string>? Arguments(JsonElement door, string on, List<string> problems)
    {
        if (!door.TryGetProperty("arguments", out var arguments)) return null;
        if (arguments.ValueKind != JsonValueKind.Array
            || arguments.EnumerateArray().Any(each => each.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(each.GetString())))
        {
            problems.Add($"{on}: 'arguments' is an array of words");
            return null;
        }

        return [.. arguments.EnumerateArray().Select(each => each.GetString()!)];
    }

    private static void Extra(JsonElement element, string[] fields, string where, List<string> problems) =>
        problems.AddRange(element.EnumerateObject().Select(property => property.Name).Where(name => !fields.Contains(name))
            .Select(name => $"{where}: '{name}' is no field of the layout ({string.Join(", ", fields)})"));

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { } text
        && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
}
