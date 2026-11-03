using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// One part's entry in a home's conversion receipt (ONEWAY1ae; the oneway design §1.1): its pressed digest, its outcome
/// (<see cref="ConversionOutcomes"/>) with its sentence, its operations, and <see cref="Body"/>, what the part keeps to go on and to undo.
/// </summary>
public sealed record PartReceipt(string Part, string Digest, string Outcome, IReadOnlyList<ConversionOp> Operations)
{
    /// <summary>The part's sentence on its outcome: why it failed, moved or was left, or a note on what it did.</summary>
    public string? Said { get; init; }

    public JsonObject? Body { get; init; }

    public ConversionOp? Of(string id) => Operations.FirstOrDefault(each => each.Id == id);

    public PartReceipt With(string id, Func<ConversionOp, ConversionOp> change) =>
        this with { Operations = [.. Operations.Select(each => each.Id == id ? change(each) : each)] };

    /// <summary>Whether a converting operation is not done yet, which holds every clear of the receipt.</summary>
    public bool Converting => Outcome is not (ConversionOutcomes.Moved or ConversionOutcomes.Left)
        && (Operations.Count == 0 ? Outcome != ConversionOutcomes.Done : Operations.Any(each => !each.Clear && each.Outcome != ConversionOutcomes.Done));

    /// <summary>
    /// The outcome its operations come to: an uncertain one makes it uncertain, a failed one failed, all done done, else pending. A part closed
    /// <c>moved</c> or <c>left</c>, or written with no operations, keeps what it holds.
    /// </summary>
    public PartReceipt Derived()
    {
        if (Outcome is ConversionOutcomes.Moved or ConversionOutcomes.Left || Operations.Count == 0) return this;
        var outcome = Operations.Any(each => each.Outcome == ConversionOutcomes.Uncertain) ? ConversionOutcomes.Uncertain
            : Operations.Any(each => each.Outcome == ConversionOutcomes.Failed) ? ConversionOutcomes.Failed
            : Operations.All(each => each.Outcome == ConversionOutcomes.Done) ? ConversionOutcomes.Done
            : ConversionOutcomes.Pending;
        return this with { Outcome = outcome };
    }
}

/// <summary>
/// A home's conversion receipt: one press of every part together, written before the first effect and at each outcome, holding each part's
/// entry, the row's choices and <see cref="Build"/>, the build that applied it (the floor's source, oneway design §1.3).
/// </summary>
public sealed record HomeReceipt(string Digest, DateTimeOffset At, ConversionChoices Choices, IReadOnlyList<PartReceipt> Parts)
{
    /// <summary>The version of the build that applied it, as stamped; null on a receipt written before parts, which gives no floor.</summary>
    public string? Build { get; init; }

    /// <summary>Set when <i>Undo</i> put its parts back: it no longer stands.</summary>
    public DateTimeOffset? UndoneAt { get; init; }

    public bool Undone => UndoneAt is not null;

    public PartReceipt? Of(string part) => Parts.FirstOrDefault(each => each.Part == part);

    public HomeReceipt With(PartReceipt entry) => this with { Parts = [.. Parts.Select(each => each.Part == entry.Part ? entry : each)] };

    /// <summary>Settled once no part is failed, uncertain or pending: what Undo waits for.</summary>
    public bool Settled => Parts.All(each => each.Outcome is ConversionOutcomes.Done or ConversionOutcomes.Moved or ConversionOutcomes.Left);

    /// <summary>Whether every part is done.</summary>
    public bool Done => Parts.All(each => each.Outcome == ConversionOutcomes.Done);

    /// <summary>Whether it stands (the rollback refusal's read): not undone, and some part done or uncertain.</summary>
    public bool Stands => !Undone && Parts.Any(each => each.Outcome is ConversionOutcomes.Done or ConversionOutcomes.Uncertain);
}

/// <summary>
/// A home's conversion receipts, beside a place's (<see cref="HomeEntry.PlaceReceipts"/>), each <c>conversion-&lt;digest&gt;.json</c> written
/// atomically, BOM-less and LF. A file that does not read is never replaced: the apply it belongs to is said to be unread.
/// </summary>
public sealed class HomeReceipts(string home)
{
    private static readonly JsonSerializerOptions Format = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string PathOf(string digest) => DaorisHome.Entry(home, HomeEntry.PlaceReceipts, $"conversion-{digest}.json");

    /// <exception cref="DriverException">A file is there and does not read.</exception>
    public HomeReceipt? Read(string digest) => ReadFile(PathOf(digest), digest);

    private static HomeReceipt? ReadFile(string path, string digest)
    {
        if (!File.Exists(path)) return null;
        try
        {
            if (JsonNode.Parse(AtomicFile.ReadText(path)) is not JsonObject root) throw new JsonException("not an object");
            if (root.ContainsKey("parts")) return root.Deserialize<HomeReceipt>(Format) ?? throw new JsonException("empty");
            // transitional: conversion-receipt-before-parts
            return WorkflowPart.Wrapped(root.Deserialize<ConversionReceipt>(Format) ?? throw new JsonException("empty"));
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            throw new DriverException($"the receipt of the conversion `{digest[..Math.Min(12, digest.Length)]}` does not read ({error.Message.TrimEnd('.', ' ')}): it is read by hand, never replaced.");
        }
    }

    public void Write(HomeReceipt receipt)
    {
        var path = PathOf(receipt.Digest);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteText(path, JsonSerializer.Serialize(receipt, Format).ReplaceLineEndings("\n") + "\n");
    }

    /// <summary>Every receipt that reads, oldest first, and how many files did not read (left as they are, said when their apply is pressed).</summary>
    public (IReadOnlyList<HomeReceipt> Read, int Unread) Each()
    {
        var folder = DaorisHome.Entry(home, HomeEntry.PlaceReceipts);
        if (!Directory.Exists(folder)) return ([], 0);
        var found = new List<HomeReceipt>();
        var unread = 0;
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
                unread++;
            }
        }

        return ([.. found.OrderBy(each => each.At)], unread);
    }

    /// <summary>Every receipt that reads, oldest first.</summary>
    public IReadOnlyList<HomeReceipt> All() => Each().Read;

    /// <summary>The home's latest conversion, undone or not; null where none was applied.</summary>
    public HomeReceipt? Latest() => All().LastOrDefault();

    /// <summary>Whether a receipt on <paramref name="home"/> was written before parts, which only the old reader reads (the register's end check).</summary>
    public static bool AnyBeforeParts(string home)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.PlaceReceipts);
        if (!Directory.Exists(folder)) return false;
        foreach (var file in Directory.EnumerateFiles(folder, "conversion-*.json"))
        {
            try
            {
                if (JsonNode.Parse(AtomicFile.ReadText(file)) is JsonObject root && !root.ContainsKey("parts")) return true;
            }
            catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
            {
                return true;
            }
        }

        return false;
    }
}
