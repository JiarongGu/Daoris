using System.Security.Cryptography;
using System.Text;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The bytes a quest carries (D65 §2): kept under the home of the machine that has them, per quest,
/// by content. The layout is the service's alone — a caller on this machine is told each kept file's
/// path rather than deriving it, because the driver's home is not always this one.
/// </summary>
public sealed class QuestFilesTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-questfiles-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static string Sha(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>The layout: the home, the quest, then the content's head and the file's own name.</summary>
    [Theory]
    [InlineData("a1b2c3", "screenshot.png", "0123456789abcdef", "quests/a1b2c3/attachments/0123456789ab-screenshot.png")]
    [InlineData("a1b2c3", "image.png", "fedcba9876543210", "quests/a1b2c3/attachments/fedcba987654-image.png")]
    [InlineData("0f0f0f", "notes.txt", "aaaaaaaaaaaaaaaa", "quests/0f0f0f/attachments/aaaaaaaaaaaa-notes.txt")]
    public void The_layout_is_the_home_the_quest_and_the_content(
        string quest, string name, string sha, string expected)
    {
        var files = new QuestFiles(_home);

        Assert.Equal(
            Path.Combine(_home, expected.Replace('/', Path.DirectorySeparatorChar)),
            files.PathOf(quest, new QuestAttachment(name, sha + new string('0', 48), 1)));
    }

    /// <summary>
    /// Two pasted screenshots are both called <c>image.png</c> — the hash in the file name is what
    /// keeps them two files, and the same file dropped twice one.
    /// </summary>
    [Fact]
    public void A_file_is_described_by_its_content_and_its_own_name()
    {
        var described = QuestFiles.Describe(new QuestUpload("image.png", Encoding.UTF8.GetBytes("pixels")));

        Assert.Equal("image.png", described.Name);
        Assert.Equal(Sha("pixels"), described.Sha256);
        Assert.Equal(6, described.Bytes);
    }

    /// <summary>
    /// A name is the person's, but a path is not theirs to give: a directory in it is dropped, and a
    /// character no file system takes becomes an underscore — so the kept file can never land outside
    /// its quest's directory, whatever a door was sent.
    /// </summary>
    [Theory]
    [InlineData("../../escape.txt", "escape.txt")]
    [InlineData("C:\\work\\private\\secret.txt", "secret.txt")]
    [InlineData("what?.png", "what_.png")]
    [InlineData("  spaced.log  ", "spaced.log")]
    [InlineData("trailing.", "trailing")]
    [InlineData("", "attachment")]
    [InlineData("..", "attachment")]
    public void A_name_is_made_safe_to_keep(string given, string kept)
    {
        Assert.Equal(kept, QuestFiles.SafeName(given));
    }

    [Fact]
    public void A_very_long_name_keeps_its_extension()
    {
        var kept = QuestFiles.SafeName(new string('x', 300) + ".png");

        Assert.True(kept.Length <= QuestFiles.MaxNameLength);
        Assert.EndsWith(".png", kept);
    }

    [Fact]
    public async Task Keeping_writes_the_bytes_where_the_layout_says_and_is_idempotent()
    {
        var files = new QuestFiles(_home);
        var upload = new QuestUpload("notes.txt", Encoding.UTF8.GetBytes("remember this"));
        var attachment = QuestFiles.Describe(upload);

        await files.KeepAsync("a1b2c3", upload);
        await files.KeepAsync("a1b2c3", upload);

        var path = files.PathOf("a1b2c3", attachment);
        Assert.Equal("remember this", await File.ReadAllTextAsync(path));
        Assert.True(files.Has("a1b2c3", attachment));
        Assert.Single(Directory.GetFiles(files.DirectoryOf("a1b2c3")));
    }

    /// <summary>
    /// The MCP door's shape: an agent attaches a file it can see, by path — relative to the repository
    /// its session runs in, since that is where its MCP host was started.
    /// </summary>
    [Fact]
    public async Task A_path_on_this_machine_is_read_whole_relative_to_where_the_session_is()
    {
        var repository = Path.Combine(_home, "repository");
        Directory.CreateDirectory(Path.Combine(repository, "logs"));
        await File.WriteAllTextAsync(Path.Combine(repository, "logs", "trace.log"), "stack");

        var (upload, refusal) = await QuestFiles.ReadAsync("logs/trace.log", repository);

        Assert.Null(refusal);
        Assert.Equal("trace.log", upload!.Name);
        Assert.Equal("stack", Encoding.UTF8.GetString(upload.Content));
    }

    [Fact]
    public async Task A_path_that_is_not_a_file_is_refused_naming_it()
    {
        var (upload, refusal) = await QuestFiles.ReadAsync("missing.png", _home);

        Assert.Null(upload);
        Assert.Contains("missing.png", refusal);
    }

    /// <summary>Too large is said before the file is read — a quest never loads what it cannot carry.</summary>
    [Fact]
    public async Task A_file_larger_than_a_quest_carries_is_refused_before_it_is_read()
    {
        Directory.CreateDirectory(_home);
        var path = Path.Combine(_home, "huge.bin");
        await using (var stream = File.Create(path)) stream.SetLength(QuestExchange.MaxAttachmentBytes + 1);

        var (upload, refusal) = await QuestFiles.ReadAsync(path, _home);

        Assert.Null(upload);
        Assert.Contains("link", refusal);
    }

    [Fact]
    public void A_file_never_kept_here_is_not_here()
    {
        var files = new QuestFiles(_home);

        Assert.False(files.Has("a1b2c3", new QuestAttachment("elsewhere.png", Sha("elsewhere"), 9)));
    }
}
