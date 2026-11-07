using System.Text.Json;

namespace Daoris.Driver;

/// <summary>How the canon a doctrine tool carries stands to the one a checkout's lock was written from (<see cref="DoctrineStatus.Update"/>).</summary>
public static class DoctrineUpdates
{
    /// <summary>The lock's version is older than the tool's canon: its files are behind (the tool's <c>newer</c>).</summary>
    public const string Behind = "behind";

    /// <summary>The versions are equal and the canons differ, so which is newer only the text can say (the tool's <c>unknown</c>).</summary>
    public const string Differs = "differs";
}

/// <summary>
/// What the doctrine tool's <c>status --json</c> says of a checkout against the canon it carries,
/// read field by field from its answer: whether the canon moved and by how many files, the keeps it reopened, and a lock a newer canon wrote.
/// The tool judges, and nothing here judges again: no copy of its table.
/// </summary>
public sealed record DoctrineStatus
{
    /// <summary>Whether the checkout has a lock at all.</summary>
    public bool Synced { get; init; }

    /// <summary>How the canon moved, one of <see cref="DoctrineUpdates"/>; null where the tool reports no update.</summary>
    public string? Update { get; init; }

    /// <summary>The version of the canon the tool carries, where its answer names one.</summary>
    public string? Carried { get; init; }

    /// <summary>The version the lock was written from, where the answer names one.</summary>
    public string? Locked { get; init; }

    /// <summary>How many files a sync would change, add and retire.</summary>
    public int Changed { get; init; }

    public int Added { get; init; }

    public int Retired { get; init; }

    /// <summary>How many of the repository's keeps the canon's file moved past since they were judged.</summary>
    public int Reopened { get; init; }

    /// <summary>The version of the newer canon that wrote the lock, which the tool never rolls back; null where none did.</summary>
    public string? NewerLock { get; init; }

    /// <summary>Whether a reconcile is offered: the canon moved, or a keep reopened, over a lock this tool may read.</summary>
    public bool Offers => Synced && NewerLock is null && (Update is not null || Reopened > 0);

    /// <summary>The tool's answer read, or null where what it printed is no status.</summary>
    public static DoctrineStatus? Parse(string said)
    {
        try
        {
            using var document = JsonDocument.Parse(said);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("synced", out var synced) || synced.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return null;
            }

            var update = Field(root, "update");
            var newer = Field(root, "newerLock");
            if (update is { ValueKind: not JsonValueKind.Object } || newer is { ValueKind: not JsonValueKind.Object }) return null;

            return new DoctrineStatus
            {
                Synced = synced.GetBoolean(),
                Update = update is { } moved
                    ? Text(moved, "direction") switch { "newer" => DoctrineUpdates.Behind, "unknown" => DoctrineUpdates.Differs, _ => null }
                    : null,
                Carried = update is { } a ? Text(a, "available") : newer is { } n ? Text(n, "carried") : null,
                Locked = update is { } l ? Text(l, "locked") : null,
                Changed = Count(update, "changed"),
                Added = Count(update, "added"),
                Retired = Count(update, "retired"),
                Reopened = Field(root, "kept") is { ValueKind: JsonValueKind.Array } kept
                    ? kept.EnumerateArray().Count(each => each.ValueKind == JsonValueKind.Object && Field(each, "reopened") is { ValueKind: JsonValueKind.True })
                    : 0,
                NewerLock = newer is { } locked ? Text(locked, "locked") ?? "" : null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The field <paramref name="name"/>, or null where it is absent or null.</summary>
    private static JsonElement? Field(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value : null;

    private static string? Text(JsonElement element, string name) => Field(element, name) is { ValueKind: JsonValueKind.String } text ? text.GetString() : null;

    private static int Count(JsonElement? element, string name) =>
        element is { } found && Field(found, name) is { ValueKind: JsonValueKind.Array } list ? list.GetArrayLength() : 0;
}

/// <summary>A checkout's status as the doctrine tool answered it, or why it did not.</summary>
/// <param name="Problem">What was found in place of an answer, in words; null where <paramref name="Status"/> was read.</param>
public sealed record DoctrineReading(DoctrineStatus? Status, string? Problem);
