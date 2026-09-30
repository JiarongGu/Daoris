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

    /// <summary>
    /// A file's preview (PREVIEW1, D111) is found through the session's record, as its review is, so on a cold
    /// start it is the same sentence; and a preview of no file is a malformed call.
    /// </summary>
    [Fact]
    public async Task Reading_a_file_for_its_preview_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_FILE", new { id = "s1a2b3c4", path = "src/chunk.ts" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        Assert.Contains("still coming up", refusal);
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "SESSION_FILE", new { id = "s1a2b3c4" }));
    }

    /// <summary>What the page receives for a file read: the path it asked for, its size, and its text — camelCase, as serialized.</summary>
    [Fact]
    public async Task A_file_read_for_its_preview_is_answered_with_its_path_its_size_and_its_text()
    {
        var tree = Path.Combine(Home, "trees", "engine");
        Directory.CreateDirectory(Path.Combine(tree, "src"));
        File.WriteAllText(Path.Combine(tree, "src", "chunk.ts"), "export const a = 1;\n");

        var answer = JsonSerializer.SerializeToElement(
            await DriverModule.PreviewAsync("s1a2b3c4", tree, "src/chunk.ts", CancellationToken.None),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Equal("s1a2b3c4", answer.GetProperty("session").GetString());
        Assert.Equal("src/chunk.ts", answer.GetProperty("path").GetString());
        Assert.Equal(20, answer.GetProperty("size").GetInt64());
        Assert.False(answer.GetProperty("binary").GetBoolean());
        Assert.False(answer.GetProperty("truncated").GetBoolean());
        Assert.Equal("export const a = 1;\n", answer.GetProperty("text").GetString());
        // The tree's own path never goes back to the page (D47 §4; platform language §4).
        Assert.DoesNotContain(tree, answer.GetRawText().Replace("\\\\", "\\"));
    }

    /// <summary>
    /// The three refusals PREVIEW1 names, and its two pieces of information, each a code of its own: the
    /// page renders each from the catalogue, and a person's next move differs for each.
    /// </summary>
    [Theory]
    [InlineData(FilePreviewRefusal.Outside, "PREVIEW_OUTSIDE_TREE")]
    [InlineData(FilePreviewRefusal.LinkLeaves, "PREVIEW_LINK_LEAVES_TREE")]
    [InlineData(FilePreviewRefusal.GitFolder, "PREVIEW_GIT_FOLDER")]
    [InlineData(FilePreviewRefusal.NoTree, "PREVIEW_NO_TREE")]
    [InlineData(FilePreviewRefusal.NotAFile, "PREVIEW_NOT_A_FILE")]
    public void Each_reason_a_file_was_not_read_is_a_refusal_of_its_own(FilePreviewRefusal why, string code)
    {
        var error = Assert.Throws<Shenora.Core.Ipc.ShenoraException>(
            () => DriverModule.FileAnswer("s1a2b3c4", "../other/secret.txt", FilePreviewResult.Refused(why)));

        Assert.Equal(code, error.Code);
        Assert.Contains(code, Refusals.All);
        // Each names what was asked for, in the words the page sent — never a machine path it was not told.
        Assert.Equal("../other/secret.txt", error.Parameters!["path"]);
    }

    /// <summary>Over a real tree: a path outside it and one under `.git` are refused by the route's own reading.</summary>
    [Fact]
    public async Task A_path_outside_the_tree_or_under_git_is_refused_by_the_route_s_reading()
    {
        var tree = Path.Combine(Home, "trees", "engine");
        Directory.CreateDirectory(Path.Combine(tree, ".git"));
        File.WriteAllText(Path.Combine(tree, ".git", "config"), "[core]\n");
        File.WriteAllText(Path.Combine(Home, "driver.json"), "{}");

        var outside = await Assert.ThrowsAsync<Shenora.Core.Ipc.ShenoraException>(
            () => DriverModule.PreviewAsync("s1a2b3c4", tree, "../../driver.json", CancellationToken.None));
        var git = await Assert.ThrowsAsync<Shenora.Core.Ipc.ShenoraException>(
            () => DriverModule.PreviewAsync("s1a2b3c4", tree, ".git/config", CancellationToken.None));
        var none = await Assert.ThrowsAsync<Shenora.Core.Ipc.ShenoraException>(
            () => DriverModule.PreviewAsync("s1a2b3c4", null, "src/chunk.ts", CancellationToken.None));

        Assert.Equal(Refusals.PreviewOutsideTree, outside.Code);
        Assert.Equal(Refusals.PreviewGitFolder, git.Code);
        Assert.Equal(Refusals.PreviewNoTree, none.Code);
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
