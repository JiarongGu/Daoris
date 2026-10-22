using System.Globalization;
using System.Text;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// A hand-over's two files under the home (MIXAGENT1a-i, MIXAGENT1a-ii): a conversation's box,
/// <c>&lt;home&gt;/sessions/&lt;session&gt;.hand-over.json</c> (<see cref="HandOverBox"/>), and a quest held on another agent,
/// <c>&lt;home&gt;/handed/&lt;quest&gt;.json</c> (<see cref="HandedQuests"/>), read without trusting their shape and written whole,
/// atomically, as BOM-less UTF-8 with LF.
/// </summary>
/// <remarks>
/// STRUCT3h, D166's note: the stores decide what is kept and by which name; this reads and writes
/// it. A file that does not read is nothing kept; a write that fails throws, as it did.
/// </remarks>
internal static class HandOverFiles
{
    /// <summary>A box written to <paramref name="path"/>, its folder made where it is not.</summary>
    public static void WriteBox(string path, HandOverRequest request)
    {
        // LF on every platform, as every file under the home is written (JsonFile).
        JsonFile.Write(path, writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("agent", request.Agent);
            if (request.Account is { } account) writer.WriteString("account", account); else writer.WriteNull("account");
            writer.WriteString("why", request.Why);
            writer.WriteString("askedBy", request.AskedBy);
            writer.WriteString("at", Moments.Write(request.At));
            writer.WriteEndObject();
        });
    }

    /// <summary>The box at <paramref name="path"/>, or null for none or a file that does not read.</summary>
    public static HandOverRequest? ReadBox(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || Text(root, "agent") is not { Length: > 0 } agent
                || Text(root, "why") is not { } why || !HandOverWhy.Known(why)
                || Text(root, "askedBy") is not { } by || !HandOverBy.Known(by)
                || Moments.Read(root, "at") is not { } at)
            {
                return null;
            }

            return new HandOverRequest(agent, Text(root, "account") is { Length: > 0 } account ? account : null, why, by, at);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>A quest as handed, written to <paramref name="path"/>, its folder made where it is not.</summary>
    public static void WriteHanded(string path, HandedQuest handed)
    {
        JsonFile.Write(path, writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("agent", handed.Agent);
            if (handed.Account is { } account) writer.WriteString("account", account); else writer.WriteNull("account");
            writer.WriteString("from", handed.From);
            writer.WriteString("why", handed.Why);
            writer.WriteString("askedBy", handed.AskedBy);
            writer.WriteString("at", Moments.Write(handed.At));
            // MIXAGENT1c: the outgoing agent's note, only where it left one, so every file from before keeps its shape.
            if (handed.Note is { } note) writer.WriteString("note", note);
            writer.WriteEndObject();
        });
    }

    /// <summary>The quest as handed at <paramref name="path"/>, or null for none or a file that does not read.</summary>
    public static HandedQuest? ReadHanded(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || Text(root, "agent") is not { Length: > 0 } agent
                || Text(root, "from") is not { Length: > 0 } from
                || Text(root, "why") is not { } why || !HandOverWhy.Known(why)
                || Text(root, "askedBy") is not { } by || !HandOverBy.Known(by)
                || Moments.Read(root, "at") is not { } at)
            {
                return null;
            }

            return new HandedQuest(agent, Text(root, "account") is { Length: > 0 } account ? account : null, from, why, by, at)
            {
                Note = Text(root, "note") is { Length: > 0 } note ? note : null,
            };
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The names of the files in <paramref name="folder"/> that <paramref name="pattern"/> matches; none where it is missing or does not list.</summary>
    public static IReadOnlyList<string> Leaves(string folder, string pattern)
    {
        if (!Directory.Exists(folder)) return [];
        try
        {
            return [.. Directory.EnumerateFiles(folder, pattern).Select(file => Path.GetFileName(file))];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>The file at <paramref name="path"/> gone; nothing to take, or a file that will not go, is no failure.</summary>
    public static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left behind: its use case judges it again where it is read.
        }
    }
}
