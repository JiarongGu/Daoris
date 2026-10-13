namespace Daoris.Driver;

/// <summary>
/// A session's transcript file opened on the disk (D46 §4), as it always was: shared for reading, from its start or going on
/// from what a run before it left. Conversations' Infrastructure (STRUCT3h, D166's note): the writer every door shares, its lock
/// and its flushes, is the Application's <see cref="TranscriptFile"/>.
/// </summary>
internal static class TranscriptStreams
{
    /// <summary>The transcript at <paramref name="path"/>, opened to write.</summary>
    public static StreamWriter Open(string path, bool append) => new(path, append);
}
