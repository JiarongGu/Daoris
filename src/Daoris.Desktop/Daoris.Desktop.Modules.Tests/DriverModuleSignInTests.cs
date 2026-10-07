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
    /// What the roster answers each door's page with: whether it signs in, and takes a key, as its agent does. Codex signs in
    /// with <c>codex login</c> and takes no key from Daoris (D67 §1 holds a key no Codex variable is measured for).
    /// </summary>
    [Theory]
    [InlineData("claude-code", true, true)]
    [InlineData("claude-code-acp", true, true)]
    [InlineData("codex-acp", true, false)]
    [InlineData("dsh", false, false)]
    public void Each_door_signs_in_and_takes_a_key_as_its_agent_does(string door, bool signsIn, bool takesKey)
    {
        var acts = DriverModule.AccountActs(new HarnessRoster(AdapterSet.Built(), HarnessSettingsPath), door);

        Assert.Equal((signsIn, takesKey), (acts.SignsIn, acts.TakesKey));
    }

    /// <summary>
    /// *Add an account…* on Agents → Codex runs <c>codex</c>'s sign-in, by the command named for <c>codex</c>, into a folder
    /// opened under <c>codex</c>'s accounts: here one that is not there, so it is refused naming it, and the folder opened
    /// for it is gone again. It was refused as a door with no sign-in flow.
    /// </summary>
    [Fact]
    public async Task Adding_a_codex_account_runs_codex_s_own_sign_in_and_keeps_nothing_when_it_cannot_start()
    {
        var codex = Path.Combine(Home, "no-codex-here");
        File.WriteAllText(DriverConfigPath, $$"""
            { "drivable": [], "holds": [], "cap": 1, "adapter": "codex-acp",
              "commands": { "codex": [{{JsonSerializer.Serialize(codex)}}] } }
            """);

        var refusal = await RefusalAsync(Module(), "HARNESS_ACTION", new { harness = "codex-acp", action = "login-new" });

        Assert.DoesNotContain("declares no sign-in flow", refusal);
        Assert.Contains("no-codex-here", refusal);
        Assert.Contains("could not be started", refusal);
        Assert.Empty(HarnessSettings.Profiles(Home, "codex"));
        Assert.False(Directory.Exists(Path.Combine(Home, "harnesses", "codex-acp")));
    }
}
