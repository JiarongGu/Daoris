using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one way the driver writes a JSON file (DEDUP3a): indented, LF only on every platform, one final newline,
/// BOM-less, Chinese text and angle brackets kept as written, and the write itself atomic.
/// </summary>
public sealed class JsonFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "daoris-jsonfile-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static void Sample(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("name", "道衍 <b>");
        writer.WriteStartArray("list");
        writer.WriteNumberValue(1);
        writer.WriteNumberValue(2);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private const string SampleText = "{\n  \"name\": \"道衍 <b>\",\n  \"list\": [\n    1,\n    2\n  ]\n}\n";

    [Fact]
    public void Serialize_answers_indented_text_with_LF_only_and_one_final_newline()
    {
        var text = JsonFile.Serialize(Sample);

        Assert.Equal(SampleText, text);
        Assert.DoesNotContain('\r', text);
    }

    [Fact]
    public void Serialize_of_a_node_answers_the_same_text_as_the_writer_form()
    {
        var node = JsonNode.Parse("""{"name":"道衍 <b>","list":[1,2]}""")!;

        Assert.Equal(SampleText, JsonFile.Serialize(node));
    }

    [Fact]
    public void Write_makes_the_folder_and_writes_BOM_less_text_leaving_nothing_beside()
    {
        var path = Path.Combine(_folder, "deeper", "file.json");

        JsonFile.Write(path, Sample);

        var bytes = File.ReadAllBytes(path);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal(SampleText, System.Text.Encoding.UTF8.GetString(bytes));
        Assert.Equal(["file.json"], Directory.GetFiles(Path.GetDirectoryName(path)!).Select(Path.GetFileName));
    }

    [Fact]
    public void Write_of_a_node_writes_the_same_bytes_and_replaces_what_was_there()
    {
        var path = Path.Combine(_folder, "node.json");
        Directory.CreateDirectory(_folder);
        File.WriteAllText(path, "old");

        JsonFile.Write(path, JsonNode.Parse("""{"name":"道衍 <b>","list":[1,2]}""")!);

        Assert.Equal(SampleText, File.ReadAllText(path));
        Assert.Equal(["node.json"], Directory.GetFiles(_folder).Select(Path.GetFileName));
    }

    [Fact]
    public void Options_are_one_shared_object()
    {
        Assert.Same(JsonFile.Options, JsonFile.Options);
        Assert.True(JsonFile.Options.WriteIndented);
        Assert.Equal("\n", JsonFile.Options.NewLine);
    }
}
