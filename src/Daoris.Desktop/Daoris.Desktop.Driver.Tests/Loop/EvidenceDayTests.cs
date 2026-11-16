using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// METHODS1 d: the evidence routine. A day's failures, read from the records handed to it, each given its root-cause class; one
/// proposed row per class, never per failure; a class the steward declined is not proposed again; and the routine is due once a
/// day after its hour, or at once on the steward's word.
/// </summary>
public sealed class EvidenceDayTests
{
    private static readonly DateTimeOffset Day = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);

    private static EvidenceFailure Failure(string kind, string code, int hour, string where = "run r1") =>
        new(kind, code, $"{kind} {code}", where, Day.AddHours(hour));

    [Fact]
    public void Each_failure_is_given_its_class_by_its_kind_and_code()
    {
        Assert.Equal("stopped-run:review", EvidenceDays.ClassOf(Failure(EvidenceKinds.StoppedRun, "review", 9)));
        Assert.Equal("unheld-wait:actor", EvidenceDays.ClassOf(Failure(EvidenceKinds.UnheldWait, "actor", 9)));
        Assert.Equal("main-red:census", EvidenceDays.ClassOf(Failure(EvidenceKinds.MainRed, "census", 9)));
        Assert.Equal("refused-act:target-changed", EvidenceDays.ClassOf(Failure(EvidenceKinds.RefusedAct, "Target-Changed", 9)));
        Assert.Equal("review-high:unclassed", EvidenceDays.ClassOf(Failure(EvidenceKinds.ReviewHigh, " ", 9)));
    }

    [Fact]
    public void One_row_is_proposed_per_class_with_its_failures_as_evidence_and_only_the_day_s()
    {
        var failures = new[]
        {
            Failure(EvidenceKinds.UnheldWait, "actor", 9, "run r1"), Failure(EvidenceKinds.UnheldWait, "actor", 14, "run r2"),
            Failure(EvidenceKinds.MainRed, "census", 11), Failure(EvidenceKinds.MainRed, "census", -3),
        };

        var rows = EvidenceDays.Propose(Day, failures, declined: new HashSet<string>());

        Assert.Equal(["unheld-wait:actor", "main-red:census"], rows.Select(row => row.Class));
        Assert.Equal(2, rows[0].Evidence.Count);
        Assert.Single(rows[1].Evidence);
        Assert.Contains("every", rows[0].Check, StringComparison.Ordinal);
    }

    [Fact]
    public void A_class_the_steward_declined_is_not_proposed_again()
    {
        var rows = EvidenceDays.Propose(Day, [Failure(EvidenceKinds.StoppedRun, "review", 10), Failure(EvidenceKinds.RefusedAct, "grant", 12)],
            declined: new HashSet<string> { "stopped-run:review" });

        Assert.Equal(["refused-act:grant"], rows.Select(row => row.Class));
    }

    [Fact]
    public void The_day_s_list_says_each_failure_and_each_proposed_row()
    {
        var failures = new[] { Failure(EvidenceKinds.ReviewHigh, "unreached-reader", 15, "opinion o1") };
        var text = EvidenceDays.Markdown(Day, failures, EvidenceDays.Propose(Day, failures, new HashSet<string>()));

        Assert.Contains("# The day's failures, 2026-11-16", text, StringComparison.Ordinal);
        Assert.Contains("`review-high:unreached-reader`", text, StringComparison.Ordinal);
        Assert.Contains("opinion o1", text, StringComparison.Ordinal);
        Assert.Contains("for the steward to accept", text, StringComparison.Ordinal);
        Assert.Contains("No failure is in the day's records.", EvidenceDays.Markdown(Day, [], []), StringComparison.Ordinal);
    }

    [Fact]
    public void The_routine_is_due_once_a_day_after_its_hour_or_at_once_on_the_steward_s_word()
    {
        var evening = Day.AddHours(EvidenceDays.Hour);

        Assert.False(EvidenceDays.Due(evening.AddMinutes(-1), lastDay: null, word: false));
        Assert.True(EvidenceDays.Due(evening, lastDay: null, word: false));
        Assert.False(EvidenceDays.Due(evening.AddHours(1), lastDay: DateOnly.FromDateTime(Day.UtcDateTime), word: false));
        Assert.True(EvidenceDays.Due(Day.AddHours(8), lastDay: DateOnly.FromDateTime(Day.UtcDateTime), word: true));
    }
}
