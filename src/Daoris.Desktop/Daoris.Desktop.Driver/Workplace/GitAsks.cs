using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One git go-ahead (GITAUTH1f, the git-auth design §6.6): a door holding an act whose class is <c>ask</c>.</summary>
/// <param name="Id">Thirty-two lower-case hex digits.</param>
/// <param name="Repository">The repository the act's working folder belongs to, where known.</param>
/// <param name="Workspace">Its workspace, where known.</param>
/// <param name="Remote">The remote's name, where git handed it.</param>
/// <param name="Host">The host the act reaches.</param>
/// <param name="Class"><c>read</c> or <c>write</c>.</param>
/// <param name="Session">The session whose marker the act carries, where it carries one.</param>
/// <param name="Key">The act's address as the judge compares it (<see cref="GitAddressKeys"/>).</param>
/// <param name="At">When the door raised it.</param>
/// <param name="Gone">Whether the door that held the act has gone (git's caller stopped it): an answer is then kept as a grant.</param>
public sealed record GitAsk(
    string Id, string? Repository, string? Workspace, string? Remote, string? Host, string Class, string? Session, string? Key, DateTimeOffset At, bool Gone);

/// <summary>
/// The git go-ahead (GITAUTH1f; the git-auth design §6.6), as files in the home's <c>run/git/asks/</c>: a door holding an act writes
/// <c>{id}.json</c> and waits for <c>{id}.answer</c> up to a held call's bound; the person's <c>git answer</c> (or the window's press)
/// writes it. Where the door has gone when the person answers, an <b>allow</b> is kept as <c>{id}.grant</c> for the same act (the
/// session, the address, the class) until the bound, and the identical retry takes it once. A go-ahead is never shared with another
/// session, act or ask. Holds no secret: an address's key is host and path.
/// </summary>
/// <remarks>The base library alone: the doors compile it in.</remarks>
public static class GitAsks
{
    /// <summary>How long a door waits, and a grant stands: a held call's bound.</summary>
    public static readonly TimeSpan Bound = GitGrants.Bound;

    private static string Folder(string home) => DaorisHome.Entry(home, HomeEntry.GitAsks);

    private static bool Valid(string id) => id.Length == 32 && id.All(char.IsAsciiHexDigitLower);

    /// <summary>Raise a go-ahead; its id.</summary>
    public static string Raise(string home, string? repository, string? workspace, string? remote, string? host, string cls, string? session, string? key, DateTimeOffset now)
    {
        var id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var folder = Folder(home);
        Directory.CreateDirectory(folder);
        var record = new JsonObject
        {
            ["repository"] = repository, ["workspace"] = workspace, ["remote"] = remote, ["host"] = host, ["class"] = cls,
            ["session"] = session, ["key"] = key, ["at"] = now.ToString("O"), ["pid"] = Environment.ProcessId,
        };
        Put(Path.Combine(folder, id + ".json"), record.ToJsonString());
        return id;
    }

    /// <summary>
    /// Hold the act: raise a go-ahead, and wait until it is answered or the bound passes. True only on an allow; the go-ahead's files
    /// are gone either way. A grant kept from an earlier, late answer for the same act is taken first (once).
    /// </summary>
    public static bool Hold(
        string home, string? repository, string? workspace, string? remote, string? host, string cls, string? session, string? key,
        Func<DateTimeOffset> clock, Action<TimeSpan> sleep)
    {
        if (TakeGrant(home, session, key, cls, clock())) return true;
        var started = clock();
        var id = Raise(home, repository, workspace, remote, host, cls, session, key, started);
        try
        {
            while (clock() - started < Bound)
            {
                if (Answered(home, id) is { } allow) return allow;
                sleep(TimeSpan.FromMilliseconds(300));
            }

            return false;
        }
        finally
        {
            Remove(home, id);
        }
    }

    /// <summary>The answer written for <paramref name="id"/>: true allow, false refuse, null none yet.</summary>
    public static bool? Answered(string home, string id)
    {
        if (!Valid(id)) return null;
        try
        {
            var path = Path.Combine(Folder(home), id + ".answer");
            if (!File.Exists(path)) return null;
            return AtomicFile.ReadText(path).Trim() == "allow";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return null;
        }
    }

    /// <summary>The go-aheads there are, oldest first; those past the bound are removed.</summary>
    public static IReadOnlyList<GitAsk> List(string home, DateTimeOffset now)
    {
        var asks = new List<GitAsk>();
        try
        {
            if (!Directory.Exists(Folder(home))) return asks;
            foreach (var file in Directory.EnumerateFiles(Folder(home), "*.json"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                if (!Valid(id) || File.Exists(Path.ChangeExtension(file, ".answer"))) continue;
                if (JsonNode.Parse(AtomicFile.ReadText(file)) is not JsonObject record || !DateTimeOffset.TryParse((string?)record["at"], out var at)) continue;
                if (now - at >= Bound)
                {
                    Remove(home, id);
                    continue;
                }

                asks.Add(new GitAsk(
                    id, (string?)record["repository"], (string?)record["workspace"], (string?)record["remote"], (string?)record["host"],
                    (string?)record["class"] ?? "write", (string?)record["session"], (string?)record["key"], at, Gone: !Alive((int?)record["pid"])));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or DriverException)
        {
        }

        return [.. asks.OrderBy(each => each.At)];
    }

    /// <summary>
    /// Answer a go-ahead (<c>git answer</c>, the window's press): the answer is written for a door that waits; for one that has gone an
    /// allow is kept as a grant for the same act, and a refuse is dropped.
    /// </summary>
    /// <returns>Whether a door was still waiting for it.</returns>
    /// <exception cref="DriverException">There is no such go-ahead.</exception>
    public static bool Answer(string home, string id, bool allow, DateTimeOffset now)
    {
        var ask = List(home, now).FirstOrDefault(each => each.Id == id)
            ?? throw new DriverException($"git: there is no git go-ahead {id} waiting (no-ask)");
        if (!ask.Gone)
        {
            Put(Path.Combine(Folder(home), id + ".answer"), allow ? "allow" : "refuse");
            return true;
        }

        if (allow && ask.Key is not null)
        {
            var grant = new JsonObject { ["session"] = ask.Session, ["key"] = ask.Key, ["class"] = ask.Class, ["until"] = (now + Bound).ToString("O") };
            Put(Path.Combine(Folder(home), id + ".grant"), grant.ToJsonString());
        }

        Remove(home, id, keepGrant: true);
        return false;
    }

    /// <summary>Take the grant a late allow kept for this session's act on this address, once; true where one stands.</summary>
    public static bool TakeGrant(string home, string? session, string? key, string cls, DateTimeOffset now)
    {
        if (key is null) return false;
        try
        {
            if (!Directory.Exists(Folder(home))) return false;
            foreach (var file in Directory.EnumerateFiles(Folder(home), "*.grant"))
            {
                if (JsonNode.Parse(AtomicFile.ReadText(file)) is not JsonObject record) continue;
                if ((string?)record["session"] != session || !string.Equals((string?)record["key"], key, StringComparison.OrdinalIgnoreCase)
                    || (string?)record["class"] != cls) continue;
                var taken = file + ".taken";
                File.Move(file, taken);
                File.Delete(taken);
                if (DateTimeOffset.TryParse((string?)record["until"], out var until) && now <= until) return true;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or DriverException)
        {
        }

        return false;
    }

    private static bool Alive(int? pid)
    {
        if (pid is not { } id) return false;
        try
        {
            using var process = Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    private static void Remove(string home, string id, bool keepGrant = false)
    {
        if (!Valid(id)) return;
        foreach (var extension in keepGrant ? new[] { ".json", ".answer" } : [".json", ".answer", ".grant"])
        {
            try
            {
                File.Delete(Path.Combine(Folder(home), id + extension));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
            {
            }
        }
    }

    private static void Put(string path, string text)
    {
        File.WriteAllText(path + ".writing", text);
        File.Move(path + ".writing", path, overwrite: true);
    }
}
