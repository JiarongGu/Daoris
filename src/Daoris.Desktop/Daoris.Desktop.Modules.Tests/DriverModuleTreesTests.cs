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
    /// Opening a preview is a line in the machine log (LEFT2, D94): the session and the file relative to the tree,
    /// asked for here by its full path, so the line is seen to keep the relative one and never the tree's own. A
    /// preview that is refused opens nothing and writes nothing.
    /// </summary>
    [Fact]
    public async Task Opening_a_preview_is_a_machine_log_line_with_the_session_and_the_path_relative_to_the_tree()
    {
        var tree = Path.Combine(Home, "trees", "engine");
        Directory.CreateDirectory(Path.Combine(tree, "src"));
        File.WriteAllText(Path.Combine(tree, "src", "chunk.ts"), "export const a = 1;\n");
        File.WriteAllText(Path.Combine(Home, "driver.json"), "{}");
        using var log = new MachineLog(Home, "desktop", () => new DateTimeOffset(2026, 9, 30, 7, 10, 0, TimeSpan.Zero));

        await DriverModule.PreviewAsync("s1a2b3c4", tree, Path.Combine(tree, "src", "chunk.ts"), CancellationToken.None, log);
        await Assert.ThrowsAsync<Shenora.Core.Ipc.ShenoraException>(
            () => DriverModule.PreviewAsync("s1a2b3c4", tree, "../../driver.json", CancellationToken.None, log));
        await Assert.ThrowsAsync<Shenora.Core.Ipc.ShenoraException>(
            () => DriverModule.PreviewAsync("s1a2b3c4", tree, "src/gone.ts", CancellationToken.None, log));

        var folder = Path.Combine(Home, MachineLog.Folder);
        var lines = Directory.GetFiles(folder).SelectMany(file =>
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }).ToArray();
        var line = JsonDocument.Parse(Assert.Single(lines)).RootElement;
        Assert.Equal("preview.opened", line.GetProperty("event").GetString());
        Assert.Equal("info", line.GetProperty("level").GetString());
        Assert.Equal("""{"session":"s1a2b3c4","path":"src/chunk.ts"}""", line.GetProperty("data").GetRawText());
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

    /// <summary>Discard reads the session's record first, so before the driver is up it is the cold-start sentence.</summary>
    [Fact]
    public async Task Discarding_a_tree_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "DISCARD_SESSION_TREE", new { id = "s1a2b3c4" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        Assert.Contains("still coming up", refusal);
    }

    /// <summary>
    /// The merge alone is retired (LEFT2): the page lands through `LAND_SESSION_TREE` since WSR1, which merges
    /// where the repository's rule says merge, and a door that merged whatever the rule said was a door past it
    /// (D87). Nothing called it, and the terminal never had its twin.
    /// </summary>
    [Fact]
    public async Task The_merge_alone_is_no_route()
    {
        Assert.DoesNotContain("MERGE_SESSION_TREE", DriverModule.Routes);
        Assert.Contains("NO_ROUTE", await RefusalAsync(Module(), "MERGE_SESSION_TREE", new { id = "s1a2b3c4" }));
    }
}
