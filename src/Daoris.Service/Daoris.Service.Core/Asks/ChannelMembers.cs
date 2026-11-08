using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>What a join asks: the channel, by an ask's id or by a session of the line whose channel it is, and who the agent says it is.</summary>
/// <param name="Agent">One of this build's harness names, or <c>other</c>; a name of no such shape is kept as <c>other</c>.</param>
/// <param name="Repository">The repository the agent says it works in, which the person's Confirm judged; or none.</param>
/// <param name="Confirmation">The person's confirmation that admitted it.</param>
public sealed record ChannelJoin(string? Ask, string? Line, string? Agent, string? Repository, string? Confirmation);

/// <summary>
/// An agent outside Daoris on one channel: this machine's, never synced. It posts and reads under its own
/// id, an <c>o</c> and seven hex characters so it is never read for a session's, with its own entry, <c>&lt;origin&gt;/outside:&lt;id&gt;</c>.
/// Its mark is its own record's <see cref="Read"/>, on an ask's channel and a line's alike. A member that left keeps its record, so its
/// messages keep their poster.
/// </summary>
/// <param name="Seen">When it last read or posted.</param>
/// <param name="Read">When it last read: what its next read returns after.</param>
/// <param name="Why">Why it left: <see cref="ByItself"/> or <see cref="ByPerson"/>.</param>
public sealed record ChannelMember(
    string Id, string Origin, string Workspace, string Entry, string Agent, string? Repository, string? Confirmation, DateTimeOffset Joined,
    DateTimeOffset? Seen = null, DateTimeOffset? Read = null, DateTimeOffset? Left = null, string? Why = null)
{
    /// <summary>It left by its own <c>leave</c>.</summary>
    public const string ByItself = "left";

    /// <summary>The person removed it.</summary>
    public const string ByPerson = "removed";

    /// <summary>Daoris's look ended it: its channel's claims expired, or it stayed quiet for a day.</summary>
    public const string Expired = "expired";

    /// <summary>An agent whose harness this build does not name.</summary>
    public const string OtherAgent = "other";

    /// <summary>
    /// Whether <paramref name="poster"/> is a member's id: an <c>o</c> and seven lowercase hex characters, also behind the machine a
    /// teammate's message came from (<c>&lt;machine&gt;/&lt;id&gt;</c>). A session's id is eight hex characters, never this.
    /// </summary>
    public static bool IsId(string? poster)
    {
        if (poster is null) return false;
        var id = poster[(poster.LastIndexOf('/') + 1)..];
        return id.Length == 8 && id[0] == 'o' && id.Skip(1).All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    internal static string NewId() => "o" + Guid.NewGuid().ToString("N")[..7];

    /// <summary>The agent's name as kept: a short lowercase name, else <see cref="OtherAgent"/>.</summary>
    internal static string AgentOf(string? named)
    {
        var name = named?.Trim().ToLowerInvariant() ?? "";
        return name.Length is > 0 and <= 32 && char.IsAsciiLetterOrDigit(name[0]) && name.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '-' or '.')
            ? name
            : OtherAgent;
    }
}

/// <summary>What a join or a leave did: the member, or why not.</summary>
public sealed record ChannelMemberOutcome(ChannelRefusal Refusal, ChannelMember? Member);

/// <summary>
/// The members, kept in <c>channel_members</c> beside the channel's tables and never synced: a membership is this machine's. The bounds are
/// judged in the insert's own statement, so two joins at once cannot both pass one.
/// </summary>
public sealed class ChannelMemberStore
{
    private readonly ConnectionGate _db;

    internal ChannelMemberStore(ConnectionGate db) => _db = db;

    /// <summary>The table, made before the journal's triggers that read it. Called inside the gate.</summary>
    internal static async Task EnsureSchemaAsync(ConnectionGate db, CancellationToken ct)
    {
        await using var command = db.Command();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS channel_members (
              id           TEXT NOT NULL PRIMARY KEY,
              origin       TEXT NOT NULL,
              workspace    TEXT NOT NULL,
              entry        TEXT NOT NULL,
              agent        TEXT NOT NULL,
              repository   TEXT NULL,
              confirmation TEXT NULL,
              joined       TEXT NOT NULL,
              seen         TEXT NULL,
              read_at      TEXT NULL,
              left_at      TEXT NULL,
              why          TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS channel_members_origin ON channel_members (origin);
            CREATE TABLE IF NOT EXISTS channel_member_reads (
              member  TEXT NOT NULL,
              message TEXT NOT NULL,
              at      TEXT NOT NULL,
              PRIMARY KEY (member, message)
            );
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Keep <paramref name="member"/> unless its channel holds <see cref="ChannelBounds.MembersPerChannel"/> or this machine <see cref="ChannelBounds.MembersHere"/>.</summary>
    /// <returns>Whether it is kept.</returns>
    public Task<bool> AdmitAsync(ChannelMember member, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            INSERT INTO channel_members (id, origin, workspace, entry, agent, repository, confirmation, joined)
            SELECT $id, $origin, $workspace, $entry, $agent, $repository, $confirmation, $joined
            WHERE (SELECT COUNT(*) FROM channel_members WHERE origin = $origin AND left_at IS NULL) < $perChannel
              AND (SELECT COUNT(*) FROM channel_members WHERE left_at IS NULL) < $here
            """;
        command.Parameters.AddWithValue("$id", member.Id);
        command.Parameters.AddWithValue("$origin", member.Origin);
        command.Parameters.AddWithValue("$workspace", member.Workspace);
        command.Parameters.AddWithValue("$entry", member.Entry);
        command.Parameters.AddWithValue("$agent", member.Agent);
        command.Parameters.AddWithValue("$repository", (object?)member.Repository ?? DBNull.Value);
        command.Parameters.AddWithValue("$confirmation", (object?)member.Confirmation ?? DBNull.Value);
        command.Parameters.AddWithValue("$joined", Moments.Write(member.Joined));
        command.Parameters.AddWithValue("$perChannel", ChannelBounds.MembersPerChannel);
        command.Parameters.AddWithValue("$here", ChannelBounds.MembersHere);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>The member under <paramref name="id"/>, whether or not it left; null for none.</summary>
    public async Task<ChannelMember?> FindAsync(string id, CancellationToken ct = default) =>
        (await SelectAsync("WHERE id = $key", id, ct).ConfigureAwait(false)).FirstOrDefault();

    /// <summary>Every member a channel has held, the first joined first.</summary>
    public Task<IReadOnlyList<ChannelMember>> OfAsync(string origin, CancellationToken ct = default) => SelectAsync("WHERE origin = $key", origin, ct);

    /// <summary>Every member this machine has held, the first joined first.</summary>
    public Task<IReadOnlyList<ChannelMember>> AllAsync(CancellationToken ct = default) => SelectAsync("", null, ct);

    /// <summary>Its read or its post, said: a read moves its mark too.</summary>
    public Task SeenAsync(string id, DateTimeOffset at, bool read, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = read
            ? "UPDATE channel_members SET seen = $at, read_at = $at WHERE id = $id"
            : "UPDATE channel_members SET seen = $at WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$at", Moments.Write(at));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>The messages a member has been handed, by id: a late arrival is told from one already read without its moment.</summary>
    public Task<IReadOnlySet<string>> HandedAsync(string id, CancellationToken ct = default) => _db.RunAsync<IReadOnlySet<string>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT message FROM channel_member_reads WHERE member = $id";
        command.Parameters.AddWithValue("$id", id);
        var handed = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) handed.Add(reader.GetString(0));
        return handed;
    }, ct);

    /// <summary>Messages handed to a member, kept before the answer reaches it so a read it never received is made again.</summary>
    public Task HandAsync(string id, IEnumerable<string> messages, DateTimeOffset at, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        foreach (var message in messages)
        {
            await using var command = _db.Command();
            command.CommandText = "INSERT OR IGNORE INTO channel_member_reads (member, message, at) VALUES ($id, $message, $at)";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$message", message);
            command.Parameters.AddWithValue("$at", Moments.Write(at));
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }, ct);

    /// <summary>End a membership once, with why.</summary>
    /// <returns>Whether it had been a member.</returns>
    public Task<bool> LeaveAsync(string id, string why, DateTimeOffset at, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "UPDATE channel_members SET left_at = $at, why = $why WHERE id = $id AND left_at IS NULL";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$why", why);
        command.Parameters.AddWithValue("$at", Moments.Write(at));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    private Task<IReadOnlyList<ChannelMember>> SelectAsync(string where, string? key, CancellationToken ct) =>
        _db.RunAsync<IReadOnlyList<ChannelMember>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"SELECT * FROM channel_members {where} ORDER BY joined, id";
        if (key is not null) command.Parameters.AddWithValue("$key", key);
        var read = new List<ChannelMember>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) read.Add(Read(reader));
        return read;
    }, ct);

    private static ChannelMember Read(SqliteDataReader reader)
    {
        string Text(string column) => reader.GetString(reader.GetOrdinal(column));
        string? Maybe(string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetString(reader.GetOrdinal(column));
        DateTimeOffset? Moment(string column) => Maybe(column) is { } at ? Moments.ReadRequired(at) : null;
        return new ChannelMember(
            Text("id"), Text("origin"), Text("workspace"), Text("entry"), Text("agent"), Maybe("repository"), Maybe("confirmation"),
            Moments.ReadRequired(Text("joined")), Moment("seen"), Moment("read_at"), Moment("left_at"), Maybe("why"));
    }
}

/// <summary>
/// An agent outside Daoris on a channel: admitted by the person's door once they confirmed
/// its join, it posts and reads the one channel it joined under its own id, and leaves. Its post is a part's in every bound and every
/// judgement but one: it names no part, since it is no part of the line. A member id is no session's, so every session door, the
/// connector's among them, finds none in the ledger.
/// </summary>
public sealed class ChannelMembers(ChannelDesk desk, ChannelStore store, AskDesk asks)
{
    private ChannelMemberStore Kept => store.Members;

    /// <summary>
    /// The person's admission of an agent to the channel <paramref name="join"/> names: an ask held here or a teammate's whose messages
    /// are mirrored here, or the channel of the line a session is a part of. Refused not-found, no-session, a helper's or a reviewer's
    /// line (<see cref="ChannelRefusal.Helper"/>), or too-many past the bounds.
    /// </summary>
    public async Task<ChannelMemberOutcome> JoinAsync(ChannelJoin join, DateTimeOffset now, CancellationToken ct = default)
    {
        var (refusal, origin, workspace) = await ChannelOfAsync(join, ct).ConfigureAwait(false);
        if (refusal != ChannelRefusal.None) return new(refusal, null);
        var id = ChannelMember.NewId();
        var member = new ChannelMember(
            id, origin!, workspace!, SessionEntries.Compose(origin!, $"outside:{id}"), ChannelMember.AgentOf(join.Agent),
            Named(join.Repository), Named(join.Confirmation), now);
        return await Kept.AdmitAsync(member, ct).ConfigureAwait(false) ? new(ChannelRefusal.None, member) : new(ChannelRefusal.TooMany, null);
    }

    /// <summary>
    /// A member's post on its channel, to everyone or one of the channel's repositories, ordinary or urgent, within a part's bounds: kept
    /// with its id as its poster. A <c>to</c> naming a session is refused <see cref="ChannelRefusal.BadPart"/>: a member is no part of the line.
    /// </summary>
    public async Task<ChannelOutcome> PostAsync(
        string? id, string? text, string? to, string? re, bool question, bool urgent, DateTimeOffset now, CancellationToken ct = default)
    {
        if (await PresentAsync(id, ct).ConfigureAwait(false) is not { } member) return new(ChannelRefusal.NotMember, null);
        var (part, named) = await desk.Parts.NamedAsync(to, member.Entry, null, ct, member.Origin).ConfigureAwait(false);
        if (part is not null || named != ChannelRefusal.None) return new(ChannelRefusal.BadPart, null);

        (ChannelRefusal Refusal, string? Text, string? To, string? Re) judged;
        ChannelMessage? kept = null;
        if (ChannelDesk.AskOfOrigin(member.Origin) is not { } askId)
        {
            judged = await desk.Parts.JudgeAsync(member.Origin, null, text, to, re, ct).ConfigureAwait(false);
            if (judged.Refusal == ChannelRefusal.None)
            {
                var row = Message(member, ChannelParts.NewId(), "", judged, question, urgent, now) with { Channel = member.Origin };
                kept = await store.Addressed.KeepAsync(row, ChannelBounds.MaxPerPart, ct).ConfigureAwait(false);
            }
        }
        else
        {
            judged = await asks.FindAsync(askId, ct).ConfigureAwait(false) is { } ask
                ? await desk.JudgeAsync(ask, text, to, re, ct).ConfigureAwait(false)
                : TeammateAskChannel.Judge(await store.OfAsync(askId, ct).ConfigureAwait(false), text, to, re);
            if (judged.Refusal == ChannelRefusal.None)
            {
                var row = Message(member, Guid.NewGuid().ToString("N")[..8], askId, judged, question, urgent, now);
                kept = await store.KeepAsync(row, ChannelBounds.MaxPerPart, ct).ConfigureAwait(false);
            }
        }

        if (judged.Refusal != ChannelRefusal.None) return new(judged.Refusal, null);
        if (kept is null) return new(ChannelRefusal.TooMany, null);
        await Kept.SeenAsync(member.Id, now, read: false, ct).ConfigureAwait(false);
        return new(ChannelRefusal.None, kept);
    }

    /// <summary>
    /// A member's read of its channel: every message on it to everyone or a repository and, on an ask held here, Daoris's lines, after its
    /// own mark or after <paramref name="since"/>; its mark then moves to <paramref name="now"/>. A message is handed by identity, once:
    /// one that arrives behind the mark (a teammate's keeps its own moment) is still handed by the next read, and Daoris's lines go by
    /// the mark. Unread is against what it has been handed and the mark it had.
    /// </summary>
    public async Task<ChannelReadOutcome> ReadAsync(string? id, DateTimeOffset? since, DateTimeOffset now, CancellationToken ct = default)
    {
        if (await PresentAsync(id, ct).ConfigureAwait(false) is not { } member) return new(ChannelRefusal.NotMember, null, null);
        var askId = ChannelDesk.AskOfOrigin(member.Origin);
        var ask = askId is null ? null : await asks.FindAsync(askId, ct).ConfigureAwait(false);
        IReadOnlyList<ChannelMessage> messages = askId is null
            ? [.. (await store.Addressed.OfAsync(member.Origin, ct).ConfigureAwait(false)).Where(m => m.Part is null)]
            : await store.OfAsync(askId, ct).ConfigureAwait(false);
        var lines = ask is null ? [] : await desk.LinesAsync(ask, ct).ConfigureAwait(false);

        var from = since ?? member.Read;
        bool After(DateTimeOffset at, DateTimeOffset? mark) => mark is null || at > mark;
        var handed = await Kept.HandedAsync(member.Id, ct).ConfigureAwait(false);
        var unread = messages.Count(m => !handed.Contains(m.Id)) + lines.Count(l => After(l.At, member.Read));
        List<ChannelMessage> returned = [.. messages.Where(m => since is null ? !handed.Contains(m.Id) : m.At > since)];
        await Kept.HandAsync(member.Id, returned.Select(m => m.Id), now, ct).ConfigureAwait(false);
        await Kept.SeenAsync(member.Id, now, read: true, ct).ConfigureAwait(false);
        var read = new ChannelRead(returned, [.. lines.Where(l => After(l.At, from))], unread, member.Read);
        return new(ChannelRefusal.None, ask, read, member.Entry) { Origin = member.Origin };
    }

    /// <summary>A membership ended, by the member's own leave or the person's <i>Remove</i> (<paramref name="why"/>); refused not-member once it has.</summary>
    public async Task<ChannelMemberOutcome> LeaveAsync(string? id, string why, DateTimeOffset now, CancellationToken ct = default)
    {
        if (await PresentAsync(id, ct).ConfigureAwait(false) is not { } member) return new(ChannelRefusal.NotMember, null);
        return await Kept.LeaveAsync(member.Id, why, now, ct).ConfigureAwait(false)
            ? new(ChannelRefusal.None, member with { Left = now, Why = why })
            : new(ChannelRefusal.NotMember, null);
    }

    /// <summary>Every member a channel has held, those that left among them.</summary>
    public Task<IReadOnlyList<ChannelMember>> OfAsync(string origin, CancellationToken ct = default) => Kept.OfAsync(origin, ct);

    /// <summary>Every member this machine has held.</summary>
    public Task<IReadOnlyList<ChannelMember>> AllAsync(CancellationToken ct = default) => Kept.AllAsync(ct);

    /// <summary>The member under <paramref name="id"/> still on its channel; null for none, or one that left.</summary>
    private async Task<ChannelMember?> PresentAsync(string? id, CancellationToken ct) =>
        id?.Trim() is { } named && ChannelMember.IsId(named) && !named.Contains('/')
        && await Kept.FindAsync(named, ct).ConfigureAwait(false) is { Left: null } member
            ? member
            : null;

    /// <summary>The origin and circle of the channel a join names, or why it names none.</summary>
    private async Task<(ChannelRefusal Refusal, string? Origin, string? Workspace)> ChannelOfAsync(ChannelJoin join, CancellationToken ct)
    {
        var line = Named(join.Line);
        var askId = Named(join.Ask)?.TrimStart('#');
        if (line is not null)
        {
            if (askId is not null) return (ChannelRefusal.NotFound, null, null);
            var (refusal, _, session, _, origin) = await desk.SpeakerAsync(line, ct).ConfigureAwait(false);
            if (refusal is not (ChannelRefusal.None or ChannelRefusal.NoAsk)) return (refusal, null, null);
            return origin is null ? (ChannelRefusal.NotFound, null, null) : (ChannelRefusal.None, origin, session!.Workspace);
        }

        if (string.IsNullOrEmpty(askId)) return (ChannelRefusal.NotFound, null, null);
        if (await asks.FindAsync(askId, ct).ConfigureAwait(false) is { } ask) return (ChannelRefusal.None, SessionEntries.AskOrigin(ask.Id), ask.Workspace);
        var mirrored = await store.OfAsync(askId, ct).ConfigureAwait(false);
        return mirrored.Count > 0 ? (ChannelRefusal.None, SessionEntries.AskOrigin(askId), mirrored[0].Workspace) : (ChannelRefusal.NotFound, null, null);
    }

    private static ChannelMessage Message(
        ChannelMember member, string id, string ask, (ChannelRefusal Refusal, string? Text, string? To, string? Re) judged, bool question, bool urgent,
        DateTimeOffset now) =>
        new(id, ask, member.Workspace, now, member.Id, member.Repository, member.Agent, member.Entry, judged.To, judged.Re, question, judged.Text!)
        {
            Urgent = urgent,
        };

    private static string? Named(string? text) => text?.Trim() is { Length: > 0 } named ? named : null;
}
