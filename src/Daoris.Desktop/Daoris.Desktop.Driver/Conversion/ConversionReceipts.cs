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

/// <summary>The home's conversion receipts as the update's rollback refusal reads them.</summary>
public static class ConversionReceipts
{
    /// <summary>
    /// Whether an applied conversion stands in <paramref name="home"/> (the oneway design §1.1): what the update's rollback refusal reads. Some receipt
    /// not undone has a part done or uncertain, or a receipt does not read, since the refusal fails closed.
    /// </summary>
    public static bool Stands(string home)
    {
        var (read, unread) = new HomeReceipts(home).Each();
        return unread > 0 || read.Any(each => each.Stands);
    }
}
