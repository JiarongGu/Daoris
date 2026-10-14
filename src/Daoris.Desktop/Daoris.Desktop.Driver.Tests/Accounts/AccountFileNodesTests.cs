using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the four account files (cooling, names, windows, reads) share when they read their JSON root (DEDUP5): a missing
/// or unreadable file is an empty root, a name is found without case, a child object is made when asked, and a moment
/// reads only as ISO 8601 writes it.
/// </summary>
public sealed class AccountFileNodesTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "daoris-accountnodes-" + Guid.NewGuid().ToString("N")[..8]);

    public AccountFileNodesTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Put(string text)
    {
        var path = Path.Combine(_folder, "file.json");
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void A_missing_file_reads_an_empty_root_that_is_not_unreadable()
    {
        var (root, unreadable) = AccountFileNodes.Read(Path.Combine(_folder, "none.json"));

        Assert.Empty(root);
        Assert.False(unreadable);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[1, 2]")]
    public void An_unreadable_file_reads_an_empty_root_marked_unreadable_and_is_left_as_it_was(string text)
    {
        var path = Put(text);

        var (root, unreadable) = AccountFileNodes.Read(path);

        Assert.Empty(root);
        Assert.True(unreadable);
        Assert.Empty(AccountFileNodes.Load(path));
        Assert.Equal(text, File.ReadAllText(path));
    }

    [Fact]
    public void A_readable_file_reads_its_root()
    {
        var root = AccountFileNodes.Load(Put("""{"claude":{"a":{}}}"""));

        Assert.NotNull(AccountFileNodes.Child(root, "claude"));
    }

    [Fact]
    public void A_name_is_found_without_case_and_a_child_must_be_an_object()
    {
        var root = (JsonObject)JsonNode.Parse("""{"Claude":{"a":1},"text":"x"}""")!;

        Assert.Equal("Claude", AccountFileNodes.Key(root, "claude"));
        Assert.Null(AccountFileNodes.Key(root, "codex"));
        Assert.NotNull(AccountFileNodes.Child(root, "CLAUDE"));
        Assert.Null(AccountFileNodes.Child(root, "text"));
        Assert.Null(AccountFileNodes.Child(null, "claude"));
    }

    [Fact]
    public void A_child_is_made_when_asked_and_found_when_there()
    {
        var root = new JsonObject();

        var made = AccountFileNodes.Made(root, "claude");
        made["x"] = 1;

        Assert.Same(made, AccountFileNodes.Made(root, "CLAUDE"));
        Assert.Single(root);
    }

    [Fact]
    public void A_moment_reads_only_as_ISO_8601_writes_it()
    {
        var entry = (JsonObject)JsonNode.Parse("""{"a":"2026-10-03T08:00:00Z","b":"Oct 3","c":5}""")!;

        Assert.Equal(new DateTimeOffset(2026, 10, 3, 8, 0, 0, TimeSpan.Zero), AccountFileNodes.Moment(entry, "a"));
        Assert.Null(AccountFileNodes.Moment(entry, "b"));
        Assert.Null(AccountFileNodes.Moment(entry, "c"));
        Assert.Null(AccountFileNodes.Moment(entry, "missing"));
    }
}
