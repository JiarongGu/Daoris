namespace Daoris.Driver;

/// <summary>
/// The workspace Ask Daoris's tools are bound to (ASKSCOPE1c, D158's ASKSCOPE1 note point 5): its room's, which is its record's
/// workspace in a room of its own, and none for <c>default</c>'s room, the whole machine (ASKSCOPE1b). Its knowledge connector is
/// handed it beside the session (<see cref="KnowledgeConnector.SessionScope"/>), and then answers from that workspace alone, refusing
/// a call that names another, or <c>all</c> (the service's <c>WorkspaceScope</c>).
/// </summary>
/// <remarks>
/// A file of its own rather than the connector's: the CLI's census of machine-local variables reads every driver file that builds a
/// path under the home, as the connector's does, and this variable names a workspace, never a path.
/// </remarks>
public static class HelpCircle
{
    /// <summary>
    /// The variable its connector reads the workspace from. 🔴 A twin: the service's <c>AmbientWorkspace.BoundVariable</c> spells the
    /// same name, and each side's test holds the spelling.
    /// </summary>
    public const string Variable = "DAORIS_SESSION_WORKSPACE";

    /// <summary>
    /// The workspace a conversation in <paramref name="room"/> is bound to, its record keeping <paramref name="workspace"/>: the
    /// room's (<see cref="HelpRoom.GoesOnIn"/>), so one going on in <c>help</c>, where it started before each workspace had a room,
    /// reads the whole machine as its room does. Null for the whole machine.
    /// </summary>
    public static string? Of(string home, string room, string? workspace) => HelpRoom.GoesOnIn(home, workspace, room);

    /// <summary>
    /// The workspace a conversation's record binds what it proposes to (<see cref="HelpProposalScope"/>): Ask Daoris's, its room's,
    /// the record's tree being the room it ran in. Null for any other record, for none, and for the whole machine's room.
    /// </summary>
    public static string? OfRecord(string home, SessionRecord? record) =>
        record is { Repository: HelpRoom.Repository } ? HelpRoom.GoesOnIn(home, record.Workspace, record.Tree) : null;

    /// <summary>What a connector bound to <paramref name="circle"/> carries beyond the session's own; null for none.</summary>
    public static IReadOnlyDictionary<string, string>? Scope(string? circle) =>
        circle is { Length: > 0 } named ? new Dictionary<string, string>(StringComparer.Ordinal) { [Variable] = named } : null;
}
