using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The harness's conversation id this machine keeps beside a session's transcript, <c>&lt;home&gt;/sessions/&lt;session&gt;.harness.json</c>
/// (ANSWER1a), read without trusting its shape and written whole, atomically, as BOM-less UTF-8 with LF. The go-on files' reader and writer
/// (<c>GoOnFiles</c>) held it until the store that keeps it, <see cref="HarnessConversations"/>, moved to Participants with its format.
/// </summary>
/// <remarks>A file that does not read is nothing kept; a write that fails throws.</remarks>
internal static class HarnessConversationFiles
{
    /// <summary>A conversation id written to <paramref name="path"/>, its folder made where it is not.</summary>
    public static void Write(string path, string adapter, string conversation)
    {
        JsonFile.Write(path, writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("adapter", adapter);
            writer.WriteString("conversation", conversation);
            writer.WriteEndObject();
        });
    }

    /// <summary>The conversation id at <paramref name="path"/>, or null for none or a file that does not read.</summary>
    public static HarnessConversation? Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            return Json.Text(root, "adapter")?.Trim() is { Length: > 0 } adapter
                   && Json.Text(root, "conversation")?.Trim() is { Length: > 0 } conversation
                ? new HarnessConversation(adapter, conversation)
                : null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
