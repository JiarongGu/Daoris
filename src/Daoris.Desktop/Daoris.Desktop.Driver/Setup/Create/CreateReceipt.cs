using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A creation as it is authorized: the plan's digest, the ask as it resolved, and each operation's outcome. Written before the first effect
/// and again at each outcome, so a re-press reads what is done, what failed and what is uncertain, and goes on from there. The commit is the
/// empty commit the history made, which is how a re-press knows its folder is this receipt's.
/// </summary>
public sealed record CreateReceipt(
    string Digest, DateTimeOffset At, CreateAsk Ask, string Workspace, string Folder, string? Line, CreateAuthor? Author, string? RemoteStep,
    IReadOnlyList<PlaceOperation> Operations)
{
    public string? Commit { get; init; }

    public PlaceOperation? Of(string id) => Operations.FirstOrDefault(each => each.Id == id);

    public CreateReceipt With(string id, Func<PlaceOperation, PlaceOperation> change) =>
        this with { Operations = [.. Operations.Select(each => each.Id == id ? change(each) : each)] };

    /// <summary>Whether every operation is done.</summary>
    public bool Finished => Operations.All(each => each.Outcome == PlaceOutcomes.Done);
}

/// <summary>
/// The creations' receipts in a home: one file per plan's digest beside the place receipts (<see cref="HomeEntry.PlaceReceipts"/>, in a folder
/// of its own), written atomically, BOM-less and LF. A file that does not read is never replaced: the creation it belongs to is said to be unread.
/// </summary>
public sealed class CreateReceipts(string home)
{
    private static readonly JsonSerializerOptions Format = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private const string Folder = "create";

    public string PathOf(string digest) => DaorisHome.Entry(home, HomeEntry.PlaceReceipts, Folder, digest + ".json");

    /// <summary>The receipt of <paramref name="digest"/>, or null where none is written.</summary>
    /// <exception cref="DriverException">A file is there and does not read.</exception>
    public CreateReceipt? Read(string digest)
    {
        var path = PathOf(digest);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<CreateReceipt>(AtomicFile.ReadText(path), Format)
                ?? throw new DriverException($"the receipt of the creation `{digest[..12]}` is empty: it is read by hand, never replaced.");
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new DriverException($"the receipt of the creation `{digest[..12]}` does not read ({error.Message.TrimEnd('.', ' ')}): it is read by hand, never replaced.");
        }
    }

    public void Write(CreateReceipt receipt)
    {
        var path = PathOf(receipt.Digest);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteText(path, JsonSerializer.Serialize(receipt, Format).ReplaceLineEndings("\n") + "\n");
    }

    /// <summary>
    /// The newest receipt of a creation of <paramref name="name"/> (any case) with an operation not done, which a re-press of it goes on from;
    /// null where there is none. A receipt that does not read is left as it is.
    /// </summary>
    public CreateReceipt? Unfinished(string name)
    {
        var folder = Path.GetDirectoryName(PathOf(new string('0', 64)))!;
        if (!Directory.Exists(folder)) return null;
        CreateReceipt? found = null;
        foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            var digest = Path.GetFileNameWithoutExtension(file);
            if (!PlaceDigests.Is(digest)) continue;
            try
            {
                if (Read(digest) is { Finished: false } receipt && string.Equals(receipt.Ask.Name, name, StringComparison.OrdinalIgnoreCase)
                    && (found is null || receipt.At > found.At))
                {
                    found = receipt;
                }
            }
            catch (DriverException)
            {
                // Unread: left as it is, and said when its own digest is pressed.
            }
        }

        return found;
    }
}
