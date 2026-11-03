using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The outcomes a conversion's receipt keeps, for an operation in the words a place's receipt uses (one in flight is <see cref="Uncertain"/> until
/// it is known), and for a part (the oneway design §1.1): <see cref="Moved"/> where what was pressed no longer holds, <see cref="Left"/> by the
/// person's <i>Leave it</i>.
/// </summary>
public static class ConversionOutcomes
{
    public const string Pending = "pending";
    public const string Uncertain = "uncertain";
    public const string Done = "done";
    public const string Failed = "failed";
    public const string Moved = "moved";
    public const string Left = "left";

    /// <summary>Whether a word is a plan's digest: 64 lower-case hex.</summary>
    public static bool IsDigest(string? value) => value is { Length: 64 } && value.All(each => each is >= '0' and <= '9' or >= 'a' and <= 'f');
}

/// <summary>One operation of an applied conversion and where it stands (<see cref="ConversionOutcomes"/>): a workflow save, or the one edit of <c>driver.json</c>.</summary>
public sealed record ConversionOp(string Id, string Kind, string Said, string Outcome)
{
    public string? Note { get; init; }

    /// <summary>A clear of finished history, run after every converting operation of the receipt and never undone.</summary>
    public bool Clear { get; init; }
}

/// <summary>
/// What the one edit replaced, kept so <i>Undo</i> can put it back: the removed rules, access entries and session checks as they were, each touched
/// place's old choice as <c>driver.json</c> wrote it (null where it had none), and the default workflow and line policies the conversion wrote.
/// </summary>
public sealed record ConversionBefore(
    string? DefaultWorkflow, Dictionary<string, LandingRule> Landings, Dictionary<string, string> Access, Dictionary<string, string> SessionChecks,
    Dictionary<string, string?> Choices);

/// <summary>
/// The workflow part's entry in a receipt as it was authorized (the cleanup design §4.4): its operations as the receipt keeps them, and what it
/// carries to go on without planning again and to undo. Read from the home's receipt (<see cref="HomeReceipts"/>), where the workflow part is one entry.
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

/// <summary>The workflow part's entries in the home's receipts (<see cref="HomeReceipts"/>), each read as a <see cref="ConversionReceipt"/>.</summary>
public sealed class ConversionReceipts(string home)
{
    private readonly HomeReceipts _receipts = new(home);

    /// <summary>The workflow part of the receipt <paramref name="digest"/> names; null where none is there or it holds no workflow part.</summary>
    /// <exception cref="DriverException">A file is there and does not read.</exception>
    public ConversionReceipt? Read(string digest) => _receipts.Read(digest) is { } receipt ? WorkflowPart.View(receipt) : null;

    /// <summary>Every receipt's workflow part that reads, oldest first; one that does not is left as it is.</summary>
    public IReadOnlyList<ConversionReceipt> All() => [.. _receipts.All().Select(WorkflowPart.View).OfType<ConversionReceipt>()];

    /// <summary>The places whose rules an applied conversion left in force for an open Current unit, which a later plan offers to remove.</summary>
    public IReadOnlySet<string> HeldPlaces() =>
        All().Where(each => each.Applied && !each.Undone).SelectMany(each => each.Held).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Whether an applied conversion stands in <paramref name="home"/> (the oneway design §1.1): what the update's rollback refusal reads. Some receipt
    /// not undone has a part done or uncertain, or a receipt does not read, since the refusal fails closed.
    /// </summary>
    public static bool Stands(string home)
    {
        var (read, unread) = new HomeReceipts(home).Each();
        return unread > 0 || read.Any(each => each.Stands);
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
