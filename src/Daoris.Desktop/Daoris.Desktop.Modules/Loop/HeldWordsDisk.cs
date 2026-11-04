using System.Text.Json;
using Daoris.Driver;
using static Daoris.Driver.Json;

namespace Daoris.Desktop;

/// <summary>
/// The held words' small file under the home (MSG1d4): the words held while a session winds up, each in the order said with its
/// session, text, file names and door, and, where it had them, its producer's key, level and display choice (FOUNDATION1b),
/// all additive by name so an older shell reads the text, files and door as before.
/// </summary>
internal static class HeldWordsDisk
{
    /// <summary>
    /// Every word held, written whole beside and renamed, LF, as every file under the home is; the file removed once nothing is
    /// held. A write that fails costs only the words' surviving a restart: they are still held in the process.
    /// </summary>
    public static void Save(string path, IReadOnlyList<(string Session, HeldSay Word)> held)
    {
        try
        {
            if (held.Count == 0)
            {
                if (File.Exists(path)) File.Delete(path);
                HomeWrites.Mine(path);
                return;
            }

            JsonFile.Write(path, writer =>
            {
                writer.WriteStartObject();
                writer.WriteStartArray("held");
                foreach (var (session, word) in held)
                {
                    writer.WriteStartObject();
                    writer.WriteString("session", session);
                    writer.WriteString("text", word.Text);
                    writer.WriteStartArray("files");
                    foreach (var name in word.Files) writer.WriteStringValue(name);
                    writer.WriteEndArray();
                    writer.WriteString("door", word.Door);
                    if (word.Key is { Length: > 0 }) writer.WriteString("key", word.Key);
                    writer.WriteString("level", MessageLevels.Spelled(word.Level));
                    if (word.Choice is { Length: > 0 }) writer.WriteString("choice", word.Choice);
                    if (word.Question is { Length: > 0 }) writer.WriteString("question", word.Question);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
            // The loop's own write: its watch of the file hears it and wakes no look (FABRIC-EVENT4).
            HomeWrites.Mine(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Still held in this process; only a restart before the record keeps them would lose them.
        }
    }

    /// <summary>The held words in a file <see cref="Save"/> wrote, in order; an entry missing its session, text or door is skipped.</summary>
    public static List<(string Session, HeldSay Word)> Read(string path)
    {
        if (!File.Exists(path)) return [];
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("held", out var held) || held.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var words = new List<(string Session, HeldSay Word)>();
        foreach (var entry in held.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (Text(entry, "session") is not { Length: > 0 } session || Text(entry, "text") is not { } text
                || Text(entry, "door") is not { Length: > 0 } door)
            {
                continue;
            }

            IReadOnlyList<string> files = entry.TryGetProperty("files", out var named) && named.ValueKind == JsonValueKind.Array
                ? [.. named.EnumerateArray().Where(name => name.ValueKind == JsonValueKind.String).Select(name => name.GetString()!)]
                : [];
            words.Add((session, new HeldSay(
                text, files, door, Choice: Text(entry, "choice") is { Length: > 0 } choice ? choice : null,
                Key: Text(entry, "key") is { Length: > 0 } key ? key : null,
                Level: MessageLevels.Read(Text(entry, "level")) ?? MessageLevel.Urgent,
                Question: Text(entry, "question") is { Length: > 0 } asked ? asked : null)));
        }

        return words;
    }
}
