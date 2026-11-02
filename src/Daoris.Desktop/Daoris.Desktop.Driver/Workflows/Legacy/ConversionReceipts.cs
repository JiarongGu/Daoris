using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The outcomes a conversion's receipt keeps for an operation, in the words a place's receipt uses; one in flight is <see cref="Uncertain"/> until it is known.</summary>
public static class ConversionOutcomes
{
    public const string Pending = "pending";
    public const string Uncertain = "uncertain";
    public const string Done = "done";
    public const string Failed = "failed";

    /// <summary>Whether a word is a plan's digest: 64 lower-case hex.</summary>
    public static bool IsDigest(string? value) => value is { Length: 64 } && value.All(each => each is >= '0' and <= '9' or >= 'a' and <= 'f');
}

/// <summary>One operation of an applied conversion and where it stands (<see cref="ConversionOutcomes"/>): a workflow save, or the one edit of <c>driver.json</c>.</summary>
public sealed record ConversionOp(string Id, string Kind, string Said, string Outcome)
{
    public string? Note { get; init; }
}

/// <summary>
/// What the one edit replaced, kept so <i>Undo</i> can put it back: the removed rules, access entries and session checks as they were, each touched
/// place's old choice as <c>driver.json</c> wrote it (null where it had none), and the default workflow and line policies the conversion wrote.
/// </summary>
public sealed record ConversionBefore(
    string? DefaultWorkflow, Dictionary<string, LandingRule> Landings, Dictionary<string, string> Access, Dictionary<string, string> SessionChecks,
    Dictionary<string, string?> Choices);

/// <summary>
/// An applied conversion as it was authorized (the cleanup design §4.4): written before the first effect and at each outcome, so a restart reads what
/// is done, failed or uncertain; it carries the operations themselves, so a restart goes on without planning again.
/// </summary>
public sealed record ConversionReceipt(
    string Digest, DateTimeOffset At, ConversionChoices Choices, IReadOnlyList<ConvertSave> Saves, IReadOnlyList<ConvertUse> Uses, string? DefaultWorkflow,
    IReadOnlyList<ConvertPolicy> Policies, IReadOnlyList<ConvertRemoval> Removals, IReadOnlyList<string> Held, IReadOnlyList<ConversionOp> Operations)
{
    public ConversionBefore? Before { get; init; }

    /// <summary>Set when <i>Undo</i> put the keys back: the conversion no longer stands.</summary>
    public DateTimeOffset? UndoneAt { get; init; }

    public bool Undone => UndoneAt is not null;

    public ConversionOp? Of(string id) => Operations.FirstOrDefault(each => each.Id == id);

    public ConversionReceipt With(string id, Func<ConversionOp, ConversionOp> change) =>
        this with { Operations = [.. Operations.Select(each => each.Id == id ? change(each) : each)] };

    /// <summary>Whether every operation is done: the conversion stands unless it was undone.</summary>
    public bool Applied => Operations.All(each => each.Outcome == ConversionOutcomes.Done);
}

/// <summary>
/// The conversions' receipts in a home, beside a place's (<see cref="HomeEntry.PlaceReceipts"/>), each <c>conversion-&lt;digest&gt;.json</c>, written
/// atomically, BOM-less and LF. A file that does not read is never replaced: the apply it belongs to is said to be unread.
/// </summary>
public sealed class ConversionReceipts(string home)
{
    private static readonly JsonSerializerOptions Format = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string PathOf(string digest) => DaorisHome.Entry(home, HomeEntry.PlaceReceipts, $"conversion-{digest}.json");

    /// <exception cref="DriverException">A file is there and does not read.</exception>
    public ConversionReceipt? Read(string digest) => ReadFile(PathOf(digest), digest);

    private static ConversionReceipt? ReadFile(string path, string digest)
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<ConversionReceipt>(AtomicFile.ReadText(path), Format)
                ?? throw new DriverException($"the receipt of the conversion `{digest[..12]}` is empty: it is read by hand, never replaced.");
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new DriverException($"the receipt of the conversion `{digest[..12]}` does not read ({error.Message.TrimEnd('.', ' ')}): it is read by hand, never replaced.");
        }
    }

    public void Write(ConversionReceipt receipt)
    {
        var path = PathOf(receipt.Digest);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteText(path, JsonSerializer.Serialize(receipt, Format).ReplaceLineEndings("\n") + "\n");
    }

    /// <summary>Every receipt that reads, oldest first; one that does not is left as it is.</summary>
    public IReadOnlyList<ConversionReceipt> All()
    {
        var folder = DaorisHome.Entry(home, HomeEntry.PlaceReceipts);
        if (!Directory.Exists(folder)) return [];
        var found = new List<ConversionReceipt>();
        foreach (var file in Directory.EnumerateFiles(folder, "conversion-*.json").Order(StringComparer.Ordinal))
        {
            var digest = Path.GetFileNameWithoutExtension(file)["conversion-".Length..];
            if (!ConversionOutcomes.IsDigest(digest)) continue;
            try
            {
                if (ReadFile(file, digest) is { } receipt) found.Add(receipt);
            }
            catch (DriverException)
            {
                // Unread: left as it is, and said when its own apply is pressed again.
            }
        }

        return [.. found.OrderBy(each => each.At)];
    }

    /// <summary>The home's latest conversion, undone or not; null where none was applied.</summary>
    public ConversionReceipt? Latest() => All().LastOrDefault();

    /// <summary>The places whose rules an applied conversion left in force for an open Current unit, which a later plan offers to remove.</summary>
    public IReadOnlySet<string> HeldPlaces() =>
        All().Where(each => each.Applied && !each.Undone).SelectMany(each => each.Held).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Whether an applied conversion stands in <paramref name="home"/> (the cleanup design §1.5): what the update's rollback refusal reads. An
    /// unread receipt stands too, since the refusal fails closed.
    /// </summary>
    public static bool Stands(string home)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.PlaceReceipts);
        if (!Directory.Exists(folder) || !Directory.EnumerateFiles(folder, "conversion-*.json").Any()) return false;
        var receipts = new ConversionReceipts(home);
        return receipts.All().Any(each => !each.Undone && each.Of("configuration")?.Outcome is ConversionOutcomes.Done or ConversionOutcomes.Uncertain)
            || Directory.EnumerateFiles(folder, "conversion-*.json").Count() > receipts.All().Count;
    }

    /// <summary>A choice as <c>driver.json</c> keeps it, or null for none.</summary>
    public static string? Wire(WorkflowChoice? choice, bool workspace)
    {
        if (choice is null) return null;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WorkflowSelection.Write(writer, choice, workspace);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>A choice read back from <see cref="Wire"/>.</summary>
    public static WorkflowChoice? Unwire(string? json, bool workspace)
    {
        if (json is null) return null;
        using var document = JsonDocument.Parse(json);
        return WorkflowSelection.Read(document.RootElement, workspace).Choice;
    }
}
