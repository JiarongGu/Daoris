namespace Daoris.Driver;

/// <summary>
/// The workspace Ask Daoris's tools default to: its room's, which is its record's
/// workspace in a room of its own, and none for <c>default</c>'s room, the whole machine. Its knowledge connector is
/// handed it beside the session (<see cref="KnowledgeConnector.SessionScope"/>), and then answers from that workspace unless a call
/// names another, or <c>all</c> (the service's <c>WorkspaceScope</c>).
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
    public static string? Of(string home, string room, string? workspace) => KnowledgeHelp.Current.GoesOnIn(home, workspace, room);

    /// <summary>
    /// The workspace a conversation focused on <paramref name="focus"/> starts at: the focus's workspace, a repository's its own; null for
    /// this machine and for <c>default</c>, which answer for every workspace.
    /// </summary>
    public static string? OfFocus(HelpFocus focus) => KnowledgeHelp.Current.ScopeOf(focus.Workspace);

    /// <summary>
    /// The workspace a conversation's record focuses what it lists on: Ask Daoris's, its room's,
    /// the record's tree being the room it ran in. Null for any other record, for none, and for the whole machine's room.
    /// </summary>
    public static string? OfRecord(string home, SessionRecord? record) =>
        record is { Repository: KnowledgeRoomIdentity.Repository } ? KnowledgeHelp.Current.GoesOnIn(home, record.Workspace, record.Tree) : null;

    /// <summary>What a connector bound to <paramref name="circle"/> carries beyond the session's own; null for none.</summary>
    public static IReadOnlyDictionary<string, string>? Scope(string? circle) =>
        circle is { Length: > 0 } named ? new Dictionary<string, string>(StringComparer.Ordinal) { [Variable] = named } : null;
}
