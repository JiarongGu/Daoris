using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PIPELINE1b (D192 point 4; the pipeline design §3.1): which checks a repository's sessions run under Current, <c>sessionChecks</c>
/// in <c>driver.json</c>, by repository, <c>light</c> or <c>heavy-at-end</c>, absent being today's. Read as a stored shape, held cell for
/// cell to ONE table, <c>fixtures/session-checks.json</c>, which the CLI's <c>driverconfig.test.ts</c> reads too; written only when set,
/// after this machine's heavy gates; and edited through one door (<see cref="SessionChecks.Apply"/>) the terminal's verb and the page's
/// go through, refusing alike. Reads text only.
/// </summary>
public sealed class SessionChecksTests
{
    public static TheoryData<string, string, string> ReadRows()
    {
        var data = new TheoryData<string, string, string>();
        var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "session-checks.json")));
        foreach (var row in document.RootElement.GetProperty("read").EnumerateArray())
        {
            data.Add(row.GetProperty("name").GetString()!, row.GetProperty("sessionChecks").GetRawText(), row.GetProperty("read").GetRawText());
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ReadRows))]
    public void The_rule_reads_as_the_shared_table_says(string name, string value, string read)
    {
        var config = DriverConfig.Parse($$"""{"sessionChecks":{{value}}}""");

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(read), Ruled(config)), $"{name}: read {Ruled(config)?.ToJsonString()}");
        // In the order the file holds them, which DeepEquals does not compare for an object's keys.
        Assert.Equal(JsonNode.Parse(read)!.AsObject().Select(pair => pair.Key), config.SessionChecks.Keys);
    }

    /// <summary>A repository's rule is found in any case, its name trimmed; one with none set runs as today.</summary>
    [Fact]
    public void A_repository_s_rule_is_found_by_its_name_in_any_case_and_none_is_today_s()
    {
        var config = DriverConfig.Parse("""{"sessionChecks":{"Engine":"light"}}""");

        Assert.Equal(SessionChecks.Light, SessionChecks.For(config, " ENGINE "));
        Assert.Null(SessionChecks.For(config, "shop"));
        Assert.Null(SessionChecks.For(DriverConfig.Empty, "engine"));
    }

    /// <summary>Written only when set, after this machine's heavy gates, repositories sorted ordinally, as every map of the file is.</summary>
    [Fact]
    public void The_rule_is_written_only_when_set_after_the_heavy_gates()
    {
        var none = JsonNode.Parse(DriverConfig.Empty.ToJson())!.AsObject();
        Assert.False(none.ContainsKey("sessionChecks"));

        var set = DriverConfig.Empty
            .WithHeavyGates("engine", [new HeavyGate("build", "dotnet build", null, true)])
            .WithSessionChecks("shop", SessionChecks.HeavyAtEnd)
            .WithSessionChecks("engine", SessionChecks.Light);
        var written = set.ToJson().ReplaceLineEndings("\n");

        Assert.Contains(
            """
              "sessionChecks": {
                "engine": "light",
                "shop": "heavy-at-end"
              }
            """.ReplaceLineEndings("\n"),
            written);
        var keys = JsonNode.Parse(written)!.AsObject().Select(pair => pair.Key).ToList();
        Assert.Equal(keys.IndexOf("sessionChecks"), keys.IndexOf("heavyGates") + 1);
    }

    /// <summary>
    /// The door: a choice set in any case is kept in its own spelling, a key keeping the spelling first written; none, or a clear, takes the
    /// repository's rule away, which is today's.
    /// </summary>
    [Fact]
    public void A_set_keeps_the_first_spelling_and_a_clear_takes_it_away()
    {
        var first = DriverConfig.Empty.WithSessionChecks(" Engine ", " LIGHT ");
        Assert.Equal("""{"Engine":"light"}""", Ruled(first)!.ToJsonString());

        var again = first.WithSessionChecks("ENGINE", "heavy-at-end");
        Assert.Equal("""{"Engine":"heavy-at-end"}""", Ruled(again)!.ToJsonString());

        Assert.Empty(again.WithSessionChecks("engine", null).SessionChecks);
    }

    public static TheoryData<string?, string?, string> Refusals() => new()
    {
        { null, "light", "a session's checks are set for a repository: name one." },
        { "engine", "heavy", "`heavy` is not a session's checks: they are `light` (the light gates and the checks a change reaches, the heavy gates left to the verification) or `heavy-at-end` (the heavy gates once, at a session's end); none is today's." },
        { "engine", " ", "` ` is not a session's checks: they are `light` (the light gates and the checks a change reaches, the heavy gates left to the verification) or `heavy-at-end` (the heavy gates once, at a session's end); none is today's." },
    };

    /// <summary>What the door refuses it refuses in one sentence, and nothing changes.</summary>
    [Theory]
    [MemberData(nameof(Refusals))]
    public void The_door_refuses_in_its_words(string? repository, string? checks, string refusal)
    {
        var before = DriverConfig.Empty.WithSessionChecks("engine", SessionChecks.Light);

        var refused = Assert.Throws<DriverException>(() => before.WithSessionChecks(repository, checks));

        Assert.Equal(refusal, refused.Message);
        Assert.Equal(SessionChecks.Light, SessionChecks.For(before, "engine"));
    }

    /// <summary>A file holding the rule is read and written with no edit as the same bytes, a value this build does not read kept.</summary>
    [Fact]
    public void A_file_holding_the_rule_reads_and_writes_back_the_same_bytes()
    {
        const string Text = """
            {
              "sessionChecks": {
                "engine": "light",
                "shop": "a newer build's choice"
              }
            }

            """;

        Assert.Equal(Text, DriverConfig.Parse(Text).ToJson());
    }

    /// <summary>The config's rule as its writer writes it whole, read from no text; an empty object for none.</summary>
    private static JsonNode? Ruled(DriverConfig config) =>
        JsonNode.Parse((config with { Source = null }).ToJson())!.AsObject()["sessionChecks"]?.DeepClone() ?? new JsonObject();
}
