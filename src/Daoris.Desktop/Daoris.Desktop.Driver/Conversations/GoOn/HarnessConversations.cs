using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// The harness's own conversation id per session (ANSWER1a, D131 §1): <c>&lt;home&gt;/sessions/&lt;session&gt;.harness.json</c>,
/// beside the session's transcript, kept the moment the wire says it and read back when the person answers a park.
/// </summary>
/// <remarks>
/// <para><b>Kept by Daoris, never read from the agent's own home.</b> The harness keeps its conversation inside the
/// account's configuration home; reading there is a read inside an account's directory (D66 §3), which D125 rejected.
/// The id is the one fact a resume needs, and the wire hands it over at the start of every session.</para>
///
/// <para><b>This machine's, never the record's.</b> A conversation id is a harness detail of one machine's checkout, like
/// the transcript beside it, and a teammate can do nothing with it.</para>
///
/// <para><b>A file that does not read is no conversation</b>: the answer falls back to a carry-on, saying Daoris kept no
/// id (<see cref="ContinueWhy.Unkept"/>), and never throws.</para>
///
/// <para><b>The file, and the store once it imports it</b> (FOUNDATION1 z-iii, D212; the design §13.6): the file's id is read into the desktop
/// store's <c>bindings</c> (<see cref="BindingStore"/>) once the start's confirmation is settled, and the file is set aside, so a build from
/// before the store finds none and carries on rather than resuming an id this build has since replaced. Until the home's first import every
/// keep writes the file and every read reads it, as before; after, every keep writes the binding and every read reads it, a file an older
/// build wrote meanwhile being imported first, its id winning. A deleted record takes its binding with its files
/// (<see cref="SessionHomeFiles"/>).</para>
///
/// <para>What is kept is this store's; the file and its format are <see cref="GoOnFiles"/>' (STRUCT3h, D166's note).</para>
/// </remarks>
public sealed class HarnessConversations(string home, Func<StoreSettled>? settled = null)
{
    public const string Suffix = ".harness.json";

    /// <summary>What a binding file's name gains when its conversation is imported into the store and it is set aside.</summary>
    public const string SetAsideSuffix = BindingStore.SetAsideSuffix;

    private readonly BindingStore _bindings = new(home, settled, ReadFile);

    /// <summary>Where a session's conversation id is kept before it is imported, and where an older build writes it.</summary>
    public string PathOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + Suffix);

    /// <summary>Whether this home keeps its conversations in the desktop store: it has imported its binding files, from which every keep and read is the store's.</summary>
    public bool InStore() => _bindings.InUse();

    /// <summary>Where the desktop store, which holds the bindings once they are imported, lies in this home.</summary>
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

    private static (string Adapter, string Conversation)? ReadFile(string path) =>
        GoOnFiles.ReadConversation(path) is { } kept ? (kept.Adapter, kept.Conversation) : null;

    /// <summary>
    /// Keep what the wire named this session's conversation, replacing what was kept. An id that is not a session's, or a
    /// blank conversation, keeps nothing; a write that fails is lost, which costs a resume and never the session.
    /// </summary>
    public void Keep(string session, string adapter, string conversation)
    {
        if (!SessionEventText.IsLocalId(session) || string.IsNullOrWhiteSpace(conversation) || string.IsNullOrWhiteSpace(adapter)) return;

        try
        {
            if (_bindings.InUse()) _bindings.Keep(session, adapter, conversation);
            else GoOnFiles.WriteConversation(PathOf(session), adapter, conversation);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DesktopStoreException or SqliteException)
        {
            using var log = new MachineLog(home, "driver");
            log.Warn("conversation.unkept", ("session", session), ("adapter", adapter), ("error", error.GetType().Name));
        }
    }

    /// <summary>
    /// Import every binding file the home holds, once the start's confirmation is settled (the loop calls it at each look until none is
    /// left): each conversation into the store, each file set aside.
    /// </summary>
    public BindingImport ImportFiles() => _bindings.ImportFiles();

    /// <summary>
    /// What a door tells the id its harness named to, for this session's record (MSG1c, D137 §4.2): a chat keeps its
    /// conversation as a quest's session does, on the protocol door from <c>session/new</c>'s answer and on the native door
    /// from its <c>init</c> line, so an ended chat the person writes to goes on in it.
    /// </summary>
    public Action<string> Keeping(string session, string adapter) => conversation => Keep(session, adapter, conversation);

    /// <summary>
    /// What was kept for this session, or null for nothing, a file that does not read, or an id that is not one: the binding once the home
    /// has imported its files, else the file. A file there after the import is an older build's, the later evidence: it is imported now, or,
    /// where the store will not take it, read as it stands, so its id wins either way.
    /// </summary>
    public HarnessConversation? Read(string session)
    {
        if (!SessionEventText.IsLocalId(session)) return null;
        try
        {
            if (!_bindings.InUse()) return GoOnFiles.ReadConversation(PathOf(session));
            if (File.Exists(PathOf(session)))
            {
                try
                {
                    _bindings.ImportFile(session);
                }
                catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
                {
                    // Not imported, and read below as it stands.
                }

                // Still there, the store would not take it (or the start is not settled): the file is the later evidence, as it stands.
                if (GoOnFiles.ReadConversation(PathOf(session)) is { } later) return later;
            }

            return _bindings.Read(session) is var (adapter, conversation) ? new HarnessConversation(adapter, conversation) : null;
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException)
        {
            return GoOnFiles.ReadConversation(PathOf(session));
        }
    }

    /// <summary>
    /// What this machine keeps for the session's conversation, apart from whether it reads: whether anything is kept, and whether what is
    /// kept reads (an access failure or invalid JSON is not an absent record, <see cref="Read"/> answering null for both). A binding in
    /// the store is a conversation; a file is read as it stands.
    /// </summary>
    public (bool Kept, bool Reads) Kept(string session)
    {
        if (!SessionEventText.IsLocalId(session)) return (false, false);
        try
        {
            if (_bindings.InUse() && _bindings.Read(session) is not null) return (true, true);
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException)
        {
            // The store would not say: the file, as it stands, is what is left to read.
        }

        try
        {
            using var _ = AtomicFile.OpenRead(PathOf(session));
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return (false, false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (true, false);
        }

        return (true, GoOnFiles.ReadConversation(PathOf(session)) is not null);
    }
}
