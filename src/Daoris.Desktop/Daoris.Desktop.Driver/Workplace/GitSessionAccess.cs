using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A running session's git access as it holds it now (D241 point 7): each class with its value, and <c>session</c> as its source where the person tightened it.</summary>
/// <param name="Session">The session's id.</param>
/// <param name="Repository">The repository it started in, where it had one.</param>
/// <param name="Workspace">Its workspace, where known.</param>
/// <param name="Classes">Read, Write and Dangerous, in the order every door says them.</param>
public sealed record GitSessionRecord(string Session, string? Repository, string? Workspace, IReadOnlyList<GitAccessClass> Classes);

/// <summary>
/// The git access a running session holds (D241 point 7, the git design §3.1 G5): a record in the home's <c>run/git/sessions/{id}.json</c>
/// begun at the session's start with its repository and workspace, and the classes the person has tightened since. What the session
/// holds is what its repository resolves to now, narrowed by those classes; a tightening only narrows, and the doors read it
/// through <see cref="Held"/> for every act the session's marker names. Holds no secret.
/// </summary>
/// <remarks>The base library alone: the doors compile it in (<c>Daoris.Desktop.GitDoor</c>).</remarks>
public static class GitSessionAccess
{
    /// <summary>The source a narrowed class says.</summary>
    public const string FromSession = "session";

    private const string Tightened = "tightened";
    private static readonly TimeSpan Kept = TimeSpan.FromDays(14);

    private static string Folder(string home) => DaorisHome.Entry(home, HomeEntry.GitSessions);

    /// <summary>Whether <paramref name="id"/> is a session's id as a file can carry it.</summary>
    public static bool Valid(string? id) =>
        id is { Length: > 0 and <= 64 } && id.All(each => char.IsAsciiLetterOrDigit(each) || each is '-' or '_');

    private static string PathOf(string home, string id) => Path.Combine(Folder(home), id + ".json");

    /// <summary>Begin a session's record at its start, keeping what a record of the same id already holds; and drop records past their keep.</summary>
    public static void Begin(string home, string id, string? repository, string? workspace)
    {
        if (!Valid(id)) return;
        try
        {
            var folder = Folder(home);
            Directory.CreateDirectory(folder);
            var held = Parse(PathOf(home, id))?["tightened"] as JsonObject;
            Put(PathOf(home, id), repository, workspace, held is null ? new JsonObject() : (JsonObject)held.DeepClone());
            foreach (var old in Directory.EnumerateFiles(folder, "*.json").Where(file => DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > Kept))
            {
                File.Delete(old);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // A record that cannot be kept leaves the session judged by its repository's access alone.
        }
    }

    /// <summary>
    /// The git access a door judges an act of <paramref name="session"/> by: its repository's, narrowed by what the person tightened. A marker that
    /// names no record is judged by the repository's alone.
    /// </summary>
    public static GitAccess Held(string home, string? session, string? repository, string? workspace)
    {
        var resolved = GitAccessKeys.Resolve(home, repository, workspace);
        return Valid(session) && Parse(PathOf(home, session!))?[Tightened] is JsonObject tightened ? Narrowed(resolved, tightened) : resolved;
    }

    /// <summary>The record of <paramref name="session"/> as it holds now; null where none was begun.</summary>
    public static GitSessionRecord? Read(string home, string? session)
    {
        if (!Valid(session) || Parse(PathOf(home, session!)) is not { } record) return null;
        var repository = (string?)record["repository"];
        var workspace = (string?)record["workspace"];
        var tightened = record[Tightened] as JsonObject ?? new JsonObject();
        return new(session!, repository, workspace, Narrowed(GitAccessKeys.Resolve(home, repository, workspace), tightened).Classes);
    }

    /// <summary>Every session's record that is held, oldest name first.</summary>
    public static IReadOnlyList<GitSessionRecord> All(string home)
    {
        try
        {
            var folder = Folder(home);
            return !Directory.Exists(folder)
                ? []
                : [.. Directory.EnumerateFiles(folder, "*.json").Select(file => Read(home, Path.GetFileNameWithoutExtension(file))).OfType<GitSessionRecord>()
                    .OrderBy(each => each.Session, StringComparer.Ordinal)];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return [];
        }
    }

    /// <summary>
    /// Narrow <paramref name="session"/>'s record by <paramref name="set"/>, class by class (<c>read</c>, <c>write</c>, <c>dangerous</c>, each
    /// <c>allow</c>, <c>ask</c> or <c>deny</c>; Dangerous never <c>ask</c>). Every class must be tighter than it is held now: a widening is
    /// the person's to make in git access, never here.
    /// </summary>
    /// <exception cref="DriverException">No record, no class, a class or value not known, or one that does not narrow.</exception>
    public static GitSessionRecord Tighten(string home, string session, IReadOnlyDictionary<string, string> set)
    {
        var held = Read(home, session) ?? throw new DriverException($"no running session {session} holds git access here.");
        if (set.Count == 0) throw new DriverException("name a class to tighten: read, write or dangerous.");
        // A copy: the held node belongs to the record it was read from, and the record written is a new one.
        var tightened = Parse(PathOf(home, session))?[Tightened]?.DeepClone() as JsonObject ?? new JsonObject();
        foreach (var (spelled, value) in set)
        {
            var name = GitAccessKeys.ClassNamed(spelled) ?? throw new DriverException($"`{spelled}` is not a git access class: read, write or dangerous.");
            var named = GitAccessKeys.ValueNamed(value) ?? throw new DriverException($"`{value}` is not a value: allow, ask or deny.");
            if (name == GitAccessKeys.Dangerous && named == GitAccessKeys.Ask) throw new DriverException("dangerous is allow or deny, never ask.");
            var now = held.Classes.First(each => each.Class == name).Value;
            if (GitAccessKeys.Looseness(named) <= GitAccessKeys.Looseness(now))
                throw new DriverException($"{name} is {now} for this session already: git access here only tightens.");
            tightened[name] = named;
        }

        Put(PathOf(home, session), held.Repository, held.Workspace, tightened);
        return Read(home, session)!;
    }

    private static GitAccess Narrowed(GitAccess resolved, JsonObject tightened) => new([
        .. resolved.Classes.Select(each =>
            (string?)tightened[each.Class] is { } value && GitAccessKeys.ValueNamed(value) is { } named && GitAccessKeys.Looseness(named) > GitAccessKeys.Looseness(each.Value)
                ? new GitAccessClass(each.Class, named, FromSession)
                : each),
    ]);

    private static JsonObject? Parse(string path)
    {
        try
        {
            return File.Exists(path) ? JsonNode.Parse(AtomicFile.ReadText(path)) as JsonObject : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or DriverException)
        {
            return null;
        }
    }

    private static void Put(string path, string? repository, string? workspace, JsonObject tightened)
    {
        var record = new JsonObject { ["repository"] = repository, ["workspace"] = workspace, [Tightened] = tightened };
        File.WriteAllText(path + ".writing", record.ToJsonString());
        File.Move(path + ".writing", path, overwrite: true);
    }
}
