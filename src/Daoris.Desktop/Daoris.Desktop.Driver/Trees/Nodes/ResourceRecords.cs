using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>Where a receipt's cleanup stands (FABRIC-PERFORM1d; the fabric events design §D3), as its unit's resource record keeps it.</summary>
public static class CleanupStates
{
    /// <summary>Written with its key before the first send, and asked again with the same key until an answer ends it.</summary>
    public const string Requested = "requested";

    /// <summary>Its plugin's answer ended it: removed, already gone, or retained with its reason (<see cref="ResourceCleanup.Disposition"/>).</summary>
    public const string Settled = "settled";

    public static readonly IReadOnlyList<string> All = [Requested, Settled];
}

/// <summary>
/// A receipt's cleanup (§D3's <c>cleanup: { state: requested, key, at, tries }</c>), persisted before it is sent: its key never changes, so a
/// restart or a lost answer sends the same request again and the plugin answers <c>already-gone</c> for what it already removed.
/// </summary>
/// <param name="State">One of <see cref="CleanupStates"/>.</param>
/// <param name="Key">The idempotency key, the same at every delivery.</param>
/// <param name="At">When it was first requested: what <see cref="ResourceCleaner.BlockedAfter"/> counts from.</param>
/// <param name="Tries">How many deliveries were answered with a failure, or not answered.</param>
public sealed record ResourceCleanup(string State, string Key, DateTimeOffset At, int Tries)
{
    /// <summary>When it is asked again after a failure (<see cref="ResourceCleaner.DelayAfter"/>), or null where it is asked at once.</summary>
    public DateTimeOffset? NextAt { get; init; }

    /// <summary>The last answer's disposition (<see cref="CleanupDispositions"/>): a settled one's ends it; <c>failed</c> is tried again.</summary>
    public string? Disposition { get; init; }

    /// <summary>The plugin's proof of a removal, as it gave it.</summary>
    public string? Proof { get; init; }

    /// <summary>The plugin's words with its last answer: a retention's reason, a failure's message.</summary>
    public string? Message { get; init; }

    /// <summary>When an answer ended it.</summary>
    public DateTimeOffset? SettledAt { get; init; }
}

/// <summary>
/// One ownership receipt (FABRIC-PERFORM1d; §D3's <i>what a plugin step created</i>): what a plugin said one of its attempts created, by the
/// schema its manifest declares and the opaque id it issued, kept on its unit's resource record with the attempt it came from. Only the
/// plugin removes it, through <c>work/cleanup</c>; Daoris runs nothing for it and never holds a path.
/// </summary>
/// <param name="Id">Daoris's id for the receipt (<c>r-</c> and hex), the <c>receipt</c> its cleanup names.</param>
/// <param name="Plugin">The plugin that created it: the one sender of its completion (§C4's first rule), and the one asked to clean it.</param>
/// <param name="Schema">What it is, as the plugin's manifest declares it in <c>resources</c>.</param>
/// <param name="Resource">The plugin's own opaque id for it.</param>
/// <param name="Role">Its declared role (<see cref="PluginResourceRoles"/>), or <see cref="ResourceReceipt.Undeclared"/> where the manifest declares no such schema.</param>
/// <param name="Node">The node whose attempt created it.</param>
/// <param name="Attempt">That attempt's number on its node.</param>
/// <param name="Invocation">That attempt's invocation.</param>
/// <param name="TakenAt">When the receipt was first taken.</param>
public sealed record ResourceReceipt(
    string Id, string Plugin, string Schema, string Resource, string Role, string Node, int Attempt, string Invocation, DateTimeOffset TakenAt)
{
    /// <summary>
    /// The role of a receipt whose schema its plugin's manifest does not declare: no rule says who cleans it, so it is never cleaned by itself
    /// and blocks its unit's retirement, said to the person (§D3: an unknown owner blocks, never a guess).
    /// </summary>
    public const string Undeclared = "undeclared";

    /// <summary>
    /// Whether the attempt that created it was superseded (§D3, <i>invalidation keeps resource accountability</i>): its completion came after
    /// its node had moved off it, so it has no result, and what it made is temporary whatever role it declared, cleaned at once.
    /// </summary>
    public bool Superseded { get; init; }

    public ResourceCleanup? Cleanup { get; init; }

    /// <summary>The role it is cleaned by: temporary for a superseded attempt's, else as declared.</summary>
    public string Effective => Superseded && Role != Undeclared ? PluginResourceRoles.Temporary : Role;

    /// <summary>Whether its cleanup ended: an answer of removed, already gone or retained.</summary>
    public bool Settled => Cleanup is { State: CleanupStates.Settled };
}

/// <summary>A unit's resource record (FABRIC-PERFORM1d): the receipts its plugins' attempts gave, oldest first.</summary>
/// <param name="Unit">The unit (<see cref="WorkUnit.Id"/>).</param>
public sealed record ResourceRecord(string Unit)
{
    /// <summary>How many receipts one unit keeps at most (§D3's <c>Resources</c>, at most 32): a completion that would pass it is held.</summary>
    public const int ReceiptsKept = 32;

    public IReadOnlyList<ResourceReceipt> Receipts { get; init; } = [];

    /// <summary>The receipt a plugin already gave for one of its resources, or null: a replay takes none twice.</summary>
    public ResourceReceipt? Of(string plugin, string schema, string resource) => Receipts.FirstOrDefault(receipt =>
        string.Equals(receipt.Plugin, plugin, StringComparison.Ordinal) && string.Equals(receipt.Schema, schema, StringComparison.Ordinal)
        && string.Equals(receipt.Resource, resource, StringComparison.Ordinal));
}

/// <summary>
/// The units' resource records under a home (FABRIC-PERFORM1d): one file per unit, <c>&lt;home&gt;/workflows/resources/&lt;key&gt;.json</c>,
/// its key the unit's node record's (<see cref="NodeRecordFiles.KeyOf"/>). A folder of its own, not a field on the node record: a build
/// before this one writes that record whole and would drop a field it never read, losing the receipts (the knowledge note
/// <c>compatible-builds</c>); none of those builds looks here. Changed only by <see cref="Edit{T}"/>, as the node records are: read, judged
/// and written whole in one hold of the record's lock with the lease asked inside it, atomically, BOM-less and LF; machine-local, never on
/// the wire. A file that does not read is never taken for an empty record: a receipt it holds would never be cleaned.
/// </summary>
public sealed class ResourceRecordFiles(string home, Func<bool>? holdsLease = null)
{
    public const string Unreadable = NodeRecordFiles.Unreadable;

    public const string NotHeld = NodeRecordFiles.NotHeld;

    private readonly Func<bool> _holdsLease = holdsLease ?? (() => HomeLease.HeldHere(home));

    public string Home => home;

    public static string FolderOf(string home) => DaorisHome.Entry(home, HomeEntry.Workflows, "resources");

    public static string PathOf(string home, string unit) => Path.Combine(FolderOf(home), NodeRecordFiles.KeyOf(unit) + ".json");

    /// <summary>The unit's record: an empty one where it has none, null where its file does not read.</summary>
    public ResourceRecord? Read(string unit)
    {
        var path = PathOf(home, unit);
        if (!File.Exists(path)) return new ResourceRecord(unit);
        try
        {
            return Parse(AtomicFile.ReadText(path)) is { } record && string.Equals(record.Unit, unit, StringComparison.Ordinal) ? record : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Every record that reads, and the files that do not (by name).</summary>
    public (IReadOnlyList<ResourceRecord> Records, IReadOnlyList<string> Unreadable) All()
    {
        var folder = FolderOf(home);
        if (!Directory.Exists(folder)) return ([], []);
        var records = new List<ResourceRecord>();
        var unreadable = new List<string>();
        foreach (var path in Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            ResourceRecord? record = null;
            try
            {
                record = Parse(AtomicFile.ReadText(path));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Said below, as a file that does not read.
            }

            if (record is not null && string.Equals(Path.GetFileNameWithoutExtension(path), NodeRecordFiles.KeyOf(record.Unit), StringComparison.Ordinal)) records.Add(record);
            else unreadable.Add(Path.GetFileName(path));
        }

        return (records, unreadable);
    }

    /// <summary>
    /// One change of a unit's record, as <see cref="NodeRecordFiles.Edit{T}"/> is: the lock held, the lease asked, the record read as it is
    /// now, <paramref name="change"/> judged on it and what it returns written whole; a null record writes nothing. Nothing is awaited inside.
    /// </summary>
    /// <returns>Why nothing was judged (<see cref="NotHeld"/>, <see cref="Unreadable"/>), else null with what the change said.</returns>
    public (string? Refused, T? Result) Edit<T>(string unit, Func<ResourceRecord, (ResourceRecord? Next, T Result)> change)
    {
        var path = PathOf(home, unit);
        using (RecordLock.Take(RecordLock.PathOf(home, "resources-" + NodeRecordFiles.KeyOf(unit))))
        {
            if (!_holdsLease()) return (NotHeld, default);
            if (Read(unit) is not { } record) return (Unreadable, default);
            var (next, result) = change(record);
            if (next is null) return (null, result);
            if (!string.Equals(next.Unit, unit, StringComparison.Ordinal)) throw new InvalidOperationException($"a change of unit `{unit}`'s resource record named another unit");
            Directory.CreateDirectory(FolderOf(home));
            AtomicFile.WriteText(path, ToJson(next));
            HomeWrites.Mine(path);
            return (null, result);
        }
    }

    /// <summary>A whole record put over its unit's, through <see cref="Edit{T}"/>: a test's. Without the lease or over a file that does not read, it throws.</summary>
    public void Write(ResourceRecord record)
    {
        if (Edit(record.Unit, _ => (record, true)).Refused is { } refused)
        {
            throw new InvalidOperationException($"unit `{record.Unit}`'s resource record was not written: {refused}");
        }
    }

    // ——— the file's format

    public static string ToJson(ResourceRecord record) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("unit", record.Unit);
        writer.WriteStartArray("receipts");
        foreach (var receipt in record.Receipts)
        {
            writer.WriteStartObject();
            writer.WriteString("id", receipt.Id);
            writer.WriteString("plugin", receipt.Plugin);
            writer.WriteString("schema", receipt.Schema);
            writer.WriteString("resource", receipt.Resource);
            writer.WriteString("role", receipt.Role);
            writer.WriteString("node", receipt.Node);
            writer.WriteNumber("attempt", receipt.Attempt);
            writer.WriteString("invocation", receipt.Invocation);
            writer.WriteString("takenAt", Moments.Write(receipt.TakenAt));
            if (receipt.Superseded) writer.WriteBoolean("superseded", true);
            if (receipt.Cleanup is { } cleanup)
            {
                writer.WriteStartObject("cleanup");
                writer.WriteString("state", cleanup.State);
                writer.WriteString("key", cleanup.Key);
                writer.WriteString("at", Moments.Write(cleanup.At));
                writer.WriteNumber("tries", cleanup.Tries);
                if (cleanup.NextAt is { } next) writer.WriteString("nextAt", Moments.Write(next));
                if (cleanup.Disposition is { } disposition) writer.WriteString("disposition", disposition);
                if (cleanup.Proof is { } proof) writer.WriteString("proof", proof);
                if (cleanup.Message is { } message) writer.WriteString("message", message);
                if (cleanup.SettledAt is { } settled) writer.WriteString("settledAt", Moments.Write(settled));
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    /// <summary>
    /// A record's file read back whole: null where it is not JSON, where a receipt misses what it always says, holds a state or a disposition
    /// no rule knows, or names a receipt or a resource twice. Never defaulted to empty: <c>{"unit":"u"}</c> does not read.
    /// </summary>
    public static ResourceRecord? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Text(root, "unit") is not { } unit
                || !root.TryGetProperty("receipts", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var receipts = new List<ResourceReceipt>();
            foreach (var each in items.EnumerateArray())
            {
                if (ReadReceipt(each) is not { } receipt) return null;
                receipts.Add(receipt);
            }

            var twice = receipts.GroupBy(receipt => receipt.Id, StringComparer.Ordinal).Any(group => group.Count() > 1)
                || receipts.GroupBy(receipt => $"{receipt.Plugin}\n{receipt.Schema}\n{receipt.Resource}", StringComparer.Ordinal).Any(group => group.Count() > 1);
            return twice ? null : new ResourceRecord(unit) { Receipts = receipts };
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static ResourceReceipt? ReadReceipt(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || Text(element, "id") is not { } id || Text(element, "plugin") is not { } plugin
            || Text(element, "schema") is not { } schema || Text(element, "resource") is not { } resource || Text(element, "role") is not { } role
            || (role != ResourceReceipt.Undeclared && !PluginResourceRoles.All.Contains(role, StringComparer.Ordinal))
            || Text(element, "node") is not { } node || !element.TryGetProperty("attempt", out var number) || number.ValueKind != JsonValueKind.Number
            || !number.TryGetInt32(out var attempt) || Text(element, "invocation") is not { } invocation || Moments.Read(element, "takenAt") is not { } takenAt)
        {
            return null;
        }

        ResourceCleanup? cleanup = null;
        if (element.TryGetProperty("cleanup", out var said))
        {
            if (said.ValueKind != JsonValueKind.Object || Text(said, "state") is not { } state || !CleanupStates.All.Contains(state, StringComparer.Ordinal)
                || Text(said, "key") is not { } key || Moments.Read(said, "at") is not { } at
                || !said.TryGetProperty("tries", out var tried) || tried.ValueKind != JsonValueKind.Number || !tried.TryGetInt32(out var tries))
            {
                return null;
            }

            var disposition = Text(said, "disposition");
            if (disposition is not null && !CleanupDispositions.All.Contains(disposition, StringComparer.Ordinal)) return null;
            if (state == CleanupStates.Settled && (disposition is null || !CleanupDispositions.Terminal(disposition))) return null;
            cleanup = new ResourceCleanup(state, key, at, tries)
            {
                NextAt = Moments.Read(said, "nextAt"),
                Disposition = disposition,
                Proof = Text(said, "proof"),
                Message = Text(said, "message"),
                SettledAt = Moments.Read(said, "settledAt"),
            };
        }

        return new ResourceReceipt(id, plugin, schema, resource, role, node, attempt, invocation, takenAt)
        {
            Superseded = element.TryGetProperty("superseded", out var superseded) && superseded.ValueKind == JsonValueKind.True,
            Cleanup = cleanup,
        };
    }
}
