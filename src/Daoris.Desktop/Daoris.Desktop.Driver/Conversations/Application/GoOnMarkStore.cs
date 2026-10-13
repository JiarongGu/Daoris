namespace Daoris.Driver;

/// <summary>
/// The go-on marks kept, read and taken away (MSG1b), under the names their callers use (STRUCT3h, as D166's STRUCT3a note cuts
/// a mixed type): <c>marks.Mark</c>, <c>Read</c>, <c>Clear</c> and <c>For</c> for the loop's look, the runner, the planner's
/// snapshot and the session groups. What is kept is this use case's; the file and its format are <see cref="GoOnFiles"/>'s.
/// </summary>
public static class GoOnMarkStore
{
    extension(GoOnMarks marks)
    {
        /// <summary>
        /// Mark these words as judged unable to go on, replacing any mark before. An id that is not a session's keeps nothing,
        /// and a write that fails is lost, which costs one more try at the next look.
        /// </summary>
        public void Mark(string session, IEnumerable<string> said, ContinueReason why, DateTimeOffset at)
        {
            var ids = said.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
            if (!SessionEvents.IsId(session) || ids.Count == 0) return;

            try
            {
                GoOnFiles.WriteMark(marks.PathOf(session), ids, why.Code, at);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Lost: the next look tries the words again and marks them then.
            }
        }

        /// <summary>The mark kept for this session, or null for none, a file that does not read, or an id that is not one.</summary>
        public GoOnMark? Read(string session) => SessionEvents.IsId(session) ? GoOnFiles.ReadMark(marks.PathOf(session)) : null;

        /// <summary>Take a session's mark away: its words went on, or were taken. Nothing to take is no failure.</summary>
        public void Clear(string session)
        {
            // A mark left behind judges only the words it names; a word said since is tried again all the same.
            if (SessionEvents.IsId(session)) GoOnFiles.Delete(marks.PathOf(session));
        }

        /// <summary>The marks a look plans by: one per record the person's words wait on that has one, by session.</summary>
        public IReadOnlyDictionary<string, GoOnMark> For(IEnumerable<PriorSession> records)
        {
            var found = new Dictionary<string, GoOnMark>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in records.Where(record => record.WordsWaiting))
            {
                if (marks.Read(record.Session) is { } mark) found[record.Session] = mark;
            }

            return found;
        }
    }
}
