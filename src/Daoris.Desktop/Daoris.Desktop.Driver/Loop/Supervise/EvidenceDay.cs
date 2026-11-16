using System.Text;

namespace Daoris.Driver;

/// <summary>The kinds of failure the evidence routine reads from the day's records.</summary>
public static class EvidenceKinds
{
    /// <summary>A run or session ended because it was stopped, not because it finished.</summary>
    public const string StoppedRun = "stopped-run";

    /// <summary>A step, request or question that waited with no holder, or on one never told.</summary>
    public const string UnheldWait = "unheld-wait";

    /// <summary>A red on the main line.</summary>
    public const string MainRed = "main-red";

    /// <summary>A review finding rated high.</summary>
    public const string ReviewHigh = "review-high";

    /// <summary>An act a grant or a re-check refused.</summary>
    public const string RefusedAct = "refused-act";
}

/// <summary>
/// One failure of the day as a record holds it: its kind, its record's own code (a node, a wait's actor, a gate, a finding's area,
/// a refusal's code), what happened in a line, where it is recorded, and when.
/// </summary>
public sealed record EvidenceFailure(string Kind, string Code, string What, string Where, DateTimeOffset At);

/// <summary>A row proposed for one root-cause class: the class, the check that would catch every member, and the day's failures in it.</summary>
public sealed record EvidenceRow(string Class, string Check, IReadOnlyList<EvidenceFailure> Evidence);

/// <summary>
/// The evidence routine (the canon's evidence-routine, run by the supervisor): the day's failures from the records, each with
/// its root-cause class, and one row proposed per class for the steward to accept, merge or decline. A declined class is not
/// proposed again. Due once a day after <see cref="Hour"/>, or at once on the steward's word. Decided with no I/O.
/// </summary>
public static class EvidenceDays
{
    /// <summary>The hour, UTC, after which the day's list is due.</summary>
    public const int Hour = 21;

    /// <summary>The class of one failure: its kind and its record's code, so one check can be written for every member.</summary>
    public static string ClassOf(EvidenceFailure failure)
    {
        var code = failure.Code.Trim().ToLowerInvariant();
        return $"{failure.Kind}:{(code.Length == 0 ? "unclassed" : code)}";
    }

    /// <summary>One row per class among the failures of <paramref name="day"/>, in the order each class first failed; none for a declined class.</summary>
    public static IReadOnlyList<EvidenceRow> Propose(DateTimeOffset day, IReadOnlyList<EvidenceFailure> failures, IReadOnlySet<string> declined) =>
    [
        .. Of(day, failures).GroupBy(ClassOf, StringComparer.Ordinal)
            .Where(group => !declined.Contains(group.Key))
            .Select(group => new EvidenceRow(group.Key, CheckOf(group.First()), [.. group])),
    ];

    /// <summary>Whether the day's list is due: once a day after its hour, or at once on the steward's word.</summary>
    public static bool Due(DateTimeOffset now, DateOnly? lastDay, bool word) =>
        word || (now.UtcDateTime.Hour >= Hour && lastDay != DateOnly.FromDateTime(now.UtcDateTime));

    /// <summary>The day's list as the steward reads it: each failure with its class and record, then each proposed row.</summary>
    public static string Markdown(DateTimeOffset day, IReadOnlyList<EvidenceFailure> failures, IReadOnlyList<EvidenceRow> rows)
    {
        var text = new StringBuilder();
        text.Append("# The day's failures, ").Append(DateOnly.FromDateTime(day.UtcDateTime).ToString("yyyy-MM-dd")).Append("\n\n");
        var today = Of(day, failures).ToList();
        if (today.Count == 0) return text.Append("No failure is in the day's records.\n").ToString();
        foreach (var failure in today)
        {
            text.Append("- ").Append(failure.At.UtcDateTime.ToString("HH:mm")).Append(" `").Append(ClassOf(failure)).Append("` ")
                .Append(failure.What).Append(" (").Append(failure.Where).Append(")\n");
        }

        text.Append("\n## Rows proposed, one per class, for the steward to accept, merge or decline\n\n");
        foreach (var row in rows)
        {
            text.Append("- `").Append(row.Class).Append("`: ").Append(row.Check).Append(' ').Append(row.Evidence.Count)
                .Append(row.Evidence.Count == 1 ? " failure today.\n" : " failures today.\n");
        }

        return text.ToString();
    }

    private static IEnumerable<EvidenceFailure> Of(DateTimeOffset day, IEnumerable<EvidenceFailure> failures)
    {
        var date = DateOnly.FromDateTime(day.UtcDateTime);
        return failures.Where(failure => DateOnly.FromDateTime(failure.At.UtcDateTime) == date).OrderBy(failure => failure.At);
    }

    private static string CheckOf(EvidenceFailure first) => first.Kind switch
    {
        EvidenceKinds.StoppedRun => $"A check that every run stopped at `{first.Code}` names why, and a sweep of the runs that stop there.",
        EvidenceKinds.UnheldWait => $"A census that every wait on `{first.Code}` has a holder who is told, and a sweep of the waits that have none.",
        EvidenceKinds.MainRed => $"The gate that should have stopped every `{first.Code}` red before it merged, and a sweep for its siblings.",
        EvidenceKinds.ReviewHigh => $"A check that finds every `{first.Code}` finding before review, and a sweep of the code it reaches.",
        EvidenceKinds.RefusedAct => $"A check that every act refused as `{first.Code}` is caught before it is tried, and a sweep of the acts like it.",
        _ => $"A check that catches every `{first.Code}` failure, and a sweep of its siblings.",
    };
}
