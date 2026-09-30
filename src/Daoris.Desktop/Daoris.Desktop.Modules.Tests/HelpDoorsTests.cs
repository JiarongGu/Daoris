using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// **Ask Daoris's Apply goes through the screen's own door** (HELP6): each door the module hands the
/// driver's <see cref="HelpProposals.ApplyAsync"/> is the code the screen's route runs — the driver's file
/// as <c>SET_*</c> edits it, an account's settings as <c>SET_AGENT_SETTINGS</c> writes them, an update as
/// <c>HARNESS_ACTION</c> starts it, and a delete through the local host's own route — never a second path.
/// </summary>
/// <remarks>
/// 🔴 <b>No real agent runs here.</b> An update runs a stand-in script as the agent's command, and the
/// service is a stand-in handler that records what it was asked.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class HelpDoorsTests : Bridge
{
    private DriverModule Module() => new(Bus, new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0"));

    [Fact]
    public void A_setting_is_the_edit_the_screens_route_makes_to_the_drivers_file()
    {
        Module().HelpDoors(null).Change(config => config.WithDrivable("engine", true));

        Assert.Contains("engine", DriverConfig.Load(DriverConfigPath).Drivable);
    }

    [Fact]
    public void An_accounts_settings_are_written_and_refused_as_the_agents_screens_route_writes_and_refuses_them()
    {
        var home = HarnessSettings.ProfileHome(Home, "claude-code", "work");
        Directory.CreateDirectory(home);
        var file = Path.Combine(home, AgentSettings.FileName);
        File.WriteAllText(file, """{"theme":"dark","effortLevel":"high"}""");
        var doors = Module().HelpDoors(null);

        // A door's accounts are its owner's (AGT7), and clearing removes the key.
        doors.SetAgentSettings("claude-code-acp", "work", new AgentSettingEdit("sonnet"), new AgentSettingEdit(null));

        Assert.Equal("""{"theme":"dark","model":"sonnet"}""", JsonSerializer.Serialize(JsonDocument.Parse(File.ReadAllText(file)).RootElement));
        Assert.Contains("one session", Assert.Throws<DriverException>(() => doors.SetAgentSettings("claude-code", "work", null, new AgentSettingEdit("max"))).Message);
        Assert.Contains("no account `play`", Assert.Throws<DriverException>(() => doors.SetAgentSettings("claude-code", "play", new AgentSettingEdit("opus"), null)).Message);
        Assert.False(Directory.Exists(HarnessSettings.ProfileHome(Home, "claude-code", "play")));
    }

    /// <summary>
    /// An update is <c>HARNESS_ACTION</c>'s own start: the agent's updater runs under its own console key,
    /// its end is the same <c>HARNESS_ENDED</c> news the Agents screen hears, and the conversation hears it too.
    /// </summary>
    [Fact]
    public async Task An_update_runs_the_agents_own_updater_and_its_end_is_heard_by_the_screen_and_the_conversation()
    {
        var fake = Path.Combine(Home, "fake-claude.mjs");
        File.WriteAllText(fake, """
            const args = process.argv.slice(2).join(' ');
            if (args === 'update') { console.log('updated to 9.9.10'); process.exit(0); }
            if (args === 'auth status') { console.log(JSON.stringify({ loggedIn: true })); process.exit(0); }
            console.log('claude 9.9.9');
            """);
        File.WriteAllText(DriverConfigPath, $$"""
            { "drivable": [], "holds": [], "commands": { "claude-code": ["node", {{JsonSerializer.Serialize(fake)}}] } }
            """);
        var ended = new TaskCompletionSource<(int Code, string? Problem)>(TaskCreationOptions.RunContinuationsAsynchronously);

        await Module().HelpDoors(null).StartAgentActionAsync(
            "claude-code", "update", null, (code, problem) => ended.TrySetResult((code, problem)), CancellationToken.None);

        Assert.Equal((0, (string?)null), await ended.Task.WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.Contains(Raised, message => message.Type == "HARNESS_ENDED"
            && JsonSerializer.SerializeToElement(message.Payload).GetProperty("Action").GetString() == "update");
        Assert.Contains(Raised, message => message.Type == "SESSION_OUTPUT"
            && JsonSerializer.Serialize(message.Payload).Contains("claude-code:update")
            && JsonSerializer.Serialize(message.Payload).Contains("updated to 9.9.10"));
    }

    /// <summary>A door with no Update is refused in the words <c>HARNESS_ACTION</c> refuses it with.</summary>
    [Fact]
    public async Task An_update_on_a_door_with_none_is_refused_as_the_agents_screens_route_refuses_it()
    {
        var refused = await Assert.ThrowsAsync<DriverException>(() =>
            Module().HelpDoors(null).StartAgentActionAsync("dsh", "update", null, (_, _) => { }, CancellationToken.None));

        Assert.Contains("declares no updater", refused.Message);
    }

    /// <summary>A delete is the local host's own route, the one the quest drawer and the ask's record call.</summary>
    [Fact]
    public async Task A_delete_goes_through_the_local_hosts_own_route()
    {
        var host = new StandInHost();
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(host));
        var doors = Module().HelpDoors(service);

        var quest = await doors.DeleteQuestAsync("q1a2b3c4", CancellationToken.None);
        var ask = await doors.DeleteAskAsync("a1b2c3d4", CancellationToken.None);

        Assert.Equal(["DELETE /api/quests/q1a2b3c4", "DELETE /api/asks/a1b2c3d4"], host.Asked);
        Assert.Equal((true, "Deleted quest #q1a2b3c4."), quest);
        Assert.Equal((true, "Deleted ask #a1b2c3d4."), ask);
    }

    [Fact]
    public async Task A_delete_before_the_driver_is_up_is_the_cold_start_sentence()
    {
        var refused = await Assert.ThrowsAnyAsync<Exception>(() => Module().HelpDoors(null).DeleteQuestAsync("q1", CancellationToken.None));

        Assert.Contains("still coming up", refused.Message);
    }

    /// <summary>
    /// PLUG9: an add is <c>daoris plugin add</c>'s copy (the driver's twin): the folder copied into the home
    /// under its id, the catalogue reading it there. 🔴 Nothing it declares runs at the press — its hook
    /// would leave a mark, and there is none; the loop starts it at its next look.
    /// </summary>
    [Fact]
    public void A_plugin_is_added_by_copying_its_folder_in_and_nothing_it_declares_runs()
    {
        // A checkout beside the home, never inside it: a folder in the home is refused.
        var checkout = Path.Combine(Path.GetDirectoryName(Home)!, "checkout-" + Path.GetFileName(Home));
        try
        {
            var mark = Path.Combine(checkout, "ran.txt");
            var source = Path.Combine(checkout, "quiet-hours");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, PluginCatalog.ManifestName), """
                { "id": "acme.quiet-hours", "hooks": { "command": ["node", "${plugin}/hooks.mjs"], "points": ["session/ended"] } }
                """);
            File.WriteAllText(Path.Combine(source, "hooks.mjs"), $"require('fs').writeFileSync({JsonSerializer.Serialize(mark)}, 'ran');");
            var doors = Module().HelpDoors(null);

            doors.AddPlugin(source);

            var entry = Assert.Single(PluginCatalog.Load(Home).Plugins);
            Assert.Equal(("acme.quiet-hours", true, (string?)null), (entry.Manifest.Id, entry.Enabled, entry.Problem));
            Assert.True(File.Exists(Path.Combine(Home, PluginCatalog.Folder, "acme.quiet-hours", "hooks.mjs")));
            Assert.False(File.Exists(mark));
            // Never a replace from here: that stays `daoris plugin add` at a terminal.
            Assert.Contains("already installed", Assert.Throws<DriverException>(() => doors.AddPlugin(source)).Message);
        }
        finally
        {
            try { Directory.Delete(checkout, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>PLUG9: a switch is <c>PLUGIN_ACTION</c>'s own — the same row, and the same refusal for an id not installed.</summary>
    [Fact]
    public void A_plugin_is_switched_as_the_plugins_screens_route_switches_it()
    {
        var folder = Path.Combine(Home, PluginCatalog.Folder, "acme.gate");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), """{ "id": "acme.gate" }""");
        var doors = Module().HelpDoors(null);

        doors.SwitchPlugin("acme.gate", on: false);
        Assert.Equal(["acme.gate"], PluginState.Load(Home).Disabled);
        doors.SwitchPlugin("acme.gate", on: true);
        Assert.Empty(PluginState.Load(Home).Disabled);

        var refused = Assert.ThrowsAny<Exception>(() => doors.SwitchPlugin("nobody", on: true));
        Assert.Contains("no plugin `nobody` on this machine", refused.Message);
    }

    /// <summary>
    /// WSR5b: a hand-off card's Apply is the review's own door — a branch the landing record does not hold is
    /// its refusal in its words, and one it holds needs the registry's checkout, so before the driver is up it
    /// is the cold-start sentence. Nothing is spoken to either way.
    /// </summary>
    [Fact]
    public async Task A_hand_off_goes_through_the_reviews_own_door()
    {
        var doors = Module().HelpDoors(null);

        var unknown = await doors.HandAsync("engine", "feature/mine", null, CancellationToken.None);
        Assert.False(unknown.Handed);
        Assert.Contains("names no branch a landing made", unknown.Message);
        Assert.Null(unknown.Plugin);

        new LandedBranches(Home).Record(new LandedBranch(
            "engine", "work", "feature/q2-second", "main", "abc1234", "s2a3b4c5", "q2", "Second", DateTimeOffset.UnixEpoch));
        var cold = await Assert.ThrowsAnyAsync<Exception>(() => doors.HandAsync("engine", "feature/q2-second", null, CancellationToken.None));
        Assert.Contains("still coming up", cold.Message);
    }

    /// <summary>
    /// PLUG9 (d): an offer is <c>PLUGIN_INSTALL</c>'s own copy, by its id, the offer recorded; (c): an update is
    /// <c>PLUGIN_UPDATE</c>'s own apply, the folder swapped and what the plugin kept untouched.
    /// </summary>
    [Fact]
    public async Task An_offer_is_installed_and_an_update_applied_as_the_plugins_screens_routes_make_them()
    {
        var offers = Path.Combine(Path.GetDirectoryName(Home)!, "offers-" + Path.GetFileName(Home));
        try
        {
            var offer = Path.Combine(offers, "acme.quiet-hours");
            Directory.CreateDirectory(offer);
            File.WriteAllText(Path.Combine(offer, PluginCatalog.ManifestName), """{ "id": "acme.quiet-hours", "version": "1.0.0" }""");
            File.WriteAllText(Path.Combine(offer, "hooks.mjs"), "// v1");
            var doors = new DriverModule(Bus, new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0")) { Offers = offers }
                .HelpDoors(null);

            doors.AddOffer("acme.quiet-hours");

            var installed = Path.Combine(Home, PluginCatalog.Folder, "acme.quiet-hours");
            Assert.Equal(PluginSource.FromOffer("acme.quiet-hours"), PluginSource.Read(installed).Source);
            Assert.Contains("already installed", Assert.Throws<DriverException>(() => doors.AddOffer("acme.quiet-hours")).Message);

            File.WriteAllText(Path.Combine(offer, "hooks.mjs"), "// v2");
            var kept = Path.Combine(Home, PluginCatalog.Folder, PluginCatalog.DataFolder, "acme.quiet-hours");
            Directory.CreateDirectory(kept);

            await doors.UpdatePluginAsync("acme.quiet-hours", CancellationToken.None);

            Assert.Equal("// v2", File.ReadAllText(Path.Combine(installed, "hooks.mjs")));
            Assert.True(Directory.Exists(kept));
            Assert.Contains("no plugin `nobody`", (await Assert.ThrowsAsync<DriverException>(() => doors.UpdatePluginAsync("nobody", CancellationToken.None))).Message);
        }
        finally
        {
            try { Directory.Delete(offers, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>The local host's delete routes, standing in: each answers the service's sentence.</summary>
    private sealed class StandInHost : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Asked.Add($"{request.Method} {path}");
            var what = path.StartsWith("/api/quests/", StringComparison.Ordinal) ? "quest" : "ask";
            var id = path[(path.LastIndexOf('/') + 1)..];
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { id, message = $"Deleted {what} #{id}." }), Encoding.UTF8, "application/json"),
            });
        }
    }
}
