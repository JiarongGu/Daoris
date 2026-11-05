using System.Diagnostics;
using Xunit.Abstractions;

namespace Daoris.Service.Http.Tests.Benchmark;

/// <summary>
/// The service answers the window's reads within their budgets at a home's real size: the host is started as the shell starts
/// it over <see cref="BenchmarkHome"/>, and each read the window and the driver's look make is timed, one after another, after one
/// read that warms it.
/// </summary>
/// <remarks>
/// <para>The budgets are the window's. A list it polls while the person looks at it (the sessions, the quests, the asks, what waits on
/// the person) is answered in 200 ms at the median, inside the tenth of a second or two in which a page still reads as answering
/// at once, with room for the page's own render. A read the person waits on once, on a press or a page's first paint (the registry,
/// the repositories, the history to clear, the whole session list for the page's older groups), is answered in a second. The
/// slowest of the samples is held to five times its median, so an answer that is quick only when nothing is read stays a failure.</para>
/// <para>Each budget is what the read costs over this home, with the margin a busy machine takes: the reading is printed whole, so a
/// failure says which read and by how much.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class RouteBudgetProcessTests(ITestOutputHelper output) : IDisposable
{
    private const int Samples = 9;

    private static readonly (string Route, int Budget)[] Reads =
    [
        ("/api/sessions", 200),
        ("/api/sessions?workspace=default", 200),
        ("/api/sessions?includeClosed=true", 1000),
        ("/api/quests", 200),
        ("/api/quests?includeClosed=true", 1000),
        ("/api/asks?workspace=default", 200),
        ("/api/asks?workspace=default&includeClosed=true", 1000),
        ("/api/memory/waits", 200),
        ("/api/history?workspace=default", 1000),
        ("/api/registry", 1000),
        ("/api/registry?workspace=default", 1000),
        ("/api/repositories", 1000),
    ];

    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "daoris-budget-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        for (var attempt = 0; attempt < 20 && Directory.Exists(_scratch); attempt++)
        {
            try
            {
                Directory.Delete(_scratch, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }

    [Fact]
    public async Task Every_read_the_window_makes_answers_in_its_budget_over_a_home_of_three_thousand_sessions()
    {
        var clock = Stopwatch.StartNew();
        await BenchmarkHome.WriteAsync(_scratch);
        output.WriteLine($"home written in {clock.ElapsedMilliseconds} ms");

        using var host = await RealHost.StartAsync(_scratch, new Dictionary<string, string?>());
        using var client = new HttpClient { BaseAddress = new Uri(host.Url), Timeout = TimeSpan.FromMinutes(3) };
        var over = new List<string>();
        output.WriteLine("one after another:");
        foreach (var (route, budget) in Reads)
        {
            var first = await TimeAsync(client, route);
            var times = new List<double>();
            for (var sample = 0; sample < Samples; sample++) times.Add(await TimeAsync(client, route));
            Judge(route, budget, times, $"first {first,8:F0} ms", over);
        }

        // A window's first paint and the driver's look ask together, so one read's cost is every other's wait.
        output.WriteLine("all at once, three rounds:");
        var together = Reads.ToDictionary(read => read.Route, _ => new List<double>());
        for (var round = 0; round < 3; round++)
        {
            var answers = await Task.WhenAll(Reads.Select(async read => (read.Route, Time: await TimeAsync(client, read.Route))));
            foreach (var (route, time) in answers) together[route].Add(time);
        }

        foreach (var (route, budget) in Reads) Judge(route, budget * 3, together[route], "together", over);

        // The index reads every checkout again on a rescan, as it does each one that moved; the registry is read meanwhile.
        output.WriteLine("while the index reads every checkout again:");
        var rescan = client.PostAsync("/api/refresh", null);
        var during = new List<double>();
        for (var sample = 0; sample < Samples; sample++) during.Add(await TimeAsync(client, "/api/registry?workspace=default"));
        var rescanned = await rescan;
        output.WriteLine($"the rescan answered {(int)rescanned.StatusCode}");
        Judge("/api/registry?workspace=default", 1000, during, "rescan", over);

        Assert.True(over.Count == 0, "over budget:\n" + string.Join('\n', over));
    }

    private void Judge(string route, int budget, List<double> times, string label, List<string> over)
    {
        times.Sort();
        var (median, slowest) = (times[times.Count / 2], times[^1]);
        var line = $"{route,-50} {label,-16} p50 {median,8:F0} ms   max {slowest,8:F0} ms   budget {budget} ms";
        output.WriteLine(line);
        if (median > budget || slowest > budget * 5) over.Add(line);
    }

    private static async Task<double> TimeAsync(HttpClient client, string route)
    {
        var clock = Stopwatch.StartNew();
        using var answer = await client.GetAsync(route);
        var body = await answer.Content.ReadAsStringAsync();
        clock.Stop();
        Assert.True(answer.IsSuccessStatusCode, $"{route} answered {(int)answer.StatusCode}: {body}");
        return clock.Elapsed.TotalMilliseconds;
    }
}
