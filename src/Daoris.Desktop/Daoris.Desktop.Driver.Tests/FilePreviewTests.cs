using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A file read for its preview (PREVIEW1, D111): only inside the session's tree, never through a link that
/// leads out of it, never under `.git`, bounded, and a binary file said rather than shown.
/// </summary>
/// <remarks>
/// Files only, so this is the suite's fast half. A link is the reader's seam here (what a path's link
/// resolves to); <see cref="FilePreviewLinkTests"/> holds the same refusals over a real link, which takes a
/// process to make on Windows.
/// </remarks>
public sealed class FilePreviewTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-preview-" + Guid.NewGuid().ToString("N")[..8]);

    private string Tree => Path.Combine(_home, "work", "engine");

    public FilePreviewTests()
    {
        Directory.CreateDirectory(Path.Combine(Tree, "src"));
        Directory.CreateDirectory(Path.Combine(_home, "work", "other"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Write(string relative, string text) => File.WriteAllText(Path.Combine(Tree, relative), text);

    private static Func<string, string?> NoLinks => _ => null;

    [Fact]
    public async Task A_file_in_the_tree_is_read_whole_and_named_relative_to_it()
    {
        Write("src/chunk.ts", "export const a = 1;\nexport const b = 2;\n");

        var read = await FilePreview.ReadAsync(Tree, "src/chunk.ts");

        Assert.Equal(FilePreviewRefusal.None, read.Refusal);
        Assert.Equal("src/chunk.ts", read.File!.Path);
        Assert.Equal("export const a = 1;\nexport const b = 2;\n", read.File.Text);
        Assert.Equal(40, read.File.Size);
        Assert.False(read.File.Binary);
        Assert.False(read.File.Truncated);
    }

    /// <summary>A tool card names the path the agent used, which is often the whole of it: still read, and answered relative.</summary>
    [Fact]
    public async Task An_absolute_path_inside_the_tree_is_read_and_answered_relative()
    {
        Write("src/chunk.ts", "x\n");

        var read = await FilePreview.ReadAsync(Tree, Path.Combine(Tree, "src", "chunk.ts"));

        Assert.Equal(FilePreviewRefusal.None, read.Refusal);
        Assert.Equal("src/chunk.ts", read.File!.Path);
    }

    [Theory]
    [InlineData("../other/secret.txt")]
    [InlineData("src/../../other/secret.txt")]
    public async Task A_path_that_climbs_out_of_the_tree_is_outside(string path)
    {
        File.WriteAllText(Path.Combine(_home, "work", "other", "secret.txt"), "not yours");

        var read = await FilePreview.ReadAsync(Tree, path);

        Assert.Equal(FilePreviewRefusal.Outside, read.Refusal);
        Assert.Null(read.File);
    }

    [Fact]
    public async Task An_absolute_path_elsewhere_is_outside()
    {
        var elsewhere = Path.Combine(_home, "work", "other", "secret.txt");
        File.WriteAllText(elsewhere, "not yours");

        Assert.Equal(FilePreviewRefusal.Outside, (await FilePreview.ReadAsync(Tree, elsewhere)).Refusal);
    }

    /// <summary>🔴 A string prefix is not a folder: `engine-old` starts with `engine` and is its sibling.</summary>
    [Fact]
    public async Task A_sibling_whose_name_starts_with_the_tree_s_is_outside()
    {
        var sibling = Tree + "-old";
        Directory.CreateDirectory(sibling);
        File.WriteAllText(Path.Combine(sibling, "a.txt"), "not yours");

        Assert.Equal(FilePreviewRefusal.Outside, (await FilePreview.ReadAsync(Tree, Path.Combine(sibling, "a.txt"))).Refusal);
    }

    [Theory]
    [InlineData(".git/config")]
    [InlineData(".GIT/config")]
    [InlineData("vendor/lib/.git/HEAD")]
    public async Task A_path_under_git_s_own_folder_is_refused(string path)
    {
        Directory.CreateDirectory(Path.Combine(Tree, Path.GetDirectoryName(path)!));
        Write(path, "[core]\n");

        Assert.Equal(FilePreviewRefusal.GitFolder, (await FilePreview.ReadAsync(Tree, path)).Refusal);
    }

    /// <summary>`.gitignore` is the work's own file: only a segment that IS `.git` is git's.</summary>
    [Fact]
    public async Task A_file_whose_name_only_begins_with_git_is_read()
    {
        Write(".gitignore", "bin/\n");

        var read = await FilePreview.ReadAsync(Tree, ".gitignore");

        Assert.Equal(FilePreviewRefusal.None, read.Refusal);
        Assert.Equal("bin/\n", read.File!.Text);
    }

    /// <summary>🔴 The string is inside and the bytes are not: a link is followed, and where it leads is what is judged.</summary>
    [Fact]
    public async Task A_path_through_a_link_that_leads_out_of_the_tree_is_refused()
    {
        var link = Path.Combine(Tree, "escape");
        var outside = Path.Combine(_home, "work", "other");
        File.WriteAllText(Path.Combine(outside, "secret.txt"), "not yours");

        var read = await FilePreview.ReadAsync(Tree, "escape/secret.txt",
            path => string.Equals(path, link, StringComparison.OrdinalIgnoreCase) ? outside : null, default);

        Assert.Equal(FilePreviewRefusal.LinkLeaves, read.Refusal);
    }

    [Fact]
    public async Task A_path_through_a_link_that_stays_inside_the_tree_is_read()
    {
        Write("src/real.ts", "inside\n");
        var link = Path.Combine(Tree, "alias");

        var read = await FilePreview.ReadAsync(Tree, "alias/real.ts",
            path => string.Equals(path, link, StringComparison.OrdinalIgnoreCase) ? Path.Combine(Tree, "src") : null, default);

        Assert.Equal(FilePreviewRefusal.None, read.Refusal);
        Assert.Equal("inside\n", read.File!.Text);
        // Named as it was asked for, which is the name the card showed.
        Assert.Equal("alias/real.ts", read.File.Path);
    }

    [Fact]
    public async Task A_link_into_git_s_own_folder_is_refused()
    {
        Directory.CreateDirectory(Path.Combine(Tree, ".git"));
        Write(".git/config", "[core]\n");
        var link = Path.Combine(Tree, "config");

        var read = await FilePreview.ReadAsync(Tree, "config",
            path => string.Equals(path, link, StringComparison.OrdinalIgnoreCase) ? Path.Combine(Tree, ".git", "config") : null, default);

        Assert.Equal(FilePreviewRefusal.GitFolder, read.Refusal);
    }

    /// <summary>A link nobody can resolve cannot be shown to stay inside, so it is not followed.</summary>
    [Fact]
    public async Task A_link_that_cannot_be_resolved_is_refused_as_leaving()
    {
        Write("src/chunk.ts", "x\n");

        var read = await FilePreview.ReadAsync(Tree, "src/chunk.ts",
            path => path.EndsWith("chunk.ts", StringComparison.Ordinal) ? throw new IOException("unreadable link") : null, default);

        Assert.Equal(FilePreviewRefusal.LinkLeaves, read.Refusal);
    }

    [Fact]
    public async Task Two_links_that_lead_to_each_other_are_refused_rather_than_followed_forever()
    {
        var a = Path.Combine(Tree, "a");
        var b = Path.Combine(Tree, "b");

        var read = await FilePreview.ReadAsync(Tree, "a/file.txt",
            path => string.Equals(path, a, StringComparison.OrdinalIgnoreCase) ? b
                : string.Equals(path, b, StringComparison.OrdinalIgnoreCase) ? a : null, default);

        Assert.Equal(FilePreviewRefusal.LinkLeaves, read.Refusal);
    }

    [Fact]
    public async Task A_tree_that_is_not_here_is_no_tree()
    {
        Assert.Equal(FilePreviewRefusal.NoTree, (await FilePreview.ReadAsync(Path.Combine(_home, "gone"), "a.txt")).Refusal);
        Assert.Equal(FilePreviewRefusal.NoTree, (await FilePreview.ReadAsync("", "a.txt")).Refusal);
    }

    [Theory]
    [InlineData("src")]
    [InlineData("src/missing.ts")]
    [InlineData("")]
    [InlineData(".")]
    public async Task A_folder_a_missing_file_or_nothing_is_not_a_file(string path)
    {
        Assert.Equal(FilePreviewRefusal.NotAFile, (await FilePreview.ReadAsync(Tree, path, NoLinks, default)).Refusal);
    }

    /// <summary>git's own test for binary: a NUL in the first 8,000 bytes. Said with its size, and no text.</summary>
    [Fact]
    public async Task A_binary_file_is_said_with_its_size_and_no_text()
    {
        File.WriteAllBytes(Path.Combine(Tree, "logo.png"), [0x89, 0x50, 0x4E, 0x47, 0x00, 0x01, 0x02]);

        var read = await FilePreview.ReadAsync(Tree, "logo.png");

        Assert.Equal(FilePreviewRefusal.None, read.Refusal);
        Assert.True(read.File!.Binary);
        Assert.Null(read.File.Text);
        Assert.Equal(7, read.File.Size);
    }

    [Fact]
    public async Task A_NUL_past_the_first_eight_thousand_bytes_is_still_text()
    {
        var bytes = Encoding.UTF8.GetBytes(new string('a', FilePreview.BinaryProbe) + "\n").Append((byte)0).ToArray();
        File.WriteAllBytes(Path.Combine(Tree, "late.txt"), bytes);

        Assert.False((await FilePreview.ReadAsync(Tree, "late.txt")).File!.Binary);
    }

    /// <summary>The bound is stated, never hidden (design §5): cut at a line's end, with the whole size said.</summary>
    [Fact]
    public async Task A_file_past_the_bound_is_cut_at_a_line_s_end_and_says_so()
    {
        var line = new string('x', 99) + "\n";
        var text = string.Concat(Enumerable.Repeat(line, FilePreview.Budget / 100 + 50));
        Write("big.txt", text);

        var read = await FilePreview.ReadAsync(Tree, "big.txt");

        Assert.True(read.File!.Truncated);
        Assert.Equal(text.Length, read.File.Size);
        Assert.True(read.File.Text!.Length <= FilePreview.Budget);
        Assert.EndsWith("\n", read.File.Text);
        Assert.Equal(0, read.File.Text.Length % 100);
    }

    [Fact]
    public async Task A_byte_order_mark_is_not_part_of_the_text()
    {
        File.WriteAllBytes(Path.Combine(Tree, "bom.cs"), [0xEF, 0xBB, 0xBF, (byte)'c', (byte)'s', (byte)'\n']);

        Assert.Equal("cs\n", (await FilePreview.ReadAsync(Tree, "bom.cs")).File!.Text);
    }

    [Fact]
    public async Task Text_outside_ASCII_is_read_as_it_was_written()
    {
        Write("说明.md", "工作树里的文件\n");

        var read = await FilePreview.ReadAsync(Tree, "说明.md");

        Assert.Equal("说明.md", read.File!.Path);
        Assert.Equal("工作树里的文件\n", read.File.Text);
    }

    [Fact]
    public async Task An_empty_file_is_text_with_nothing_in_it()
    {
        Write("empty.txt", "");

        var read = await FilePreview.ReadAsync(Tree, "empty.txt");

        Assert.Equal("", read.File!.Text);
        Assert.False(read.File.Binary);
        Assert.Equal(0, read.File.Size);
    }
}
