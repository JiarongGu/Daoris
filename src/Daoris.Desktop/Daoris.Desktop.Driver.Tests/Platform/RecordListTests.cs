using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The small store three of the driver's list files share (DEDUP5): a missing file reads none, an unreadable one reads
/// none and is not changed by reading, and an edit under the gate writes atomically, bounded.
/// </summary>
public sealed class RecordListTests : IDisposable
{
    private sealed record Item(string Name);

    private static readonly object Gate = new();

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "daoris-recordlist-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string PathOf => Path.Combine(_folder, "nested", "items.json");

    private static RecordList<Item> Make(Func<IReadOnlyList<Item>, IReadOnlyList<Item>>? bound = null) => new(
        Gate, "items",
        element => Json.Text(element, "name") is { } name ? new Item(name) : null,
        entries => JsonFile.Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("items");
            foreach (var entry in entries)
            {
                writer.WriteStartObject();
                writer.WriteString("name", entry.Name);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }),
        bound);

    [Fact]
    public void A_missing_file_reads_none()
    {
        Assert.Empty(Make().Read(PathOf));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{"other":[]}""")]
    [InlineData("""{"items":"x"}""")]
    public void An_unreadable_file_reads_none_and_reading_does_not_write_over_it(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf)!);
        File.WriteAllText(PathOf, text);

        Assert.Empty(Make().Read(PathOf));
        Assert.Equal(text, File.ReadAllText(PathOf));
    }

    [Fact]
    public void An_entry_that_lacks_what_makes_it_one_is_dropped()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf)!);
        File.WriteAllText(PathOf, """{"items":[{"name":"a"},{"x":1},5,{"name":"b"}]}""");

        Assert.Equal(["a", "b"], Make().Read(PathOf).Select(item => item.Name));
    }

    [Fact]
    public void An_edit_makes_the_folder_writes_whole_and_leaves_no_beside_file()
    {
        var list = Make();

        list.Edit(PathOf, all => [.. all, new Item("a")]);
        list.Edit(PathOf, all => [.. all, new Item("b")]);

        Assert.Equal(["a", "b"], list.Read(PathOf).Select(item => item.Name));
        var text = File.ReadAllText(PathOf);
        Assert.EndsWith("}\n", text);
        Assert.DoesNotContain('\r', text);
        Assert.Equal([PathOf], Directory.GetFiles(Path.GetDirectoryName(PathOf)!));
    }

    [Fact]
    public void An_edit_is_bounded_by_the_list_s_own_rule()
    {
        var list = Make(entries => [.. entries.TakeLast(2)]);

        foreach (var name in new[] { "a", "b", "c" }) list.Edit(PathOf, all => [.. all, new Item(name)]);

        Assert.Equal(["b", "c"], list.Read(PathOf).Select(item => item.Name));
    }

    [Fact]
    public void Writers_in_parallel_drop_none_of_each_other()
    {
        var list = Make();

        Parallel.For(0, 20, i => list.Edit(PathOf, all => [.. all, new Item("n" + i)]));

        Assert.Equal(20, list.Read(PathOf).Count);
    }
}
