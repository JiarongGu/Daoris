using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEYREPLACE1a: a key its provider refused is replaced in the same account (D125's ACCTUX1 note). The account keeps its
/// id, so its name, lists and defaults stand; what marked it refused, its last reading and its cool-off, is cleared. The
/// CLI's <c>replaceKeyAccount</c> is the twin, over the same <c>keys.json</c>.
/// </summary>
public sealed class KeyReplaceTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-key-replace-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public void A_new_key_replaces_an_accounts_key_and_clears_what_marked_it_refused_and_leaves_another_alone()
    {
        var first = HarnessKeys.Add(_home, "claude-code", "sk-first-1111");
        var second = HarnessKeys.Add(_home, "claude-code", "sk-second-2222");
        var now = DateTimeOffset.UtcNow;
        AccountReads.Keep(_home, "claude-code", first, LoginState.Out, now);
        AccountCooling.Cool(_home, new CoolingEntry("claude-code", first, now.AddDays(2), true, null, now, null), now);

        var made = HarnessKeys.Replace(_home, "claude-code", first, " sk-ant-api03-new-abcd \n");

        Assert.Equal(first, made);
        Assert.Equal("sk-ant-api03-new-abcd", HarnessKeys.Of(_home, "claude-code", first));
        Assert.Equal("sk-second-2222", HarnessKeys.Of(_home, "claude-code", second));
        Assert.Equal(2, HarnessSettings.Profiles(_home, "claude-code").Count);
        Assert.False(AccountReads.Of(_home, "claude-code").Accounts.TryGetValue(first, out var read) && read.Login == LoginState.Out);
        Assert.Null(AccountCooling.Of(_home, "claude-code", first, now));
        Assert.DoesNotContain("sk-first-1111", File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.Keys)));
    }

    [Fact]
    public void A_key_is_replaced_only_in_an_account_that_exists_and_is_a_key_with_a_key_that_is_one()
    {
        var kept = HarnessKeys.Add(_home, "claude-code", "sk-kept-1111");
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "claude-code", "signed-in"));

        Assert.Contains("no account", Assert.Throws<DriverException>(() => HarnessKeys.Replace(_home, "claude-code", "nobody", "sk-x-1234")).Message);
        Assert.Contains("not an API key account", Assert.Throws<DriverException>(() => HarnessKeys.Replace(_home, "claude-code", "signed-in", "sk-x-1234")).Message);
        Assert.Contains("not an API key", Assert.Throws<DriverException>(() => HarnessKeys.Replace(_home, "claude-code", kept, "   ")).Message);
        Assert.Contains("not an API key", Assert.Throws<DriverException>(() => HarnessKeys.Replace(_home, "claude-code", kept, "sk a b")).Message);
        Assert.Equal("sk-kept-1111", HarnessKeys.Of(_home, "claude-code", kept));
    }
}
