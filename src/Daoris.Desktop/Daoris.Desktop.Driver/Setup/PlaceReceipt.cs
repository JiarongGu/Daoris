using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>An acceptance the person chose in a plan: the repository and its purpose (<see cref="PlacePlans.Landed"/> or <see cref="PlacePlans.Integration"/>).</summary>
public sealed record PlaceAccept(string Repository, string Purpose)
{
    /// <summary>The declaration's blob the plan read, which the acceptance keeps: set from the plan, never from the caller.</summary>
    public string? Blob { get; init; }
}

/// <summary>The outcomes a receipt keeps for an operation; one in flight is <see cref="Uncertain"/> until it is known.</summary>
public static class PlaceOutcomes
{
    /// <summary>Authorized by the receipt and not begun: nothing of it can have happened.</summary>
    public const string Pending = "pending";

    /// <summary>Begun, or its answer lost: it may have happened, and its postcondition says.</summary>
    public const string Uncertain = "uncertain";

    public const string Done = "done";

    /// <summary>Refused or failed with nothing left behind: it did not happen.</summary>
    public const string Failed = "failed";
}

/// <summary>The kinds of operation an apply authorizes, and their ids.</summary>
public static class PlaceOperations
{
    /// <summary>Every configuration change and acceptance of the plan, written in one edit of <c>driver.json</c>.</summary>
    public const string Configuration = "configuration";

    public const string Adoption = "adoption";

    /// <summary>A copy of a shipped default saved in the home (<c>WORKFLOW_SAVE</c>), before the configuration that chooses it.</summary>
    public const string WorkflowSave = "workflow-save";

    public const string Room = "room";

    public static string AdoptionOf(string repository) => $"{Adoption}:{repository}";

    public static string RoomOf(string repository) => $"{Room}:{repository}";

    public static string WorkflowSaveOf(string workflow) => $"{WorkflowSave}:{workflow}";
}

/// <summary>One operation an apply authorizes and what came of it; <see cref="Quest"/> is the adoption quest once it is known.</summary>
public sealed record PlaceOperation(string Id, string Kind, string? Repository, string Said, string Outcome)
{
    public string? Note { get; init; }

    public string? Quest { get; init; }

    /// <summary>For a <see cref="PlaceOperations.WorkflowSave"/>: the id the copy is saved under, the default it copies and the gates it adds.</summary>
    public string? Workflow { get; init; }

    public string? Default { get; init; }

    public bool Opinion { get; init; }

    public bool Look { get; init; }
}

/// <summary>
/// A place's apply as it is authorized: the plan's digest, the ask and the acceptances chosen, and
/// each operation's outcome. Written before the first effect and again at each outcome, so a restart reads what is done, what failed and what
/// is uncertain.
/// </summary>
public sealed record PlaceReceipt(string Digest, DateTimeOffset At, PlaceAsk Ask, IReadOnlyList<PlaceAccept> Accepted, IReadOnlyList<PlaceOperation> Operations)
{
    public PlaceOperation? Of(string id) => Operations.FirstOrDefault(each => each.Id == id);

    public PlaceReceipt With(string id, Func<PlaceOperation, PlaceOperation> change) =>
        this with { Operations = [.. Operations.Select(each => each.Id == id ? change(each) : each)] };
}

/// <summary>A plan's digest: what the person read, said once, so an apply can tell that it no longer holds (<c>moved</c>).</summary>
public static class PlaceDigests
{
    /// <summary>
    /// The SHA-256 of the plan's ask, each repository's changes in order, each declaration's line and blob, and the acceptances it offers. The
    /// commit, the text and every sentence a reader sees beside them are read from these, so they are not a part of it.
    /// </summary>
    public static string Of(PlacePlanned plan)
    {
        var ask = plan.Asked;
        var parts = new List<string[]>();
        parts.Add(["ask", ask.Preset, Flag(ask.Opinion), Flag(ask.Look), Flag(ask.Adoption), ask.Builds ?? "", Flag(ask.Drive), .. ask.Repositories]);
        // Pacing joins the digest only where it is said, so a plan read without it keeps the digest it had.
        if (ask.AtOnce is not null || ask.Pilot is not null) parts.Add(["pacing", ask.AtOnce?.ToString() ?? "", ask.Pilot?.ToString() ?? ""]);
        if (ask.Room is not null || ask.Purpose is not null) parts.Add(["room", ask.Room ?? "", ask.Purpose ?? ""]);
        foreach (var row in plan.Repositories)
        {
            parts.Add(["repository", row.Repository, row.Workspace]);
            // The copy's saving is its own operation, done once; the choice that names it is in the digest, so a plan read again after the
            // copy is saved is the plan read.
            parts.AddRange(row.Changes.Where(change => change.Key != PlacePlans.WorkflowSaveKey)
                .Select(change => (string[])["change", change.Key, change.Part, change.Said, change.Before, change.After]));
            if (row.Declaration is { } declaration)
            {
                parts.Add(["declaration", declaration.Line, declaration.Blob, .. declaration.Acceptances.Select(each => $"{each.Purpose}={each.Standing}")]);
            }
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parts))));
    }

    /// <summary>Whether a word is a digest as <see cref="Of"/> makes one: 64 lower-case hex.</summary>
    public static bool Is(string? value) => value is { Length: 64 } && value.All(each => each is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string Flag(bool on) => on ? "1" : "0";
}

/// <summary>
/// The receipts in a home (<see cref="HomeEntry.PlaceReceipts"/>): one file per plan's digest, written atomically, BOM-less and LF, and read
/// again at a restart. A file that does not read is never replaced: the apply it belongs to is said to be unread.
/// </summary>
public sealed class PlaceReceipts(string home)
{
    private static readonly JsonSerializerOptions Format = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string PathOf(string digest) => DaorisHome.Entry(home, HomeEntry.PlaceReceipts, digest + ".json");

    /// <summary>The receipt of <paramref name="digest"/>, or null where none is written.</summary>
    /// <exception cref="DriverException">A file is there and does not read.</exception>
    public PlaceReceipt? Read(string digest)
    {
        var path = PathOf(digest);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<PlaceReceipt>(AtomicFile.ReadText(path), Format)
                ?? throw new DriverException($"the receipt of the plan `{digest[..12]}` is empty: it is read by hand, never replaced.");
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new DriverException($"the receipt of the plan `{digest[..12]}` does not read ({error.Message.TrimEnd('.', ' ')}): it is read by hand, never replaced.");
        }
    }

    public void Write(PlaceReceipt receipt)
    {
        var path = PathOf(receipt.Digest);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteText(path, JsonSerializer.Serialize(receipt, Format).ReplaceLineEndings("\n") + "\n");
    }

    /// <summary>Every receipt that reads, for a restart to settle; one that does not is left as it is.</summary>
    public IReadOnlyList<PlaceReceipt> All()
    {
        var folder = DaorisHome.Entry(home, HomeEntry.PlaceReceipts);
        if (!Directory.Exists(folder)) return [];
        var found = new List<PlaceReceipt>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            var digest = Path.GetFileNameWithoutExtension(file);
            if (!PlaceDigests.Is(digest)) continue;
            try
            {
                if (Read(digest) is { } receipt) found.Add(receipt);
            }
            catch (DriverException)
            {
                // Unread: left as it is, and said when its own apply is pressed again.
            }
        }

        return found;
    }
}
