using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The head of every long session events record (<see cref="SessionEventHeads"/>), built at the first start after an update rather than by
/// the first window to open each conversation. It builds the newest-written record first and stops when <see cref="Budget"/> is spent, each
/// head saved as it is built; a record it left is built by its first reader as before, so the budget bounds the start and costs the reads
/// nothing. It changes no file. It ends when the owner's install has run it, and the row that deletes it is STARTPERF1's follow-up.
/// </summary>
public sealed class SessionEventHeadsStep(TimeSpan budget) : IHomeMigrationStep
{
    /// <summary>How long a start spends building heads before it leaves the rest to their readers.</summary>
    public static TimeSpan DefaultBudget { get; } = TimeSpan.FromSeconds(8);

    public SessionEventHeadsStep() : this(DefaultBudget)
    {
    }

    private const string Suffix = ".events.jsonl";

    public TimeSpan Budget => budget;

    public string Name => "session-event-heads";

    public void Run(HomeMigrationRun run)
    {
        var folder = DaorisHome.Entry(run.Home, HomeEntry.Sessions);
        if (!Directory.Exists(folder) || SessionEventHeads.In(folder) is not { } heads) return;

        var clock = Stopwatch.StartNew();
        var records = new DirectoryInfo(folder).EnumerateFiles("*" + Suffix)
            .Where(file => file.Length >= SessionEventHeads.HeadedFrom)
            .OrderByDescending(file => file.LastWriteTimeUtc);
        foreach (var file in records)
        {
            if (clock.Elapsed >= budget) return;
            var id = file.Name[..^Suffix.Length];
            if (SessionEventText.IsId(id)) heads.Of(id, file.FullName);
        }
    }
}
