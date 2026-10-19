using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// One turn as the history keeps it (USAGECAP1a2, D179 point 2): whose it was, when, its four counts each null where its door
/// said none, the models they were spent on, and what its door's report means.
/// </summary>
/// <param name="Agent">The account's owner (AGT7), or null where no start bound the session.</param>
/// <param name="Account">The account's opaque local id, <see cref="AccountWindows.Own"/> for the tool's own sign-in, or null.</param>
/// <param name="Epoch">The account's binding epoch when it was kept; null with no account, or where the epochs file did not read.</param>
/// <param name="Door">The adapter the session ran through, or null where no start bound it.</param>
/// <param name="Seq">The turn's event in its session's record (<see cref="SessionEvent.Seq"/>): with the session, its key.</param>
/// <param name="Started">When the session took the turn's ask, or null where this process did not see it.</param>
/// <param name="Ended">When the turn's end was recorded.</param>
/// <param name="Tokens">The turn's four counts as its door reported them, each null where it said none: absent is never zero (D57).</param>
/// <param name="Model">The one model, <see cref="TurnModels.Mixed"/> or <see cref="TurnModels.Unknown"/>.</param>
/// <param name="Split">Each model's share where the door's split agreed with the total, else null.</param>
/// <param name="Attribution">Where the model and the split came from (<see cref="TurnModels.From"/>), or null.</param>
/// <param name="Whole">False where a helper model's tokens fall outside the total; null where no split says.</param>
/// <param name="Cancelled">Whether the turn was stopped: its counts are as reported, and may be none.</param>
/// <param name="Meaning">What its door's counts mean: <see cref="UsageHistory.PerTurn"/> or <see cref="UsageHistory.MeaningUnknown"/>.</param>
/// <param name="Reader">The version of the reading of its tokens and models.</param>
public sealed record KeptTurn(
    string? Agent, string? Account, int? Epoch, string? Door, string Session, long Seq, DateTimeOffset? Started, DateTimeOffset Ended,
    TurnTokens Tokens, string Model, IReadOnlyList<ModelTokens>? Split, string? Attribution, bool? Whole, bool Cancelled, string Meaning,
    int Reader);

/// <summary>What the turns' log holds, in the order it was written: its turns, its gaps, and the lines that did not read.</summary>
public sealed record TurnsRead(IReadOnlyList<KeptTurn> Turns, IReadOnlyList<HistoryGap> Gaps, int Unread);

/// <summary>
/// The turns' log (USAGECAP1a2, D179 point 2): <c>turns-YYYY-MM.jsonl</c> beside the readings, on the same writer and under the
/// same bound, which counts both logs together (<see cref="Append"/>), so the estimate (USAGECAP1b) finds each window's readings
/// and the turns between them for the same months.
/// </summary>
/// <remarks>
/// <para><b>A turn is keyed by its session and its event's sequence</b>: heard twice, it is written once, remembered per home
/// for the process's life as the readings' duplicates are.</para>
/// <para><b>A turn is a session that ran</b>: it carries its account's epoch, beginning the first for an account the history has
/// not met and the next after a sign-out, as a reading does, and changing no plan.</para>
/// <para><b>Never written</b>: a prompt, a word, a frame, a key, an email, an organisation or who signed in. A turn holds
/// Daoris's names, numbers and times, and a model's name only where it is a plain one (<see cref="TurnModels.Name"/>).</para>
/// </remarks>
public static partial class UsageHistory
{
    /// <summary>A door's counts are each turn's own (CONV5's measure on both Claude doors).</summary>
    public const string PerTurn = "per-turn";

    /// <summary>A door's counts whose meaning was never measured: Codex's until a frame is recorded (D179 point 8).</summary>
    public const string MeaningUnknown = "unknown";

    /// <summary>The version of the reading of a turn's tokens and models.</summary>
    public const int TurnReader = 1;

    /// <summary>The turns' file a moment's turn is kept in: its month's, in UTC.</summary>
    public static string TurnsSegmentOf(DateTimeOffset at) => SegmentOf(TurnsLog, at);

    /// <summary>
    /// Keep a turn (USAGECAP1a2): one record, with its account's epoch where a start bound it, appended to its month's file after
    /// any gap a prune made. Never throws: a turn not kept costs the history, never the session.
    /// </summary>
    /// <param name="binding">Whose the session is, as its start chose; null where no start bound it.</param>
    /// <param name="now">When it is written, the caller's clock.</param>
    /// <param name="cap">Where the logs' files stop growing together; <see cref="CapBytes"/> but in a test.</param>
    public static void Turned(string home, TurnBinding? binding, TurnSeen turn, DateTimeOffset now, long cap = CapBytes)
    {
        if (!SessionEventText.IsId(turn.Session)) return;
        try
        {
            lock (Gate)
            {
                var folder = FolderOf(home);
                if (!Remember(folder, string.Join('\n', TurnsLog, turn.Session, turn.Seq.ToString(CultureInfo.InvariantCulture)))) return;

                Directory.CreateDirectory(folder);
                var bound = binding is { Agent: not null, Account: not null } ? binding : null;
                var epoch = bound is null ? null : EpochOf(home, bound.Agent!, bound.Account!, null, turn.Ended);
                Append(home, TurnsLog, [TurnLine(binding, bound, turn, epoch, now)], now, cap);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Dropped: the history is evidence, never a reason to fail.
        }
    }

    /// <summary>
    /// What the turns' log holds, oldest month first and each in the order written: its turns, the gaps a reader meets (the
    /// readings' rules), and how many lines did not read. Never throws.
    /// </summary>
    public static TurnsRead ReadTurns(string home)
    {
        var (turns, gaps, unread) = ReadLog(home, TurnsLog, "turn", TurnOf);
        return new(turns, gaps, unread);
    }

    private static byte[] TurnLine(TurnBinding? binding, TurnBinding? bound, TurnSeen turn, int? epoch, DateTimeOffset now) => Line(json =>
    {
        var models = turn.Models;
        // A split is written whole or not at all: one share left out would leave the rest summing to nothing said.
        var split = models?.Split is { } shares && shares.All(each => TurnModels.Name(each.Model) is not null) ? shares : null;

        Head(json, "turn", now);
        Text(json, "agent", bound?.Agent);
        Text(json, "account", bound?.Account);
        Number(json, "epoch", epoch);
        Text(json, "door", Word(binding?.Door));
        json.WriteString("session", turn.Session);
        json.WriteNumber("seq", turn.Seq);
        Text(json, "started", turn.Started is { } started ? Stamp(started) : null);
        json.WriteString("ended", Stamp(turn.Ended));
        Counts(json, turn.Tokens);
        json.WriteString("model", models?.Model is TurnModels.Mixed or TurnModels.Unknown ? models.Model : TurnModels.Name(models?.Model) ?? TurnModels.Unknown);
        if (split is null)
        {
            json.WriteNull("split");
        }
        else
        {
            json.WriteStartArray("split");
            foreach (var share in split)
            {
                json.WriteStartObject();
                json.WriteString("model", share.Model);
                Counts(json, share.Tokens);
                json.WriteEndObject();
            }

            json.WriteEndArray();
        }

        Text(json, "attribution", Word(models?.From));
        if (split is not null && models?.Whole is { } whole) json.WriteBoolean("whole", whole);
        else json.WriteNull("whole");
        json.WriteBoolean("cancelled", turn.Cancelled);
        json.WriteString("meaning", Word(binding?.Meaning) ?? MeaningUnknown);
        json.WriteNumber("reader", TurnReader);
    });

    private static void Counts(Utf8JsonWriter json, TurnTokens? tokens)
    {
        Number(json, "input", tokens?.Input);
        Number(json, "output", tokens?.Output);
        Number(json, "cacheRead", tokens?.CacheRead);
        Number(json, "cacheWrite", tokens?.CacheWrite);
    }

    private static void Number(Utf8JsonWriter json, string name, long? value)
    {
        if (value is { } number) json.WriteNumber(name, number);
        else json.WriteNull(name);
    }

    /// <summary>A turn read back whole, or null where it does not read.</summary>
    private static KeptTurn? TurnOf(JsonObject record)
    {
        if (Json.NonEmpty(record, "session") is not { } session || Count(record, "seq") is not { } seq
            || Moments.ReadStrict(Json.Text(record, "ended")) is not { } ended || Json.NonEmpty(record, "model") is not { } model
            || Json.NonEmpty(record, "meaning") is not { } meaning || Whole(record, "reader") is not { } reader
            || Flag(record, "cancelled") is not { } cancelled)
        {
            return null;
        }

        IReadOnlyList<ModelTokens>? split = null;
        if (record["split"] is JsonArray shares)
        {
            var read = new List<ModelTokens>();
            foreach (var node in shares)
            {
                if (node is not JsonObject share || Json.NonEmpty(share, "model") is not { } name) return null;
                read.Add(new(name, TokensOf(share)));
            }

            split = read;
        }

        var agent = Json.NonEmpty(record, "agent");
        return new KeptTurn(
            agent, agent is null ? null : Json.Text(record, "account"), Whole(record, "epoch"), Json.NonEmpty(record, "door"), session, seq,
            Moments.ReadStrict(Json.Text(record, "started")), ended, TokensOf(record), model, split, Json.NonEmpty(record, "attribution"),
            Flag(record, "whole"), cancelled, meaning, reader);
    }

    private static TurnTokens TokensOf(JsonObject record) =>
        new(Count(record, "input"), Count(record, "output"), Count(record, "cacheRead"), Count(record, "cacheWrite"));

    private static long? Count(JsonObject record, string name) =>
        record[name] is JsonValue value && value.TryGetValue<long>(out var count) ? count : null;

    private static bool? Flag(JsonObject record, string name) =>
        record[name] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;
}
