using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A session's tree over the bridge (`DriverModule.Trees.cs`, MOD5): the review, the files a composer
/// offers, merge and discard, landing and the hand-off.
/// </summary>
public sealed class DriverModuleTreesTests : DriverModuleBridge
{
    /// <summary>
    /// The review route on a cold start (SURF6). Every other refusal it can raise needs a service to
    /// answer first; this one is the state a person actually meets, and it has to be a sentence.
    /// </summary>
    [Fact]
    public async Task Asking_what_a_session_landed_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_DIFF", new { id = "s1a2b3c4" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        // The sentence, not just the code: this is the state a person meets on a cold start.
        Assert.Contains("still coming up", refusal);
    }

    /// <summary>A review of nothing in particular is a malformed call, not an empty answer.</summary>
    [Fact]
    public async Task Asking_what_a_session_landed_without_naming_one_is_refused()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "SESSION_DIFF", new { }));
    }

    /// <summary>
    /// The files a person may `@` (CONV4d) are found through the session's record, as its review is —
    /// so on a cold start the answer is the same sentence, never an empty list, which would read as a
    /// tree with nothing in it.
    /// </summary>
    [Fact]
    public async Task Asking_what_a_session_tree_holds_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_FILES", new { id = "s1a2b3c4" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        Assert.Contains("still coming up", refusal);
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "SESSION_FILES", new { }));
    }

    /// <summary>A plan or a press reads the session's record, so before the driver is up each is the cold-start sentence.</summary>
    [Fact]
    public async Task Landing_before_the_driver_is_up_is_a_sentence()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "LANDING", new { id = "s1a2b3c4" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "LAND_SESSION_TREE", new { id = "s1a2b3c4" }));
    }

    /// <summary>A hand-off (WSR5b) reads the registry's checkouts, so before the driver is up each route is the cold-start sentence.</summary>
    [Fact]
    public async Task A_hand_off_before_the_driver_is_up_is_a_sentence()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "HANDOFF_PLAN", new { id = "s1a2b3c4" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "HANDOFF", new { id = "s1a2b3c4", plugin = "example.lands" }));
    }

    /// <summary>
    /// Merge and discard read the session's record first, so before the driver is up each is the cold-start
    /// sentence. The page lands through `LAND_SESSION_TREE` since WSR1 and no longer sends
    /// `MERGE_SESSION_TREE`, which stays the merge alone; held here so the route is still asked (MOD5).
    /// </summary>
    [Theory]
    [InlineData("MERGE_SESSION_TREE")]
    [InlineData("DISCARD_SESSION_TREE")]
    public async Task Merging_or_discarding_a_tree_before_the_driver_is_up_is_a_sentence(string type)
    {
        var refusal = await RefusalAsync(Module(), type, new { id = "s1a2b3c4" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        Assert.Contains("still coming up", refusal);
    }
}
