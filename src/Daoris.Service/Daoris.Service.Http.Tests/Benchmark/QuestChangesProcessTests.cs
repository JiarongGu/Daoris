using System.Diagnostics;
using Xunit.Abstractions;

namespace Daoris.Service.Http.Tests.Benchmark;

/// <summary>
/// What a reader of every quest costs at an install's count (950 quests of about four kilobytes each): the whole list made again, as
/// every read made it before the list was kept by the store's mark, the whole list answered again while nothing moved, and the change
/// since the reader's mark, which is what each of the desktop's readers asks now.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class QuestChangesProcessTests(LocalHost host, ITestOutputHelper output) : IClassFixture<LocalHost>
{
    private const int Quests = 950;

    [Fact]
    public async Task A_reader_asking_since_its_mark_is_answered_what_moved_and_no_more()
    {
        var body = string.Concat(Enumerable.Repeat("Carry the change through the module and say what moved, with the evidence. ", 50));
        var at = DateTimeOffset.UtcNow;
        for (var quest = 0; quest < Quests; quest++) await host.Composed.Quests.PublishAsync("Asker", "Keeper", $"Quest {quest}", body, at.AddSeconds(quest));

        var (made, madeBytes) = await TimeAsync("/api/quests?includeClosed=true");
        var (kept, _) = await TimeAsync("/api/quests?includeClosed=true");
        var mark = (await host.GetAsync("/api/quests/changes")).Json.GetProperty("mark").GetString()!;
        var (since, sinceBytes) = await TimeAsync($"/api/quests/changes?since={Uri.EscapeDataString(mark)}");
        await host.Composed.Quests.PublishAsync("Asker", "Keeper", "One more", body, at.AddHours(1));
        var (moved, movedBytes) = await TimeAsync($"/api/quests/changes?since={Uri.EscapeDataString(mark)}");

        output.WriteLine($"{Quests} quests: the whole list made {made:F0} ms ({madeBytes / 1024} KB), answered again unmoved {kept:F0} ms");
        output.WriteLine($"the change since a mark, nothing moved: {since:F1} ms ({sinceBytes} bytes); one quest moved: {moved:F0} ms ({movedBytes / 1024} KB)");
        Assert.True(sinceBytes < madeBytes / 10, $"the change since a mark carried {sinceBytes} bytes, the whole list {madeBytes}");
    }

    private async Task<(double Ms, long Bytes)> TimeAsync(string route)
    {
        var clock = Stopwatch.StartNew();
        var answer = await host.GetAsync(route);
        clock.Stop();
        Assert.Equal(200, answer.Status);
        return (clock.Elapsed.TotalMilliseconds, answer.Body.Length);
    }
}
