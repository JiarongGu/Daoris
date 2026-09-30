using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LEFT3 g: <c>landings.json</c> keeps a trace of every gone branch (REVIEW2, D113), and nothing pruned them, so the file
/// the clean-up, the hand-off, bringing up to date and Ask Daoris each read whole grew by one entry every landing. Each
/// repository now keeps its newest traces, by when each went, and a standing entry is never dropped. Files only.
/// </summary>
public sealed class LandedTracesTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-landed-traces-" + Guid.NewGuid().ToString("N")[..8]);

    public LandedTracesTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly DateTimeOffset At = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static LandedBranch Entry(string branch, string session, string repository = "engine") =>
        new(repository, "aurora", branch, "main", "tip00001", session, "q1", "Fix the gap", At);

    private static LandedBranch Trace(string branch, string session, DateTimeOffset gone, string repository = "engine") =>
        Entry(branch, session, repository) with { GoneAt = gone };

    [Fact]
    public void Each_repository_keeps_its_newest_traces_and_every_standing_entry()
    {
        var landings = new LandedBranches(_home);
        var kept = LandedBranches.TracesKept;
        landings.Record(Entry("feature/standing", "s-standing"));
        for (var i = 0; i < 3; i++) landings.Record(Trace($"feature/g{i}", $"g{i}", At.AddMinutes(i), repository: "game"));

        for (var i = 0; i < kept + 5; i++) landings.Record(Trace($"feature/q{i}", $"s{i}", At.AddMinutes(i)));

        // The five that went first are gone from the record; the newest stay, as many as a repository keeps.
        Assert.All(Enumerable.Range(0, 5), i => Assert.Null(landings.Landing($"s{i}")));
        Assert.All(Enumerable.Range(5, kept), i => Assert.NotNull(landings.Landing($"s{i}")));
        // Another repository's traces are its own count, and a standing entry is never a trace to drop.
        Assert.All(Enumerable.Range(0, 3), i => Assert.NotNull(landings.Landing($"g{i}")));
        Assert.Equal(["feature/standing"], landings.All().Select(entry => entry.Branch));
    }

    /// <summary>Which traces go is decided by when each went, never by where it sits in the file.</summary>
    [Fact]
    public void The_trace_that_went_first_goes_first_wherever_it_was_written()
    {
        var landings = new LandedBranches(_home);
        for (var i = 0; i < LandedBranches.TracesKept; i++) landings.Record(Trace($"feature/q{i}", $"s{i}", At.AddMinutes(i)));

        landings.Record(Trace("feature/old", "s-old", At.AddDays(-1)));

        Assert.Null(landings.Landing("s-old"));
        Assert.NotNull(landings.Landing("s0"));
    }
}
