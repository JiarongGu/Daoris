using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A home shaped like an installed machine's for the session list: <see cref="BenchHome"/>'s service and records, every record with its
/// conversation's events file in the home's sessions folder, of the sizes a machine keeps (most a few dozen kilobytes, a few dozen of
/// five megabytes, some two hundred megabytes in all). Every word is invented; the shape is the measure, never a copy of a machine.
/// </summary>
internal sealed class SessionListHome : IAsyncDisposable
{
    /// <summary>How many records' files are long, and how long each is: the largest a machine keeps.</summary>
    public const int Long = 30;

    public const int LongBytes = 5 * 1024 * 1024;

    /// <summary>How long every other record's file is.</summary>
    public const int ShortBytes = 28 * 1024;

    private SessionListHome(BenchHome bench) => Bench = bench;

    public BenchHome Bench { get; }

    /// <summary>The bytes of every events file written.</summary>
    public long Bytes { get; private set; }

    /// <summary>A home of <paramref name="records"/> ended records, <paramref name="trees"/> with a tree, <paramref name="archived"/> archived, the first tree's build <paramref name="built"/> files.</summary>
    public static async Task<SessionListHome> MakeAsync(int records, int trees, int archived = 0, int built = 0)
    {
        var bench = await BenchHome.MakeAsync(records, trees, built);
        var made = new SessionListHome(bench);
        try
        {
            var listed = await bench.Client.SessionRecordsAsync();
            var ids = listed.Select(record => record.Id).ToList();
            if (archived > 0)
            {
                var every = Math.Max(1, listed.Count / archived);
                new ConversationFacts(bench.Home).Archive(
                    listed.Where((_, at) => at % every == 0).Take(archived).Select(ConversationArchive.Of), new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
            }

            var folder = DaorisHome.Entry(bench.Home, HomeEntry.Sessions);
            Directory.CreateDirectory(folder);
            var written = new long[ids.Count];
            Parallel.For(0, ids.Count, new ParallelOptions { MaxDegreeOfParallelism = 8 }, at =>
                written[at] = Write(Path.Combine(folder, $"{ids[at]}.events.jsonl"), at, at % (ids.Count / Long) == 0 ? LongBytes : ShortBytes));
            made.Bytes = written.Sum();
            return made;
        }
        catch
        {
            await made.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// One conversation's events to about <paramref name="bytes"/>: the person's opening, then turns of the agent's words, its calls and
    /// their usage, each turn ended, a word of the person's between; the last turn ended.
    /// </summary>
    private static long Write(string path, int session, int bytes)
    {
        var at = new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);
        var seq = 0L;
        var text = new StringBuilder();
        string Line(SessionEvent e) => JsonSerializer.Serialize(e with { Seq = ++seq, At = at.AddSeconds(seq) }, SessionEventText.Json);

        text.AppendLine(Line(new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = $"Look at the engine's build step {session}, and why it waits." }));
        var words = string.Join(' ', Enumerable.Repeat("The build reads its inputs and writes its outputs once.", 6));
        while (text.Length < bytes)
        {
            for (var call = 0; call < 8; call++)
            {
                text.AppendLine(Line(new SessionEvent { Kind = SessionEventKind.Message, Text = words }));
                text.AppendLine(Line(new SessionEvent { Kind = SessionEventKind.Tool, Id = $"call-{seq}", Title = $"Read part-{call}.txt", Status = "completed" }));
                text.AppendLine(Line(new SessionEvent { Kind = SessionEventKind.Usage }));
            }

            text.AppendLine(Line(new SessionEvent { Kind = SessionEventKind.Turn }));
            if (text.Length < bytes) text.AppendLine(Line(new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Go on." }));
        }

        File.WriteAllText(path, text.ToString());
        return new FileInfo(path).Length;
    }

    public ValueTask DisposeAsync() => Bench.DisposeAsync();
}
