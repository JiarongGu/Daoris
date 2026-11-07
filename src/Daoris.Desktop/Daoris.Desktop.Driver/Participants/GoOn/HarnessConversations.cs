using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// The harness's own conversation id per session (ANSWER1a, D131 §1): the desktop store's <c>bindings</c> row, kept the moment the wire
/// says it and read back when the person answers a park.
/// </summary>
/// <remarks>
/// <para><b>Kept by Daoris, never read from the agent's own home.</b> The harness keeps its conversation inside the
/// account's configuration home; reading there is a read inside an account's directory (D66 §3), which D125 rejected.
/// The id is the one fact a resume needs, and the wire hands it over at the start of every session.</para>
///
/// <para><b>This machine's, never the record's.</b> A conversation id is a harness detail of one machine's checkout, like
/// the transcript beside it, and a teammate can do nothing with it.</para>
///
/// <para><b>A binding that does not read is no conversation</b>: the answer falls back to a carry-on, saying Daoris kept no
/// id (<see cref="ContinueWhy.Unkept"/>), and never throws.</para>
///
/// <para><b>The store alone</b> (FOUNDATION1 z-iii, D212; the design §13.6): the binding files an older build kept are imported once by the
/// home's migration (<see cref="BindingFilesStep"/>), and nothing here reads or writes a file. A deleted record takes its binding with
/// its files (<see cref="SessionHomeFiles"/>).</para>
/// </remarks>
public sealed class HarnessConversations(string home, Func<StoreSettled>? settled = null) : IWorkplaceHarnessBindings
{
    private readonly BindingStore _bindings = new(home, settled);

    /// <summary>Where the desktop store, which holds the bindings, lies in this home.</summary>
    public string StoreLocation => _bindings.Location;

    /// <summary>
    /// Which of these sessions the store holds a binding for, read without writing anything: what a record's deletion takes with it
    /// (<see cref="ForgetBindings"/>). Throws what the store's file throws where it is there and does not read.
    /// </summary>
    public IReadOnlyList<string> BindingsOf(IReadOnlyCollection<string> sessions) => _bindings.Holding(sessions);

    /// <summary>
    /// The bindings of these deleted records gone from the store, those that held one named. A store that will not take the delete (busy,
    /// or written by a newer Daoris) throws, and the binding stays.
    /// </summary>
    public IReadOnlyList<string> ForgetBindings(IReadOnlyCollection<string> sessions) => _bindings.Forget(sessions);

    /// <summary>
    /// Keep what the wire named this session's conversation, replacing what was kept. An id that is not a session's, or a
    /// blank conversation, keeps nothing; a write that fails is lost, which costs a resume and never the session.
    /// </summary>
    public void Keep(string session, string adapter, string conversation)
    {
        if (!SessionEventText.IsLocalId(session) || string.IsNullOrWhiteSpace(conversation) || string.IsNullOrWhiteSpace(adapter)) return;

        try
        {
            _bindings.Keep(session, adapter, conversation);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DesktopStoreException or SqliteException)
        {
            using var log = new MachineLog(home, "driver");
            log.Warn("conversation.unkept", ("session", session), ("adapter", adapter), ("error", error.GetType().Name));
        }
    }

    /// <summary>
    /// Keep that the person picked <paramref name="account"/> for this session (FOUNDATION1a-ii): a start they named an account for, never an
    /// agent's ask or the curve's account. Kept in the desktop store beside the binding, never on the service's record or any wire, so
    /// the account rule reads it as its constraint. A write that fails is lost, which costs the wait and never the start: the conversation
    /// then walks its agent's accounts as a record from before does, and the log says so.
    /// </summary>
    public void Pick(string session, string? account)
    {
        if (!SessionEventText.IsLocalId(session) || string.IsNullOrWhiteSpace(account)) return;

        try
        {
            _bindings.Pick(session, account.Trim());
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            using var log = new MachineLog(home, "driver");
            log.Warn("account.unpicked", ("session", session), ("account", account.Trim()), ("error", error.GetType().Name));
        }
    }

    /// <summary>
    /// Keep the role a start took and its revision on the session's binding (FOUNDATION1x). A write that fails is lost, which costs the newer
    /// build's continuation its role and never the start: the record's <c>actor</c>, the seeded id, then says what it ran as.
    /// </summary>
    public void KeepRole(string session, string role, long revision)
    {
        if (!SessionEventText.IsLocalId(session) || string.IsNullOrWhiteSpace(role)) return;

        try
        {
            _bindings.KeepRole(session, role, revision);
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            using var log = new MachineLog(home, "driver");
            log.Warn("role.unkept", ("session", session), ("role", role), ("error", error.GetType().Name));
        }
    }

    /// <summary>The role and revision a session's start took, or null where the binding holds none or the store cannot say.</summary>
    public (string Role, long Revision)? RoleOf(string session)
    {
        if (!SessionEventText.IsLocalId(session)) return null;

        try
        {
            return _bindings.RoleOf(session);
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether the person picked the account a session's record runs on (<paramref name="account"/>): what the account rule reads as its
    /// pick constraint. False for an agent's pick, the curve's account, a record from before and a teammate's mirror, none of which holds
    /// one, and where the store cannot say, the conversation then walking as such a record does.
    /// </summary>
    public bool PickedOn(string session, string? account)
    {
        if (!SessionEventText.IsLocalId(session) || string.IsNullOrWhiteSpace(account)) return false;

        try
        {
            return _bindings.Picked(session) is { } picked && string.Equals(picked, account.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// What a door tells the id its harness named to, for this session's record (MSG1c, D137 §4.2): a chat keeps its
    /// conversation as a quest's session does, on the protocol door from <c>session/new</c>'s answer and on the native door
    /// from its <c>init</c> line, so an ended chat the person writes to goes on in it.
    /// </summary>
    public Action<string> Keeping(string session, string adapter) => conversation => Keep(session, adapter, conversation);

    /// <summary>
    /// What was kept for this session, or null for nothing, a store that does not read, or an id that is not one.
    /// </summary>
    public HarnessConversation? Read(string session)
    {
        if (!SessionEventText.IsLocalId(session)) return null;
        try
        {
            return _bindings.Read(session) is var (adapter, conversation) ? new HarnessConversation(adapter, conversation) : null;
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// What this machine keeps for the session's conversation, apart from whether it reads: whether anything is kept, and whether what is
    /// kept reads (a store that will not say is not an absent record, <see cref="Read"/> answering null for both).
    /// </summary>
    public (bool Kept, bool Reads) Kept(string session)
    {
        if (!SessionEventText.IsLocalId(session)) return (false, false);
        try
        {
            return _bindings.Read(session) is not null ? (true, true) : (false, false);
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException)
        {
            return (true, false);
        }
    }
}
