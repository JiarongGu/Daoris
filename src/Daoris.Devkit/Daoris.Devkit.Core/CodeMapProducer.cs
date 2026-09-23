using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;

namespace Daoris.Devkit;

/// <summary>One module of the map: what a dependency names, where it lives, and one line about it.</summary>
public sealed record MapModule(string Id, string Path, string Summary);

/// <summary>One module depending on another, in the producer's word for how.</summary>
public sealed record MapDependency(string From, string To, string Kind);

/// <summary>What producing a repository's code map found.</summary>
/// <param name="File">Where the map goes — the candidate the reader would read.</param>
/// <param name="Text">The file as the devkit writes it; null when <paramref name="Problem"/> says why not.</param>
/// <param name="Notes">What the project files say that the map cannot draw — a reference to a project this repository does not track.</param>
/// <param name="Problem">Why nothing can be written, naming the first break; null when the map is whole.</param>
public sealed record ProducedMap(
    string File, string? Text, IReadOnlyList<MapModule> Modules, IReadOnlyList<MapDependency> Dependencies,
    IReadOnlyList<string> Notes, string? Problem);

/// <summary>
/// A repository's code map, produced from its own project files (MAP3c,
/// <c>docs/2026-09-23-map-design.md</c> §3) — the tool half of "both": exact where a stack declares its
/// structure, without a compiler.
/// </summary>
/// <remarks>
/// <para><b>What it reads.</b> Every tracked <c>*.csproj</c> is a module, and its
/// <c>ProjectReference</c> items are its dependencies (<see cref="ProjectKind"/>). Every tracked
/// <c>package.json</c> below the root is a module named by its package, and a dependency of any
/// section that names another of the repository's packages is drawn (<see cref="PackageKind"/>). The
/// root <c>package.json</c> is the repository itself, not a module of it. Tracked files only, so a
/// build output or an installed package is never a module.</para>
///
/// <para><b>What it keeps.</b> The tool owns the modules and the two kinds it derives. A summary is the
/// project's own description where it declares one, and otherwise the line already in the map — a
/// person writes it once and the tool never erases it. A dependency of any other kind (a service called
/// over HTTP, say) is a person's too, and stays while both its ends are modules.</para>
///
/// <para><b>What it refuses.</b> A file it would write that the service's reader would refuse: two
/// modules on one id, more than the reader's bounds. And an existing file it cannot read, because
/// rewriting it would lose what a person wrote in it. The reader's rules are restated here, since the
/// devkit shares no code with the service; a test holds them to the reader's own source.</para>
/// </remarks>
public static class CodeMapProducer
{
    /// <summary>Where the reader looks, in its order — so the map is written where it will be read.</summary>
    public static readonly IReadOnlyList<string> Candidates = ["docs/code-map.json", "code-map.json"];

    public const int MaxModules = 500;

    public const int MaxDependencies = 5000;

    /// <summary>A C# project referencing another (<c>ProjectReference</c>).</summary>
    public const string ProjectKind = "project";

    /// <summary>A package depending on another of the repository's packages.</summary>
    public const string PackageKind = "package";

    private static readonly string[] PackageSections = ["dependencies", "devDependencies", "peerDependencies", "optionalDependencies"];

    public static ProducedMap Produce(string repositoryRoot, IGit git)
    {
        var file = Candidates.FirstOrDefault(candidate => System.IO.File.Exists(Absolute(repositoryRoot, candidate))) ?? Candidates[0];
        var existing = Existing(repositoryRoot, file, out var unreadable);
        if (unreadable)
        {
            return Refused(file,
                $"`{file}` is not a code map this devkit can read, and rewriting it would lose the summaries and "
                + "dependencies a person wrote in it. Fix it or delete it, then run `daoris-devkit map` again.");
        }

        var tracked = git.TrackedFiles();
        var sources = new List<(MapModule Module, string Source)>();
        var projects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // csproj path → id
        var packages = new Dictionary<string, string>(StringComparer.Ordinal); // package name → id

        foreach (var project in tracked.Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal))
        {
            var document = LoadProject(repositoryRoot, project);
            var id = System.IO.Path.GetFileNameWithoutExtension(project);
            var description = document?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Description")?.Value;
            sources.Add((new MapModule(id, DirectoryOf(project), Summary(description, existing, id)), project));
            projects[project] = id;
        }

        foreach (var manifest in tracked.Where(IsPackage).Order(StringComparer.Ordinal))
        {
            var (name, description) = PackageIdentity(repositoryRoot, manifest);
            var id = name ?? DirectoryOf(manifest);
            sources.Add((new MapModule(id, DirectoryOf(manifest), Summary(description, existing, id)), manifest));
            if (name is not null) packages[name] = id;
        }

        foreach (var clash in sources.GroupBy(s => s.Module.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            return Refused(file,
                $"two modules would both be `{clash.Key}`: {string.Join(" and ", clash.Select(s => s.Source))} — "
                + "a code map names each module once.");
        }

        if (sources.Count > MaxModules)
        {
            return Refused(file, $"this repository has {sources.Count} modules — more than {MaxModules} is no longer a picture, and the reader refuses it.");
        }

        var ids = sources.Select(s => s.Module.Id).ToHashSet(StringComparer.Ordinal);
        var notes = new List<string>();
        var dependencies = new HashSet<MapDependency>();

        foreach (var (project, from) in projects)
        {
            foreach (var include in References(LoadProject(repositoryRoot, project)))
            {
                var target = Resolve(DirectoryOf(project), include);
                if (target is not null && projects.TryGetValue(target, out var to))
                {
                    dependencies.Add(new MapDependency(from, to, ProjectKind));
                }
                else
                {
                    notes.Add($"{project} references `{include}`, which is not a project this repository tracks — not drawn.");
                }
            }
        }

        foreach (var manifest in tracked.Where(IsPackage))
        {
            var (name, _) = PackageIdentity(repositoryRoot, manifest);
            if (name is null) continue;
            foreach (var needed in PackageDependencies(repositoryRoot, manifest))
            {
                if (needed != name && packages.TryGetValue(needed, out var to)) dependencies.Add(new MapDependency(packages[name], to, PackageKind));
            }
        }

        // What the files do not say is a person's: kept while both its ends are still modules.
        foreach (var kept in existing?.Dependencies ?? [])
        {
            if (kept.Kind is ProjectKind or PackageKind) continue;
            if (ids.Contains(kept.From) && ids.Contains(kept.To)) dependencies.Add(kept);
        }

        if (dependencies.Count > MaxDependencies)
        {
            return Refused(file, $"this repository has {dependencies.Count} dependencies — more than {MaxDependencies}, which the reader refuses.");
        }

        var modules = sources.Select(s => s.Module)
            .OrderBy(m => m.Path, StringComparer.Ordinal).ThenBy(m => m.Id, StringComparer.Ordinal).ToList();
        var ordered = dependencies
            .OrderBy(d => d.From, StringComparer.Ordinal).ThenBy(d => d.To, StringComparer.Ordinal)
            .ThenBy(d => d.Kind, StringComparer.Ordinal).ToList();
        return new ProducedMap(file, Render(modules, ordered), modules, ordered, notes, null);
    }

    /// <summary>
    /// Whether the committed map is what the project files say — a FACT, so it gates (D54). Stale, it
    /// names what differs and the command that fixes it.
    /// </summary>
    public static (bool Fresh, string Detail) Check(string repositoryRoot, ProducedMap produced)
    {
        if (produced.Problem is not null) return (false, produced.Problem);

        var path = Absolute(repositoryRoot, produced.File);
        if (!System.IO.File.Exists(path))
        {
            return (false, $"no code map yet — `daoris-devkit map` writes `{produced.File}` from the project files.");
        }

        var committed = System.IO.File.ReadAllText(path).Replace("\r\n", "\n");
        if (committed == produced.Text)
        {
            return (true, $"`{produced.File}` is what the project files say — {produced.Modules.Count} module(s), {produced.Dependencies.Count} dependency(ies)");
        }

        var existing = Parse(committed);
        var differences = new List<string>();
        if (existing is not null)
        {
            var had = existing.Modules.ToDictionary(m => m.Id, StringComparer.Ordinal);
            foreach (var module in produced.Modules)
            {
                if (!had.TryGetValue(module.Id, out var old)) differences.Add($"module `{module.Id}` is missing");
                else if (old.Path != module.Path || old.Summary != module.Summary) differences.Add($"module `{module.Id}` has changed");
            }

            var now = produced.Modules.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
            differences.AddRange(existing.Modules.Where(m => !now.Contains(m.Id)).Select(m => $"module `{m.Id}` is no longer a project"));
            differences.AddRange(produced.Dependencies.Except(existing.Dependencies).Select(d => $"{d.From} → {d.To} ({d.Kind}) is missing"));
            differences.AddRange(existing.Dependencies.Except(produced.Dependencies).Select(d => $"{d.From} → {d.To} ({d.Kind}) is no longer declared"));
        }

        return (false,
            $"`{produced.File}` is not what the project files say"
            + (differences.Count > 0 ? $": {string.Join("; ", differences)}" : " — it is not written the way the devkit writes it")
            + ". Run `daoris-devkit map` and commit the result.");
    }

    /// <summary>Write beside, then rename — a map is never half-written where a reader might open it.</summary>
    public static void Write(string repositoryRoot, ProducedMap produced)
    {
        if (produced.Text is null) throw new DevkitException(produced.Problem ?? "nothing to write");

        var path = Absolute(repositoryRoot, produced.File);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var beside = path + ".writing";
        System.IO.File.WriteAllText(beside, produced.Text, new UTF8Encoding(false));
        System.IO.File.Move(beside, path, overwrite: true);
    }

    /// <summary>The file's shape, the same way every time: two-space indent, LF, a final newline.</summary>
    public static string Render(IReadOnlyList<MapModule> modules, IReadOnlyList<MapDependency> dependencies)
    {
        using var stream = new MemoryStream();
        // The relaxed encoder writes an apostrophe and a 中文 line as themselves: the default escapes them
        // for an HTML page this file is never embedded in, and the file is reviewed as a diff.
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", 1);
            writer.WriteStartArray("modules");
            foreach (var module in modules)
            {
                writer.WriteStartObject();
                writer.WriteString("id", module.Id);
                writer.WriteString("path", module.Path);
                writer.WriteString("summary", module.Summary);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("dependencies");
            foreach (var dependency in dependencies)
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

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static ProducedMap Refused(string file, string problem) => new(file, null, [], [], [], problem);

    /// <summary>One line: a project's description may wrap, and the reader takes one line.</summary>
    private static string Summary(string? declared, ExistingMap? existing, string id)
    {
        var said = string.Join(' ', (declared ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (said.Length > 0) return said;

        return existing?.Modules.FirstOrDefault(m => m.Id == id)?.Summary ?? "";
    }

    private sealed record ExistingMap(IReadOnlyList<MapModule> Modules, IReadOnlyList<MapDependency> Dependencies);

    private static ExistingMap? Existing(string repositoryRoot, string file, out bool unreadable)
    {
        unreadable = false;
        var path = Absolute(repositoryRoot, file);
        if (!System.IO.File.Exists(path)) return null;

        var parsed = Parse(System.IO.File.ReadAllText(path));
        unreadable = parsed is null;
        return parsed;
    }

    /// <summary>Only as much as the tool keeps from a map: its summaries and its dependencies. Null when it is not one.</summary>
    private static ExistingMap? Parse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || version.GetInt32() != 1
                || !root.TryGetProperty("modules", out var modules) || modules.ValueKind != JsonValueKind.Array
                || !root.TryGetProperty("dependencies", out var dependencies) || dependencies.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return new ExistingMap(
                modules.EnumerateArray().Select(m => new MapModule(Text(m, "id"), Text(m, "path"), Text(m, "summary"))).ToList(),
                dependencies.EnumerateArray().Select(d => new MapDependency(Text(d, "from"), Text(d, "to"), Text(d, "kind"))).ToList());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static XDocument? LoadProject(string repositoryRoot, string project)
    {
        try
        {
            return XDocument.Load(Absolute(repositoryRoot, project));
        }
        catch (Exception error) when (error is System.Xml.XmlException or IOException)
        {
            throw new DevkitException($"{project} is not a project file the devkit can read: {error.Message}");
        }
    }

    /// <summary>Every <c>ProjectReference</c>'s <c>Include</c>, split where MSBuild would split it.</summary>
    private static IEnumerable<string> References(XDocument? project) =>
        project?.Descendants().Where(e => e.Name.LocalName == "ProjectReference")
            .SelectMany(e => ((string?)e.Attribute("Include") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        ?? [];

    /// <summary>
    /// A reference, relative to its project's directory, as a repository-relative path — or null when
    /// it is an MSBuild expression or climbs out of the repository.
    /// </summary>
    private static string? Resolve(string directory, string include)
    {
        if (include.Contains("$(", StringComparison.Ordinal) || include.Contains(':')) return null;

        var parts = new List<string>();
        foreach (var part in (directory == "." ? include : $"{directory}/{include}").Split('/', '\\'))
        {
            if (part is "" or ".") continue;
            if (part == "..")
            {
                if (parts.Count == 0) return null;
                parts.RemoveAt(parts.Count - 1);
                continue;
            }

            parts.Add(part);
        }

        return string.Join('/', parts);
    }

    private static bool IsPackage(string file) =>
        file.EndsWith("/package.json", StringComparison.Ordinal);

    private static (string? Name, string? Description) PackageIdentity(string repositoryRoot, string manifest)
    {
        using var document = LoadPackage(repositoryRoot, manifest);
        var root = document.RootElement;
        var name = Text(root, "name");
        var description = Text(root, "description");
        return (name.Length > 0 ? name : null, description.Length > 0 ? description : null);
    }

    private static IReadOnlyList<string> PackageDependencies(string repositoryRoot, string manifest)
    {
        using var document = LoadPackage(repositoryRoot, manifest);
        var needed = new List<string>();
        foreach (var section in PackageSections)
        {
            if (document.RootElement.TryGetProperty(section, out var listed) && listed.ValueKind == JsonValueKind.Object)
            {
                needed.AddRange(listed.EnumerateObject().Select(p => p.Name));
            }
        }

        return needed;
    }

    private static JsonDocument LoadPackage(string repositoryRoot, string manifest)
    {
        try
        {
            var document = JsonDocument.Parse(System.IO.File.ReadAllText(Absolute(repositoryRoot, manifest)));
            if (document.RootElement.ValueKind == JsonValueKind.Object) return document;
            document.Dispose();
        }
        catch (JsonException)
        {
        }

        throw new DevkitException($"{manifest} is not a package manifest the devkit can read");
    }

    private static string DirectoryOf(string file)
    {
        var slash = file.LastIndexOf('/');
        return slash < 0 ? "." : file[..slash];
    }

    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static string Absolute(string repositoryRoot, string relative) =>
        System.IO.Path.Combine(repositoryRoot, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
}
