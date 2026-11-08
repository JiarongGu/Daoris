using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The asks a start left waiting, the home's <c>data/files-asks.json</c>: one entry per act an <c>ask</c> of the person's holds, kept until
/// a start finds it no longer held or a supervisor's allow for it stands. Every change reads, decides and writes under the file's own
/// lock. A file that does not read is empty: each ask is noted again at the next start that meets it.
/// </summary>
public static class FileAsks
{
    internal const string LockName = "files-asks";

    private static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.FilesAsks);

    /// <summary>Every ask waiting or being decided, in the order they were met.</summary>
    public static IReadOnlyList<FileAsk> Read(string home) => Load(home);

    /// <summary>The ask noted, unless one for the same act already waits, whose start and decision are kept; the ask as stored.</summary>
    public static FileAsk Note(string home, FileAsk ask) => Change(home, asks =>
    {
        if (asks.FirstOrDefault(each => each.Is(ask.Repository, ask.What, ask.Text)) is { } held)
        {
            if (held.Person == ask.Person && (ask.Others.Count == 0 || ask.Others.SequenceEqual(held.Others))) return held;
            var kept = held with { Person = ask.Person, Others = ask.Others.Count > 0 ? ask.Others : held.Others };
            asks[asks.IndexOf(held)] = kept;
            return kept;
        }

        asks.Add(ask);
        return ask;
    });

    /// <summary>The ask for this act dropped, once a start found it held no more; it is kept where a supervisor's allow for it is being written.</summary>
    public static void Close(string home, string repository, string what, string text)
    {
        if (File.Exists(PathOf(home))) Drop(home, repository, what, text);
    }

    private static void Drop(string home, string repository, string what, string text) => Change(home, asks =>
    {
        asks.RemoveAll(each => each.Is(repository, what, text) && each.Decision is null);
        return 0;
    });

    /// <summary>
    /// The ask marked as being decided by <paramref name="role"/> under <paramref name="decision"/>, if it still waits at <paramref name="start"/>
    /// with no decision on it; null where it does not.
    /// </summary>
    public static FileAsk? Decide(string home, string repository, string start, string what, string text, string role, string decision) => Change(home, asks =>
    {
        if (asks.FirstOrDefault(each => each.Is(repository, what, text) && each.Start == start && each.Decision is null) is not { } held) return null;
        var decided = held with { Decision = decision, Role = role };
        asks[asks.IndexOf(held)] = decided;
        return decided;
    });

    /// <summary>The ask of <paramref name="decision"/> taken off the file: its allow stands, or it did not and the ask waits again.</summary>
    public static void Settle(string home, string decision, bool made) => Change(home, asks =>
    {
        foreach (var held in asks.Where(each => each.Decision == decision).ToList())
        {
            if (made) asks.Remove(held);
            else asks[asks.IndexOf(held)] = held with { Decision = null, Role = null };
        }

        return 0;
    });

    private static T Change<T>(string home, Func<List<FileAsk>, T> change)
    {
        using var held = RecordLock.Take(RecordLock.PathOf(home, LockName));
        var asks = Load(home);
        var list = asks.ToList();
        var result = change(list);
        if (list.SequenceEqual(asks)) return result;
        var path = PathOf(home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteText(path, Write(list).ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n");
        return result;
    }

    private static List<FileAsk> Load(string home)
    {
        var path = PathOf(home);
        if (!File.Exists(path)) return [];
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path).TrimStart('﻿'));
            if (!document.RootElement.TryGetProperty("asks", out var listed) || listed.ValueKind != JsonValueKind.Array) return [];
            var asks = new List<FileAsk>();
            foreach (var each in listed.EnumerateArray())
            {
                if (each.ValueKind != JsonValueKind.Object || Json.Text(each, "repository") is not { } repository || Json.Text(each, "start") is not { } start
                    || Json.Text(each, "what") is not { } what || Json.Text(each, "text") is not { } text)
                {
                    continue;
                }

                asks.Add(new FileAsk(repository, start, what, text)
                {
                    Others = each.TryGetProperty("others", out var others) && others.ValueKind == JsonValueKind.Array
                        ? [.. others.EnumerateArray().Where(one => one.ValueKind == JsonValueKind.String).Select(one => one.GetString()!)]
                        : [],
                    Person = each.TryGetProperty("person", out var person) && person.ValueKind == JsonValueKind.True,
                    Decision = Json.Text(each, "decision"),
                    Role = Json.Text(each, "role"),
                });
            }

            return asks;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static JsonObject Write(IReadOnlyList<FileAsk> asks) => new()
    {
        ["asks"] = new JsonArray([.. asks.Select(ask =>
        {
            var written = new JsonObject { ["repository"] = ask.Repository, ["start"] = ask.Start, ["what"] = ask.What, ["text"] = ask.Text };
            if (ask.Others.Count > 0) written["others"] = new JsonArray([.. ask.Others.Select(other => (JsonNode)JsonValue.Create(other)!)]);
            if (ask.Person) written["person"] = true;
            if (ask.Decision is not null) (written["decision"], written["role"]) = (ask.Decision, ask.Role);
            return (JsonNode)written;
        })]),
    };
}
