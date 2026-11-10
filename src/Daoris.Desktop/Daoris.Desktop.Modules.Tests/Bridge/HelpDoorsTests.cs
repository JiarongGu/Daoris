using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// **Ask Daoris's Apply goes through the screen's own door**: each door the module hands the
/// driver's <see cref="HelpProposals.ApplyAsync"/> is the code the screen's route runs — the driver's file
/// as <c>SET_*</c> edits it, an account's settings as <c>SET_AGENT_SETTINGS</c> writes them, an update as
/// <c>HARNESS_ACTION</c> starts it, and a delete through the local host's own route — never a second path.
/// </summary>
/// <remarks>
/// 🔴 <b>No real agent runs here.</b> An update runs a stand-in script as the agent's command, and the
/// service is a stand-in handler that records what it is asked.
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

        Assert.Equal((0, (string?)null), await ended.Task.WaitAsync(Poll.Patience));
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

    /// <summary>
    /// An add is <c>daoris plugin add</c>'s copy (the driver's twin): the folder copied into the home
    /// under its id, the catalogue reading it there. 🔴 Nothing it declares runs at the press — its hook
    /// would leave a mark, and there is none; the loop starts it at its next look.
    /// </summary>
    [Fact]
    public void A_plugin_is_added_by_copying_its_folder_in_and_nothing_it_declares_runs()
    {
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
            Assert.True(File.Exists(DaorisHome.Entry(Home, HomeEntry.Plugins,"acme.quiet-hours", "hooks.mjs")));
            Assert.False(File.Exists(mark));
            Assert.Contains("already installed", Assert.Throws<DriverException>(() => doors.AddPlugin(source)).Message);
        }
        finally
        {
            try { Directory.Delete(checkout, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void A_plugin_is_switched_as_the_plugins_screens_route_switches_it()
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Plugins,"acme.gate");
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
    /// An offer is <c>PLUGIN_INSTALL</c>'s own copy, by its id, the offer recorded; (c): an update is
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

            var installed = DaorisHome.Entry(Home, HomeEntry.Plugins,"acme.quiet-hours");
            Assert.Equal("acme.quiet-hours", PluginSource.Read(installed).Source?.Offer);
            Assert.Contains("already installed", Assert.Throws<DriverException>(() => doors.AddOffer("acme.quiet-hours")).Message);

            File.WriteAllText(Path.Combine(offer, "hooks.mjs"), "// v2");
            var kept = DaorisHome.Entry(Home, HomeEntry.Plugins,PluginCatalog.DataFolder, "acme.quiet-hours");
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

    /// <summary>The local host's delete routes and its accept route, standing in: each answers the service's sentence.</summary>
    private sealed class StandInHost : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Asked.Add($"{request.Method} {path}");
            if (path.EndsWith("/accept", StringComparison.Ordinal))
            {
                var quest = path.Split('/')[^2];
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new { message = $"Accepted quest #{quest}." }), Encoding.UTF8, "application/json"),
                });
            }

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
