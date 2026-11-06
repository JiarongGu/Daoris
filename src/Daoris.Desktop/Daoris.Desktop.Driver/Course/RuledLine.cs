namespace Daoris.Driver;

/// <summary>
/// A line as the supervisor's rules read it (CONTEXTDRIVE1p; the context counters design §4.4): the role it goes on under and its place.
/// Every reader of a line's rules resolves it here: the answers-alone hold, the line's facts and the supervisor's print, a conversation's
/// rules, the planner's park and course, a run's tries and the rule acts.
/// </summary>
/// <param name="Role">The role the line's start was bound to while the catalogue holds it, else the role taking its work at its place now.</param>
/// <param name="Repository">The line's repository; null for a line at none.</param>
/// <param name="Workspace">The repository's workspace, as the record names it, else as the registry places the quest's repository.</param>
internal sealed record RuledLine(string Role, string? Repository, string? Workspace)
{
    /// <summary>The line's effective rules per count in <paramref name="book"/>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> In(SupervisorRuleBook book) => book.For(Role, Repository, Workspace);

    /// <summary>The line's place, as the staffing course reads it.</summary>
    public StaffPlace Place => new(Repository, Workspace);

    /// <summary>A driven quest's line: its last record where one ran, at the quest's repository and its registered <paramref name="workspace"/>.</summary>
    public static RuledLine OfQuest(string? home, QuestView quest, PriorSession? record, string? workspace) =>
        Of(home, record, quest.To, workspace, string.Equals(quest.Weight, ParticipantWork.Design, StringComparison.Ordinal) ? RoleWork.Design : RoleWork.Build);

    /// <summary>
    /// The line <paramref name="record"/> goes on from. A first start, with no record, is at <paramref name="repository"/> and
    /// <paramref name="workspace"/> and does <paramref name="work"/>; a record's own repository and workspace are its place.
    /// </summary>
    /// <param name="home">The home whose bindings and catalogue are read; null reads neither, and the role is the work's seeded one.</param>
    public static RuledLine Of(
        string? home, PriorSession? record, string? repository = null, string? workspace = null, string? work = null)
    {
        var at = record?.Repository ?? repository;
        var circle = record?.Workspace ?? workspace;
        return new(RoleOf(home, record, work ?? WorkOf(record), at, circle), at, circle);
    }

    /// <summary>The work a record's line does: Ask Daoris's, a conversation's, else a driven line's, design where its seeded role is Design's.</summary>
    public static string WorkOf(PriorSession? record) =>
        record?.HelpMode is not null ? RoleWork.AskDaoris
        : record?.Kind == SessionKinds.Chat ? RoleWork.Chat
        : string.Equals(record?.Actor, RoleWork.Design, StringComparison.Ordinal) ? RoleWork.Design
        : RoleWork.Build;

    // A driven record's `actor` is the seeded role's id, so an added role is read from the start's binding or the catalogue, never from it.
    private static string RoleOf(string? home, PriorSession? record, string work, string? repository, string? workspace)
    {
        if (home is null) return StartPlan.RoleOf(work);
        try
        {
            var bound = record is null ? null : new BindingStore(home, null).RoleOf(record.Session)?.Role;
            return RoleOrders.Bound(bound, work, RoleCatalog.ReadHome(home), new RolePlace(workspace, repository));
        }
        catch (Exception error) when (error is DriverException or DesktopStoreException or Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            return StartPlan.RoleOf(work);
        }
    }
}
