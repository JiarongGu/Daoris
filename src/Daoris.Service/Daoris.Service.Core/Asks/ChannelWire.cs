using System.Text.Json;
using static Daoris.Knowledge.JsonFields;

namespace Daoris.Knowledge;

/// <param name="Messages">The team's messages, each keyed `origin/id` and carrying its origin.</param>
/// <param name="Through">The last number the page covers: where the fetching machine's cursor moves.</param>
/// <param name="More">Whether another page follows.</param>
public sealed record ChannelFetch(IReadOnlyList<ChannelMessage> Messages, long Through, bool More);

/// <summary>
/// An ask's channel messages on the wire (SESSONE2d; D167 point 8), written once and read by both sides: the remote's two channel
/// doors and the machine's client that speaks to them. Hand-written, for the reason every JSON shape in Core is.
/// </summary>
/// <remarks>
/// What travels is what the channel keeps: the words, who said them (a repository and an adapter name, never a model; a session, by its
/// id), where they were said (the ask) and what they answer. What has no field here does not travel: a machine path is cleaned out of the
/// words before they are written (<see cref="SessionNote.ForAnotherMachine"/>), and Daoris's lines are not stored, so they are not here.
/// The optional fields are said only where there is something to say, so a feed from before one of them reads as it always did.
/// </remarks>
public static class ChannelWire
{
    /// <summary>A feed of this machine's own messages, as the remote's feed door reads it.</summary>
    public static string Feed(IReadOnlyList<ChannelMessage> messages) => Written(writer =>
    {
        writer.WriteStartObject();
        writer.WriteStartArray("messages");
        foreach (var message in messages) Write(writer, message, origin: null);
        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    /// <summary>A feed read back at the door; null when it is not one.</summary>
    public static IReadOnlyList<ChannelMessage>? ReadFeed(string json)
    {
        if (ParseObject(json) is not { } root) return null;
        var messages = new List<ChannelMessage>();
        foreach (var item in Items(root, "messages"))
        {
            if (Read(item) is not { } message) return null;
            messages.Add(message);
        }

        return messages;
    }

    /// <summary>A page of the team's messages the remote holds, each carrying its origin.</summary>
    public static string Page(ChannelFetch page) => Written(writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("through", page.Through);
        writer.WriteBoolean("more", page.More);
        writer.WriteStartArray("messages");
        foreach (var message in page.Messages) Write(writer, message, message.Origin);
        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    /// <summary>A page read back on a machine, or null when it is not one, or any message in it is not whole or names no origin.</summary>
    public static ChannelFetch? ReadPage(string json)
    {
        if (ParseObject(json) is not { } root) return null;
        var messages = new List<ChannelMessage>();
        foreach (var item in Items(root, "messages"))
        {
            if (Read(item) is not { } message || Text(item, "origin") is not { Length: > 0 } origin) return null;
            messages.Add(message with { Origin = origin });
        }

        return new ChannelFetch(
            messages,
            root.TryGetProperty("through", out var through) && through.ValueKind == JsonValueKind.Number ? through.GetInt64() : 0,
            root.TryGetProperty("more", out var more) && more.ValueKind == JsonValueKind.True);
    }

    private static void Write(Utf8JsonWriter writer, ChannelMessage message, string? origin)
    {
        writer.WriteStartObject();
        writer.WriteString("id", message.Id);
        if (origin is not null) writer.WriteString("origin", origin);
        writer.WriteString("ask", message.Ask);
        writer.WriteString("at", Moments.Write(message.At));
        if (message.Session is not null) writer.WriteString("session", message.Session);
        if (message.Repository is not null) writer.WriteString("repository", message.Repository);
        if (message.Agent is not null) writer.WriteString("agent", message.Agent);
        if (message.Entry is not null) writer.WriteString("entry", message.Entry);
        if (message.To is not null) writer.WriteString("to", message.To);
        if (message.Re is not null) writer.WriteString("re", message.Re);
        if (message.Question) writer.WriteBoolean("question", true);
        // COWORK1d (the message-urgency design §9): said whenever kept, an explicit false included, since absent is a message from
        // before and keeps its old reach. An older machine ignores the field.
        if (message.Urgent is { } urgent) writer.WriteBoolean("urgent", urgent);
        writer.WriteString("text", message.Text);
        writer.WriteEndObject();
    }

    /// <summary>One message, or null when it lacks what a message is: an id, its ask, a moment and words. The workspace is the receiver's to name.</summary>
    private static ChannelMessage? Read(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object
            || Text(item, "id") is not { Length: > 0 } id || Text(item, "ask") is not { Length: > 0 } ask
            || Text(item, "text") is not { Length: > 0 } text || Moments.Read(item, "at") is not { } at)
        {
            return null;
        }

        string? Maybe(string name) => Text(item, name) is { Length: > 0 } value ? value : null;
        return new ChannelMessage(
            id, ask, Workspaces.Default, at, Maybe("session"), Maybe("repository"), Maybe("agent"), Maybe("entry"), Maybe("to"), Maybe("re"),
            item.TryGetProperty("question", out var question) && question.ValueKind == JsonValueKind.True, text)
        {
            Urgent = item.TryGetProperty("urgent", out var urgent) ? urgent.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            } : null,
        };
    }
}

/// <summary>
/// The channel feed's judgement on a shared host (SESSONE2d), in Core for the reason <see cref="SessionFeed"/> is: one judgement for every
/// door. Messages are kept by the key that carried them, once each; a message of a repository that has not joined is refused whole, as a
/// session record is, and a feed refused changes nothing. The words are cleaned again on arrival, which a cleaned sentence survives
/// unchanged, so a client that forgot to is not what a teammate then reads.
/// </summary>
public sealed class ChannelFeed(KnowledgeService service, ChannelStore channel)
{
    public async Task<SessionFeedOutcome> FeedAsync(string origin, IReadOnlyList<ChannelMessage> messages, CancellationToken ct = default)
    {
        var registered = await service.RegistryAsync(ct: ct).ConfigureAwait(false);
        var joined = registered.Where(r => r.Joined).Select(r => r.Repository).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var workspaces = registered.ToDictionary(r => r.Repository, r => r.InWorkspace, StringComparer.OrdinalIgnoreCase);

        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message.Id) || string.IsNullOrWhiteSpace(message.Ask) || string.IsNullOrWhiteSpace(message.Text))
            {
                return new(SessionFeedRefusal.Malformed, $"message `{message.Id}` is not a channel message: an id, its ask and words are required", 0);
            }

            if (message.Repository is not null && !joined.Contains(message.Repository))
            {
                return new(
                    SessionFeedRefusal.NotJoined,
                    $"`{message.Repository}` has not joined this deployment: the manifest's `remote.join` is the declaration that admits its records, and silence means local.",
                    0);
            }
        }

        foreach (var message in messages)
        {
            // A person's message names no repository, so it is filed in the default circle here; the machine that fetches it files it in its own.
            var workspace = message.Repository is not null && workspaces.TryGetValue(message.Repository, out var named) ? named : Workspaces.Default;
            await channel.MirrorAsync(message with
            {
                Workspace = workspace,
                Origin = origin,
                Session = message.Session is null ? null : $"{origin}/{message.Session}",
                Text = SessionNote.ForAnotherMachine(message.Text, null)!,
            }, ct).ConfigureAwait(false);
        }

        return new(SessionFeedRefusal.None, $"{messages.Count} channel message(s) mirrored from `{origin}`.", messages.Count);
    }
}
