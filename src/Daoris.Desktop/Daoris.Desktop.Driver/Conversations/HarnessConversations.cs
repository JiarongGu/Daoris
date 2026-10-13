using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The harness's own conversation a session ran, as its wire named it, and the adapter that opened it (ANSWER1a).</summary>
/// <param name="Adapter">The Daoris adapter whose door opened it: a conversation is only that door's to resume.</param>
/// <param name="Conversation">The id the harness gave it: ACP's <c>sessionId</c>, or the native door's <c>session_id</c>.</param>
public sealed record HarnessConversation(string Adapter, string Conversation);

/// <summary>
/// The harness's own conversation id per session (ANSWER1a, D131 §1): <c>&lt;home&gt;/sessions/&lt;session&gt;.harness.json</c>,
/// beside the session's transcript, kept the moment the wire says it and read back when the person answers a park.
/// </summary>
/// <remarks>
/// <para><b>Kept by Daoris, never read from the agent's own home.</b> The harness keeps its conversation inside the
/// account's configuration home; reading there is a read inside an account's directory (D66 §3), which D125 rejected.
/// The id is the one fact a resume needs, and the wire hands it over at the start of every session.</para>
///
/// <para><b>This machine's, never the record's.</b> A conversation id is a harness detail of one machine's checkout, like
/// the transcript beside it, and a teammate can do nothing with it.</para>
///
/// <para><b>A file that does not read is no conversation</b>: the answer falls back to a carry-on, saying Daoris kept no
/// id (<see cref="ContinueWhy.Unkept"/>), and never throws.</para>
/// </remarks>
public sealed class HarnessConversations(string home)
{
    public const string Suffix = ".harness.json";

    /// <summary>Where a session's conversation id is kept.</summary>
    public string PathOf(string session) => Path.Combine(home, "sessions", session + Suffix);

    /// <summary>
    /// Keep what the wire named this session's conversation, replacing what was kept. An id that is not a session's, or a
    /// blank conversation, keeps nothing; a write that fails is lost, which costs a resume and never the session.
    /// </summary>
    public void Keep(string session, string adapter, string conversation)
    {
        if (!SessionEvents.IsId(session) || string.IsNullOrWhiteSpace(conversation) || string.IsNullOrWhiteSpace(adapter)) return;

        try
        {
            var path = PathOf(session);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteString("adapter", adapter);
                writer.WriteString("conversation", conversation);
                writer.WriteEndObject();
            }

            // LF on every platform, as every file under the home is written: the writer's own line end is the platform's.
            var text = Encoding.UTF8.GetString(buffer.ToArray()).Replace("\r\n", "\n") + "\n";
            AtomicFile.WriteText(path, text);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Lost: the next answer to this session carries on in a new one, saying no id was kept.
        }
    }

    /// <summary>
    /// What a door tells the id its harness named to, for this session's record (MSG1c, D137 §4.2): a chat keeps its
    /// conversation as a quest's session does, on the protocol door from <c>session/new</c>'s answer and on the native door
    /// from its <c>init</c> line, so an ended chat the person writes to goes on in it.
    /// </summary>
    public Action<string> Keeping(string session, string adapter) => conversation => Keep(session, adapter, conversation);

    /// <summary>What was kept for this session, or null for nothing, a file that does not read, or an id that is not one.</summary>
    public HarnessConversation? Read(string session)
    {
        if (!SessionEvents.IsId(session)) return null;

        try
        {
            var path = PathOf(session);
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            return Text(root, "adapter") is { Length: > 0 } adapter && Text(root, "conversation") is { Length: > 0 } conversation
                ? new HarnessConversation(adapter, conversation)
                : null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;
}
