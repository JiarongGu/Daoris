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

    private DriverModule WiredModule(WiringSizes sizes) => new(Bus, Loop())
    {
        WiringWorlds = world => world with
        {
            Account = name => name == "DAORIS_SERVICE_KEY" ? Secret + "-account" : null,
            Machine = _ => null,
            Sizes = sizes,
        },
    };

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
