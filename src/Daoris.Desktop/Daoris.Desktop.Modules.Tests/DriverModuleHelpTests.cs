using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Ask Daoris over the bridge (`DriverModule.Help.cs`, MOD5): its start and its proposals. The doors an
/// Apply goes through are `HelpDoorsTests`'.
/// </summary>
public sealed class DriverModuleHelpTests : DriverModuleBridge
{
    /// <summary>
    /// Ask Daoris (HELP1a, D89) is off until its agent is named, and says where to name one — asked
    /// before the loop is, since no service answer changes it.
    /// </summary>
    [Fact]
    public async Task Ask_Daoris_with_no_agent_named_says_where_to_name_one()
    {
        var refusal = await RefusalAsync(Module(), "START_HELP");

        Assert.Contains(Refusals.DriverRefused, refusal);
        Assert.Contains("Settings → Daoris's own AI", refusal);
        Assert.Contains("daoris driver helper <agent>", refusal);
    }

    /// <summary>
    /// HELP1c: the person's Not now settles a proposal of Ask Daoris's, and a settled one takes no second
    /// press. Listing and applying judge against the registry, so they wait for the loop like the review.
    /// </summary>
    [Fact]
    public async Task Not_now_settles_an_Ask_Daoris_proposal_once_and_listing_waits_for_the_loop()
    {
        var folder = HelpProposals.FolderOf(Path.GetDirectoryName(DriverConfigPath)!);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "p1a2b3c4.json"), """
            { "id": "p1a2b3c4", "proposed": "2026-09-29T10:00:00Z", "by": { "session": "h1" }, "kind": "setting",
              "door": "drive", "target": "engine", "workspace": null, "value": null, "sentence": null,
              "why": "the person asked", "state": "proposed", "note": null }
            """);
        var module = Module();

        var dismissed = await AnswerAsync(module, "HELP_DISMISS", new { id = "p1a2b3c4" });

        Assert.Contains("did not apply `#p1a2b3c4`", dismissed.GetProperty("message").GetString());
        Assert.Equal("dismissed", HelpProposals.Find(Path.GetDirectoryName(DriverConfigPath)!, "p1a2b3c4")!.State);
        Assert.Contains("already dismissed", await RefusalAsync(module, "HELP_DISMISS", new { id = "p1a2b3c4" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(module, "HELP_PROPOSALS", new { session = "h1" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(module, "HELP_APPLY", new { id = "p1a2b3c4" }));
    }

    [Fact]
    public async Task Ask_Daoris_asked_for_before_the_loop_is_up_says_so()
    {
        var module = Module();
        await AnswerAsync(module, "SET_HELPER", new { adapter = "claude-code-acp" });

        var refusal = await RefusalAsync(module, "START_HELP");

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }
}
