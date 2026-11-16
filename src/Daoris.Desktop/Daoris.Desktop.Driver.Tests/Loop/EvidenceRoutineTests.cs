using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// METHODS1 c on the look: the evidence routine runs once a day after its hour or at once on the steward's word, keeps its proposed
/// class rows for the steward role, and an accepted row reaches the backlog through the records door alone; a declined class is not
/// proposed again.
/// </summary>
public sealed class EvidenceRoutineTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-evidence-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static readonly DateTimeOffset Evening = new(2026, 11, 16, 21, 30, 0, TimeSpan.Zero);

    private sealed class Records : IEvidenceRecords
    {
        public int Reads { get; private set; }

        public Task<IReadOnlyList<EvidenceFailure>> ReadAsync(DateOnly day, CancellationToken ct)
        {
            Reads++;
            return Task.FromResult<IReadOnlyList<EvidenceFailure>>([
                new(EvidenceKinds.StoppedRun, "check", "a run stopped at its check", "workflows/runs/r1", Evening.AddHours(-3)),
                new(EvidenceKinds.UnheldWait, "review", "a review waited on nobody", "workflows/runs/r2", Evening.AddHours(-2)),
            ]);
        }
    }

    private sealed class Backlog : IEvidenceBacklog
    {
        public List<EvidenceRow> Appended { get; } = [];

        public Task<bool> AppendAsync(EvidenceRow row, CancellationToken ct)
        {
            Appended.Add(row);
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task It_runs_once_a_day_after_its_hour_or_at_once_on_the_stewards_word()
    {
        var records = new Records();
        var routine = new EvidenceRoutine(_home, records);

        Assert.Null(await routine.RunAsync(Evening.AddHours(-2), word: false, default));
        var day = await routine.RunAsync(Evening, word: false, default);
        Assert.Equal(2, day!.Rows.Count);
        Assert.Null(await routine.RunAsync(Evening.AddMinutes(10), word: false, default));
        Assert.NotNull(await routine.RunAsync(Evening.AddMinutes(20), word: true, default));
        Assert.Equal(2, records.Reads);
    }

    [Fact]
    public async Task An_accepted_row_goes_through_the_records_door_and_a_declined_class_is_not_proposed_again()
    {
        var routine = new EvidenceRoutine(_home, new Records());
        var day = (await routine.RunAsync(Evening, word: true, default))!;
        var backlog = new Backlog();

        Assert.True(await routine.AcceptAsync(day.Day, "stopped-run:check", backlog, default));
        Assert.False(await routine.AcceptAsync(day.Day, "stopped-run:check", backlog, default));
        routine.Decline(day.Day, "unheld-wait:review");

        Assert.Equal(["stopped-run:check"], backlog.Appended.Select(row => row.Class));
        Assert.False(File.Exists(Path.Combine(_home, "TASKS.md")));
        var again = (await routine.RunAsync(Evening.AddMinutes(5), word: true, default))!;
        Assert.DoesNotContain(again.Rows, row => row.Class == "unheld-wait:review");
        Assert.Equal(EvidenceRoutine.Accepted, routine.Read(day.Day)!.States["stopped-run:check"]);
    }
}
