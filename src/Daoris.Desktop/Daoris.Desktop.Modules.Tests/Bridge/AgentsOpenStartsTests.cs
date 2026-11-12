using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Opening Agents starts no process for an account's usage: the page answers from the kept readings (<c>ACCOUNTS</c>,
/// <c>HARNESSES</c>), however old they are, and an account is asked its use again only at a press (<c>ACCOUNTS_READ</c>,
/// <i>Read now</i>), which asks none whose reading is younger than its question's floor. Every start is counted through the start
/// gate (<see cref="ProcessStarts.Within"/>), as <c>AgentSheetStartsTests</c> counts them.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class AgentsOpenStartsTests : DriverModuleBridge
{
    private void Read(string agent, string account, TimeSpan age) =>
        AccountWindows.Said(
            Home, agent, account, [new WindowReading("session", 0.4, DateTimeOffset.UtcNow.AddHours(2))], DateTimeOffset.UtcNow - age, null,
            source: AccountWindows.SourceRead);

    /// <summary>The starts <paramref name="open"/> made, by their file's name and arguments.</summary>
    private static async Task<List<string>> StartsOfAsync(Func<Task> open)
    {
        var starts = new List<string>();
        using (ProcessStarts.Within(info =>
        {
            lock (starts) starts.Add(string.Join(' ', [Path.GetFileName(info.FileName), .. info.ArgumentList]));
            return Process.Start(info);
        }))
        {
            await open();
        }

        return starts;
    }

    [Fact]
    public async Task Opening_the_page_starts_no_process_for_an_account_whose_reading_is_older_than_its_floor()
    {
        foreach (var agent in new[] { "codex", "claude-code" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, agent, "account-1"));
            Read(agent, "account-1", TimeSpan.FromHours(3));
        }

        var module = Module();
        // The roster's version reads are kept after the first, and are not this test's: what is counted is the page's open.
        await AnswerAsync(module, "HARNESSES");

        var starts = await StartsOfAsync(async () =>
        {
            await AnswerAsync(module, "ACCOUNTS");
            await AnswerAsync(module, "HARNESSES");
        });

        Assert.Empty(starts);
    }

    [Fact]
    public async Task Read_now_starts_no_process_for_an_account_whose_reading_is_younger_than_its_floor()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "codex", "account-1"));
        Read("codex", "account-1", TimeSpan.FromMinutes(1));
        var module = Module();

        var starts = await StartsOfAsync(() => AnswerAsync(module, "ACCOUNTS_READ", new { door = "codex" }));

        Assert.Empty(starts);
    }
}
