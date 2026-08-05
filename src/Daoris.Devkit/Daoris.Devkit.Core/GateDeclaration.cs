using System.Text.Json;

namespace Daoris.Devkit;

/// <summary>A gate the repository declared: a name and the command it runs.</summary>
/// <param name="Name">Shown in the run log and usable as `devkit run &lt;name&gt;`.</param>
/// <param name="Run">The command line, run through the platform shell.</param>
/// <param name="WorkingDirectory">Repository-relative; null means the repository root.</param>
public sealed record DeclaredGate(string Name, string Run, string? WorkingDirectory = null);

/// <summary>What the sensitive-content scan needs that cannot be universal.</summary>
/// <param name="PatternsFile">
/// Repository-relative path to the private pattern list — the brand names, sibling names and network
/// details that must never be committed. It is itself gitignored: the whole point is that the tokens
/// being looked for are not in the repository that is being scanned.
/// </param>
/// <param name="ReviewedObjects">
/// Git object shas whose history findings have been read and judged benign — a test fixture that
/// deliberately contains the shape the scanner looks for, most often.
///
/// **Only ever consulted for a history audit**, never for staged changes or the working tree. That
/// asymmetry is the whole safety argument. A working-tree ignore-list is how leaks get in: it silences
/// a file, and the next secret written to that file is silent too. An acknowledgement here names one
/// immutable object by content hash, so it cannot cover anything that does not already exist — a new
/// leak is a new object with a new sha, and it is reported.
/// </param>
public sealed record SensitiveOptions(
    string PatternsFile = "local/sensitive-patterns.txt",
    IReadOnlyList<string>? ReviewedObjects = null);

/// <summary>Where the version lives and which files must agree with it.</summary>
/// <param name="Source">The one file that owns the version.</param>
/// <param name="Pattern">A regex with a single capturing group around the version.</param>
/// <param name="Mirrors">Files that restate it and must not disagree.</param>
public sealed record VersionOptions(string? Source = null, string? Pattern = null, IReadOnlyList<string>? Mirrors = null);

/// <summary>Which documents have to keep up.</summary>
/// <param name="Changelog">The release-facing log.</param>
/// <param name="Tracked">
/// Documents that must not fall behind the code. Each names a document and the paths whose changes it
/// is supposed to describe.
/// </param>
/// <param name="GraceDays">
/// How many days the code may lead the document before it counts as stale. Zero means same-day is
/// fine and the next day is not, which is the useful default: within a session the two are edited in
/// whatever order the work happened.
/// </param>
public sealed record DocsOptions(
    string? Changelog = null, IReadOnlyList<TrackedDocument>? Tracked = null, int GraceDays = 0);

/// <summary>How to invoke the doctrine tool, for repositories where it is not simply on PATH.</summary>
/// <param name="Command">The executable. Defaults to the published CLI's name.</param>
/// <param name="Arguments">
/// Anything that must precede `check` — a script path, say. Explicit rather than one command LINE that
/// gets split on spaces, because a path with a space in it is the case that silently breaks.
/// </param>
public sealed record DoctrineOptions(string Command = "daoris", IReadOnlyList<string>? Arguments = null);

/// <param name="Document">The document that must keep up.</param>
/// <param name="Describes">Repository-relative paths whose modification times it is compared against.</param>
public sealed record TrackedDocument(string Document, IReadOnlyList<string> Describes);

/// <summary>
/// `daoris.gates.json` — what this repository runs, and how the universal gates are configured for it.
/// </summary>
/// <remarks>
/// A separate file from `daoris.json` on purpose (D26). The manifest is inert data the CLI parses on
/// every invocation; this file names commands that execute. Keeping the two apart means a reader of the
/// manifest can still be certain nothing in it runs.
///
/// Parsed with <see cref="JsonDocument"/> rather than deserialized into these records: it is
/// AOT-clean without a source generator, and every field gets an error message that names the file and
/// the field rather than a framework type.
/// </remarks>
public sealed class GateDeclaration
{
    public const string FileName = "daoris.gates.json";

    /// <summary>The devkit release this repository is pinned to. Informational here; the launcher enforces it.</summary>
    public string? Devkit { get; init; }

    public IReadOnlyList<DeclaredGate> Gates { get; init; } = [];

    public SensitiveOptions Sensitive { get; init; } = new();

    public VersionOptions Version { get; init; } = new();

    public DocsOptions Docs { get; init; } = new();

    public DoctrineOptions Doctrine { get; init; } = new();

    /// <summary>
    /// Universal gates the repository has deliberately turned off, by name.
    /// </summary>
    /// <remarks>
    /// Opting out is allowed and is recorded in the repository, which is the difference that matters: a
    /// gate that is off because someone wrote it down is a decision, and one that is off because it was
    /// never wired up is an accident nobody can tell from the outside. The runner prints what was
    /// disabled on every run, so the decision stays visible rather than becoming invisible.
    /// </remarks>
    public IReadOnlySet<string> Disabled { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static GateDeclaration Read(string repositoryRoot)
    {
        var file = Path.Combine(repositoryRoot, FileName);
        if (!File.Exists(file))
        {
            throw new DevkitException(
                $"no {FileName} in '{repositoryRoot}' — run 'daoris-devkit init' to write one");
        }

        using var document = Parse(file);
        var root = document.RootElement;

        return new GateDeclaration
        {
            Devkit = String(root, "devkit"),
            Gates = ReadGates(root),
            Sensitive = ReadSensitive(root),
            Version = ReadVersion(root),
            Docs = ReadDocs(root),
            Doctrine = ReadDoctrine(root),
            Disabled = new HashSet<string>(Strings(root, "disabled"), StringComparer.OrdinalIgnoreCase),
        };
    }

    private static JsonDocument Parse(string file)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(file));
        }
        catch (JsonException error)
        {
            // The framework message alone says "invalid JSON" without saying which file, and this one
            // is hand-edited by definition.
            throw new DevkitException($"{FileName} is not valid JSON: {error.Message}");
        }
    }

    private static IReadOnlyList<DeclaredGate> ReadGates(JsonElement root)
    {
        if (!root.TryGetProperty("gates", out var gates) || gates.ValueKind != JsonValueKind.Array) return [];

        var declared = new List<DeclaredGate>();
        foreach (var gate in gates.EnumerateArray())
        {
            var name = String(gate, "name");
            var run = String(gate, "run");
            if (name is null || run is null)
            {
                throw new DevkitException($"{FileName}: every entry in 'gates' needs both 'name' and 'run'");
            }

            declared.Add(new DeclaredGate(name, run, String(gate, "cwd")));
        }

        return declared;
    }

    private static SensitiveOptions ReadSensitive(JsonElement root) =>
        root.TryGetProperty("sensitive", out var element) && element.ValueKind == JsonValueKind.Object
            ? new SensitiveOptions(
                String(element, "patternsFile") ?? new SensitiveOptions().PatternsFile,
                Strings(element, "reviewedObjects"))
            : new SensitiveOptions();

    private static VersionOptions ReadVersion(JsonElement root) =>
        root.TryGetProperty("version", out var element) && element.ValueKind == JsonValueKind.Object
            ? new VersionOptions(String(element, "source"), String(element, "pattern"), Strings(element, "mirrors"))
            : new VersionOptions();

    private static DoctrineOptions ReadDoctrine(JsonElement root) =>
        root.TryGetProperty("doctrine", out var element) && element.ValueKind == JsonValueKind.Object
            ? new DoctrineOptions(String(element, "command") ?? "daoris", Strings(element, "args"))
            : new DoctrineOptions();

    private static DocsOptions ReadDocs(JsonElement root)
    {
        if (!root.TryGetProperty("docs", out var element) || element.ValueKind != JsonValueKind.Object)
        {
            return new DocsOptions();
        }

        var tracked = new List<TrackedDocument>();
        if (element.TryGetProperty("tracked", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in list.EnumerateArray())
            {
                var document = String(entry, "document");
                if (document is null)
                {
                    throw new DevkitException($"{FileName}: every entry in 'docs.tracked' needs a 'document'");
                }

                tracked.Add(new TrackedDocument(document, Strings(entry, "describes")));
            }
        }

        return new DocsOptions(String(element, "changelog"), tracked, Int(element, "graceDays") ?? 0);
    }

    private static int? Int(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<string> Strings(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } text) items.Add(text);
        }

        return items;
    }
}

/// <summary>A problem with the repository's setup rather than with its content.</summary>
/// <remarks>
/// Separate from a failing gate on purpose. A gate that fails has done its job; this means the devkit
/// could not do its job at all, and the two exit with different codes so a script can tell them apart.
/// </remarks>
public sealed class DevkitException(string message) : Exception(message);
