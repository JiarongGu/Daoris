using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The heavy executor's check queue by class: three ordinary checks wait for the one slot and an urgent quest's check asks after them;
/// it takes the next freed slot, the waiting rows say so in that order, and the others follow in the order they first arrived.
/// </summary>
/// <remarks>Real git, node and job processes: the Process half's.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class HeavyExecutorPriorityProcessTests : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "heavy-priority", Guid.NewGuid().ToString("N")[..8]);
    private HeavyExecutor? _executor;
    private string Home => Path.Combine(_folder, "home");
    private string Starts => Path.Combine(_folder, "starts.log");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Home);
        await File.WriteAllTextAsync(Path.Combine(_folder, "gate.mjs"), """
            import { appendFileSync } from 'node:fs';
            const [tag, log] = process.argv.slice(2);
            appendFileSync(log, `${tag}\n`);
            """);
    }

    public async Task DisposeAsync()
    {
        if (_executor is not null) await _executor.DisposeAsync();
        GitSeed.Discard(_folder);
    }

    private async Task<VerifySend> SendOfAsync(int at)
    {
        var checkout = Path.Combine(_folder, $"r{at}");
        Directory.CreateDirectory(checkout);
        await GitFixture.GitAsync(checkout, "init", "-q", "-b", "main");
        await GitFixture.GitAsync(checkout, "config", "user.email", "test@example.com");
        await GitFixture.GitAsync(checkout, "config", "user.name", "Test");
        var gate = $"node \"{Path.Combine(_folder, "gate.mjs")}\" \"t{at}\" \"{Starts}\"";
        await File.WriteAllTextAsync(Path.Combine(checkout, "daoris.gates.json"), JsonSerializer.Serialize(new { gates = new[] { new { name = "unit", run = gate, heavy = true } } }));
        await GitFixture.GitAsync(checkout, "add", "-A");
        await GitFixture.GitAsync(checkout, "commit", "-q", "-m", "gates");
        var commit = (await GitFixture.GitAsync(checkout, "rev-parse", "HEAD")).Trim();
        var blob = (await GitFixture.GitAsync(checkout, "rev-parse", "HEAD:daoris.gates.json")).Trim();
        return new VerifySend($"v-000000000000000{at}", $"i-{at}", $"r{at}", checkout, commit, blob) { Tree = checkout + "-verify" };
    }

    private string Rows() => string.Join("; ", VerifySlot.Look(Home).Waiters.Select(waiter => $"{waiter.Invocation} {waiter.Class} for {waiter.For ?? "-"}"))
        + " | holders: " + string.Join(", ", VerifySlot.Look(Home).Holders.Select(holder => holder.Invocation));

    [Fact]
    public async Task An_urgent_quest_s_check_asking_after_three_ordinary_ones_takes_the_next_freed_slot()
    {
        var sends = new Dictionary<int, VerifySend>();
        foreach (var at in new[] { 1, 2, 3, 4 }) sends[at] = await SendOfAsync(at);
        _executor = new HeavyExecutor(Home, new VerifyWorld { Checks = () => 1, Poll = TimeSpan.FromMilliseconds(50) });
        var set = new DateTimeOffset(2026, 11, 26, 11, 0, 0, TimeSpan.Zero);
        Assert.Null(PriorityBook.Set(Home, new PriorityEvent(set, PriorityBook.Quest, "v-0000000000000004", "games", PriorityClasses.Urgent, "the fix that slows every session", "the person")
        {
            Until = set.AddYears(50),
        }, set, PriorityRules.Default));

        using (var held = VerifySlot.TryTake(Home))
        {
            Assert.NotNull(held);
            foreach (var at in new[] { 1, 2, 3, 4 })
            {
                Assert.True(await _executor.SendAsync(sends[at]));
                await Poll.Until(() => VerifySlot.Look(Home).Waiters.Count == at, Rows);
            }

            await Poll.Until(() => VerifySlot.Look(Home).Waiters is { Count: 4 } waiters && waiters.All(waiter => waiter.For is not null) && waiters[0].Class == PriorityClasses.Urgent, Rows);
            Assert.Equal(["i-4", "i-1", "i-2", "i-3"], VerifySlot.Look(Home).Waiters.Select(waiter => waiter.Invocation));
        }

        foreach (var at in new[] { 4, 1, 2, 3 })
        {
            var result = VerifyFiles.ResultOf(Home, sends[at].Run, sends[at].Invocation);
            await Poll.Until(() => File.Exists(result), () => $"no result at {result}; " + Rows());
            Assert.Equal(VerifyVerdicts.Reached, VerifyResult.Read(result)?.Verdict);
        }

        Assert.Equal(["t4", "t1", "t2", "t3"], File.ReadAllLines(Starts));
    }
}
