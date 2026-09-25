using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Where this machine's remotes are — machine-local configuration, never per-repository (D47 §9,
/// D48 §5): the home's `remotes.json` (D63), a MAP of workspace → `{ "url": "...", "key": "dk_..." }`, with
/// the environment overriding.
/// </summary>
/// <remarks>
/// <para>A map rather than a single remote because one shared deployment serves one workspace (D48 §5),
/// and a machine may hold repositories from several circles. The workspace NAME keys it; whether a
/// given repository may feed at all stays its own manifest's declaration — the manifest says MAY, the
/// machine says WHERE.</para>
///
/// <para>The FILE is the contract this component shares with the service's hosts; the driver
/// deliberately reads it with its own code, because it links against no service assembly — the
/// service-side twin is `RemoteConfig`, and each carries a test table the other must keep matching.
/// The twins move TOGETHER: a rule enforced in one only is a rule the other will contradict.</para>
/// </remarks>
public sealed record RemoteTarget(string Url, string Key)
{
    public const string UrlVariable = "DAORIS_REMOTE_URL";
    public const string KeyVariable = "DAORIS_REMOTE_KEY";

    /// <summary>Which workspace the environment pair serves. Absent is <see cref="DefaultWorkspace"/>.</summary>
    public const string WorkspaceVariable = "DAORIS_REMOTE_WORKSPACE";

    public const string PathVariable = "DAORIS_REMOTE_CONFIG";

    /// <summary>
    /// Where a repository lives when nobody has said otherwise — the service's `Workspaces.Default`,
    /// duplicated deliberately: this assembly links against no service code, and the two constants
    /// being the same string is what the twin test tables exist to keep true.
    /// </summary>
    public const string DefaultWorkspace = "default";

    /// <summary>
    /// The map's conventional home, beside the driver's own config under the Daoris home (D63) — and
    /// null where there is none: a machine with no home has no remotes, the documented default anyway.
    /// </summary>
    public static string? DefaultPath => DaorisHome.File("remotes.json");

    /// <summary>A workspace name as it is stored and compared: trimmed, and the default when unstated.</summary>
    public static string Workspace(string? name) =>
        string.IsNullOrWhiteSpace(name) ? DefaultWorkspace : name.Trim();

    /// <summary>This machine's remotes, by workspace. Absence is the default and it is silent (D21).</summary>
    public static IReadOnlyDictionary<string, RemoteTarget> Load() => Load(
        Environment.GetEnvironmentVariable,
        Environment.GetEnvironmentVariable(PathVariable) ?? DefaultPath);

    /// <summary>
    /// The testable shape: the same judgement over injected surroundings.
    /// </summary>
    /// <remarks>
    /// Either environment variable present means the environment IS the answer — for the WHOLE
    /// MACHINE, not one entry of it, and a half-set pair is no remote at all. Never a mix of an env
    /// URL with the file's key, which would quietly aim one machine's key at another's host; and never
    /// a merge, which would let a real map leak into a process that thought it had named its only
    /// remote — the gate's hermetic guard rests on exactly that.
    /// </remarks>
    public static IReadOnlyDictionary<string, RemoteTarget> Load(Func<string, string?> environment, string? path)
    {
        var map = new Dictionary<string, RemoteTarget>(StringComparer.OrdinalIgnoreCase);
        var url = environment(UrlVariable);
        var key = environment(KeyVariable);

        if (!string.IsNullOrWhiteSpace(url) || !string.IsNullOrWhiteSpace(key))
        {
            if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(key))
            {
                map[Workspace(environment(WorkspaceVariable))] = new(url.TrimEnd('/'), key);
            }

            return map;
        }

        if (!File.Exists(path)) return map;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return map;

            foreach (var entry in document.RootElement.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Object) continue;
                var entryUrl = Text(entry.Value, "url");
                var entryKey = Text(entry.Value, "key");
                // An entry missing half its pair is one workspace with no remote, never a machine with
                // none: a typo in one circle must not silently unwire the others.
                if (string.IsNullOrWhiteSpace(entryUrl) || string.IsNullOrWhiteSpace(entryKey)) continue;

                map[Workspace(entry.Name)] = new(entryUrl.TrimEnd('/'), entryKey);
            }
        }
        catch (JsonException)
        {
            return new Dictionary<string, RemoteTarget>(StringComparer.OrdinalIgnoreCase);
        }

        return map;
    }

    /// <summary>
    /// The map as it sits on disk, whatever the environment currently says — what an EDITOR reads.
    /// </summary>
    /// <remarks>
    /// An edit must land in the file even when the environment outranks it (a machine with the env
    /// pair set could otherwise never wire a second workspace); which one is LIVE is a separate
    /// question, and the surface answers it separately. Same judgement as the CLI's `remote` verb.
    /// </remarks>
    public static IReadOnlyDictionary<string, RemoteTarget> LoadFile(string path) =>
        Load(_ => null, path);

    /// <summary>
    /// Write the map back — atomically, beside-then-rename, like every write in this family: a driver
    /// tick may read this file at any moment, and a torn read must never be what it finds.
    /// </summary>
    public static void Save(string path, IReadOnlyDictionary<string, RemoteTarget> remotes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var (workspace, target) in remotes.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                writer.WriteStartObject(workspace);
                writer.WriteString("url", target.Url);
                writer.WriteString("key", target.Key);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        AtomicFile.WriteText(path, System.Text.Encoding.UTF8.GetString(stream.ToArray()) + "\n");
    }

    /// <summary>
    /// A key as it may be SHOWN: the audit prefix a deployment's own `keys list` prints, and nothing
    /// else. Anything too short to have a prefix shows as nothing at all — a redaction that leaks a
    /// short key is worse than printing it, because it reads as safe.
    /// </summary>
    public static string Redact(string key) => key.Length > 11 ? key[..11] + "…" : "…";

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
