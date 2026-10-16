using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// This Daoris's read over the bridge (`SystemRoutes.cs`, WIRING1b, the this-Daoris design §6): the driver library's one
/// reader as the window answers it, the window's own facts from its loop and supervisor, a null kept as a field, sizes
/// measured only when asked, and no secret in the answer. The account's and the machine's variables are this test's own, so
/// nothing of the machine running it is read.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class DriverModuleSystemTests : DriverModuleBridge
{
    private const string Secret = "a-service-key-never-shown";

    /// <summary>🔴 A scratch tool home, handed to every world: no test here reads or touches the machine's own.</summary>
    private string ToolHome => Path.Combine(Home, "scratch-person", ".claude");

    private DriverModule WiredModule(WiringSizes sizes) => new(Bus, Loop())
    {
        WiringWorlds = world => world with
        {
            Account = name => name == "DAORIS_SERVICE_KEY" ? Secret + "-account" : null,
            Machine = _ => null,
            Sizes = sizes,
            ToolHome = ToolHome,
            ToolHomeFrom = "variable",
        },
    };

    /// <summary>A project folder in the scratch tool home, named as Claude Code names the folder a session ran in, written long ago.</summary>
    private string Project(string ranIn)
    {
        var folder = Path.Combine(ToolHome, "projects", ToolHomeLeftovers.Flatten(ranIn));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "s.jsonl");
        File.WriteAllBytes(file, new byte[7]);
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(-3));
        Directory.SetLastWriteTimeUtc(folder, DateTime.UtcNow.AddHours(-3));
        return folder;
    }

    [Fact]
    public async Task The_wiring_lists_the_tool_home_s_folders_Daoris_made_and_the_press_removes_only_those()
    {
        var mine = Project(DaorisHome.Entry(Home, HomeEntry.Trees, "main", "repo", "s-1"));
        var theirs = Project(Path.Combine(Path.GetTempPath(), "somebody", "app"));
        var module = WiredModule(new WiringSizes());

        var listed = (await AnswerAsync(module, "SYSTEM_WIRING")).GetProperty("toolHome");
        Assert.Equal(ToolHome, listed.GetProperty("path").GetString());
        Assert.Equal("variable", listed.GetProperty("from").GetString());
        var row = Assert.Single(listed.GetProperty("folders").EnumerateArray());
        Assert.Equal(Path.GetFileName(mine), row.GetProperty("name").GetString());
        // Listed by name alone: no size until a measure is asked, and its why is a field, not an absent one.
        Assert.Equal(JsonValueKind.Null, row.GetProperty("bytes").ValueKind);

        var cleared = await AnswerAsync(module, "SYSTEM_TOOL_CLEAR", new { names = new[] { Path.GetFileName(mine), Path.GetFileName(theirs) } });

        Assert.Equal(Path.GetFileName(mine), Assert.Single(cleared.GetProperty("cleared").EnumerateArray()).GetProperty("name").GetString());
        var kept = Assert.Single(cleared.GetProperty("kept").EnumerateArray());
        Assert.Equal("not-listed", kept.GetProperty("why").GetString());
        Assert.False(Directory.Exists(mine));
        Assert.True(Directory.Exists(theirs));
    }

    [Fact]
    public async Task The_press_without_the_folders_its_list_held_is_refused()
    {
        Assert.NotNull(await RefusalAsync(WiredModule(new WiringSizes()), "SYSTEM_TOOL_CLEAR", new { }));
    }

    [Fact]
    public async Task The_window_answers_its_wiring_with_what_only_it_knows_and_each_unread_fact_as_a_null_field()
    {
        var wiring = await AnswerAsync(WiredModule(new WiringSizes()), "SYSTEM_WIRING");

        Assert.Equal(1, wiring.GetProperty("schema").GetInt32());
        Assert.Equal("window", wiring.GetProperty("reader").GetString());
        Assert.Equal(Home, wiring.GetProperty("folder").GetProperty("path").GetString());
        // This loop's supervisor has neither started nor adopted a host.
        Assert.Equal("none", wiring.GetProperty("service").GetProperty("held").GetString());
        // 🔴 A null survives the bridge as a field with its why, which the page reads (design §6): the bridge leaves a null out
        // of an object it serializes, so the answer is handed as the reader's own JSON.
        var bytes = wiring.GetProperty("folders").EnumerateArray().First(row => row.GetProperty("id").GetString() == "data")
            .GetProperty("bytes");
        Assert.Equal(JsonValueKind.Null, bytes.ValueKind);
        Assert.False(wiring.GetProperty("measuring").GetBoolean());
    }

    [Fact]
    public async Task A_measure_asked_starts_in_the_background_and_the_next_read_has_the_sizes()
    {
        File.WriteAllText(DriverConfigPath, "{}");
        var sizes = new WiringSizes();
        var module = WiredModule(sizes);

        var asked = await AnswerAsync(module, "SYSTEM_WIRING", new { measure = true });
        await sizes.Finished(Home);
        var read = await AnswerAsync(module, "SYSTEM_WIRING");

        Assert.True(asked.GetProperty("measuring").GetBoolean() || asked.GetProperty("measured").ValueKind != JsonValueKind.Null);
        var data = read.GetProperty("folders").EnumerateArray().First(row => row.GetProperty("id").GetString() == "data");
        Assert.Equal(2, data.GetProperty("bytes").GetInt64());
    }

    [Fact]
    public async Task No_key_is_in_the_windows_answer()
    {
        Redirect("DAORIS_SERVICE_KEY", Secret);

        var response = await AskAsync(WiredModule(new WiringSizes()), "SYSTEM_WIRING");
        var text = JsonSerializer.Serialize(response);

        Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
        var key = (await AnswerAsync(WiredModule(new WiringSizes()), "SYSTEM_WIRING")).GetProperty("variables").EnumerateArray()
            .Single(row => row.GetProperty("name").GetString() == "DAORIS_SERVICE_KEY");
        Assert.True(key.GetProperty("set").GetBoolean());
        Assert.Equal(JsonValueKind.Null, key.GetProperty("value").ValueKind);
        Assert.True(key.GetProperty("accountSet").GetBoolean());
    }
}
