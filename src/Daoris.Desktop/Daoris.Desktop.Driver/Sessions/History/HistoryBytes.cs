namespace Daoris.Driver;

/// <summary>
/// What the home holds, by kind, in bytes (HIST1c, the history-clearing design §2.4): a conversation, a transcript, a
/// session's files, the kept files of a quest or an ask, and the small files beside them (a conversation id, a marker,
/// a mark, a choice, the spawn files). Measuring a path is <see cref="Of"/>.
/// </summary>
public sealed record HistoryBytes(long Conversations, long Transcripts, long Files, long Kept, long Other)
{
    public static HistoryBytes None { get; } = new(0, 0, 0, 0, 0);

    public long Total => Conversations + Transcripts + Files + Kept + Other;

    public static HistoryBytes operator +(HistoryBytes a, HistoryBytes b) => new(
        a.Conversations + b.Conversations, a.Transcripts + b.Transcripts, a.Files + b.Files, a.Kept + b.Kept, a.Other + b.Other);

    /// <summary>A file's size, or a folder's whole size; nothing for what is not there or will not be read.</summary>
    public static long Of(string path)
    {
        try
        {
            if (File.Exists(path)) return new FileInfo(path).Length;
            if (!Directory.Exists(path)) return 0;
            return new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
