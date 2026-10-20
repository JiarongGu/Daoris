using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// CODEXACCT1 over the bridge: a door signs in, and takes a key into, the accounts of the agent it runs as (AGT7), with that
/// agent's own flow. Agents → Codex offered no *Add an account…*, since its one door, <c>codex-acp</c>, has no sign-in of
/// its own, while <c>daoris agent login codex --new</c> signed one in at a terminal (D50).
/// </summary>
/// <remarks>
/// The fast half (MOD8): the roster's facts are read from the toolchains, and the sign-in names a command that is not
/// there, so it is refused as it starts and no process runs. A sign-in that runs is the <c>Process</c> half's
/// (<c>HarnessProfileTests</c>).
/// </remarks>
public sealed class DriverModuleSignInTests : DriverModuleBridge
{
    /// <summary>
    /// What the roster answers each door's page with: whether it signs in, takes a key, and has a device code beside its own
    /// sign-in, as its agent does. Codex signs in by its browser first and its device code beside it (CODEXLOGIN1) and takes
    /// no key from Daoris (D67 §1 holds a key no Codex variable is measured for).
    /// </summary>
    [Theory]
    [InlineData("claude-code", true, true, false)]
    [InlineData("claude-code-acp", true, true, false)]
    [InlineData("codex-acp", true, false, true)]
    [InlineData("dsh", false, false, false)]
    public void Each_door_signs_in_and_takes_a_key_as_its_agent_does(string door, bool signsIn, bool takesKey, bool deviceCode)
    {
        var acts = AgentsRoutes.AccountActs(new HarnessRoster(AdapterSet.Built(), HarnessSettingsPath), door);

        Assert.Equal((signsIn, takesKey, deviceCode), (acts.SignsIn, acts.TakesKey, acts.DeviceCode));
    }

    /// <summary>A module whose loop tries the browser's ports by <paramref name="ports"/>, never this machine's 1455 and 1457.</summary>
    private DriverModule Module(Func<int, PortHold> ports)
    {
        var loop = Loop();
        loop.Harnesses.Ports = ports;
        return new DriverModule(Bus, loop);
    }

    private void CodexNamed(string codex) => File.WriteAllText(DriverConfigPath, $$"""
        { "drivable": [], "holds": [], "cap": 1, "adapter": "codex-acp",
          "commands": { "codex": [{{JsonSerializer.Serialize(codex)}}] } }
        """);

    /// <summary>
    /// *Add an account…* on Agents → Codex runs <c>codex</c>'s sign-in, by the command named for <c>codex</c>, into a folder
    /// opened under <c>codex</c>'s accounts: here one that is not there, so it is refused naming it, and the folder opened
    /// for it is gone again. It was refused as a door with no sign-in flow. CODEXLOGIN1: with a port free, by its browser.
    /// </summary>
    [Fact]
    public async Task Adding_a_codex_account_runs_codex_s_own_sign_in_and_keeps_nothing_when_it_cannot_start()
    {
        var codex = Path.Combine(Home, "no-codex-here");
        CodexNamed(codex);
        var tried = new List<int>();

        var refusal = await RefusalAsync(Module(port =>
        {
            tried.Add(port);
            return PortHold.Free;
        }), "HARNESS_ACTION", new { harness = "codex-acp", action = "login-new" });

        Assert.DoesNotContain("declares no sign-in flow", refusal);
        Assert.Contains("no-codex-here", refusal);
        Assert.Contains("could not be started", refusal);
        Assert.Empty(HarnessSettings.Profiles(Home, "codex"));
        Assert.False(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.AgentHomes, "codex-acp")));
        // CODEXLOGIN1: the console's first line names the sign-in it started, by Codex's browser, its first port free.
        Assert.Equal([1455], tried);
        await UntilAsync(() => Raised.Select(Line).Any(line => line.StartsWith("$ ", StringComparison.Ordinal)));
        Assert.Contains($"$ {codex} login", Raised.Select(Line));
    }

    /// <summary>
    /// CODEXLOGIN1, the port held: where neither 1455 nor 1457 can be taken, the browser sign-in is refused before anything
    /// starts, in plain words naming each holder and offering the device code, with no account made and no tool run. Asked by
    /// its device code, the same press starts with no port tried: the panel's other way.
    /// </summary>
    [Fact]
    public async Task A_browser_sign_in_whose_ports_are_held_is_refused_in_plain_words_and_the_device_code_starts()
    {
        var codex = Path.Combine(Home, "no-codex-here");
        CodexNamed(codex);
        var tried = new List<int>();
        var module = Module(port =>
        {
            tried.Add(port);
            return port == 1455 ? PortHold.InUse : PortHold.Reserved;
        });

        var refusal = await RefusalAsync(module, "HARNESS_ACTION", new { harness = "codex-acp", action = "login-new" });

        Assert.Contains(
            "`codex login` waits for your browser on port 1455, else 1457, and neither can be taken now: 1455 is held by another "
            + "program; 1457 is held by a range Windows reserves, which a restart may move. Nothing was signed in — sign in with a "
            + "device code instead.",
            refusal);
        Assert.Equal([1455, 1457], tried);
        Assert.Empty(HarnessSettings.Profiles(Home, "codex"));
        Assert.DoesNotContain(Raised.Select(Line), line => line.StartsWith("$ ", StringComparison.Ordinal));

        tried.Clear();
        var started = await RefusalAsync(module, "HARNESS_ACTION", new { harness = "codex-acp", action = "login-new", way = "device-code" });
        Assert.Contains("could not be started", started);
        Assert.Empty(tried);
        await UntilAsync(() => Raised.Select(Line).Any(line => line.StartsWith("$ ", StringComparison.Ordinal)));
        Assert.Contains($"$ {codex} login --device-auth", Raised.Select(Line));
    }

    /// <summary>
    /// CODEXLOGIN1, the switch: a press that switches a sign-in's way with nothing of it running starts the way asked, never
    /// refused as busy; a way the agent does not have is refused before anything starts, as is a word that is no way.
    /// </summary>
    [Fact]
    public async Task A_switch_with_nothing_running_starts_the_way_asked_and_an_unknown_way_is_refused()
    {
        var codex = Path.Combine(Home, "no-codex-here");
        CodexNamed(codex);
        var module = Module(_ => PortHold.Free);

        var switched = await RefusalAsync(
            module, "HARNESS_ACTION", new { harness = "codex-acp", action = "login-new", way = "device-code", @switch = true });
        Assert.Contains("could not be started", switched);
        await UntilAsync(() => Raised.Select(Line).Any(line => line.StartsWith("$ ", StringComparison.Ordinal)));
        Assert.Contains($"$ {codex} login --device-auth", Raised.Select(Line));

        var unknown = await RefusalAsync(module, "HARNESS_ACTION", new { harness = "codex-acp", action = "login-new", way = "by-phone" });
        Assert.Contains("`by-phone` is no way to sign in — `browser` or `device-code`.", unknown);
        var none = await RefusalAsync(module, "HARNESS_ACTION", new { harness = "claude-code", action = "login-new", way = "device-code" });
        Assert.Contains("`claude-code` signs in by no device code, so nothing was signed in — its own sign-in is its one way.", none);
        Assert.Empty(HarnessSettings.Profiles(Home, "codex"));
        Assert.Empty(HarnessSettings.Profiles(Home, "claude-code"));
    }

    /// <summary>The text of one relayed console line, or empty for any other event.</summary>
    private static string Line(Shenora.Core.Events.EventMessage message)
    {
        if (message.Type != "SESSION_OUTPUT") return string.Empty;
        var json = JsonSerializer.SerializeToElement(message.Payload);
        return string.Join("\n", json.GetProperty("Lines").EnumerateArray().Select(l => l.GetProperty("Text").GetString()));
    }

    /// <summary>
    /// The sign-in runs the <c>codex</c> its status question asks (D57 rule 4): with no command named, the pin, here one with
    /// nothing installed at it, so it is refused naming the pin, never run from <c>PATH</c>, and keeps nothing.
    /// </summary>
    [Fact]
    public async Task Adding_a_codex_account_on_a_pin_with_nothing_installed_is_refused_naming_the_pin()
    {
        new HarnessSettings().WithVersion("codex", "0.160.0").Save(HarnessSettingsPath);

        var refusal = await RefusalAsync(Module(_ => PortHold.Free), "HARNESS_ACTION", new { harness = "codex-acp", action = "login-new" });

        Assert.Contains("`codex` is pinned to 0.160.0 on this machine, and nothing is installed at that version", refusal);
        Assert.Empty(HarnessSettings.Profiles(Home, "codex"));
    }
}
