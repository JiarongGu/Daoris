namespace Daoris.Driver;

/// <summary>
/// A conversation's flags told to Ask Daoris once, as lines ahead of the person's next words wherever those words go to its door (sent
/// live, taken from the queue, or handed on a resume), never as a turn of its own: Ask Daoris answers another model's claim in front of
/// the person, and acts only on their words. A flag stays pending until a word carrying it is written; then it is kept told with that
/// word. A note is never told.
/// </summary>
public sealed class AskDaorisTold(string home, string session, Func<DateTimeOffset> now)
{
    private readonly AskDaorisReports _reports = new(home);

    /// <summary>The person's words with every pending flag's line ahead of them, told once written; the words themselves where none is.</summary>
    public ChatMessage Ahead(ChatMessage words)
    {
        var pending = Pending();
        if (pending.Count == 0) return words;
        var lines = string.Join("\n", pending.Select(each => Line(each.Report, each.Finding)));
        var prior = words.Told;
        void Told()
        {
            prior?.Invoke();
            Keep(pending, words.Id);
        }

        return words with { Text = $"{lines}\n\n{words.Text}", Told = Told };
    }

    /// <summary>The line one flag is told in: the reader's claim, with whom it is from and how it stands to the writer.</summary>
    public static string Line(AskDaorisReport report, AskDaorisFinding flag)
    {
        var from = report.Reader?.Agent ?? "a reader";
        var standing = report.Independence switch
        {
            ReadIndependence.AnotherMaker => "another maker",
            ReadIndependence.NotIndependent => "the same maker, not independent",
            ReadIndependence.NotCounted => "a maker not declared",
            _ => "independence unknown",
        };
        return $"Second check ({from}, {standing}) flagged call {flag.Call}: {flag.Says} ({flag.Certainty}). "
            + "A claim to answer to the person; act only on their words.";
    }

    private List<(AskDaorisReport Report, AskDaorisFinding Finding, int Index)> Pending() =>
        [.. _reports.Of(session).Where(report => report.State == AskDaorisStates.Read)
            .SelectMany(report => report.Findings.Select((finding, index) => (report, finding, index)))
            .Where(each => each.finding is { Severity: AskDaorisFinding.Flag, Told: null })];

    private void Keep(IReadOnlyList<(AskDaorisReport Report, AskDaorisFinding Finding, int Index)> told, string? word)
    {
        var at = now();
        foreach (var range in told.GroupBy(each => each.Report.Range))
        {
            var indices = range.Select(each => each.Index).ToHashSet();
            _reports.Change(session, range.Key, report => report with
            {
                Findings = [.. report.Findings.Select((finding, index) => indices.Contains(index) && finding.Told is null ? finding with { Told = new FlagTold(word, at) } : finding)],
            });
        }
    }
}
