using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A model family's cool-off over the bridge (`AccountsRoutes.cs`): the account's cooling names its family, which the
/// Agents place words, and the whole account's names none. A file read under the test's home; nothing is probed.
/// </summary>
public sealed class AccountsModelCoolingTests : DriverModuleBridge
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task An_account_s_cooling_names_its_model_family_and_the_whole_account_s_none()
    {
        foreach (var name in new[] { "account-1", "account-2" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "claude-code", name));
        var until = Now.AddHours(3);
        AccountCooling.Cool(Home, new CoolingEntry("claude-code", "account-1", until, true, "weekly", Now, null), Now);
        AccountCooling.Cool(Home, new CoolingEntry("claude-code", "account-2", until, true, "weekly", Now, null, Model: "opus"), Now);

        var claude = (await AnswerAsync(Module(), "ACCOUNTS")).GetProperty("agents").EnumerateArray()
            .Single(each => each.GetProperty("agent").GetString() == "claude-code");

        Assert.Equal(JsonValueKind.Null, Cooling(claude, "account-1").GetProperty("model").ValueKind);
        Assert.Equal("opus", Cooling(claude, "account-2").GetProperty("model").GetString());
    }

    private static JsonElement Cooling(JsonElement agent, string name) =>
        agent.GetProperty("accounts").EnumerateArray().Single(each => each.GetProperty("name").GetString() == name).GetProperty("cooling");
}
