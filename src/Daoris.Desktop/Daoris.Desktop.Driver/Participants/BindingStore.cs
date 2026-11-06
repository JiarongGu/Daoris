namespace Daoris.Driver;

/// <summary>
/// The participants' bindings in the desktop store (FOUNDATION1 z-iii, D212; the design §13.3, §13.5, §13.6): the kept conversation of each
/// session record of this machine, <c>bindings</c>. The store is the only place a conversation is kept; the files an older build kept are
/// imported once by the home's migration (<see cref="BindingFilesStep"/>).
/// </summary>
/// <remarks>
/// <para>Every keep writes the row, naming its own columns and never replacing the row (O3-16), and every read reads it.</para>
/// <para><b>Opened for one use and closed.</b> Callers build these over any home, often for one read, so the store is opened for each use and
/// never held, a read never creating or writing it. Every open of the process names one run (<see cref="StoreProcess.Run"/>), so
/// <c>store_runs</c> holds the process once.</para>
/// </remarks>
/// <param name="home">The home whose store this is.</param>
/// <param name="settled">The reading of the start's confirmation; null reads this install's swap journal.</param>
public sealed class BindingStore(string home, Func<StoreSettled>? settled)
{
    /// <summary>The family's name in <c>imports</c> and <c>store_revisions</c>.</summary>
    public const string Family = "bindings";

    private Func<StoreSettled> Settled => settled ?? StartConfirmation.Read;

    /// <summary>The store's file in this home.</summary>
    public string Location => DesktopStore.LocationIn(home);

    /// <summary>The conversation the store holds for a session, or null for none; the store's failure is thrown.</summary>
    public (string Door, string Conversation)? Read(string session)
    {
        using var store = DesktopStoreReader.Open(Location);
        if (store is not { Version: >= 2 }) return null;
        return store.Query(
            "SELECT door, conversation FROM bindings WHERE session = $session",
            row => ((string, string)?)(row.GetString(0), row.GetString(1)),
            ("$session", session)).FirstOrDefault();
    }

    /// <summary>
    /// Keep what the wire named a session's conversation: its row's door and conversation set by name, the row's other columns left as
    /// they are, in one transaction of the store's. Throws what the store throws where it cannot write.
    /// </summary>
    public void Keep(string session, string door, string conversation)
    {
        using var store = Open();
        store.Write(Family, write => Upsert(write, session, door, conversation));
    }

    /// <summary>
    /// Keep that the person picked this session's account (FOUNDATION1a-ii, the design §3.2 step 4): the account a start they named one for
    /// opened on, set by name beside the binding, so the account rule reads it as its constraint. Only a session of this machine has one, and
    /// a blank account keeps nothing. Throws what the store throws where it cannot write.
    /// </summary>
    public void Pick(string session, string account)
    {
        using var store = Open();
        store.Write(
            Family,
            write => write.Execute(
                """
                INSERT INTO binding_picks (session, account, at) VALUES ($session, $account, $at)
                ON CONFLICT (session) DO UPDATE SET account = excluded.account, at = excluded.at
                """,
                ("$session", session), ("$account", account),
                ("$at", DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture))));
    }

    /// <summary>
    /// The account the person picked for a session, or null where they picked none: an agent's pick, the curve's account, a record from before
    /// the pick was kept and a teammate's mirror hold none, and a home with no store holds none. The store's failure is thrown.
    /// </summary>
    public string? Picked(string session)
    {
        using var store = DesktopStoreReader.Open(Location);
        if (store is not { Version: >= 3 }) return null;
        return store.Query("SELECT account FROM binding_picks WHERE session = $session", row => row.GetString(0), ("$session", session)).FirstOrDefault();
    }

    /// <summary>
    /// Keep the role a start took and its revision (FOUNDATION1x), set by name beside the binding: the record's <c>actor</c> keeps the seeded id
    /// every build reads, and this is the role the newer build's continuation reads first. Throws what the store throws where it cannot write.
    /// </summary>
    public void KeepRole(string session, string role, long revision)
    {
        using var store = Open();
        store.Write(
            Family,
            write => write.Execute(
                """
                INSERT INTO binding_roles (session, role, revision, at) VALUES ($session, $role, $revision, $at)
                ON CONFLICT (session) DO UPDATE SET role = excluded.role, revision = excluded.revision, at = excluded.at
                """,
                ("$session", session), ("$role", role), ("$revision", revision),
                ("$at", DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture))));
    }

    /// <summary>The role and revision the store holds for a session's start, or null for none (a record from before, a mirror, no store); the store's failure is thrown.</summary>
    public (string Role, long Revision)? RoleOf(string session)
    {
        using var store = DesktopStoreReader.Open(Location);
        if (store is not { Version: >= 11 }) return null;
        return store.Query(
            "SELECT role, revision FROM binding_roles WHERE session = $session",
            row => ((string, long)?)(row.GetString(0), row.GetInt64(1)),
            ("$session", session)).FirstOrDefault();
    }

    /// <summary>The bindings of these sessions gone, each by its record's deletion (§13.6): the ones that held a row. Nothing is made for a home with no store.</summary>
    public IReadOnlyList<string> Forget(IReadOnlyCollection<string> sessions)
    {
        var held = Holding(sessions);
        if (held.Count == 0) return [];
        using var store = Open();
        store.Write(Family, write =>
        {
            foreach (var session in held)
            {
                write.Execute("DELETE FROM bindings WHERE session = $session", ("$session", session));
                write.Execute("DELETE FROM binding_picks WHERE session = $session", ("$session", session));
                write.Execute("DELETE FROM binding_roles WHERE session = $session", ("$session", session));
            }
        });
        return held;
    }

    /// <summary>
    /// Which of these sessions the store holds a binding for, or a pick of its account, read without writing or making anything; none for a
    /// home with no store.
    /// </summary>
    public IReadOnlyList<string> Holding(IReadOnlyCollection<string> sessions)
    {
        if (sessions.Count == 0) return [];
        using var store = DesktopStoreReader.Open(Location);
        if (store is not { Version: >= 2 }) return [];
        var picks = store.Version >= 3;
        var roles = store.Version >= 11;
        return [.. sessions.Where(session =>
            store.Query("SELECT 1 FROM bindings WHERE session = $session", row => row.GetInt32(0), ("$session", session)).Count > 0
            || (picks && store.Query("SELECT 1 FROM binding_picks WHERE session = $session", row => row.GetInt32(0), ("$session", session)).Count > 0)
            || (roles && store.Query("SELECT 1 FROM binding_roles WHERE session = $session", row => row.GetInt32(0), ("$session", session)).Count > 0))];
    }

    /// <summary>The row's door and conversation set by name, the row's other columns left as they are.</summary>
    internal static void Upsert(DesktopWrite write, string session, string door, string conversation) =>
        write.Execute(
            """
            INSERT INTO bindings (session, door, conversation, kept) VALUES ($session, $door, $conversation, $kept)
            ON CONFLICT (session) DO UPDATE SET door = excluded.door, conversation = excluded.conversation, kept = excluded.kept
            """,
            ("$session", session), ("$door", door), ("$conversation", conversation),
            ("$kept", DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>The store opened under the process's one run and kind (<see cref="StoreProcess"/>), which every family's writes share.</summary>
    private DesktopStore Open() => DesktopStore.Open(Location, StoreProcess.Options(Settled));
}
