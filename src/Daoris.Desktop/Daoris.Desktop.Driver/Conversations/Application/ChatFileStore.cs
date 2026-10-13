namespace Daoris.Driver;

/// <summary>
/// A session's attached files kept and found under the home (CONV4c, MSG1d3), under the names their callers use (STRUCT3h, as
/// D166's STRUCT3a note cuts a mixed type): <c>ChatFiles.Keep</c>, <c>Find</c> and <c>Kept</c> for the sessions' doors, the loop,
/// the modules and the runner. Another module reaches them here, in the Application; the rules are <see cref="ChatFiles"/>'s, pure,
/// and the folder is <see cref="ChatFileFolder"/>'s.
/// </summary>
public static class ChatFileStore
{
    extension(ChatFiles)
    {
        /// <summary>
        /// Keep a message's files, or refuse all of them in a sentence and keep none. Idempotent per
        /// content; written beside and renamed, so a half-written file is never what an agent reads.
        /// </summary>
        public static IReadOnlyList<KeptFile> Keep(string home, string sessionId, IReadOnlyList<ChatUpload> uploads)
        {
            if (uploads.Count == 0) return [];
            var folder = ChatFiles.Folder(home, sessionId);
            return ChatFileFolder.Keep(folder, ChatFiles.Keeping(uploads));
        }

        /// <summary>
        /// A file kept for a session under the name a record gives it (MSG1d3), where <c>Keep</c> laid it:
        /// <c>&lt;first 12 of its hash&gt;-&lt;name&gt;</c>. Of two kept under one name, the newer, which is the one said last. Null
        /// where none is kept by that name, or the id names no session.
        /// </summary>
        public static KeptFile? Find(string home, string sessionId, string name)
        {
            if (!SessionEvents.IsId(sessionId) || name.Length == 0) return null;
            var path = ChatFileFolder.Newest(ChatFiles.Folder(home, sessionId), leaf => ChatFiles.IsKeptAs(leaf, name));
            return path is null ? null : new KeptFile(name, path);
        }

        /// <summary>
        /// The files kept for a session under the names a record gives them (MSG1d3), in order: what a resumed run is handed of
        /// the words it goes on with. A name nothing keeps any more is left out, never named as a path that is not there.
        /// </summary>
        public static IReadOnlyList<KeptFile> Kept(string home, string sessionId, IReadOnlyList<string> names) =>
            [.. names.Select(name => ChatFiles.Find(home, sessionId, name)).OfType<KeptFile>()];
    }
}
