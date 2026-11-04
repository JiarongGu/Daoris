using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The runtime's last fresh read of the quests, kept beside the node records as <c>workflows/quests.json</c> (FABRIC-CLOSE1q5), so the
/// terminal, which has no runtime, judges a held close as the page does (<see cref="QuestClosing.Moved"/>). Only what <c>Moved</c> needs is
/// kept: per quest, its current submission's machine and sequence, or null for a quest with none. Never under <c>units/</c>, where any file
/// not a unit's record is reported unreadable. A read left by a closed desktop is kept and used: a submission that moved stays moved (its
/// identity is machine and sequence), so a stale read can only miss a move, which the pass still refuses, never invent one.
/// </summary>
public static class QuestReads
{
    /// <summary>The file, beside <c>units/</c> and <c>closures/</c>.</summary>
    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Workflows, "quests.json");

    /// <summary>Keeps <paramref name="quests"/>' submissions, atomically; a failed write keeps nothing and says nothing.</summary>
    public static void Keep(string home, IReadOnlyDictionary<string, QuestView> quests)
    {
        var path = PathOf(home);
        try
        {
            var body = new System.Text.Json.Nodes.JsonObject();
            foreach (var (id, quest) in quests.OrderBy(each => each.Key, StringComparer.Ordinal))
            {
                body[id] = quest.Submission is { } submission
                    ? new System.Text.Json.Nodes.JsonObject { ["machine"] = submission.Machine, ["sequence"] = submission.Sequence }
                    : null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.WriteText(path, new System.Text.Json.Nodes.JsonObject { ["quests"] = body }.ToJsonString().ReplaceLineEndings("\n"));
            HomeWrites.Mine(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The kept read as stand-ins <see cref="QuestClosing.Moved"/> judges, or null where none is kept or it does not read.</summary>
    public static IReadOnlyDictionary<string, QuestView>? Read(string home)
    {
        try
        {
            if (!File.Exists(PathOf(home))) return null;
            using var document = System.Text.Json.JsonDocument.Parse(AtomicFile.ReadText(PathOf(home)));
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object
                || !document.RootElement.TryGetProperty("quests", out var kept) || kept.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
            var read = new Dictionary<string, QuestView>(StringComparer.Ordinal);
            foreach (var each in kept.EnumerateObject())
            {
                QuestSubmissionView? submission = null;
                if (each.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (!each.Value.TryGetProperty("machine", out var machine) || machine.ValueKind != System.Text.Json.JsonValueKind.String
                        || !each.Value.TryGetProperty("sequence", out var sequence) || !sequence.TryGetInt64(out var number)) return null;
                    submission = new QuestSubmissionView(machine.GetString()!, number, null, null);
                }
                else if (each.Value.ValueKind != System.Text.Json.JsonValueKind.Null) return null;
                read[each.Name] = new QuestView(each.Name, "", "", "", "", "") { Submission = submission };
            }

            return read;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Deletes the kept read, at a disarm; nothing kept is nothing to delete.</summary>
    public static void Delete(string home)
    {
        try
        {
            File.Delete(PathOf(home));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }
}
