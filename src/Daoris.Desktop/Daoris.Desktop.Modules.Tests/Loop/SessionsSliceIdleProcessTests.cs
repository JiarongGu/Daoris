using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Shenora.Core.Events;
using Xunit.Abstractions;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What the sessions slice costs a period with no window open and nothing moved, over an install's count of records (1,261): each
/// period judged every record and compared every partition, where the kept list handed back unmoved is now judged once.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class SessionsSliceIdleProcessTests(ITestOutputHelper output)
{
    private const int Records = 1261;
    private const int Periods = 20;

    private static IReadOnlyList<JsonObject> List() =>
    [
        .. Enumerable.Range(0, Records).Select(at => new JsonObject
        {
            ["id"] = $"s{at:D4}", ["quest"] = $"q{at % 950}", ["repository"] = $"repository-{at % 47}", ["door"] = "claude-code",
            ["state"] = at % 50 == 0 ? "working" : "completed", ["kind"] = at % 3 == 0 ? "chat" : "driven",
            ["note"] = $"what session {at} did, in a sentence or two of the note it ended with", ["evidence"] = $"commits landed:\n{at:x7} the change",
            ["created"] = "2026-10-19T08:00:00+00:00", ["updated"] = "2026-10-19T09:00:00+00:00", ["workspace"] = at % 5 == 0 ? "games" : "default",
            ["tree"] = $"X:/daoris/trees/repository-{at % 47}/s{at:D4}", ["baseCommit"] = new string('a', 40),
            ["said"] = new JsonArray(), ["noteParts"] = new JsonArray(new JsonObject { ["words"] = "done" }),
            ["ran"] = new JsonObject { ["models"] = new JsonArray("reported-model"), ["more"] = 0 }, ["work"] = "build",
        }),
    ];

    [Fact]
    public async Task A_period_with_nothing_moved_judges_and_compares_nothing()
    {
        var list = List();
        var before = await PeriodsAsync(_ => SessionsStateSlice.Judge(list));
        var once = new JudgedOnce<IReadOnlyList<JsonObject>>(SessionsStateSlice.Judge);
        var after = await PeriodsAsync(_ => once.Of(list));

        output.WriteLine($"{Records} records, {Periods} periods with nothing moved: judged every period {before:F2} ms a period, judged once {after:F3} ms a period");
        Assert.True(after < before, $"judged once took {after} ms a period, judged every period {before} ms");
    }

    private static async Task<double> PeriodsAsync(Func<int, IReadOnlyList<SliceJudged>> judge)
    {
        var slice = new StateSlice("sessions", new EventBus(NullLogger<EventBus>.Instance), _ => Task.FromResult<IReadOnlyList<SliceJudged>?>(judge(0)))
        {
            Keyed = true,
        };
        await slice.JudgeAsync();
        var clock = Stopwatch.StartNew();
        for (var period = 0; period < Periods; period++) await slice.JudgeAsync();
        return clock.Elapsed.TotalMilliseconds / Periods;
    }
}
