using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>One module of a repository, as its code map names it (MAP3).</summary>
/// <param name="Id">Unique in the file; what a dependency names.</param>
/// <param name="Path">Repository-relative — the map is read by machines that are not this one.</param>
/// <param name="Summary">One line.</param>
public sealed record CodeModule(string Id, string Path, string Summary);

/// <summary>One module depending on another, in the producer's own word for how.</summary>
public sealed record CodeDependency(string From, string To, string Kind);

/// <summary>A repository's modules and how they depend on each other.</summary>
public sealed record CodeMap(IReadOnlyList<CodeModule> Modules, IReadOnlyList<CodeDependency> Dependencies);

/// <summary>What reading a checkout's code map found.</summary>
/// <param name="Map">The map, when the file exists and keeps every rule; otherwise null.</param>
/// <param name="File">Which candidate was read, repository-relative; null when there is none.</param>
/// <param name="Problem">Why the file was refused, naming the first break; null when it was not.</param>
/// <param name="Fed">
/// Where the map came from when it was not read from a checkout here (MAP3b, MAP3e): the commit it
/// was fed at, its line, and whose key fed it. Null for a checkout read from disk, and for a
/// repository nobody has fed a map for.
/// </param>
public sealed record CodeMapRead(CodeMap? Map, string? File, string? Problem, FeedProvenance? Fed = null);

/// <summary>
/// Reads a repository's code map (MAP3a, <c>docs/2026-09-23-map-design.md</c> §3) — one committed
/// file, found by convention, <b>judged whole</b>.
/// </summary>
/// <remarks>
/// <para>Candidates rather than configuration, for the reason <see cref="RepositoryScanner"/> gives:
/// a reader that needs setting up gets set up for one repository and never for the rest.</para>
///
/// <para>🔴 <b>A file that breaks any rule is refused, naming the first break, and nothing of it is
/// kept.</b> A half-drawn map reads as a whole one, and the person looking at it has no way to know
/// which half is missing.</para>
///
/// <para>Read by hand, like every store here, so nothing stops working under AOT.</para>
/// </remarks>
public static class CodeMapReader
{
    /// <summary>The names it may have, in the order they are tried. The first that exists wins.</summary>
    public static readonly IReadOnlyList<string> Candidates = ["docs/code-map.json", "code-map.json"];

    /// <summary>More modules than this is no longer a picture — and a mistaken producer's flood.</summary>
    public const int MaxModules = 500;

    public const int MaxDependencies = 5000;

    /// <summary>The file itself, before it is parsed: a generous ceiling for a small file.</summary>
    public const long MaxBytes = 1024 * 1024;

    /// <summary>Read the checkout's code map, if it keeps one.</summary>
    public static CodeMapRead Read(string repositoryRoot)
    {
        var found = Candidates.FirstOrDefault(candidate =>
            File.Exists(Path.Combine(repositoryRoot, candidate.Replace('/', Path.DirectorySeparatorChar))));
        if (found is null) return new CodeMapRead(null, null, null);

        var path = Path.Combine(repositoryRoot, found.Replace('/', Path.DirectorySeparatorChar));
        if (new FileInfo(path).Length > MaxBytes)
        {
            return new CodeMapRead(null, found, $"`{found}` is over {MaxBytes / 1024 / 1024} MB — a code map is a small file.");
        }

        var (map, problem) = Parse(File.ReadAllText(path), found);
        return new CodeMapRead(map, found, problem);
    }

    /// <summary>Judge a code map's text whole: the map, or the sentence naming the first break.</summary>
    public static (CodeMap? Map, string? Problem) Parse(string json, string file)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException error)
        {
            return (null, $"`{file}` is not JSON: {error.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return (null, $"`{file}` is not a JSON object.");

            var version = root.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32()
                : 0;
            if (version != 1)
            {
                return (null, $"`{file}` is version {version}; this service reads version 1.");
            }

            if (!Array(root, "modules", out var modulesElement) || !Array(root, "dependencies", out var dependenciesElement))
            {
                return (null, $"`{file}` needs a `modules` list and a `dependencies` list.");
            }

            if (modulesElement.GetArrayLength() > MaxModules)
            {
                return (null, $"`{file}` names more than {MaxModules} modules — past that it is no longer a picture.");
            }

            if (dependenciesElement.GetArrayLength() > MaxDependencies)
            {
                return (null, $"`{file}` names more than {MaxDependencies} dependencies.");
            }

            var modules = new List<CodeModule>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in modulesElement.EnumerateArray())
            {
                var id = Field(element, "id");
                var modulePath = Field(element, "path");
                var summary = Field(element, "summary");

                if (id.Length == 0) return (null, $"`{file}` has a module with no id — every module needs an id.");
                if (!OneLine(id)) return (null, $"`{file}` has a module id of more than one line.");
                if (!ids.Add(id)) return (null, $"`{file}` names module `{id}` twice — an id is what a dependency points at.");
                if (!RepositoryRelative(modulePath))
                {
                    return (null,
                        $"`{file}` gives module `{id}` the path `{modulePath}`, which is not repository-relative — "
                        + "no leading slash, no drive and no `..`: the map is read by machines that are not this one.");
                }

                if (!OneLine(summary)) return (null, $"`{file}` gives module `{id}` a summary of more than one line.");
                modules.Add(new CodeModule(id, modulePath, summary.Trim()));
            }

            var dependencies = new List<CodeDependency>();
            foreach (var element in dependenciesElement.EnumerateArray())
            {
                var from = Field(element, "from");
                var to = Field(element, "to");
                var kind = Field(element, "kind");
                foreach (var end in new[] { from, to })
                {
                    if (!ids.Contains(end))
                    {
                        return (null, $"`{file}` has a dependency naming `{end}`, which is not a module in the file.");
                    }
                }

                if (!OneLine(kind)) return (null, $"`{file}` has a dependency kind of more than one line.");
                dependencies.Add(new CodeDependency(from, to, kind.Trim()));
            }

            return (new CodeMap(modules, dependencies), null);
        }
    }

    /// <summary>
    /// A judged map in the file's own shape and one canonical form — what a deployment keeps of a fed
    /// map (MAP3b), so that two readings saying the same thing hash alike whatever their whitespace.
    /// </summary>
    public static string Write(CodeMap map)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", 1);
            writer.WriteStartArray("modules");
            foreach (var module in map.Modules)
            {
                writer.WriteStartObject();
                writer.WriteString("id", module.Id);
                writer.WriteString("path", module.Path);
                writer.WriteString("summary", module.Summary);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("dependencies");
            foreach (var dependency in map.Dependencies)
            {
                writer.WriteStartObject();
                writer.WriteString("from", dependency.From);
                writer.WriteString("to", dependency.To);
                writer.WriteString("kind", dependency.Kind);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static bool Array(JsonElement root, string name, out JsonElement value) =>
        root.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Array;

    private static string Field(JsonElement element, string name) => JsonFields.Text(element, name) ?? "";

    private static bool OneLine(string text) => !text.Contains('\n') && !text.Contains('\r');

    /// <summary>Relative to the repository: not rooted, no drive, and never climbing out of it.</summary>
    private static bool RepositoryRelative(string path) =>
        path.Length > 0
        && OneLine(path)
        && path[0] is not ('/' or '\\')
        && !path.Contains(':')
        && !path.Split('/', '\\').Contains("..");
}
