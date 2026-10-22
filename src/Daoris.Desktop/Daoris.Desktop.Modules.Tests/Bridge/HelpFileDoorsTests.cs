using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Ask Daoris's doors that start nothing (HELP10): each is the code the screen's own route runs, tested here without a
/// process, so a worktree's fast half holds them. <c>HelpDoorsTests</c> holds the doors that start one, or ask the
/// roster again.
/// </summary>
public sealed class HelpFileDoorsTests : Bridge
{
    private DriverModule Module() => new(Bus, new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0"));

    /// <summary>
    /// A default on the protocol stub is written as its owner's, as one on <c>claude-code-acp</c> is (AGT7): since
    /// TOOL4j the stub's door runs as the stub's accounts, so no built-in agent is left without a toolchain.
    /// </summary>
    [Fact]
    public async Task A_default_on_the_protocol_stub_is_written_as_the_stubs()
    {
        await Module().HelpDoors(null).SetDefaultAccountAsync("acp-stub", "work", null, CancellationToken.None);

        var settings = HarnessSettings.Load(HarnessSettingsPath);
        Assert.Equal("work", settings.Defaults["stub"]);
        Assert.False(settings.Defaults.ContainsKey("acp-stub"));
    }

    /// <summary>
    /// TOOL4l-b: an order is <c>ACCOUNT_USE</c>'s own <c>order</c> and <c>inherit</c> — the set's order written whole as its owner's
    /// (AGT7; POOL1a1, D197 point 2: the accounts named first, the rest after), refused in the screen's words where an account is none,
    /// and a workspace sent back to the machine's accounts with its list cleared. A workspace's list, which the screen's route refuses
    /// since POOL1a1, is still written here until POOL1b refuses it too; a default laid raw beside it is folded away by the next edit
    /// (D197 §6), so no list is refused for dropping it.
    /// </summary>
    [Fact]
    public async Task An_order_is_written_and_refused_as_the_accounts_screens_route_writes_and_refuses_it()
    {
        foreach (var account in new[] { "work", "play", "spare" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "claude-code", account));
        var doors = Module().HelpDoors(null);

        await doors.SetAccountOrderAsync("claude-code-acp", ["work", "play"], false, null, CancellationToken.None);
        await doors.SetAccountOrderAsync("claude-code", ["spare", "play"], false, "lab", CancellationToken.None);
        HarnessSettings.Load(HarnessSettingsPath).WithWorkspaceDefault("lab", "claude-code", "play").Save(HarnessSettingsPath);

        var settings = HarnessSettings.Load(HarnessSettingsPath);
        Assert.Equal(["work", "play", "spare"], settings.Rotation["claude-code"]);
        Assert.Equal(["spare", "play"], settings.WorkspaceRotation["lab"]["claude-code"]);
        Assert.Contains("no account `gone`", (await Assert.ThrowsAsync<DriverException>(() =>
            doors.SetAccountOrderAsync("claude-code", ["work", "gone"], false, null, CancellationToken.None))).Message);

        await doors.SetAccountOrderAsync("claude-code", ["spare"], false, "lab", CancellationToken.None);

        var folded = HarnessSettings.Load(HarnessSettingsPath);
        Assert.False(folded.Workspaces.ContainsKey("lab"));
        Assert.Equal(["spare"], folded.WorkspaceRotation["lab"]["claude-code"]);
        Assert.Contains(folded.Pooled["claude-code"], receipt => receipt.Scopes.Any(scope => scope is { Workspace: "lab", Default: "play" }));

        await doors.SetAccountOrderAsync("claude-code", [], true, "lab", CancellationToken.None);

        var back = HarnessSettings.Load(HarnessSettingsPath);
        Assert.False(back.WorkspaceRotation.ContainsKey("lab"));
        Assert.False(back.Workspaces.ContainsKey("lab"));
        Assert.Equal(["work", "play", "spare"], back.Rotation["claude-code"]);
        Assert.Contains("names the workspace", (await Assert.ThrowsAsync<DriverException>(() =>
            doors.SetAccountOrderAsync("claude-code", [], true, null, CancellationToken.None))).Message);
    }

    /// <summary>
    /// TOOL4l-b: a ready is <c>ACCOUNT_USE</c>'s own <c>ready</c> — <i>Try now</i>, an account's cool-off ended as its owner's
    /// (AGT7), the tool's own sign-in's where none is named; one that was not cooling is told so and nothing changes.
    /// </summary>
    [Fact]
    public async Task A_ready_ends_the_cool_off_as_the_accounts_screens_try_now_does()
    {
        var now = DateTimeOffset.UtcNow;
        AccountCooling.Cool(Home, new CoolingEntry("claude-code", "play", now.AddHours(2), true, "weekly", now, "s1a2b3c4"), now);
        AccountCooling.Cool(Home, new CoolingEntry("claude-code", null, now.AddHours(1), false, null, now, null), now);
        var doors = Module().HelpDoors(null);

        Assert.True(await doors.ReadyAccountAsync("claude-code-acp", "play", CancellationToken.None));
        Assert.False(await doors.ReadyAccountAsync("claude-code-acp", "play", CancellationToken.None));
        Assert.Equal([null], AccountCooling.Read(Home, now).Select(entry => entry.Account));
        Assert.True(await doors.ReadyAccountAsync("claude-code", null, CancellationToken.None));
        Assert.Empty(AccountCooling.Read(Home, now));
    }

    /// <summary>
    /// HELP10: the browser's settings are <c>BrowserModule</c>'s own edits, written to the files the Browser screen and
    /// <c>daoris browser</c> write, and read by <c>daoris-browser</c> at its next start.
    /// </summary>
    [Fact]
    public void The_browsers_settings_are_the_browser_screens_own_edits()
    {
        var doors = Module().HelpDoors(null);

        doors.ChangeBrowser("use", "edge", null, null);
        doors.ChangeBrowser("links", "daoris", null, null);
        doors.ChangeBrowser("extensions", "refuse", null, null);
        doors.ChangeBrowser("favorite", "add", "site.example/board", "Board");

        var settings = BrowserSettings.Read(Home);
        Assert.Equal((BrowserChoice.Edge, LinksSetting.Daoris, ExtensionsSetting.Refuse), (settings.Browser, settings.Links, settings.Extensions));
        Assert.Equal([new Favorite("https://site.example/board", "Board")], BrowserFavorites.Read(Home).Favorites);

        doors.ChangeBrowser("favorite", "remove", "https://site.example/board", null);
        Assert.Empty(BrowserFavorites.Read(Home).Favorites);
    }

    /// <summary>What the Browser screen's route refuses is refused here in its words, as the driver's refusal, so the card settles refused.</summary>
    [Fact]
    public void A_browser_change_the_screens_route_refuses_is_refused_in_its_words()
    {
        var doors = Module().HelpDoors(null);
        Directory.CreateDirectory(Path.GetDirectoryName(BrowserSettings.FilePath(Home))!);
        File.WriteAllText(BrowserSettings.FilePath(Home), "{ not json");

        Assert.Contains("Daoris will not write over a file it could not read",
            Assert.Throws<DriverException>(() => doors.ChangeBrowser("links", "daoris", null, null)).Message);
        Assert.Contains("is not a web page, so it cannot be a favorite",
            Assert.Throws<DriverException>(() => doors.ChangeBrowser("favorite", "add", "javascript:alert(1)", null)).Message);
        Assert.Equal("{ not json", File.ReadAllText(BrowserSettings.FilePath(Home)));
    }

    /// <summary>
    /// HELP10: bringing up to date is <c>TREES_SYNC_PLAN</c>'s list and <c>TREES_SYNC</c>'s press (WSR6), which read the
    /// registry and the running sessions, so before the driver is up both are the cold-start sentence and nothing is fetched.
    /// </summary>
    [Fact]
    public async Task Bringing_up_to_date_before_the_driver_is_up_is_the_cold_start_sentence()
    {
        var doors = Module().HelpDoors(null);

        Assert.Contains("still coming up", (await Assert.ThrowsAnyAsync<Exception>(() => doors.SyncPlanAsync(null, null, CancellationToken.None))).Message);
        Assert.Contains("still coming up",
            (await Assert.ThrowsAnyAsync<Exception>(() => doors.SyncAsync("engine", null, new HashSet<string> { "engine:main" }, CancellationToken.None))).Message);
    }

    /// <summary>HELP10: what a browser proposal is judged against is the files as the Browser screen reads them, and the route's reader of an address.</summary>
    [Fact]
    public void The_browsers_facts_are_its_files_as_the_screen_reads_them()
    {
        BrowserSettings.SetLinks(Home, LinksSetting.Daoris);
        BrowserFavorites.Add(Home, "site.example/board", null);

        var facts = BrowserModule.HelpFacts(Home);

        Assert.Equal((BrowserChoice.Daoris, LinksSetting.Daoris, ExtensionsSetting.Offer), (facts.Browser, facts.Links, facts.Extensions));
        Assert.Equal(["https://site.example/board"], facts.Favorites);
        Assert.Equal(BrowserFavorites.Address("docs.example/guide"), facts.Page("docs.example/guide"));
        Assert.Null(facts.Page("javascript:alert(1)"));
        Assert.Equal((null, null), (facts.Problem, facts.FavoritesProblem));
    }
}
