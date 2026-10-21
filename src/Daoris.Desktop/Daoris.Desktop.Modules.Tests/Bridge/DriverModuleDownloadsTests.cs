using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The Downloads row's list and press over the bridge (`SystemRoutes.cs`, CACHECLEAR1): the read lists each downloaded version with
/// what keeps it and removes nothing; the press removes only the rows it names that nothing keeps, saying what it kept and why.
/// </summary>
public sealed class DriverModuleDownloadsTests : DriverModuleBridge
{
    /// <summary>A version folder written long ago, so nothing reads as a download a moment old.</summary>
    private static string Old(string folder)
    {
        Directory.CreateDirectory(Path.Combine(folder, "bin"));
        File.WriteAllText(Path.Combine(folder, "bin", "claude.exe"), "claude");
        var longAgo = DateTime.UtcNow.AddDays(-3);
        Directory.SetCreationTimeUtc(folder, longAgo);
        Directory.SetLastWriteTimeUtc(folder, longAgo);
        return folder;
    }

    [Fact]
    public async Task The_list_says_what_keeps_each_version_and_the_press_removes_only_what_nothing_keeps()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        var home = loop.Home;
        var pinned = Old(HarnessSettings.ManagedHome(home, "claude-code", "2.1.0"));
        var unused = Old(HarnessSettings.ManagedHome(home, "claude-code", "2.0.1"));
        new HarnessSettings(Versions: new Dictionary<string, string> { ["claude-code"] = "2.1.0" }).Save(loop.Harnesses.SettingsPath);

        var listed = (await AnswerAsync(module, "SYSTEM_DOWNLOADS")).GetProperty("versions").EnumerateArray().ToArray();
        Assert.Equal(["2.0.1", "2.1.0"], listed.Select(row => row.GetProperty("version").GetString()));
        // Nothing keeps it: the bridge leaves the null out, which the page reads as none.
        Assert.False(listed[0].TryGetProperty("keeps", out var keeps) && keeps.ValueKind != JsonValueKind.Null);
        Assert.Equal("pinned", listed[1].GetProperty("keeps").GetString());
        Assert.True(Directory.Exists(unused), "the list removes nothing");

        var answer = await AnswerAsync(module, "SYSTEM_DOWNLOADS_CLEAR", new
        {
            versions = listed.Select(row => new { kind = row.GetProperty("kind").GetString(), name = row.GetProperty("name").GetString(), version = row.GetProperty("version").GetString() }).ToArray(),
        });

        var cleared = answer.GetProperty("cleared").EnumerateArray().ToArray();
        Assert.Equal(["removed", "kept"], cleared.Select(row => row.GetProperty("outcome").GetString()));
        Assert.Equal("pinned", cleared[1].GetProperty("keeps").GetString());
        Assert.False(Directory.Exists(unused));
        Assert.True(Directory.Exists(pinned));
    }

    /// <summary>
    /// KEEP1b (the cleanup design §9): the Downloads row says each version's date by the rule: the rule's days, and for a version nothing
    /// uses when its current unused stretch began and when the rule takes it, from <c>keeping.json</c>; a pinned one has neither.
    /// </summary>
    [Fact]
    public async Task The_list_says_when_the_rule_takes_each_unused_version()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        var home = loop.Home;
        Old(HarnessSettings.ManagedHome(home, "claude-code", "2.1.0"));
        Old(HarnessSettings.ManagedHome(home, "claude-code", "2.0.1"));
        new HarnessSettings(Versions: new Dictionary<string, string> { ["claude-code"] = "2.1.0" }).Save(loop.Harnesses.SettingsPath);
        var since = new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
        Assert.Null(KeepingRecord.WriteUnused(home, KeepingRecord.Read(home), new Dictionary<string, DateTimeOffset?> { ["agent/claude-code/2.0.1"] = since }));

        var answer = await AnswerAsync(module, "SYSTEM_DOWNLOADS");

        Assert.Equal(7, answer.GetProperty("days").GetInt32());
        var rows = answer.GetProperty("versions").EnumerateArray().ToArray();
        Assert.Equal(since, rows[0].GetProperty("unusedSince").GetDateTimeOffset());
        Assert.Equal(since.AddDays(7), rows[0].GetProperty("goes").GetDateTimeOffset());
        Assert.False(rows[1].TryGetProperty("goes", out var goes) && goes.ValueKind != JsonValueKind.Null, "a pinned version goes by no rule");
    }

    [Fact]
    public async Task A_press_that_names_no_versions_is_malformed()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "SYSTEM_DOWNLOADS_CLEAR", new { }));
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "SYSTEM_DOWNLOADS_CLEAR", new { versions = new[] { new { kind = "file", name = "x", version = "1" } } }));
    }
}
