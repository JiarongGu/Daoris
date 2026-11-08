using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// The units' node records under a home: one file per unit,
/// <c>&lt;home&gt;/workflows/units/&lt;key&gt;.json</c>, its key a digest of the unit's id, which the file holds. Changed only by
/// <see cref="Edit{T}"/>: read, judged and written whole in one hold of the record's lock, the lease asked inside it;
/// atomically, BOM-less and LF; the driver's alone to write, in the loop that holds the home's lease, and machine-local, never on the wire.
/// A file that does not read is never taken for an empty record: an attempt it may hold could be sent twice.
/// </summary>
/// <param name="home">The home the records are under.</param>
/// <param name="holdsLease">Whether this process holds the home's lease, asked inside each edit's lock; by default the home's lease file.</param>
public sealed class NodeRecordFiles(string home, Func<bool>? holdsLease = null)
{
    /// <summary>Why an edit wrote nothing: the record's file does not read, or this process does not hold the home's lease.</summary>
    public const string Unreadable = "unreadable";

    public const string NotHeld = "not-held";

    /// <summary>How old a record file is before a read of it is kept while its stamp stands: a younger file could be written again within the clock's grain, to the same stamp and length.</summary>
    public static readonly TimeSpan Settled = TimeSpan.FromSeconds(2);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Written, long Length, NodeRecord Record)> Kept =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Func<bool> _holdsLease = holdsLease ?? (() => HomeLease.HeldHere(home));

    /// <summary>The folder's record files, where a test names them; null lists the folder.</summary>
    internal Func<string, string[]>? Lists { get; init; }

    /// <summary>The home the records are under: where a closure graph a quest is bound to is kept beside them.</summary>
    public string Home => home;

    /// <summary>Whether this process holds the home's lease, as each edit asks it: what the resource records beside these ask too.</summary>
    public Func<bool> HoldsLease => _holdsLease;

    /// <summary>The folder the records sit in, beside the runs (<see cref="WorkflowRunBindings.FolderOf"/>).</summary>
    public static string FolderOf(string home) => DaorisHome.Entry(home, HomeEntry.Workflows, "units");

    /// <summary>A unit's record's file: its id's digest, so no unit id is read as a path.</summary>
    public static string PathOf(string home, string unit) => Path.Combine(FolderOf(home), KeyOf(unit) + ".json");

    public static string KeyOf(string unit) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(unit)))[..32];

    /// <summary>The unit's record: an empty one where it has none, null where its file does not read.</summary>
    public NodeRecord? Read(string unit)
    {
        var path = PathOf(home, unit);
        var file = new FileInfo(path);
        if (!file.Exists) return new NodeRecord(unit);
        var (written, length) = (file.LastWriteTimeUtc, file.Length);
        if (Kept.TryGetValue(path, out var kept) && kept.Written == written && kept.Length == length) return kept.Record;
        try
        {
            if (Parse(AtomicFile.ReadText(path)) is not { } record || !string.Equals(record.Unit, unit, StringComparison.Ordinal)) return null;
            if (DateTime.UtcNow - written > Settled) Kept[path] = (written, length, record);
            return record;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Every record that reads, and the files that do not (by name), for the loop to say.</summary>
    public (IReadOnlyList<NodeRecord> Records, IReadOnlyList<string> Unreadable) All()
    {
        var folder = FolderOf(home);
        if (!Directory.Exists(folder)) return ([], []);
        var records = new List<NodeRecord>();
        var unreadable = new List<string>();
        foreach (var path in (Lists?.Invoke(folder) ?? Directory.GetFiles(folder, "*.json")).Order(StringComparer.Ordinal))
        {
            // A record the history clear deleted since the listing is gone, not a file for the person to mend.
            var file = new FileInfo(path);
            if (!file.Exists) continue;
            var (written, length) = (file.LastWriteTimeUtc, file.Length);
            if (Kept.TryGetValue(path, out var kept) && kept.Written == written && kept.Length == length)
            {
                records.Add(kept.Record);
                continue;
            }

            NodeRecord? record = null;
            try
            {
                record = Parse(AtomicFile.ReadText(path));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Said below, as a file that does not read.
            }

            if (record is null && !File.Exists(path)) continue;

            if (record is not null && string.Equals(Path.GetFileNameWithoutExtension(path), KeyOf(record.Unit), StringComparison.Ordinal))
            {
                records.Add(record);
                if (DateTime.UtcNow - written > Settled) Kept[path] = (written, length, record);
            }
            else unreadable.Add(Path.GetFileName(path));
        }

        return (records, unreadable);
    }

    /// <summary>
    /// The files that do not read, for the person to mend: each by its name, the unit it names where its text still
    /// names one whose key is that name, and its last write time. Read-only.
    /// </summary>
    public IReadOnlyList<UnreadableRecord> Unread() =>
    [
        .. All().Unreadable.Select(name =>
        {
            var path = Path.Combine(FolderOf(home), name);
            DateTimeOffset? at = File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) : null;
            return new UnreadableRecord(name, UnitNamed(path, name)) { At = at };
        }),
    ];

    /// <summary>The unit a file that does not read still names, where it is JSON naming one whose key is the file's name; else null.</summary>
    private static string? UnitNamed(string path, string name)
    {
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            return document.RootElement.ValueKind == JsonValueKind.Object && Text(document.RootElement, "unit") is { } unit
                && string.Equals(KeyOf(unit) + ".json", name, StringComparison.Ordinal)
                ? unit
                : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// One change of a unit's record (as <see cref="RecordList{T}.Edit"/> is for a list): the record's lock held, the
    /// lease asked, the record read as it is now, <paramref name="change"/> judged on it, and the record it returns written whole, atomically,
    /// and marked as this process's own write; a null record writes nothing. Two callers that read the same record therefore never both
    /// decide on it: the second reads what the first wrote. Nothing is awaited inside it.
    /// </summary>
    /// <returns>Why nothing is judged (<see cref="NotHeld"/>, <see cref="Unreadable"/>), else null with what the change says.</returns>
    /// <exception cref="IOException">Another edit held the record's lock for the whole of <see cref="RecordLock.Wait"/>.</exception>
    public (string? Refused, T? Result) Edit<T>(string unit, Func<NodeRecord, (NodeRecord? Next, T Result)> change)
    {
        var path = PathOf(home, unit);
        using (RecordLock.Take(RecordLock.PathOf(home, "units-" + KeyOf(unit))))
        {
            // The lease is asked here, under the lock, so a loop that lost it between its look and its write writes nothing.
            if (!_holdsLease()) return (NotHeld, default);
            if (Read(unit) is not { } record) return (Unreadable, default);
            var (next, result) = change(record);
            if (next is null) return (null, result);
            if (!string.Equals(next.Unit, unit, StringComparison.Ordinal)) throw new InvalidOperationException($"a change of unit `{unit}`'s record named another unit");
            Directory.CreateDirectory(FolderOf(home));
            AtomicFile.WriteText(path, ToJson(Trimmed(next)));
            Kept.TryRemove(path, out _);
            HomeWrites.Mine(path);
            return (null, result);
        }
    }

    /// <summary>
    /// A whole record put over its unit's, through <see cref="Edit{T}"/>: a test's, or a person's mend. Over a file that does not read, or
    /// without the lease, it writes nothing and says so.
    /// </summary>
    /// <exception cref="InvalidOperationException">Nothing is written, and why.</exception>
    public void Write(NodeRecord record)
    {
        if (Edit(record.Unit, _ => (record, true)).Refused is { } refused)
        {
            throw new InvalidOperationException($"unit `{record.Unit}`'s node record was not written: {refused}");
        }
    }

    /// <summary>
    /// Every attempt a status still needs, and the newest <see cref="NodeRecord.SettledKept"/> settled others; the newest history. Needed:
    /// one not settled (recorded, sent, unknown or held), each node's current one (a pending retry is its failed node's current attempt,
    /// what <see cref="WorkPerformer.Due"/> reads its delay from), and one a node holds unreconciled.
    /// </summary>
    private static NodeRecord Trimmed(NodeRecord record)
    {
        bool Needed(NodeAttempt attempt) =>
            attempt.State != AttemptStates.Settled
            || record.Statuses.GetValueOrDefault(attempt.Node) is { } status && (status.Attempt == attempt.Attempt || status.Unreconciled == attempt.Attempt);

        var spare = record.Attempts.Where(attempt => !Needed(attempt)).ToList();
        var dropped = spare.Take(Math.Max(0, spare.Count - NodeRecord.SettledKept)).Select(attempt => attempt.Call.Invocation).ToHashSet(StringComparer.Ordinal);
        return record with
        {
            Attempts = [.. record.Attempts.Where(attempt => !dropped.Contains(attempt.Call.Invocation))],
            History = [.. record.History.Skip(Math.Max(0, record.History.Count - NodeRecord.HistoryKept))],
        };
    }

    // ——— the file's format

    /// <summary>A record as its file holds it, every name spelled here, LF, with a last newline.</summary>
    public static string ToJson(NodeRecord record) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("unit", record.Unit);
        writer.WriteStartObject("nodes");
        foreach (var (node, status) in record.Statuses.OrderBy(each => each.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(node);
            WriteStatus(writer, status);
        }

        writer.WriteEndObject();
        writer.WriteStartArray("attempts");
        foreach (var attempt in record.Attempts) WriteAttempt(writer, attempt);
        writer.WriteEndArray();
        writer.WriteStartArray("fired");
        foreach (var key in record.Fired) writer.WriteStringValue(key);
        writer.WriteEndArray();
        writer.WriteStartArray("history");
        foreach (var entry in record.History)
        {
            writer.WriteStartObject();
            writer.WriteString("at", Moments.Write(entry.At));
            writer.WriteString("why", entry.Why);
            writer.WritePropertyName("was");
            WriteStatus(writer, entry.Was);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        // The events observed nodes were taken on, written only where there are some.
        if (record.Observations.Count > 0)
        {
            writer.WriteStartArray("observations");
            foreach (var each in record.Observations)
            {
                writer.WriteStartObject();
                writer.WriteString("plugin", each.Plugin);
                writer.WriteString("repository", each.Repository);
                writer.WriteString("step", each.Step);
                writer.WriteString("commit", each.Commit);
                writer.WriteString("id", each.Id);
                writer.WriteString("state", each.State);
                writer.WriteString("at", Moments.Write(each.At));
                writer.WriteString("node", each.Node);
                // The subject a run's node is told under, written only where set.
                if (each.Subject is { } subject) writer.WriteString("subject", subject);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        // A run's end, written only on a run's record that holds one.
        if (record.Ended is { } ended)
        {
            writer.WriteStartObject("ended");
            writer.WriteString("quest", ended.Quest);
            writer.WriteString("at", Moments.Write(ended.At));
            writer.WriteEndObject();
        }

        if (record.Pending.Count > 0)
        {
            writer.WriteStartArray("pending");
            foreach (var word in record.Pending)
            {
                NodeWordFiles.Write(writer, word);
            }

            writer.WriteEndArray();
        }

        // The repairs a carried review Accept waits on, written only while one waits.
        if (record.Repairs.Count > 0)
        {
            writer.WriteStartArray("repairs");
            foreach (var repair in record.Repairs)
            {
                writer.WriteStartObject();
                writer.WriteString("node", repair.Node);
                writer.WriteString("tip", repair.Tip);
                writer.WriteString("turn", repair.Turn);
                writer.WriteNumber("carries", repair.Carries);
                writer.WriteString("at", Moments.Write(repair.At));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    });

    /// <summary>A status as the record writes it: two statuses are the same where their texts are, whoever built their collections.</summary>
    public static string StatusText(NodeStatus status) => JsonFile.Serialize(writer => WriteStatus(writer, status));

    private static void WriteStatus(Utf8JsonWriter writer, NodeStatus status)
    {
        writer.WriteStartObject();
        writer.WriteString("unit", status.Unit);
        writer.WriteString("node", status.Node);
        writer.WriteString("state", status.State);
        writer.WriteNumber("attempt", status.Attempt);
        writer.WriteNumber("tries", status.Tries);
        if (status.Unreconciled is { } unreconciled) writer.WriteNumber("unreconciled", unreconciled);
        if (status.Binding is { } binding)
        {
            writer.WritePropertyName("binding");
            WriteAcceptance(writer, binding);
        }

        if (status.Scope is { } scope)
        {
            writer.WriteStartObject("scope");
            writer.WriteString("graph", scope.GraphDigest);
            writer.WriteString("run", scope.Run);
            writer.WriteEndObject();
        }

        if (status.Acceptance is { } acceptance)
        {
            writer.WritePropertyName("acceptance");
            WriteAcceptance(writer, acceptance);
        }

        if (status.By is { } by) writer.WriteString("by", by);
        if (status.Code is { } code) writer.WriteString("code", code);
        if (status.Transient) writer.WriteBoolean("transient", true);
        if (status.Words is { } words) writer.WriteString("words", words);
        if (status.Since is { } since) writer.WriteString("since", Moments.Write(since));
        if (status.Holds is { } holds) writer.WriteString("holds", holds);
        writer.WriteEndObject();
    }

    private static void WriteAcceptance(Utf8JsonWriter writer, NodeAcceptance acceptance)
    {
        writer.WriteStartObject();
        writer.WriteString("unit", acceptance.Unit);
        // Written only where set.
        if (acceptance.Subject is { } subject) writer.WriteString("subject", subject);
        if (acceptance.SubjectRevision is { } revision) writer.WriteString("revision", revision);
        writer.WriteString("graph", acceptance.GraphDigest);
        writer.WriteString("node", acceptance.Node);
        writer.WriteNumber("version", acceptance.NodeVersion);
        writer.WriteNumber("attempt", acceptance.Attempt);
        WritePorts(writer, "inputs", acceptance.Inputs);
        writer.WriteStartArray("receipts");
        foreach (var receipt in acceptance.PrerequisiteReceipts) writer.WriteStringValue(receipt);
        writer.WriteEndArray();
        if (acceptance.Evidence is { } evidence)
        {
            writer.WritePropertyName("evidence");
            WritePort(writer, evidence);
        }

        // Written only where set.
        if (acceptance.Outcome is { } outcome) writer.WriteString("outcome", outcome);
        WritePorts(writer, "outputs", acceptance.Outputs);
        writer.WriteEndObject();
    }

    private static void WritePorts(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, NodePort> ports)
    {
        writer.WriteStartObject(name);
        foreach (var (port, value) in ports)
        {
            writer.WritePropertyName(port);
            WritePort(writer, value);
        }

        writer.WriteEndObject();
    }

    private static void WritePort(Utf8JsonWriter writer, NodePort port)
    {
        writer.WriteStartObject();
        writer.WriteString("schema", port.Schema);
        writer.WriteString("ref", port.Ref);
        if (port.Revision is { } revision) writer.WriteString("revision", revision);
        if (port.Acceptance is { } acceptance) writer.WriteString("acceptance", acceptance);
        writer.WriteEndObject();
    }

    private static void WriteAttempt(Utf8JsonWriter writer, NodeAttempt attempt)
    {
        writer.WriteStartObject();
        writer.WriteString("node", attempt.Node);
        writer.WriteNumber("attempt", attempt.Attempt);
        writer.WriteString("plugin", attempt.Plugin);
        writer.WriteString("state", attempt.State);
        writer.WriteStartObject("call");
        writer.WriteString("unit", attempt.Call.Unit);
        writer.WriteString("step", attempt.Call.Step);
        writer.WriteNumber("stepVersion", attempt.Call.StepVersion);
        writer.WriteString("invocation", attempt.Call.Invocation);
        writer.WriteString("idempotencyKey", attempt.Call.IdempotencyKey);
        writer.WriteString("subjectRevision", attempt.Call.SubjectRevision);
        writer.WriteStartArray("inputs");
        foreach (var input in attempt.Call.Inputs) writer.WriteStringValue(input);
        writer.WriteEndArray();
        writer.WriteString("deadlineAt", Moments.Write(attempt.Call.DeadlineAt));
        writer.WriteEndObject();
        writer.WriteString("recordedAt", Moments.Write(attempt.RecordedAt));
        if (attempt.Generation is { } generation) writer.WriteNumber("generation", generation);
        writer.WriteNumber("deliveries", attempt.Deliveries);
        if (attempt.Outcome is { } outcome) writer.WriteString("outcome", outcome);
        if (attempt.Code is { } code) writer.WriteString("code", code);
        if (attempt.RetryAt is { } retryAt) writer.WriteString("retryAt", Moments.Write(retryAt));
        if (attempt.AskAt is { } askAt) writer.WriteString("askAt", Moments.Write(askAt));
        if (attempt.Resolution is { } resolution) writer.WriteString("resolution", resolution);
        if (attempt.ResolvedAt is { } resolvedAt) writer.WriteString("resolvedAt", Moments.Write(resolvedAt));
        if (attempt.Decision is { } decision) writer.WriteString("decision", decision);

        // A close's token and the quests it names, written only on a close's attempt.
        if (attempt.Close is { } close)
        {
            writer.WriteStartObject("close");
            writer.WriteString("token", close.Token);
            writer.WriteStartArray("quests");
            foreach (var quest in close.Quests)
            {
                writer.WriteStartObject();
                writer.WriteString("id", quest.Id);
                writer.WriteString("machine", quest.Machine);
                writer.WriteNumber("sequence", quest.Sequence);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (close.Words is { } words) writer.WriteString("words", words);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// A close's part of an attempt: absent is none, as on every plugin's attempt; present, it reads whole or the attempt
    /// does not read, since a close sent again must be the same close.
    /// </summary>
    private static (bool Reads, QuestCloseCall? Close) ReadClose(JsonElement attempt)
    {
        if (!attempt.TryGetProperty("close", out var close)) return (true, null);
        if (close.ValueKind != JsonValueKind.Object || Text(close, "token") is not { } token
            || !close.TryGetProperty("quests", out var named) || named.ValueKind != JsonValueKind.Array)
        {
            return (false, null);
        }

        var quests = new List<QuestCloseItem>();
        foreach (var quest in named.EnumerateArray())
        {
            if (quest.ValueKind != JsonValueKind.Object || Text(quest, "id") is not { } id || Text(quest, "machine") is not { } machine
                || !quest.TryGetProperty("sequence", out var sequence) || sequence.ValueKind != JsonValueKind.Number || !sequence.TryGetInt64(out var number))
            {
                return (false, null);
            }

            quests.Add(new QuestCloseItem(id, machine, number));
        }

        return (true, new QuestCloseCall(token, quests) { Words = Text(close, "words") });
    }

    /// <summary>
    /// A record's file read back whole: null where it is not JSON, where any part misses what that part always says or holds it as another
    /// type (a container absent, a list holding a number, a state no rule knows), or where the record does not agree with itself
    /// (<see cref="Consistent"/>). A record's structure is never defaulted to empty: <c>{"unit":"u"}</c> does not read.
    /// </summary>
    public static NodeRecord? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Text(root, "unit") is not { } unit || Properties(root, "nodes") is not { } nodes
                || Items(root, "attempts") is not { } attemptItems || Items(root, "history") is not { } historyItems || Texts(root, "fired") is not { } fired)
            {
                return null;
            }

            var statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal);
            foreach (var each in nodes)
            {
                if (ReadStatus(each.Value) is not { } status || status.Node != each.Name) return null;
                statuses[each.Name] = status;
            }

            var attempts = new List<NodeAttempt>();
            foreach (var each in attemptItems)
            {
                if (ReadAttempt(each) is not { } attempt) return null;
                attempts.Add(attempt);
            }

            var history = new List<NodeHistory>();
            foreach (var each in historyItems)
            {
                if (Moments.Read(each, "at") is not { } at || Text(each, "why") is not { } why
                    || !each.TryGetProperty("was", out var was) || ReadStatus(was) is not { } status)
                {
                    return null;
                }

                history.Add(new NodeHistory(at, why, status));
            }

            // Absent where none were taken, and read whole where present.
            var observations = new List<ObservedEvent>();
            if (root.TryGetProperty("observations", out var observed))
            {
                if (observed.ValueKind != JsonValueKind.Array) return null;
                foreach (var each in observed.EnumerateArray())
                {
                    if (each.ValueKind != JsonValueKind.Object || Text(each, "plugin") is not { } plugin || Text(each, "repository") is not { } repository
                        || Text(each, "step") is not { } step || Text(each, "commit") is not { } commit || Text(each, "id") is not { } id
                        || Text(each, "state") is not { } state || Moments.Read(each, "at") is not { } at || Text(each, "node") is not { } node)
                    {
                        return null;
                    }

                    // Absent on every unit's own; present, it is text or the record does not read.
                    if (each.TryGetProperty("subject", out var told) && told.ValueKind != JsonValueKind.String) return null;
                    observations.Add(new ObservedEvent(plugin, repository, step, commit, id, state, at, node) { Subject = Text(each, "subject") });
                }
            }

            // A run's end, absent on every unit's own; present, it reads whole.
            RunEnd? ended = null;
            if (root.TryGetProperty("ended", out var end))
            {
                if (end.ValueKind != JsonValueKind.Object || Text(end, "quest") is not { } quest || Moments.Read(end, "at") is not { } endedAt) return null;
                ended = new RunEnd(quest, endedAt);
            }

            var pending = new List<NodeWord>();
            if (root.TryGetProperty("pending", out var words))
            {
                if (words.ValueKind != JsonValueKind.Array) return null;
                foreach (var each in words.EnumerateArray())
                {
                    if (NodeWordFiles.Read(each) is not { } word) return null;
                    pending.Add(word);
                }
            }

            // The repairs a carried Accept waits on, absent wherever none waits; present, each reads whole.
            var repairs = new List<NodeRepair>();
            if (root.TryGetProperty("repairs", out var kept))
            {
                if (kept.ValueKind != JsonValueKind.Array) return null;
                foreach (var each in kept.EnumerateArray())
                {
                    if (each.ValueKind != JsonValueKind.Object || Text(each, "node") is not { } node || Text(each, "tip") is not { } tip
                        || Text(each, "turn") is not { } turn || Moments.Read(each, "at") is not { } at
                        || !each.TryGetProperty("carries", out var carries) || !carries.TryGetInt32(out var count))
                    {
                        return null;
                    }

                    repairs.Add(new NodeRepair(node, tip, turn, count, at));
                }
            }

            return Consistent(unit, statuses, attempts)
                ? new NodeRecord(unit)
                {
                    Statuses = statuses.ToImmutableSortedDictionary(StringComparer.Ordinal), Attempts = attempts, Fired = fired, History = history,
                    Observations = observations, Ended = ended, Pending = pending, Repairs = repairs,
                }
                : null;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether a record agrees with itself: every status and every attempt's call on its unit; each invocation once;
    /// each attempt's node holding a status, never numbered past it; and a node with attempts holding the one its status names, its current
    /// one, which trimming always keeps. A node with no attempts is a built-in's, whose attempts are not dispatched and not kept here.
    /// A run's attempt is called under the subject's unit it is sent for, never <c>run:</c>, so
    /// on a run's record a call names a unit of its own.
    /// </summary>
    private static bool Consistent(string unit, IReadOnlyDictionary<string, NodeStatus> statuses, IReadOnlyList<NodeAttempt> attempts)
    {
        if (statuses.Values.Any(status => !string.Equals(status.Unit, unit, StringComparison.Ordinal))) return false;
        var invocations = new HashSet<string>(StringComparer.Ordinal);
        var run = RunUnits.Is(unit);
        foreach (var attempt in attempts)
        {
            if (!invocations.Add(attempt.Call.Invocation)
                || !(run ? attempt.Call.Unit.Length > 0 : string.Equals(attempt.Call.Unit, unit, StringComparison.Ordinal)))
            {
                return false;
            }

            if (!statuses.TryGetValue(attempt.Node, out var status) || attempt.Attempt > status.Attempt) return false;
        }

        return attempts.GroupBy(attempt => attempt.Node, StringComparer.Ordinal)
            .All(node => node.Any(attempt => attempt.Attempt == statuses[node.Key].Attempt));
    }

    /// <summary>An array the record always writes, or null where it is absent or another type.</summary>
    private static List<JsonElement>? Items(JsonElement element, string name) =>
        element.TryGetProperty(name, out var items) && items.ValueKind == JsonValueKind.Array ? [.. items.EnumerateArray()] : null;

    /// <summary>An object the record always writes, its properties, or null where it is absent or another type.</summary>
    private static List<JsonProperty>? Properties(JsonElement element, string name) =>
        element.TryGetProperty(name, out var items) && items.ValueKind == JsonValueKind.Object ? [.. items.EnumerateObject()] : null;

    /// <summary>An array of texts the record always writes, or null where it is absent, another type, or holds anything but a text.</summary>
    private static List<string>? Texts(JsonElement element, string name) =>
        Items(element, name) is { } items && items.All(item => item.ValueKind == JsonValueKind.String) ? [.. items.Select(item => item.GetString()!)] : null;

    private static int? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

    private static NodeStatus? ReadStatus(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || Text(element, "unit") is not { } unit || Text(element, "node") is not { } node
            || Text(element, "state") is not { } state || !NodeStates.All.Contains(state, StringComparer.Ordinal)
            || Number(element, "attempt") is not { } attempt || Number(element, "tries") is not { } tries)
        {
            return null;
        }

        NodeAcceptance? binding = null, acceptance = null;
        if (element.TryGetProperty("binding", out var bound) && (binding = ReadAcceptance(bound)) is null) return null;
        if (element.TryGetProperty("acceptance", out var accepted) && (acceptance = ReadAcceptance(accepted)) is null) return null;
        NodeScope? scope = null;
        if (element.TryGetProperty("scope", out var scoped))
        {
            if (Text(scoped, "graph") is not { } graph || Text(scoped, "run") is not { } run) return null;
            scope = new NodeScope(graph, run);
        }

        return new NodeStatus(unit, node, state)
        {
            Attempt = attempt,
            Tries = tries,
            Unreconciled = Number(element, "unreconciled"),
            Binding = binding,
            Scope = scope,
            Acceptance = acceptance,
            By = Text(element, "by"),
            Code = Text(element, "code"),
            Transient = element.TryGetProperty("transient", out var transient) && transient.ValueKind == JsonValueKind.True,
            Words = Text(element, "words"),
            Since = Moments.Read(element, "since"),
            Holds = Text(element, "holds"),
        };
    }

    private static NodeAcceptance? ReadAcceptance(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || Text(element, "unit") is not { } unit || Text(element, "graph") is not { } graph
            || Text(element, "node") is not { } node || Number(element, "version") is not { } version || Number(element, "attempt") is not { } attempt
            || ReadPorts(element, "inputs") is not { } inputs || ReadPorts(element, "outputs") is not { } outputs || Texts(element, "receipts") is not { } receipts)
        {
            return null;
        }

        NodePort? evidence = null;
        if (element.TryGetProperty("evidence", out var evidenced) && (evidence = ReadPort(evidenced)) is null) return null;
        return new NodeAcceptance(unit, Text(element, "revision"), graph, node, version, attempt)
        {
            // Absent where the node observes its own unit.
            Subject = Text(element, "subject"),
            Inputs = inputs,
            PrerequisiteReceipts = receipts,
            Evidence = evidence,
            Outputs = outputs,
            Outcome = Text(element, "outcome"),
        };
    }

    private static Dictionary<string, NodePort>? ReadPorts(JsonElement element, string name)
    {
        if (Properties(element, name) is not { } properties) return null;
        var ports = new Dictionary<string, NodePort>(StringComparer.Ordinal);
        foreach (var each in properties)
        {
            if (ReadPort(each.Value) is not { } port) return null;
            ports[each.Name] = port;
        }

        return ports;
    }

    private static NodePort? ReadPort(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object && Text(element, "schema") is { } schema && Text(element, "ref") is { } reference
            ? new NodePort(schema, reference, Text(element, "revision"), Text(element, "acceptance"))
            : null;

    private static NodeAttempt? ReadAttempt(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || Text(element, "node") is not { } node || Number(element, "attempt") is not { } number
            || Text(element, "plugin") is not { } plugin || Text(element, "state") is not { } state || !AttemptStates.All.Contains(state, StringComparer.Ordinal)
            || !element.TryGetProperty("call", out var call) || call.ValueKind != JsonValueKind.Object
            || Text(call, "unit") is not { } unit || Text(call, "step") is not { } step || Number(call, "stepVersion") is not { } stepVersion
            || Text(call, "invocation") is not { } invocation || Text(call, "idempotencyKey") is not { } key
            || Text(call, "subjectRevision") is not { } revision || Moments.Read(call, "deadlineAt") is not { } deadline || Texts(call, "inputs") is not { } inputs
            || Moments.Read(element, "recordedAt") is not { } recordedAt || Number(element, "deliveries") is not { } deliveries
            || ReadClose(element) is not (true, var close))
        {
            return null;
        }

        // The person's word, absent wherever none is given; one no rule knows does not read, as a state's does.
        var resolution = Text(element, "resolution");
        if (resolution is not null && !HeldResolutions.All.Contains(resolution, StringComparer.Ordinal)) return null;

        return new NodeAttempt(node, number, plugin, new WorkPerformCall(unit, step, stepVersion, invocation, key, revision, inputs, deadline), state)
        {
            RecordedAt = recordedAt,
            Generation = Number(element, "generation"),
            Deliveries = deliveries,
            Outcome = Text(element, "outcome"),
            Code = Text(element, "code"),
            RetryAt = Moments.Read(element, "retryAt"),
            AskAt = Moments.Read(element, "askAt"),
            Resolution = resolution,
            ResolvedAt = Moments.Read(element, "resolvedAt"),
            Decision = Text(element, "decision"),
            Close = close,
        };
    }
}
