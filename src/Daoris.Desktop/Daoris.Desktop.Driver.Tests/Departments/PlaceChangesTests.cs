using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A folder's review is its turn's change set: each file the turn added, changed or removed against its start's manifest, a text file
/// with its diff from the content store, a file that is not text a row with no hunks. A turn still running is read against the folder
/// as it stands; a git place's review is not this one's.
/// </summary>
public sealed class PlaceChangesTests : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("place-changes");
    private string Place => Path.Combine(_scratch, "designs");
    private string Home => Path.Combine(_scratch, "home");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    private void Write(string path, string text) => File.WriteAllText(Path.Combine(Place, path), text);

    [Fact]
    public async Task A_folder_turn_s_files_come_with_a_text_diff_and_a_row_with_no_hunks_for_what_is_not_text()
    {
        Directory.CreateDirectory(Place);
        Write("notes.md", "one\ntwo\nthree\n");
        Write("old.md", "gone\n");
        await PlaceTurns.StartAsync(Home, "default", "designs", Place, "s-aaaaaaaa", null, CancellationToken.None);

        Write("notes.md", "one\n2\nthree\n");
        File.Delete(Path.Combine(Place, "old.md"));
        File.WriteAllBytes(Path.Combine(Place, "picture.png"), [0x89, 0x50, 0x00, 0x01]);

        // While it runs, against the folder as it stands; once ended, against its end.
        var running = PlaceChanges.Of(Home, "s-aaaaaaaa")!;
        await PlaceTurns.EndAsync(Home, "s-aaaaaaaa", CancellationToken.None);
        Write("notes.md", "the person's after\n");
        var ended = PlaceChanges.Of(Home, "s-aaaaaaaa")!;

        foreach (var review in new[] { running, ended })
        {
            Assert.Equal(["notes.md", "old.md", "picture.png"], review.Files.Select(file => file.Path));
            var notes = review.Files[0];
            Assert.Equal(("modified", 1, 1), (notes.Status, notes.Added, notes.Removed));
            Assert.Contains("@@ -1,3 +1,3 @@\n one\n-two\n+2\n three\n", notes.Patch);
            Assert.StartsWith("diff --git a/notes.md b/notes.md\n--- a/notes.md\n+++ b/notes.md\n", notes.Patch);
            Assert.Equal(("deleted", 0, 1), (review.Files[1].Status, review.Files[1].Added, review.Files[1].Removed));
            Assert.Contains("+++ /dev/null", review.Files[1].Patch);
            Assert.Equal(("added", (int?)null, (string?)null), (review.Files[2].Status, review.Files[2].Added, review.Files[2].Patch));
            Assert.Equal("", review.Base);
        }
    }

    [Fact]
    public void A_record_with_no_folder_turn_has_no_folder_review()
    {
        Assert.Null(PlaceChanges.Of(Home, "s-aaaaaaaa"));
        new ManifestStore(Home, "default", "reports").Keep(
            "s-bbbbbbbb", ManifestMoment.Start, new RunManifest(PlaceKind.Git, null, "main", DateTimeOffset.UtcNow, []) { Root = Place });
        Assert.Null(PlaceChanges.Of(Home, "s-bbbbbbbb"));
    }

    [Fact]
    public void A_patch_keeps_three_lines_of_context_and_joins_close_hunks()
    {
        var before = string.Concat(Enumerable.Range(1, 20).Select(i => $"{i}\n"));
        var after = before.Replace("2\n3\n", "2\nthree\n", StringComparison.Ordinal).Replace("\n18\n", "\neighteen\n", StringComparison.Ordinal);

        var (patch, added, removed) = TextPatch.Unified("n.txt", before, after);

        Assert.Equal((2, 2), (added, removed));
        Assert.Contains("@@ -1,6 +1,6 @@\n 1\n 2\n-3\n+three\n 4\n 5\n 6\n", patch);
        Assert.Contains("@@ -15,6 +15,6 @@\n 15\n 16\n 17\n-18\n+eighteen\n 19\n 20\n", patch);
        Assert.Equal((0, 2), (TextPatch.Unified("n.txt", "a\nb\n", null).Added, TextPatch.Unified("n.txt", "a\nb\n", null).Removed));
        Assert.Contains("--- /dev/null\n+++ b/n.txt\n@@ -0,0 +1,1 @@\n+x\n", TextPatch.Unified("n.txt", null, "x\n").Patch);
    }

    [Fact]
    public async Task A_file_that_is_not_utf8_text_is_a_row_with_no_hunks_not_a_patch_of_replacement_characters()
    {
        Directory.CreateDirectory(Place);
        File.WriteAllBytes(Path.Combine(Place, "old.txt"), [0xC4, 0xE3, 0xBA, 0xC3, 0x0A]);
        await PlaceTurns.StartAsync(Home, "default", "designs", Place, "s-aaaaaaaa", null, CancellationToken.None);
        File.WriteAllBytes(Path.Combine(Place, "old.txt"), [0xCA, 0xC0, 0xBD, 0xE7, 0x0A]);

        var review = PlaceChanges.Of(Home, "s-aaaaaaaa")!;

        var file = Assert.Single(review.Files);
        Assert.Equal("modified", file.Status);
        Assert.Null(file.Patch);
    }
}
