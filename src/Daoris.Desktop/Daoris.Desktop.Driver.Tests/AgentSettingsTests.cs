using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// **An account's own model and effort** (AGT6, D98), in the tool's own settings file.
/// </summary>
/// <remarks>
/// <para>The file is Claude Code's, in the account's configuration home: its <c>settings.json</c>, the
/// user tier its ACP adapter reads at <c>CLAUDE_CONFIG_DIR/settings.json</c>. Daoris changes
/// <c>model</c>, <c>effortLevel</c> and an effort per model under <c>modelSettings</c>, and leaves every
/// other key the tool keeps there exactly as it was.</para>
///
/// <para>The driver's half of a TWIN CONTRACT: the CLI's <c>agentsettings.ts</c> reads and writes the same
/// file from <c>daoris agent settings</c>. They share no code, so the same seven rules are asserted on
/// both sides, case for case:</para>
/// <list type="number">
/// <item>The file is the tool's own <c>settings.json</c>, in the account's configuration home.</item>
/// <item>No file is the tool's defaults: nothing set, and reading creates nothing.</item>
/// <item>A file that is not a JSON object is a sentence to a read and a refusal to a write, which leaves
/// it exactly as it was.</item>
/// <item>A write changes only what it names; every other key keeps its place and its value.</item>
/// <item>An effort is low, medium, high or xhigh — <c>max</c> is refused, since the tool keeps it for
/// one session only; a model is one word, and the tool's own aliases are offered first.</item>
/// <item>Clearing removes the key; an entry per model left empty goes, and so does an empty
/// <c>modelSettings</c>.</item>
/// <item>A new file holds exactly what was written, in the tool's formatting: two-space indent, LF, and
/// a final newline.</item>
/// </list>
/// <para>Every case runs in a temporary directory. No account's real file is read or written.</para>
/// </remarks>
public sealed class AgentSettingsTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-agent-settings-" + Guid.NewGuid().ToString("N")[..8]);

    public AgentSettingsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string File_(string text)
    {
        var path = Path.Combine(_home, AgentSettings.FileName);
        File.WriteAllText(path, text);
        return path;
    }

    private string Absent => Path.Combine(_home, "account", AgentSettings.FileName);

    private static JsonObject Parsed(string path) => (JsonObject)JsonNode.Parse(File.ReadAllText(path))!;

    // ——— Rule 1: the file, and which tools declare one.

    [Fact]
    public void The_file_is_the_tools_own_settings_json_declared_by_Claude_Code_alone()
    {
        Assert.Equal("settings.json", AgentSettings.FileName);
        var adapters = AdapterSet.Built();
        var declaring = adapters.Names
            .Where(name => adapters.Resolve(name).Toolchain is { SettingsFile: { Length: > 0 } })
            .Order(StringComparer.Ordinal)
            .ToArray();

        // The ACP door runs as Claude Code's accounts (AGT7), so its settings are its owner's file.
        Assert.Equal(["claude-code"], declaring);
        Assert.Equal(AgentSettings.FileName, adapters.Resolve("claude-code").Toolchain!.SettingsFile);
    }

    [Fact]
    public void The_choices_offered_are_the_tools_own_aliases_and_the_efforts_its_settings_keep()
    {
        // The Agent SDK's alias list at 0.3.284, which `claude-agent-acp` 0.84.0 carries, with `default`.
        Assert.Equal(
            ["default", "sonnet", "opus", "haiku", "fable", "best", "sonnet[1m]", "opus[1m]", "fable[1m]", "opusplan"],
            AgentSettings.Models);
        // The settings schema's `effortLevel`: `max` is session-only and never written to a file.
        Assert.Equal(["low", "medium", "high", "xhigh"], AgentSettings.Efforts);
    }

    // ——— Rule 2: no file.

    [Fact]
    public void No_file_is_the_tools_defaults_and_reading_it_creates_nothing()
    {
        var read = AgentSettings.Read(Absent);

        Assert.Null(read.Model);
        Assert.Null(read.Effort);
        Assert.Empty(read.PerModel);
        Assert.Null(read.Problem);
        Assert.False(File.Exists(Absent));
    }

    [Fact]
    public void A_file_is_read_for_its_model_its_effort_and_each_models_own_effort()
    {
        var path = File_("""
            {"model":"opus[1m]","effortLevel":"high",
             "modelSettings":{"claude-opus-5":{"effortLevel":"xhigh"},"claude-haiku-4-5":{"maxEffortLevel":"low"}}}
            """);

        var read = AgentSettings.Read(path);

        Assert.Equal("opus[1m]", read.Model);
        Assert.Equal("high", read.Effort);
        Assert.Equal([new ModelEffort("claude-opus-5", "xhigh")], read.PerModel);
        Assert.Null(read.Problem);
    }

    // ——— Rule 3: a file that is not a JSON object.

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""["an", "array"]""")]
    [InlineData("\"a string\"")]
    public void A_file_that_is_not_a_JSON_object_is_a_sentence_to_a_read_and_a_write_leaves_it_as_it_was(string text)
    {
        var path = File_(text);

        var read = AgentSettings.Read(path);
        Assert.Null(read.Model);
        Assert.Matches("could not be read|not a JSON object", read.Problem ?? "");

        var refused = Assert.Throws<DriverException>(() => AgentSettings.Write(path, model: new("opus")));
        Assert.Contains("nothing was written", refused.Message);
        Assert.Equal(text, File.ReadAllText(path));
    }

    // ——— Rule 4: a write changes only what it names.

    [Fact]
    public void A_write_changes_the_two_keys_and_keeps_every_other_key_in_its_place()
    {
        var path = File_("""
            {
              "$schema": "https://json.schemastore.org/claude-code-settings.json",
              "model": "sonnet",
              "permissions": { "allow": ["Bash(ls)"], "deny": [] },
              "env": { "FOO": "1" },
              "alwaysThinkingEnabled": true
            }

            """);

        var after = AgentSettings.Write(path, model: new("opus"), effort: new("high"));

        Assert.Equal("opus", after.Model);
        Assert.Equal("high", after.Effort);
        var written = Parsed(path);
        Assert.Equal(
            ["$schema", "model", "permissions", "env", "alwaysThinkingEnabled", "effortLevel"],
            written.Select(entry => entry.Key));
        Assert.Equal("""{"allow":["Bash(ls)"],"deny":[]}""", written["permissions"]!.ToJsonString());
        Assert.Equal("""{"FOO":"1"}""", written["env"]!.ToJsonString());
        Assert.True(written["alwaysThinkingEnabled"]!.GetValue<bool>());
    }

    [Fact]
    public void An_effort_per_model_is_written_under_that_model_beside_what_the_entry_already_holds()
    {
        var path = File_("""{"modelSettings":{"claude-opus-5":{"maxEffortLevel":"max"}}}""");

        var after = AgentSettings.Write(path, perModel: new Dictionary<string, AgentSettingEdit>
        {
            ["claude-opus-5"] = new("medium"),
            ["claude-sonnet-5"] = new("low"),
        });

        Assert.Equal(
            [new ModelEffort("claude-opus-5", "medium"), new ModelEffort("claude-sonnet-5", "low")],
            after.PerModel);
        Assert.Equal(
            """{"claude-opus-5":{"maxEffortLevel":"max","effortLevel":"medium"},"claude-sonnet-5":{"effortLevel":"low"}}""",
            Parsed(path)["modelSettings"]!.ToJsonString());
    }

    // ——— Rule 5: what may be written.

    [Fact]
    public void An_effort_is_one_of_the_four_the_settings_keep_and_max_is_refused_as_session_only()
    {
        var path = File_("{\"model\":\"opus\"}\n");

        var max = Assert.Throws<DriverException>(() => AgentSettings.Write(path, effort: new("max")));
        Assert.Contains("`max`", max.Message);
        Assert.Contains("one session", max.Message);
        var other = Assert.Throws<DriverException>(() => AgentSettings.Write(path, effort: new("ultra")));
        Assert.Contains("low, medium, high, xhigh", other.Message);
        Assert.Equal("{\"model\":\"opus\"}\n", File.ReadAllText(path));

        foreach (var effort in AgentSettings.Efforts)
        {
            Assert.Equal(effort, AgentSettings.Write(path, effort: new(effort)).Effort);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("claude opus")]
    public void A_model_is_one_word_never_blank_and_never_with_a_space(string model)
    {
        var refused = Assert.Throws<DriverException>(() => AgentSettings.Write(Absent, model: new(model)));

        Assert.Contains("model", refused.Message);
        Assert.False(File.Exists(Absent));
    }

    [Theory]
    [InlineData("opus[1m]")]
    [InlineData("claude-opus-5-5")]
    [InlineData("default")]
    public void A_model_may_be_an_alias_or_a_full_id(string model) =>
        Assert.Equal(model, AgentSettings.Write(Absent, model: new(model)).Model);

    // ——— Rule 6: clearing.

    [Fact]
    public void Clearing_removes_the_key_an_emptied_entry_and_an_emptied_modelSettings()
    {
        var path = File_("""
            {"model":"opus","effortLevel":"high","theme":"dark",
             "modelSettings":{"claude-opus-5":{"effortLevel":"xhigh"},"claude-sonnet-5":{"effortLevel":"low","maxEffortLevel":"high"}}}
            """);

        AgentSettings.Write(path, model: new(null), effort: new(null),
            perModel: new Dictionary<string, AgentSettingEdit> { ["claude-opus-5"] = new(null) });
        Assert.Equal(
            """{"theme":"dark","modelSettings":{"claude-sonnet-5":{"effortLevel":"low","maxEffortLevel":"high"}}}""",
            Parsed(path).ToJsonString());

        AgentSettings.Write(path, perModel: new Dictionary<string, AgentSettingEdit> { ["claude-sonnet-5"] = new(null) });
        Assert.Equal(
            """{"theme":"dark","modelSettings":{"claude-sonnet-5":{"maxEffortLevel":"high"}}}""",
            Parsed(path).ToJsonString());

        var bare = Path.Combine(_home, "bare.json");
        File.WriteAllText(bare, """{"modelSettings":{"claude-opus-5":{"effortLevel":"xhigh"}}}""");
        AgentSettings.Write(bare, perModel: new Dictionary<string, AgentSettingEdit> { ["claude-opus-5"] = new(null) });
        Assert.Equal("{}", Parsed(bare).ToJsonString());
    }

    // ——— Rule 7: a new file.

    [Fact]
    public void A_new_file_holds_exactly_what_was_written_in_the_tools_formatting()
    {
        AgentSettings.Write(Absent, model: new("sonnet"));

        Assert.Equal("{\n  \"model\": \"sonnet\"\n}\n", File.ReadAllText(Absent));
    }

    /// <summary>
    /// The read's shape is what the Settings screen renders, so a key the tool keeps as something other
    /// than text is simply not set — the tool ignores a model that is not a string, and so does this.
    /// </summary>
    [Fact]
    public void A_model_that_is_not_text_is_not_set()
    {
        var read = AgentSettings.Read(File_("""{"model":42,"effortLevel":["high"]}"""));

        Assert.Null(read.Model);
        Assert.Null(read.Effort);
        Assert.Null(read.Problem);
    }
}
