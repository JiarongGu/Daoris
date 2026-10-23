using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;
using StandInService = Daoris.Desktop.Driver.Tests.OrphanedSessionTests.StandInService;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HELPWIRED1 (D183 point 4, the Ask Daoris modes design §6 and §7): each start of Ask Daoris records what it handed its agent,
/// read from the spawn's own inputs at the start and never worked out again from configuration: its mode, its agent and account,
/// its folder, Daoris's tools, the skills, each server with its approval, the rules, the checkout grants, and in restricted mode
/// what it lacks with each door. Kept beside the conversation's other files, appended at each start, and cleaned of every secret.
/// </summary>
/// <remarks>No process: the start gate's stand-in takes the agent's start (PROCSTART2), reading what the spawn was handed at that
/// moment, and the service is a stand-in.</remarks>
public sealed class HelpWiredTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-wired-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpWiredTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly TimeSpan Within = TimeSpan.FromSeconds(20);

    private const string Secret = "sk-hostile";

    /// <summary>
    /// A plugin handing Ask Daoris an approved server with a write the person accepted, a server still waiting for its approval,
    /// one declared for sessions alone, and a skill for Ask Daoris. Its approved server's arguments and environment carry secrets.
    /// </summary>
    private void Plugin()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "acme.tickets");
        Directory.CreateDirectory(Path.Combine(folder, "skills", "triage"));
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), $$"""
            { "id": "acme.tickets",
              "servers": [
                { "name": "tickets", "command": ["tickets", "--key", "{{Secret}}-argument"], "env": { "TICKETS_TOKEN": "{{Secret}}-server" },
                  "to": ["help"], "allow": ["search"], "writes": ["create_issue"] },
                { "name": "waiting", "command": ["waiting"], "to": ["help"], "allow": ["read"] },
                { "name": "docs", "command": ["docs"] } ],
              "skills": [ { "name": "triage", "to": ["help"] } ] }
            """);
        File.WriteAllText(
            Path.Combine(folder, "skills", "triage", SpawnSkills.SkillFile),
            "---\nname: triage\ndescription: Sort the issues by what they block.\n---\n\nRead the board first.\n");
        HelpGrants.Approve(_home, PluginCatalog.Load(_home, AdapterSet.Built().Names), "acme.tickets", "tickets", readsOnly: false);
    }

    /// <summary>A plugin whose one server drives Daoris's own browser, approved for Ask Daoris.</summary>
    private void Browser()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "in-app-browser");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), """
            { "id": "in-app-browser", "servers": [
                { "name": "browser", "command": ["node", "server.js", "--cdp", "${browser}"], "to": ["sessions", "help"],
                  "allow": ["browser_snapshot"] } ] }
            """);
        HelpGrants.Approve(_home, PluginCatalog.Load(_home, AdapterSet.Built().Names), "in-app-browser", "browser", readsOnly: false);
    }

    private sealed record Spawned(ProcessStartInfo Info, string? Settings, string? Servers);

    /// <summary>
    /// Ask Daoris's start on <paramref name="adapter"/> in <paramref name="mode"/>, its agent's start taken by the stand-in, which
    /// reads the settings and servers files the spawn was handed while they are there (a start that did not start removes them).
    /// </summary>
    private async Task<Spawned> StartAsync(
        StandInService service, string id, string adapter, string mode, IReadOnlyDictionary<string, string>? environment = null)
    {
        service.Seed(id, "queued");
        var adapters = AdapterSet.Built();
        using var client = new ServiceClient(service.Url, null);
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)),
            events: new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { [adapter] = ["node", "never-started.mjs"] },
            HelpMode = mode,
        };
        var placed = new HelpPlaced(id, $"Ask Daoris `{id}` opened.")
        {
            Adapter = adapters.Resolve(adapter),
            Selection = new HarnessSelection(Refusal: null, Profile: "work", Version: "2.1.299", Environment: environment),
        };
        var machine = new HelpMachine { Adapter = adapter, Helper = adapter, Cap = 1 };
        Spawned? spawned = null;
        using (ProcessStarts.Within(info =>
               {
                   string? Text(string name) => File.Exists(DaorisHome.Entry(_home, HomeEntry.Spawn, id + name))
                       ? File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.Spawn, id + name))
                       : null;
                   spawned = new Spawned(info, Text(".settings.json"), Text(".mcp.json"));
                   return null;
               }))
        {
            await runner.RunHelpAsync(placed, config, _ => Task.FromResult<HelpMachine?>(machine)).WaitAsync(Within);
        }

        Assert.NotNull(spawned);
        return spawned!;
    }

    private static IReadOnlyList<string> Strings(JsonElement element, string name) =>
        [.. element.GetProperty(name).EnumerateArray().Select(each => each.GetString()!)];

    /// <summary>
    /// The receipt equals what the spawn was given, field for field, in each mode on each Claude door: its rules are the settings
    /// file's, its servers on the pipe the servers file's, Daoris's tools the allow-list's own (none in full), its folder the
    /// spawn's, its agent, version and account the selection's; a server is said handed, handed by full access without an
    /// approval, or withheld with why; and restricted mode says what it lacks, each with its door.
    /// </summary>
    [Theory]
    [InlineData("claude-code", HelpModes.Restricted)]
    [InlineData("claude-code", HelpModes.Full)]
    [InlineData("claude-code-acp", HelpModes.Restricted)]
    [InlineData("claude-code-acp", HelpModes.Full)]
    public async Task The_receipt_is_what_the_spawn_was_handed_field_for_field_in_each_mode(string adapter, string mode)
    {
        Plugin();
        await using var service = StandInService.Start(_home);

        var spawned = await StartAsync(service, "help0001", adapter, mode);

        var receipt = Assert.Single(new HelpWired(_home).Read("help0001"));
        Assert.Equal((mode, HelpWired.New, adapter, "2.1.299", "work"), (receipt.Mode, receipt.Start, receipt.Harness, receipt.Version, receipt.Account));
        Assert.Equal(Path.GetFullPath(spawned.Info.WorkingDirectory), Path.GetFullPath(receipt.Folder));
        var pipe = adapter == "claude-code";
        Assert.Equal(pipe ? HelpWired.Pipe : HelpWired.Acp, receipt.Door);

        // The rules: the settings file's, list for list.
        using var settings = JsonDocument.Parse(spawned.Settings ?? throw new Xunit.Sdk.XunitException("no settings file was handed"));
        var permissions = settings.RootElement.GetProperty("permissions");
        var rules = Assert.IsType<HelpWiredRules>(receipt.Rules);
        Assert.Equal(Strings(permissions, "allow"), rules.Allow);
        Assert.Equal(Strings(permissions, "ask"), rules.Ask);
        Assert.Equal(Strings(permissions, "deny"), rules.Deny);
        Assert.Equal([.. rules.Allow.Where(rule => rule.StartsWith($"mcp__{KnowledgeConnector.ServerName}__", StringComparison.Ordinal))], receipt.Tools);
        var guarded = settings.RootElement.TryGetProperty("hooks", out var hooks) && hooks.TryGetProperty("PreToolUse", out var before)
            && before.EnumerateArray().Any(hook => hook.GetProperty("matcher").GetString() == TreeGuard.Matcher);
        Assert.Equal(guarded, receipt.Guarded);

        // The servers: on the pipe, the servers file's, name for name.
        var onSpawn = receipt.Servers.Where(server => server.State is HelpWired.Handed or HelpWired.FullAccess).Select(server => server.Name).ToList();
        if (pipe)
        {
            using var servers = JsonDocument.Parse(spawned.Servers ?? throw new Xunit.Sdk.XunitException("no servers file was handed"));
            Assert.Equal(servers.RootElement.GetProperty("mcpServers").EnumerateObject().Select(each => each.Name), onSpawn);
            Assert.Null(receipt.Posture);
        }

        var byName = receipt.Servers.ToDictionary(server => server.Name);
        Assert.Equal((HelpWired.Handed, "acme.tickets"), (byName["tickets"].State, byName["tickets"].Plugin));
        Assert.Equal(["search", "create_issue"], byName["tickets"].Tools);
        var skill = Assert.Single(receipt.Skills);
        Assert.Equal(("acme.tickets:triage", pipe ? HelpWired.Handed : HelpWired.Unverified), (skill.Name, skill.State));

        if (mode == HelpModes.Restricted)
        {
            Assert.NotEmpty(receipt.Tools);
            Assert.True(receipt.Guarded);
            Assert.Equal((HelpWired.Withheld, HelpWired.NotApproved), (byName["waiting"].State, byName["waiting"].Why));
            Assert.False(byName.ContainsKey("docs"));
            Assert.Equal(pipe ? null : HelpRoom.Posture, receipt.Posture);
            Assert.Null(receipt.PermissionMode);
            Assert.Contains(receipt.Lacks, lack => lack is { Kind: "server", Name: "waiting", Door: HelpWired.DoorPlugins, Plugin: "acme.tickets" });
            Assert.Contains(receipt.Lacks, lack => lack is { Kind: "server", Name: "docs", Door: HelpWired.DoorMode });
            Assert.Contains(receipt.Lacks, lack => lack is { Kind: "shell", Door: HelpWired.DoorMode });
            Assert.Contains(receipt.Lacks, lack => lack is { Kind: "edits", Door: HelpWired.DoorMode });
        }
        else
        {
            Assert.Empty(receipt.Tools);
            Assert.False(receipt.Guarded);
            Assert.Equal(HelpWired.FullAccess, byName["waiting"].State);
            Assert.Equal(HelpWired.FullAccess, byName["docs"].State);
            Assert.Empty(receipt.Lacks);
            Assert.Empty(receipt.Checkouts);
            if (pipe)
            {
                Assert.Equal("bypassPermissions", receipt.PermissionMode);
                var arguments = spawned.Info.ArgumentList.ToList();
                Assert.Equal("bypassPermissions", arguments[arguments.IndexOf("--permission-mode") + 1]);
            }
            else
            {
                Assert.Equal("bypassPermissions", receipt.Posture);
            }
        }
    }

    /// <summary>
    /// The receipt leaks nothing (design §7): an account's key on the spawn's environment and a plugin server's secret in its
    /// arguments and environment all reach the spawn, and none reaches the receipt kept or the answer made of it.
    /// </summary>
    [Fact]
    public async Task A_secret_in_the_environment_or_a_server_s_arguments_never_reaches_the_receipt()
    {
        Plugin();
        await using var service = StandInService.Start(_home);

        var spawned = await StartAsync(
            service, "help0002", "claude-code", HelpModes.Full, new Dictionary<string, string> { ["ANTHROPIC_API_KEY"] = Secret + "-account" });

        // The fixture is hostile: the spawn carried every secret.
        Assert.Equal(Secret + "-account", spawned.Info.Environment["ANTHROPIC_API_KEY"]);
        Assert.Contains(Secret + "-argument", spawned.Servers);
        Assert.Contains(Secret + "-server", spawned.Servers);

        var kept = File.ReadAllText(new HelpWired(_home).PathOf("help0002"));
        Assert.Contains("tickets", kept);
        Assert.DoesNotContain(Secret, kept);
        var answer = HelpWired.Answer("help0002", new HelpWired(_home).Read("help0002")).GetRawText();
        Assert.Contains("tickets", answer);
        Assert.DoesNotContain(Secret, answer);
    }

    /// <summary>
    /// A headless start (design §7): no browser host runs, so the server that drives Daoris's browser is recorded withheld with
    /// that reason, never as an unresolved endpoint, and restricted mode says its door is the host.
    /// </summary>
    [Fact]
    public async Task With_no_browser_host_the_browser_server_is_recorded_withheld_with_why_never_an_unresolved_endpoint()
    {
        Browser();
        await using var service = StandInService.Start(_home);

        await StartAsync(service, "help0003", "claude-code", HelpModes.Restricted);

        var receipt = Assert.Single(new HelpWired(_home).Read("help0003"));
        var browser = Assert.Single(receipt.Servers, server => server.Name == "browser");
        Assert.Equal((HelpWired.Withheld, "browser.no-host", "in-app-browser"), (browser.State, browser.Why, browser.Plugin));
        Assert.Contains(receipt.Lacks, lack => lack is { Kind: "server", Name: "browser", Door: HelpWired.DoorHost });
        Assert.DoesNotContain(InAppBrowserServers.Placeholder, File.ReadAllText(new HelpWired(_home).PathOf("help0003")));
    }

    /// <summary>
    /// HELPMODE1c2a (D183's note): the receipt says the housekeeping its start was handed, the operations chosen in the record at
    /// that moment, whatever the agent: on Claude its tools are on the allow-list beside the room's own, and on Codex, which takes
    /// no rules, the receipt still says them, since the driver judges each call by it. Nothing chosen is none handed.
    /// </summary>
    [Fact]
    public async Task The_receipt_says_the_housekeeping_its_start_was_handed_on_every_agent()
    {
        await using var service = StandInService.Start(_home);
        await StartAsync(service, "help0005", "claude-code", HelpModes.Restricted);
        Assert.Empty(Assert.Single(new HelpWired(_home).Read("help0005")).Housekeeping);
        Assert.Empty(Assert.Single(new HelpWired(_home).Read("help0005")).Settings);

        File.WriteAllText(ConfiguredAccess.PathOf(_home), """{ "housekeeping": ["inspect", "clean-up"], "settings": ["cap"] }""");
        var spawned = await StartAsync(service, "help0006", "claude-code", HelpModes.Restricted);
        await StartAsync(service, "help0007", "codex-acp", HelpModes.Restricted);

        var claude = Assert.Single(new HelpWired(_home).Read("help0006"));
        Assert.Equal([ConfiguredAccess.Inspect, ConfiguredAccess.CleanUp], claude.Housekeeping);
        Assert.Contains($"mcp__{KnowledgeConnector.ServerName}__housekeeping_inspect", claude.Tools);
        Assert.Contains($"mcp__{KnowledgeConnector.ServerName}__housekeeping_clean_up", claude.Tools);
        Assert.DoesNotContain($"mcp__{KnowledgeConnector.ServerName}__housekeeping_discard_branch", claude.Tools);
        // HELPMODE1c3: the chosen settings, kept beside housekeeping the same way.
        Assert.Equal([ConfiguredAccess.Cap], claude.Settings);
        Assert.Contains($"mcp__{KnowledgeConnector.ServerName}__settings_cap", claude.Tools);
        using var settings = JsonDocument.Parse(spawned.Settings!);
        Assert.Contains($"mcp__{KnowledgeConnector.ServerName}__housekeeping_clean_up", Strings(settings.RootElement.GetProperty("permissions"), "allow"));
        Assert.Contains($"mcp__{KnowledgeConnector.ServerName}__settings_cap", Strings(settings.RootElement.GetProperty("permissions"), "allow"));
        var codex = Assert.Single(new HelpWired(_home).Read("help0007"));
        Assert.Null(codex.Rules);
        Assert.Equal([ConfiguredAccess.Inspect, ConfiguredAccess.CleanUp], codex.Housekeeping);
        Assert.Equal([ConfiguredAccess.Cap], codex.Settings);
        Assert.Contains("\"housekeeping\"", File.ReadAllText(new HelpWired(_home).PathOf("help0007")));
        Assert.Contains("\"settings\"", File.ReadAllText(new HelpWired(_home).PathOf("help0007")));
    }

    /// <summary>
    /// A conversation going on records a second receipt (design §6), its start said as going on, and the first is kept as it was;
    /// the route's reading answers the newest first.
    /// </summary>
    [Fact]
    public async Task A_conversation_going_on_records_a_second_receipt_and_keeps_the_first()
    {
        Plugin();
        await using var service = StandInService.Start(_home);
        await StartAsync(service, "help0004", "claude-code", HelpModes.Restricted);
        var was = Assert.Single(new HelpWired(_home).Read("help0004"));

        var adapters = AdapterSet.Built();
        using var client = new ServiceClient(service.Url, null);
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)),
            events: new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["claude-code"] = ["node", "never-started.mjs"] },
        };
        var goOn = new GoingOn(
            new PriorSession("help0004", HelpRoom.PathOf(_home), "working", Repository: HelpRoom.Repository) { HelpMode = HelpModes.Restricted },
            new ResumeAsk("conv-help0004", [], "and now?"));
        // HELPWIREDLIVE1b: the event is raised once the receipt is on disk, so a subscriber publishing on it reads the new one.
        var seen = new List<(string Id, int Receipts)>();
        runner.WiredKept += id => seen.Add((id, new HelpWired(_home).Read(id).Count));
        using (ProcessStarts.Within(_ => null))
        {
            await runner.RunAsync(
                "ask-daoris", "help0004", "and now?", adapters.Resolve("claude-code"), new HarnessSelection(Refusal: null, Profile: "work", Version: "2.1.300"),
                config, runner.HelpPlace(HelpRoom.PathOf(_home), []), null, CancellationToken.None, goOn).WaitAsync(Within);
        }
        Assert.Equal([("help0004", 2)], seen);

        var receipts = new HelpWired(_home).Read("help0004");
        Assert.Equal([HelpWired.GoingOn, HelpWired.New], receipts.Select(receipt => receipt.Start));
        Assert.Equal(["2.1.300", "2.1.299"], receipts.Select(receipt => receipt.Version));
        // The first, field for field as it was kept.
        Assert.Equal(HelpWired.Answer("help0004", [was]).GetRawText(), HelpWired.Answer("help0004", [receipts[1]]).GetRawText());
    }

    /// <summary>
    /// ACCESSSWAP1 (D183 point 2): a conversation whose access changed while it ran resumes at its next words under the access chosen,
    /// and the resumed start's receipt names it: the mode widened and the housekeeping granted since, beside the first start's restricted
    /// receipt with none; its record keeps the new mode, and the spawn resumes its own harness conversation (D131).
    /// </summary>
    [Fact]
    public async Task A_conversation_resumed_after_its_access_changed_records_the_new_access()
    {
        await using var service = StandInService.Start(_home);
        await StartAsync(service, "help0008", "claude-code", HelpModes.Restricted);
        ConfiguredAccess.Choose(_home, ConfiguredAccess.HousekeepingGrant, ConfiguredAccess.CleanUp, chosen: true);

        var adapters = AdapterSet.Built();
        using var client = new ServiceClient(service.Url, null);
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)),
            events: new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["claude-code"] = ["node", "never-started.mjs"] },
            HelpMode = HelpModes.Full,
        };
        var goOn = new GoingOn(
            new PriorSession("help0008", HelpRoom.PathOf(_home), "working", Repository: HelpRoom.Repository) { HelpMode = HelpModes.Restricted },
            new ResumeAsk("conv-help0008", [], "and now?"));
        ProcessStartInfo? resumed = null;
        using (ProcessStarts.Within(info =>
               {
                   resumed = info;
                   return null;
               }))
        {
            await runner.RunAsync(
                "ask-daoris", "help0008", "and now?", adapters.Resolve("claude-code"), new HarnessSelection(Refusal: null, Profile: "work", Version: "2.1.299"),
                config, runner.HelpPlace(HelpRoom.PathOf(_home), []), null, CancellationToken.None, goOn).WaitAsync(Within);
        }

        var receipts = new HelpWired(_home).Read("help0008");
        Assert.Equal([HelpWired.GoingOn, HelpWired.New], receipts.Select(receipt => receipt.Start));
        Assert.Equal([HelpModes.Full, HelpModes.Restricted], receipts.Select(receipt => receipt.Mode));
        Assert.Equal([ConfiguredAccess.CleanUp], receipts[0].Housekeeping);
        Assert.Empty(receipts[1].Housekeeping);
        Assert.Equal("bypassPermissions", receipts[0].PermissionMode);
        Assert.Equal(HelpModes.Full, service.HelpMode("help0008"));
        Assert.Contains("--resume", resumed!.ArgumentList);
    }

    /// <summary>A delete takes the receipts with the conversation's other files, named as Ask Daoris's own (<see cref="SessionHomeFiles"/>).</summary>
    [Fact]
    public void A_delete_removes_the_receipt_file()
    {
        var wired = new HelpWired(_home);
        wired.Keep("help0005", Receipt(DateTimeOffset.Parse("2026-10-19T09:00:00Z")));
        Assert.True(File.Exists(wired.PathOf("help0005")));

        var removed = new SessionHomeFiles(_home).Remove(["help0005"], events: null);

        Assert.False(File.Exists(wired.PathOf("help0005")));
        Assert.Contains(SessionHomeFiles.Help, removed.Went["help0005"]);
    }

    /// <summary>
    /// The receipts read newest first, the earlier kept; a conversation with none, an id that is no session's, and a file that does
    /// not read answer none recorded; a field the file holds that a receipt has no place for is never read back.
    /// </summary>
    [Fact]
    public void Receipts_read_newest_first_and_none_is_not_recorded()
    {
        var wired = new HelpWired(_home);
        wired.Keep("help0006", Receipt(DateTimeOffset.Parse("2026-10-19T09:00:00Z")));
        wired.Keep("help0006", Receipt(DateTimeOffset.Parse("2026-10-19T10:00:00Z")) with { Start = HelpWired.GoingOn, Mode = HelpModes.Full });

        var read = wired.Read("help0006");
        Assert.Equal([HelpModes.Full, HelpModes.Restricted], read.Select(receipt => receipt.Mode));
        Assert.Empty(wired.Read("help0007"));
        Assert.Empty(wired.Read("../escape"));

        File.WriteAllText(wired.PathOf("help0008"), "{ not json");
        Assert.Empty(wired.Read("help0008"));

        File.WriteAllText(wired.PathOf("help0009"), $$"""
            { "receipts": [ { "at": "2026-10-19T09:00:00Z", "start": "new", "mode": "restricted", "harness": "claude-code",
              "door": "pipe", "folder": "room", "token": "{{Secret}}-file", "servers": [ { "name": "x", "state": "handed", "env": { "K": "{{Secret}}-env" } } ] } ] }
            """);
        var hostile = HelpWired.Answer("help0009", wired.Read("help0009"));
        Assert.True(hostile.GetProperty("recorded").GetBoolean());
        Assert.DoesNotContain(Secret, hostile.GetRawText());
        Assert.False(HelpWired.Answer("help0007", []).GetProperty("recorded").GetBoolean());
    }

    private static HelpReceipt Receipt(DateTimeOffset at) => new()
    {
        At = at, Start = HelpWired.New, Mode = HelpModes.Restricted, Harness = "claude-code", Door = HelpWired.Pipe, Folder = "room",
    };
}
