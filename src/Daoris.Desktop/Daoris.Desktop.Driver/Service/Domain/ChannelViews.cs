using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One item of an ask's channel (SESSONE2a, D167 point 6), as the service answers it: a message the person or one of the ask's
/// sessions wrote, or one of Daoris's lines read from the ask's quests.
/// </summary>
/// <param name="Kind"><see cref="Message"/> or <see cref="Line"/>, as the wire spells it.</param>
/// <param name="At">When it was posted, or when the quest reached the state its line says.</param>
public sealed record ChannelItemView(string Kind, DateTimeOffset At)
{
    public const string Message = "message";

    public const string Line = "line";

    /// <summary>A message's id; <c>origin/id</c> where a teammate's machine made it. None for a line.</summary>
    public string? Id { get; init; }

    /// <summary>Whether the person wrote it. A session's message is that agent's claim, never the person's words.</summary>
    public bool Person { get; init; }

    /// <summary>The session that posted it, for a session's message.</summary>
    public string? Session { get; init; }

    /// <summary>The poster's repository, for a session's message.</summary>
    public string? Repository { get; init; }

    /// <summary>The poster's harness adapter, a name and never a model (D24), for a session's message.</summary>
    public string? Agent { get; init; }

    /// <summary>The poster's entry (D167 point 2), for a session's message: whose line of work wrote it.</summary>
    public string? Entry { get; init; }

    /// <summary>The one repository a message is addressed to; null for everyone. A line's receiving repository.</summary>
    public string? To { get; init; }

    /// <summary>The message a message answers, where it answers one.</summary>
    public string? Re { get; init; }

    /// <summary>Whether its poster marked it a question.</summary>
    public bool Question { get; init; }

    /// <summary>Whether it is a question no message answers yet (SESSONE2c): the page marks it, as the terminal does.</summary>
    public bool Open { get; init; }

    /// <summary>A message's words as written.</summary>
    public string? Text { get; init; }

    /// <summary>
    /// The teammate's machine a message came from, where not this one (SESSONE2d); null for this machine's own. What tells the person's
    /// own words from a teammate's (NOTIFY2g), both <see cref="Person"/>.
    /// </summary>
    public string? Origin { get; init; }

    /// <summary>A line's code: <c>published</c>, <c>taken</c>, <c>done</c>, <c>declined</c> or <c>awaiting</c>.</summary>
    public string? Code { get; init; }

    /// <summary>A line's quest, its door.</summary>
    public string? Quest { get; init; }

    /// <summary>A line's quest as a list names it.</summary>
    public string? Name { get; init; }

    /// <summary>A line's asker: a repository, or <c>ask #id</c>.</summary>
    public string? From { get; init; }

    /// <summary>
    /// A message's level (COWORK1d, D188 point 2): true urgent, false ordinary; null on one from before, which keeps its old reach,
    /// the next step where the door takes words then. Null never means ordinary.
    /// </summary>
    public bool? Urgent { get; init; }

    /// <summary>
    /// The reading entry's acknowledgment of a message by its id (COWORK1d, the design §6; <see cref="ChannelAcks"/>), or null where it
    /// holds none: what decides whether a part of the entry is handed it.
    /// </summary>
    public string? Ack { get; init; }

    /// <summary>The part's session that wrote <see cref="Ack"/>, where one did.</summary>
    public string? AckSession { get; init; }

    /// <summary>
    /// For the person's read (COWORK1d, the design §8.4): what the message reached in each repository it addresses, as the service
    /// judged it; null for an entry's read, a line, and a host from before.
    /// </summary>
    public IReadOnlyList<ChannelReceiptView>? Receipts { get; init; }

    /// <summary>A message with words: what reaches a session. A line, or a message the wire answered without words, is not.</summary>
    public bool IsMessage => Kind == Message && Id is { Length: > 0 } && Text is { Length: > 0 };
}

/// <summary>
/// What a message reached in one repository it addresses (COWORK1d): <c>waiting</c>, <c>held</c>, <c>sent</c>, <c>taken</c>, <c>read</c>,
/// <c>partly</c>, <c>answered</c> or <c>not-reached</c>, as the service spells it; the page words it.
/// </summary>
public sealed record ChannelReceiptView(string Repository, string State);

/// <summary>An ask's channel as one entry reads it (SESSONE2a's <c>GET /api/asks/{id}/channel?entry=</c>), which moves no mark.</summary>
/// <param name="Ask">The ask's id.</param>
/// <param name="Entry">The entry it was read for: the key whose mark <paramref name="Mark"/> is.</param>
/// <param name="Items">Every item, or those after the read's <c>since</c>, in time order.</param>
/// <param name="Unread">How many items are after the entry's mark.</param>
/// <param name="Mark">The moment the entry has read up to; null when it never has.</param>
public sealed record ChannelView(string Ask, string Entry, IReadOnlyList<ChannelItemView> Items, int Unread, DateTimeOffset? Mark)
{
    /// <summary>The newest moment the read holds, of a message or a line; null for an empty channel.</summary>
    public DateTimeOffset? Newest => Items.Count == 0 ? null : Items.Max(item => item.At);

    /// <summary>
    /// The channel as the service answers it: its items, read without trusting the shape, an item without its kind or a moment
    /// that reads passed over. Null for an answer that is not the channel's.
    /// </summary>
    internal static ChannelView? Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || Json.Text(root, "ask") is not { Length: > 0 } ask) return null;

        var items = new List<ChannelItemView>();
        if (root.TryGetProperty("items", out var listed) && listed.ValueKind == JsonValueKind.Array)
        {
            items.AddRange(listed.EnumerateArray().Select(ReadItem).OfType<ChannelItemView>());
        }

        var unread = root.TryGetProperty("unread", out var count) && count.ValueKind == JsonValueKind.Number ? count.GetInt32() : 0;
        return new ChannelView(ask, Json.Text(root, "entry") ?? "", items, unread, Moments.Read(root, "mark"));
    }

    /// <summary>One item as the service answers it; null for one without its kind or a moment that reads (passed over).</summary>
    internal static ChannelItemView? ReadItem(JsonElement item) =>
        item.ValueKind != JsonValueKind.Object || Json.Text(item, "kind") is not { Length: > 0 } kind || Moments.Read(item, "at") is not { } at
            ? null
            : new ChannelItemView(kind, at)
            {
                Id = Json.Text(item, "id"),
                Person = Flag(item, "person"),
                Session = Json.Text(item, "session"),
                Repository = Json.Text(item, "repository"),
                Agent = Json.Text(item, "agent"),
                Entry = Json.Text(item, "entry"),
                To = Json.Text(item, "to"),
                Re = Json.Text(item, "re"),
                Question = Flag(item, "question"),
                Open = Flag(item, "open"),
                Text = Json.Text(item, "text"),
                Origin = Json.Text(item, "origin"),
                Code = Json.Text(item, "code"),
                Quest = Json.Text(item, "quest"),
                Name = Json.Text(item, "name"),
                From = Json.Text(item, "from"),
                Urgent = item.TryGetProperty("urgent", out var urgent)
                    ? urgent.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null }
                    : null,
                Ack = Json.Text(item, "ack"),
                AckSession = Json.Text(item, "ackSession"),
                Receipts = item.TryGetProperty("receipts", out var receipts) && receipts.ValueKind == JsonValueKind.Array
                    ? [.. receipts.EnumerateArray()
                        .Select(receipt => (Repository: Json.Text(receipt, "repository"), State: Json.Text(receipt, "state")))
                        .Where(receipt => receipt.Repository is { Length: > 0 } && receipt.State is { Length: > 0 })
                        .Select(receipt => new ChannelReceiptView(receipt.Repository!, receipt.State!))]
                    : null,
            };

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var flag) && flag.ValueKind == JsonValueKind.True;
}

/// <summary>
/// The person's post as the service answered it (SESSONE2c): the message kept, or the code that refused it (<c>empty</c>,
/// <c>too-long</c>, <c>bad-to</c>, <c>bad-re</c>, <c>not-found</c>), a word each door says in the reader's language.
/// </summary>
public sealed record ChannelPosted(bool Ok, ChannelItemView? Message, string? Code);
