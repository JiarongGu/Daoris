using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// This machine's heavy gates as <c>driver.json</c> keeps them (FOUNDATION1 mod-iii): the settings' own reading and writing, which the
/// mapping answers through, so the key is read and kept alike wherever a gate list is named.
/// </summary>
public sealed class HeavyKeyTests
{
    [Fact]
    public void The_mapping_answers_through_the_key_for_its_key_and_its_judgements()
    {
        Assert.Equal(HeavyKey.Key, HeavyMapping.Key);
        Assert.True(HeavyKey.UnderRoot("web/src"));
        Assert.False(HeavyKey.UnderRoot("../web"));
        Assert.Equal(HeavyKey.UnderRoot("c:/web"), HeavyMapping.UnderRoot("c:/web"));
        var nameless = new[] { new HeavyGate("", "npm test", null, true) };
        Assert.Equal(HeavyKey.Problem(nameless), HeavyMapping.Problem(nameless));
        Assert.NotNull(HeavyKey.Problem(nameless));
    }

    [Fact]
    public void A_list_is_read_trimmed_where_every_gate_reads_and_kept_as_written()
    {
        const string text = """{"heavyGates":{"web":[{"name":"test","run":"npm test","cwd":"web"},{"name":"e2e","run":"npm run e2e"}],"bad":[{"name":"x"}],"twice":[{"name":"a","run":"b"},{"name":"A","run":"c"}]}}""";
        using var document = JsonDocument.Parse(text);

        var map = HeavyKey.Map(document.RootElement);

        Assert.Equal(["web"], map.Keys);
        Assert.Equal(["test", "e2e"], map["web"].Select(gate => gate.Name));
        Assert.All(map["web"], gate => Assert.True(gate.Heavy));
        Assert.Equal("web", map["web"][0].WorkingDirectory);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            HeavyKey.WriteMap(writer, map);
            writer.WriteEndObject();
        }

        Assert.Equal("""{"heavyGates":{"web":[{"name":"test","run":"npm test","cwd":"web"},{"name":"e2e","run":"npm run e2e"}]}}""",
            System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }
}
