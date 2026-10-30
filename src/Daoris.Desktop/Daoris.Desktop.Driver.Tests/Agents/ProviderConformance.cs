using System.Collections.Concurrent;
using System.Text.Json;
using Daoris.Driver;
using Xunit.Sdk;

namespace Daoris.Desktop.Driver.Tests;

// The conformance suite's engine (AGENTPROVIDER1 row b, part 2; the agent-provider design §10): the views it runs over, the cases, the
// pending reader and its two failing rules. Its three test classes are the halves: in-process, the spawn cases that plant the process's
// environment (run alone), and the stand-ins' live cases (the Process half).

/// <summary>Which of the suite's three classes runs a case.</summary>
internal enum ConformanceHalf
{
    /// <summary>No process, nothing set in the process's environment: the fast half.</summary>
    InProcess,

    /// <summary>Plants sentinel variables in the process's environment and reads a prepared spawn: run alone, after the rest.</summary>
    Environment,

    /// <summary>A stand-in's program run live, as real model-free processes: the Process half.</summary>
    Live,
}

/// <summary>Whether a case is a provider's as a whole, its row's door <c>*</c>, or each of its doors'.</summary>
internal enum ConformanceScope
{
    Provider,
    Door,
}

/// <summary>One case of §10: its name, the half that runs it, its scope, the clause it holds, where it applies and what it checks.</summary>
internal sealed record ConformanceCase(
    string Name, ConformanceHalf Half, ConformanceScope Scope, string Says,
    Func<ConformanceSubject, Provider, AgentDoor?, bool> Applies, Func<ConformanceRun, Task> Body);

/// <summary>
/// A view of the providers the suite runs over: the registry over today's adapters, the registration list's providers as written on the
/// contract, or a plugin's provider made in a test home. Each with the adapters its doors are presented through.
/// </summary>
internal sealed record ConformanceSubject(string Source, AdapterSet Adapters, ProviderRegistry Registry, IReadOnlyList<Provider> Providers)
{
    /// <summary><c>ProviderRegistry.Of(AdapterSet.Built())</c>: every provider and door today's adapters hold.</summary>
    public const string RegistrySource = "registry";

    /// <summary><c>Providers.Built()</c>: the providers written on the contract, through the shim.</summary>
    public const string ListSource = "list";

    /// <summary>A test home's plugins: an enabled one's own provider and a disabled one's unavailable descriptor.</summary>
    public const string PluginSource = "plugin";
}

/// <summary>What one case runs over: its view, the provider, the door (null for a provider's case) and its scratch folder.</summary>
internal sealed record ConformanceRun(ConformanceSubject Subject, Provider Provider, AgentDoor? Door, string Scratch)
{
    public ProviderRegistry Registry => Subject.Registry;

    public AgentDoor TheDoor => Door ?? throw new InvalidOperationException("a provider's case has no door");

    /// <summary>The door as today's readers hold it.</summary>
    public ISessionAdapter Adapter => Subject.Adapters.Resolve(TheDoor.Id.Value);

    /// <summary>A home of the case's own, never the machine's.</summary>
    public string Home() => Folder("home");

    /// <summary>The repository's root a start runs in.</summary>
    public string Root() => Folder("engine");

    public SessionTarget Target() => new("q-1", "title", "body", "asker", "engine", Root(), "http://127.0.0.1:9") { Home = Home() };

    public ChatTarget Chat() => new("engine", Root(), "http://127.0.0.1:9");

    /// <summary>A command naming a program in the scratch, as a home's <c>commands</c> would.</summary>
    public IReadOnlyList<string> Command(string script = "agent.mjs") => ["node", Path.Combine(Scratch, script)];

    private string Folder(string name)
    {
        var folder = Path.Combine(Scratch, name);
        Directory.CreateDirectory(folder);
        return folder;
    }
}

/// <summary>The suite's views, made once.</summary>
internal static class ConformanceSubjects
{
    /// <summary>The test home's enabled plugin's harness, a provider of its own.</summary>
    public const string PluginDoor = "conformance-door";

    /// <summary>The test home's disabled plugin's harness: an unavailable descriptor, known and never started.</summary>
    public const string PluginOff = "conformance-off-door";

    private static readonly Lazy<IReadOnlyList<ConformanceSubject>> Made = new(Make);

    public static IReadOnlyList<ConformanceSubject> Every => Made.Value;

    public static ConformanceSubject Of(string source) => Every.Single(subject => subject.Source == source);

    private static IReadOnlyList<ConformanceSubject> Make()
    {
        var built = AdapterSet.Built();
        var registry = ProviderRegistry.Of(built);
        var listed = new AdapterSet(new Dictionary<string, ISessionAdapter>(LegacyDoor.Over(Providers.Built()), StringComparer.OrdinalIgnoreCase));
        var plugins = WithPlugins(built);
        var made = ProviderRegistry.Of(plugins);

        return
        [
            new(ConformanceSubject.RegistrySource, built, registry, registry.Providers),
            // Reserved by registering: every id the registration list registers (O4-F9).
            new(ConformanceSubject.ListSource, listed, new ProviderRegistry(Providers.Built(), listed.Reserved), Providers.Built()),
            new(ConformanceSubject.PluginSource, plugins, made,
                [.. made.Providers.Where(provider => provider.Identity.DeclaredBy.Count > 0 || provider.Unavailable)]),
        ];
    }

    /// <summary>The built set and a test home's two plugins, written as a person's would be, read, and the home removed.</summary>
    private static AdapterSet WithPlugins(AdapterSet built)
    {
        var home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "provider-conformance", "plugins-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Plugin(home, "conformance-plugin", PluginDoor, disabled: false);
            Plugin(home, "conformance-off", PluginOff, disabled: true);
            return built.WithPlugins(AgentPluginReads.Current.Load(home, built.Reserved));
        }
        finally
        {
            ConformanceScratch.Remove(home);
        }
    }

    private static void Plugin(string home, string id, string harness, bool disabled)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Plugins, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "plugin.json"),
            $$"""{ "id": "{{id}}", "harnesses": [ { "name": "{{harness}}", "command": ["node"] } ] }""");
        if (disabled) PluginState.Disable(home, id);
    }
}

/// <summary>A scratch folder per test, under the repository's <c>_fixtures/</c>, never the operating system's temporary folder.</summary>
internal static class ConformanceScratch
{
    public static string New(string half)
    {
        var folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "provider-conformance", $"{half}-{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static void Remove(string folder) => GitSeed.Discard(folder);
}

/// <summary>The suite's rows and their run.</summary>
internal static class Conformance
{
    /// <summary>A provider's case's door, and a pending line's for every door the case runs on.</summary>
    public const string EveryDoor = "*";

    public static IReadOnlyList<ConformanceCase> Cases => ConformanceCases.All;

    /// <summary>Every case each view runs, per provider and, for a door's case, per door it applies to.</summary>
    public static IEnumerable<(ConformanceSubject Subject, Provider Provider, string Door, ConformanceCase Case)> Runs()
    {
        foreach (var subject in ConformanceSubjects.Every)
        {
            foreach (var provider in subject.Providers)
            {
                foreach (var each in Cases)
                {
                    if (each.Scope == ConformanceScope.Provider)
                    {
                        if (each.Applies(subject, provider, null)) yield return (subject, provider, EveryDoor, each);
                        continue;
                    }

                    foreach (var door in provider.Doors.Where(door => each.Applies(subject, provider, door)))
                    {
                        yield return (subject, provider, door.Id.Value, each);
                    }
                }
            }
        }
    }

    /// <summary>One half's rows, as a theory's data: the view, the provider, the door (<c>*</c> for a provider's case) and the case.</summary>
    public static TheoryData<string, string, string, string> Rows(ConformanceHalf half)
    {
        var rows = new TheoryData<string, string, string, string>();
        foreach (var (subject, provider, door, each) in Runs().Where(run => run.Case.Half == half))
        {
            rows.Add(subject.Source, provider.Id.Value, door, each.Name);
        }

        return rows;
    }

    /// <summary>
    /// Runs one row: its case must pass, unless a line of its provider's pending file holds it, when it must fail. Answers the held case's
    /// reason, or null for a case that passed.
    /// </summary>
    /// <param name="pending">The lines to read; the provider's own file's where null.</param>
    public static Task<string?> RunAsync(
        string source, string provider, string door, string @case, string scratch, IReadOnlyList<PendingLine>? pending = null)
    {
        var subject = ConformanceSubjects.Of(source);
        var held = subject.Providers.Single(each => each.Id.Value == provider);
        var on = door == EveryDoor ? null : held.Doors.Single(each => each.Id.Value == door);
        var run = Cases.Single(each => each.Name == @case);
        var lines = pending ?? ConformancePending.Of(provider);
        return ConformancePending.VerdictAsync(
            () => run.Body(new ConformanceRun(subject, held, on, scratch)), ConformancePending.Holding(lines, @case, door));
    }
}

/// <summary>One line of a provider's <c>pending.json</c>: a case it cannot pass yet, on a door or every door, with its row and why.</summary>
internal sealed record PendingLine(string Case, string Door, string Row, string Why, string File);

/// <summary>A pending file as read: its lines, and what in it breaks the layout.</summary>
internal sealed record PendingRead(IReadOnlyList<PendingLine> Lines, IReadOnlyList<string> Problems);

/// <summary>A case held pending that passes: the suite fails until its line goes.</summary>
internal sealed class PendingCasePasses(string message) : XunitException(message);

/// <summary>A case whose reader no row has written yet: the case is written, and held pending with the row that writes the reader.</summary>
internal sealed class NotAnsweredYet(string reader) : Exception($"no reader answers this case yet: {reader}");

/// <summary>
/// The pending layout (D213's row b part 2 note): one <c>pending.json</c> per provider, an array of <c>{ case, door, row, why }</c>, a
/// stand-in's beside its <c>provider.json</c> in its folder under <c>Agents/Providers/</c>, a real provider's under the tests'
/// <c>fixtures/providers/&lt;id&gt;/</c>. A new provider brings its own file and edits none shared.
/// </summary>
internal static class ConformancePending
{
    public const string FileName = "pending.json";

    /// <summary>The rows of the design's §11 a case can wait for: row a has landed, so none names it.</summary>
    public static IReadOnlyList<string> Rows { get; } =
    [
        "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "m-i", "m-ii", "n", "o", "p", "q", "r",
        "s-i", "s-ii", "s-iii", "s-iv", "s-v", "s-vi", "s-vii", "s-viii", "s-ix", "t", "u",
    ];

    private static readonly string[] Fields = ["case", "door", "row", "why"];

    private static readonly ConcurrentDictionary<string, PendingRead> Read = new(StringComparer.OrdinalIgnoreCase);

    private static string ProvidersFolder => Path.Combine(SourceFiles.Project(SourceFiles.DriverProject), "Agents", "Providers");

    private static string FixturesFolder => Path.Combine(SourceFiles.Project(SourceFiles.DriverTestsProject), "fixtures", "providers");

    /// <summary>The folder each provider written on the contract keeps its facts in, by its id, read from the folder's <c>provider.json</c>.</summary>
    public static IReadOnlyDictionary<string, string> Folders() =>
        Directory.EnumerateDirectories(ProvidersFolder)
            .Select(folder => Path.Combine(folder, "provider.json"))
            .Where(File.Exists)
            .ToDictionary(
                file => JsonDocument.Parse(File.ReadAllText(file)).RootElement.GetProperty("id").GetString()!,
                file => Path.GetDirectoryName(file)!,
                StringComparer.OrdinalIgnoreCase);

    /// <summary>Where a provider's pending cases are: beside its <c>provider.json</c> where it has a folder, else under the fixtures.</summary>
    public static string FileOf(string provider) =>
        Folders().TryGetValue(provider, out var folder) ? Path.Combine(folder, FileName) : Path.Combine(FixturesFolder, provider, FileName);

    /// <summary>A provider's lines; a provider with no file fails its every row, naming where the file goes.</summary>
    public static IReadOnlyList<PendingLine> Of(string provider)
    {
        var file = FileOf(provider);
        if (!File.Exists(file))
        {
            throw new XunitException(
                $"provider '{provider}' brings no {FileName} at {Relative(file)}: each provider keeps its own, [] where it passes every case.");
        }

        return Read.GetOrAdd(file, path => Parse(File.ReadAllText(path), path)).Lines;
    }

    /// <summary>The line holding a case on a door: one naming that door, or every door.</summary>
    public static PendingLine? Holding(IReadOnlyList<PendingLine> lines, string @case, string door) =>
        lines.FirstOrDefault(line => line.Case == @case && (line.Door == Conformance.EveryDoor || line.Door == door));

    /// <summary>
    /// The first rule: a case with no line must pass; a case a line holds must fail, and passing fails the suite naming the file its line
    /// goes from. Answers the held case's reason.
    /// </summary>
    public static async Task<string?> VerdictAsync(Func<Task> body, PendingLine? pending)
    {
        if (pending is null)
        {
            await body().ConfigureAwait(false);
            return null;
        }

        try
        {
            await body().ConfigureAwait(false);
        }
        catch (Exception held)
        {
            return $"held pending (row {pending.Row}): {pending.Why} Today: {held.Message.Split('\n')[0].Trim()}";
        }

        throw new PendingCasePasses(
            $"pending case '{pending.Case}' on '{pending.Door}' passes: remove its line from {Relative(pending.File)} (row {pending.Row} held it).");
    }

    /// <summary>The second rule: each line names a case the suite runs for its provider, on a door it runs that case on, or <c>*</c>.</summary>
    public static IReadOnlyList<string> Unmatched(IReadOnlyList<PendingLine> lines, IReadOnlyCollection<(string Case, string Door)> runs) =>
    [
        .. lines
            .Where(line => !runs.Any(run => run.Case == line.Case && (line.Door == Conformance.EveryDoor || run.Door == line.Door)))
            .Select(line =>
                $"{Relative(line.File)}: '{line.Case}' on '{line.Door}' names no case the suite runs for this provider: name one it runs, or remove the line"),
    ];

    /// <summary>A file's text read by the one layout, each break of it a problem naming the line.</summary>
    public static PendingRead Parse(string text, string file)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException error)
        {
            return new([], [$"{Relative(file)}: not JSON ({error.Message})"]);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new([], [$"{Relative(file)}: the file is an array of lines, never {document.RootElement.ValueKind}"]);
            }

            var lines = new List<PendingLine>();
            var problems = new List<string>();
            var at = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                at++;
                var where = $"{Relative(file)}, line {at}";
                if (element.ValueKind != JsonValueKind.Object)
                {
                    problems.Add($"{where}: a line is an object of {string.Join(", ", Fields)}");
                    continue;
                }

                var extra = element.EnumerateObject().Select(property => property.Name).Where(name => !Fields.Contains(name)).ToList();
                problems.AddRange(extra.Select(name => $"{where}: '{name}' is no field of the layout ({string.Join(", ", Fields)})"));
                var missing = Fields.Where(name => Field(element, name) is null).ToList();
                problems.AddRange(missing.Select(name => $"{where}: '{name}' is missing or blank"));
                if (extra.Count > 0 || missing.Count > 0) continue;

                var (name, door, row, why) = (Field(element, "case")!, Field(element, "door")!, Field(element, "row")!, Field(element, "why")!);
                if (!Rows.Contains(row))
                {
                    problems.Add($"{where}: row '{row}' is no row of the design's §11 a case can wait for ({string.Join(", ", Rows)})");
                }
                else if (!OneSentence(why))
                {
                    problems.Add($"{where}: the why is one sentence ending in a full stop: {why}");
                }
                else if (lines.Any(line => line.Case == name && (line.Door == door || line.Door == Conformance.EveryDoor || door == Conformance.EveryDoor)))
                {
                    problems.Add($"{where}: '{name}' on '{door}' is held twice");
                }
                else
                {
                    lines.Add(new PendingLine(name, door, row, why, file));
                }
            }

            return new(lines, problems);
        }
    }

    /// <summary>
    /// What breaks the layout across the suite: a provider it runs with no file or with two, a file out of shape, a fixture's file for a
    /// provider nobody runs, and a folder's file beside no <c>provider.json</c>.
    /// </summary>
    public static IReadOnlyList<string> Problems(IReadOnlyCollection<string> providers)
    {
        var problems = new List<string>();
        var folders = Folders();
        foreach (var provider in providers)
        {
            var fixture = Path.Combine(FixturesFolder, provider, FileName);
            if (folders.TryGetValue(provider, out var folder) && File.Exists(fixture))
            {
                problems.Add(
                    $"{provider}: its pending cases are in {Relative(Path.Combine(folder, FileName))} and {Relative(fixture)}: a provider with a folder keeps them beside its provider.json alone");
            }

            var file = FileOf(provider);
            if (!File.Exists(file))
            {
                problems.Add($"{provider}: no {FileName} at {Relative(file)}: each provider brings its own, [] where it passes every case");
                continue;
            }

            problems.AddRange(Parse(File.ReadAllText(file), file).Problems);
        }

        if (Directory.Exists(FixturesFolder))
        {
            foreach (var file in Directory.EnumerateFiles(FixturesFolder, FileName, SearchOption.AllDirectories))
            {
                var holder = Path.GetDirectoryName(file)!;
                if (!string.Equals(Path.GetDirectoryName(holder), FixturesFolder, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"{Relative(file)}: a provider's file is fixtures/providers/<id>/{FileName}, one folder deep");
                }
                else if (!providers.Contains(Path.GetFileName(holder), StringComparer.OrdinalIgnoreCase))
                {
                    problems.Add($"{Relative(file)}: names provider '{Path.GetFileName(holder)}', which the suite runs nowhere");
                }
            }
        }

        foreach (var file in Directory.EnumerateFiles(ProvidersFolder, FileName, SearchOption.AllDirectories))
        {
            if (!File.Exists(Path.Combine(Path.GetDirectoryName(file)!, "provider.json")))
            {
                problems.Add($"{Relative(file)}: stands beside no provider.json, so no provider is its");
            }
        }

        return problems;
    }

    /// <summary>A path as the repository names it, or the name given where it is none.</summary>
    public static string Relative(string file) =>
        Path.IsPathRooted(file) ? Path.GetRelativePath(WorkspaceRoot.Folder, file).Replace('\\', '/') : file;

    private static string? Field(JsonElement line, string name) =>
        line.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { } text
        && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    /// <summary>One sentence: it ends in a full stop and holds no other sentence's end, nor a line break.</summary>
    private static bool OneSentence(string why) =>
        why.EndsWith('.') && !why.Contains('\n') && !why[..^1].Contains(". ", StringComparison.Ordinal);
}
