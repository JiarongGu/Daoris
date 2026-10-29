namespace Daoris.Driver;

/// <summary>The kind of id a record's field holds (the agent-provider design §5): resolved by its kind, never guessed.</summary>
public enum IdKind
{
    /// <summary>A door's id: an adapter's name.</summary>
    Door,

    /// <summary>A provider's id; <c>codex</c> resolves here and is refused as a door.</summary>
    Provider,

    /// <summary>The string account records hold, resolving to a pool.</summary>
    AccountKey,

    /// <summary>A program's owner: a provider's id for its binary, a door's for a door's package.</summary>
    Program,
}

/// <summary>One record's field and the kind of id it holds.</summary>
public sealed record RecordIdField(string Record, string Field, IdKind Kind);

/// <summary>
/// §5's table of the agent-provider design, written once: each field a stored record holds an id in, with the kind of id, so
/// <c>ProviderRegistry.Resolve</c> resolves it by its kind and no reader guesses. No record changes shape (the brief §10
/// point 4); this is the reading of what is stored. The record names are the design's own.
/// </summary>
public static class RecordIds
{
    public static IReadOnlyList<RecordIdField> Fields { get; } =
    [
        new("The session record", "Adapter", IdKind.Door),
        new("The actor asked and ran", "agent", IdKind.Door),
        new("The store's bindings", "adapter", IdKind.Door),
        new("Ask Daoris's wired file", "harness", IdKind.Door),
        new("SessionEnded, the usage history", "Adapter", IdKind.Door),
        new("SessionEnded, the usage history", "TurnBinding.Agent", IdKind.AccountKey),
        new("SessionEnded, the usage history", "TurnBinding.Door", IdKind.Door),
        new("The offers", "door", IdKind.Door),
        new("The machine log", "adapter", IdKind.Door),
        new("The machine log", "agent", IdKind.Door),
        new("An opinion's state, a brainstorm's and a meeting's files", "adapter", IdKind.Door),
        new("A hand-over's, a helper's, a partner's ask; a go-on mark", "agent", IdKind.Door),
        new("driver.json, and role staffing", "adapter", IdKind.Door),
        new("driver.json, and role staffing", "intakeAdapter", IdKind.Door),
        new("driver.json, and role staffing", "helperAdapter", IdKind.Door),
        new("driver.json, and role staffing", "commands", IdKind.Door),
        new("driver.json, and role staffing", "actors[].agent", IdKind.Door),
        new("driver.json, and role staffing", "questions pause", IdKind.Provider),
        new("The pins", "versions by id", IdKind.Program),
        new("The account homes, cooling, windows", "profiles/agents/<agent>/", IdKind.AccountKey),
        new("The account homes, cooling, windows", "by agent", IdKind.AccountKey),
        new("The lineage", "harness", IdKind.Provider),
    ];

    /// <summary>The kind of id a field holds; a field the table does not name is an error, never a guess.</summary>
    public static IdKind KindOf(string record, string field) =>
        Fields.FirstOrDefault(row => row.Record == record && row.Field == field)?.Kind
        ?? throw new DriverException(
            $"no id is recorded for the field '{field}' of '{record}' — one of: "
            + string.Join(", ", Fields.Where(row => row.Record == record).Select(row => row.Field)) + ".");
}
