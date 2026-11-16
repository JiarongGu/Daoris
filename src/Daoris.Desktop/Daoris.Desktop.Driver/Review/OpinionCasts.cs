using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The journal of each act a line level's holder made on a second opinion's vacancy: <c>&lt;home&gt;/opinions/gates/&lt;chain&gt;.casts.jsonl</c>,
/// beside the gate's own record, one line per act, appended and never rewritten. This machine's own, as the gate is.
/// </summary>
/// <remarks>A line that does not read is passed over; the acts around it still read.</remarks>
public sealed class OpinionCasts(string home)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    // One writer at a time in this process: the look and the decision door may write at once.
    private static readonly object Gate = new();

    /// <summary>Where a chain's acts are kept; null for a name that is no plain id.</summary>
    public string? PathOf(string chain) =>
        SafeNames.IsPlainId(chain) ? DaorisHome.Entry(home, HomeEntry.OpinionGates, $"{chain}.casts.jsonl") : null;

    /// <summary>An act on the chain's work in a repository, kept after every one before it.</summary>
    public void Keep(string chain, string repository, OpinionCast cast)
    {
        if (PathOf(chain) is not { } path) throw new DriverException($"`{chain}` names no chain this machine keeps a gate for.");
        var line = JsonSerializer.Serialize(new Kept(repository, cast.At, cast.Session, cast.Actor, cast.Role, cast.Act, cast.Agent, cast.Why), Options);
        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, line + "\n", new UTF8Encoding(false));
        }
    }

    /// <summary>The acts on one session's vacancy in a repository, in the order kept; none where nothing reads.</summary>
    public IReadOnlyList<OpinionCast> Read(string chain, string repository, string session)
    {
        if (PathOf(chain) is not { } path || !File.Exists(path)) return [];
        string[] lines;
        lock (Gate) lines = File.ReadAllLines(path);
        return [.. lines.Select(Parse).OfType<Kept>()
            .Where(kept => string.Equals(kept.Repository, repository, StringComparison.OrdinalIgnoreCase) && string.Equals(kept.Session, session, StringComparison.OrdinalIgnoreCase))
            .Select(kept => new OpinionCast(kept.At, kept.Session!, kept.Actor!, kept.Role!, kept.Act!, kept.Agent, kept.Why ?? ""))];
    }

    private static Kept? Parse(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<Kept>(line, Options) is { Repository: not null, Session: not null, Actor: not null, Role: not null, Act: not null } kept ? kept : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record Kept(string? Repository, DateTimeOffset At, string? Session, string? Actor, string? Role, string? Act, string? Agent, string? Why);
}
