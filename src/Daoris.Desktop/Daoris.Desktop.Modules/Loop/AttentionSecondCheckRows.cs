using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.AttentionJson;

namespace Daoris.Desktop;

/// <summary>
/// The second check's flags the person has not marked seen, one row each under Ask Daoris's line, counted on the bell: a flag on a turn
/// with no press included. Its acts are <i>Open the conversation</i> and <i>Seen</i>; nothing is held, so the row holds nothing up, and
/// a note is never a row. Every window shows it: Ask Daoris's reach is the machine's.
/// </summary>
public static class AttentionSecondCheckRows
{
    public static IEnumerable<JsonObject> Rows(AttentionFacts facts)
    {
        foreach (var flag in facts.SecondCheck)
        {
            var (session, range, index) = (Text(flag, "session"), Text(flag, "range"), Part(flag["index"]));
            var row = Row("second-check", $"{session}#{range}#{index}", Text(flag, "at"));
            row["session"] = session;
            row["secondCheck"] = flag.DeepClone();
            yield return row;
        }
    }

    /// <summary>The flags not yet seen, in the shape the facts carry them.</summary>
    public static IEnumerable<object> Unseen(string home) =>
        AskDaorisReadings.Unseen(new AskDaorisReports(home)).Select(flag => new
        {
            flag.Session, flag.Range, flag.Index, flag.Says, flag.Certainty, flag.Call, At = Moments.Write(flag.At), flag.Reader, flag.Independence,
        });

    /// <summary>Wake the slice where a report of <paramref name="home"/> is written: a reading closed, or a flag seen.</summary>
    public static IDisposable Hear(string home, Action wake)
    {
        void Heard(string written, string _)
        {
            if (string.Equals(written, home, StringComparison.Ordinal)) wake();
        }

        AskDaorisReports.Changed += Heard;
        return new Hearing(() => AskDaorisReports.Changed -= Heard);
    }

    private sealed class Hearing(Action stop) : IDisposable
    {
        public void Dispose() => stop();
    }
}
