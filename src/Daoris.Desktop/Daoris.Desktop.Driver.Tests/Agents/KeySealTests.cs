using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A key is kept encrypted for the Windows user it was typed by: <c>keys.json</c> holds no key a reader of the file could use,
/// and no other file of the home does either.
/// </summary>
public sealed class KeySealTests : IDisposable
{
    private const string Key = "sk-seal-test-0000-wxyz";

    private readonly string _home = Path.Combine(
        TestHomes.Root, "daoris-key-seal-" + Guid.NewGuid().ToString("N")[..8]);

    private string KeysFile => DaorisHome.Entry(_home, HomeEntry.Keys);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public void A_key_is_encrypted_at_rest_and_read_back_whole()
    {
        if (!OperatingSystem.IsWindows()) return;
        var account = HarnessKeys.Add(_home, "fake", Key);

        var text = File.ReadAllText(KeysFile);
        Assert.DoesNotContain(Key, text);
        Assert.DoesNotContain("wxyz", text);
        Assert.Contains(KeySeal.Prefix, text);
        Assert.Equal(Key, HarnessKeys.Of(_home, "fake", account));
    }

    [Fact]
    public void A_key_kept_before_the_seal_is_read_and_sealed_by_the_next_write()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(_home);
        File.WriteAllText(KeysFile, """{"fake":{"old":"sk-legacy-1111"}}""");
        Assert.Equal("sk-legacy-1111", HarnessKeys.Of(_home, "fake", "old"));

        HarnessKeys.Add(_home, "fake", Key);

        var text = File.ReadAllText(KeysFile);
        Assert.DoesNotContain("sk-legacy-1111", text);
        Assert.DoesNotContain(Key, text);
        Assert.Equal("sk-legacy-1111", HarnessKeys.Of(_home, "fake", "old"));
    }

    [Fact]
    public void A_sealed_value_this_user_did_not_seal_opens_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(_home);
        File.WriteAllText(KeysFile, "{\"fake\":{\"theirs\":\"" + KeySeal.Prefix + "AAAAAAAA\"}}");

        Assert.Null(HarnessKeys.Of(_home, "fake", "theirs"));
    }

    /// <summary>
    /// The census: after a key is added and a start's selection and a status question have carried it, no file of the home holds it.
    /// </summary>
    [Fact]
    public async Task No_file_of_the_home_holds_the_key_after_it_has_been_added_selected_and_asked()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(_home);
        var script = Path.Combine(_home, "keyed.mjs");
        File.WriteAllText(script, "if (process.argv[2] === '--version') { console.log('keyed 1.0'); process.exit(0); }\nconsole.log(process.env.FAKE_KEY ? 'logged-in' : 'logged-out');\n");
        var toolchain = new TestAgent(
            ["node", script], ["--version"], ProfileVariable: "FAKE_HOME", LoginCheck: new LoginQuestion(["auth"], "logged-in", "logged-out"),
            KeyVariable: "FAKE_KEY");
        var roster = new HarnessRoster(TestDoors.Registry([TestDoors.Door("fake", toolchain)]), DaorisHome.Entry(_home, HomeEntry.Harnesses));
        var account = HarnessKeys.Add(_home, "fake", Key);
        new HarnessSettings().WithDefault("fake", account).Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));

        var selection = await roster.SelectAsync("fake", DriverConfig.Empty with { Actors = StaffedRoles.On("fake") }, null, null);
        Assert.True(selection.Allowed, selection.Refusal);

        foreach (var file in Directory.EnumerateFiles(_home, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith("keyed.mjs", StringComparison.Ordinal)) continue;
            Assert.DoesNotContain(Key, File.ReadAllText(file));
        }
    }
}
