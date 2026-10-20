using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The bridge onto <see cref="DriverModule"/> that every domain's test class shares (MOD5), as the
/// partials share the module.
/// </summary>
/// <remarks>
/// No service is running in these tests, deliberately. Everything here is either an edit to
/// `driver.json`, a read of this machine's harnesses, or a refusal — and the refusals are the point:
/// "the driver is still coming up" is the state a person meets most often on a cold start, and it has
/// to be a sentence rather than a crash.
/// </remarks>
public abstract class DriverModuleBridge : Bridge
{
    protected DriverModuleBridge(string? repositoryFixture = null) : base(repositoryFixture) { }
    protected DriverLoop Loop() => new(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0");

    protected DriverModule Module() => new(Bus, Loop());

    /// <summary>
    /// The person's Apply on the card as drawn now (HELPAPPLY1): the page sends the revision its card showed, so a test that
    /// is not about the binding draws the card and presses what it shows.
    /// </summary>
    protected static async Task<JsonElement> ApplyShownAsync(IIpcModule module, string session, string id)
    {
        var drawn = await AnswerAsync(module, "HELP_PROPOSALS", new { session });
        var card = drawn.GetProperty("proposals").EnumerateArray().First(each => each.GetProperty("id").GetString() == id);
        return await AnswerAsync(module, "HELP_APPLY", new { id, revision = card.GetProperty("revision").GetString() });
    }

    protected static async Task UntilAsync(Func<bool> condition)
    {
        var patience = DateTime.UtcNow + Poll.Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < patience, "the condition never held");
            await Task.Delay(25);
        }
    }
}
