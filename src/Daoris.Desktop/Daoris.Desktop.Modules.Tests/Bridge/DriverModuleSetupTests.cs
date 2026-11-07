using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Setting This Daoris up from the page (WIRING1e, the this-Daoris design §4): <c>SYSTEM_OPEN_FOLDER</c>, <c>SYSTEM_PATH</c> and
/// <c>SYSTEM_SERVICE_ADDRESS</c> as the window answers them. Every press runs against <see cref="AccountStandIn"/> and a recording
/// launcher, so nothing here reads or changes the machine's account, its <c>PATH</c> or its file manager; each refusal is data, and the
/// page never names a path.
/// </summary>
public sealed class DriverModuleSetupTests : DriverModuleBridge
{
    private readonly List<string> _opened = [];

    private void Open(string folder)
    {
        lock (_opened) _opened.Add(folder);
    }

    /// <summary>This test's home as an installed Daoris: the marker at its root and the launchers' folder, so <c>app/bin</c> is there.</summary>
    private string Installed()
    {
        File.WriteAllText(Path.Combine(Home, DaorisHome.InstallMarker), "# Daoris\n");
        var bin = Path.Combine(Home, "app", "bin");
        Directory.CreateDirectory(bin);
        return bin;
    }

    private DriverModule WithAccount(AccountStandIn? account, OpenFolder? open = null) => new(Bus, Loop(), open ?? Open, null, account);

    [Fact]
    public async Task Open_opens_the_folder_the_id_names_and_the_page_never_names_a_path()
    {
        var module = WithAccount(null);
        var logs = DaorisHome.Entry(Home, HomeEntry.Logs);
        Directory.CreateDirectory(logs);

        var opened = await AnswerAsync(module, "SYSTEM_OPEN_FOLDER", new { folder = "logs" });

        Assert.True(opened.GetProperty("done").GetBoolean());
        Assert.Equal(logs, Assert.Single(_opened));
    }

    [Theory]
    [InlineData("nothing-by-this-name")]
    [InlineData(@"C:\Windows")]
    [InlineData("../../elsewhere")]
    public async Task An_id_the_read_never_gave_is_refused_as_FOLDER_UNKNOWN_and_nothing_opens(string folder)
    {
        var refused = await AnswerAsync(WithAccount(null), "SYSTEM_OPEN_FOLDER", new { folder });

        Assert.False(refused.GetProperty("done").GetBoolean());
        Assert.Equal("FOLDER_UNKNOWN", refused.GetProperty("why").GetString());
        Assert.False(string.IsNullOrWhiteSpace(refused.GetProperty("message").GetString()));
        Assert.Empty(_opened);
    }

    [Fact]
    public async Task A_folder_that_is_not_there_or_a_file_manager_that_fails_is_refused_as_FOLDER_NOT_OPENED()
    {
        var absent = await AnswerAsync(WithAccount(null), "SYSTEM_OPEN_FOLDER", new { folder = "cache" });
        Assert.Equal("FOLDER_NOT_OPENED", absent.GetProperty("why").GetString());
        Assert.Empty(_opened);

        Directory.CreateDirectory(Path.Combine(Home, "cache"));
        var failing = await AnswerAsync(
            WithAccount(null, _ => throw new InvalidOperationException("no file manager")), "SYSTEM_OPEN_FOLDER", new { folder = "cache" });
        Assert.Equal("FOLDER_NOT_OPENED", failing.GetProperty("why").GetString());
    }

    [Fact]
    public async Task Put_on_my_PATH_adds_the_launchers_folder_to_the_stand_in_account_and_declares_the_wiring()
    {
        var bin = Installed();
        var account = new AccountStandIn().With("PATH", @"C:\tools");

        var done = await AnswerAsync(WithAccount(account), "SYSTEM_PATH", new { on = true });

        Assert.True(done.GetProperty("done").GetBoolean());
        Assert.True(done.GetProperty("changed").GetBoolean());
        Assert.Equal(("PATH", (string?)@$"C:\tools;{bin}"), Assert.Single(account.Writes));
        // That it tells the wiring is declared on the route, which StatePublisherTests holds.
        Assert.Equal("wiring", string.Join(",", StatePublisher.Names(DriverModule.Declared(new Shenora.Core.Ipc.IpcRequest { Id = "x", Module = DriverModule.Name, Type = "SYSTEM_PATH" })!.Scopes)));
    }

    [Fact]
    public async Task Take_off_my_PATH_removes_exactly_that_entry()
    {
        var bin = Installed();
        var account = new AccountStandIn().With("PATH", @$"C:\a;{bin};C:\b");

        var done = await AnswerAsync(WithAccount(account), "SYSTEM_PATH", new { on = false });

        Assert.True(done.GetProperty("changed").GetBoolean());
        Assert.Equal(@"C:\a;C:\b", Assert.Single(account.Writes).Value);
    }

    [Fact]
    public async Task The_PATH_press_refuses_as_data_a_development_run_a_machine_with_no_account_and_an_unreadable_PATH()
    {
        var account = new AccountStandIn();
        var development = await AnswerAsync(WithAccount(account), "SYSTEM_PATH", new { on = true });
        Assert.Equal("PATH_NOT_INSTALL", development.GetProperty("why").GetString());

        Installed();
        var none = await AnswerAsync(WithAccount(null), "SYSTEM_PATH", new { on = true });
        Assert.Equal("PATH_NOT_WINDOWS", none.GetProperty("why").GetString());

        var unreadable = new AccountStandIn { Unreadable = true };
        var unread = await AnswerAsync(WithAccount(unreadable), "SYSTEM_PATH", new { on = true });
        Assert.Equal("PATH_UNREADABLE", unread.GetProperty("why").GetString());
        Assert.Empty(account.Writes);
        Assert.Empty(unreadable.Writes);
    }

    [Fact]
    public async Task A_PATH_press_that_says_neither_on_nor_off_is_refused_before_anything_is_read()
    {
        Installed();
        var account = new AccountStandIn();

        var answer = await AskAsync(WithAccount(account), "SYSTEM_PATH", new { });

        Assert.False(answer.Success);
        Assert.Empty(account.Writes);
    }

    [Fact]
    public async Task Change_the_service_s_address_sets_and_clears_the_stand_in_account_s_variable()
    {
        var account = new AccountStandIn();
        var module = WithAccount(account);

        var set = await AnswerAsync(module, "SYSTEM_SERVICE_ADDRESS", new { url = "http://127.0.0.1:6001" });
        var cleared = await AnswerAsync(module, "SYSTEM_SERVICE_ADDRESS", new { url = (string?)null });

        Assert.True(set.GetProperty("changed").GetBoolean());
        Assert.True(cleared.GetProperty("changed").GetBoolean());
        Assert.Equal(
            new (string, string?)[] { ("DAORIS_SERVICE_URL", "http://127.0.0.1:6001"), ("DAORIS_SERVICE_URL", null) },
            account.Writes);
    }

    [Theory]
    [InlineData("http://example.com:6001", "SERVICE_NOT_LOOPBACK")]
    [InlineData("six-thousand", "SERVICE_MALFORMED")]
    public async Task An_address_refused_is_data_and_writes_nothing(string url, string why)
    {
        var account = new AccountStandIn();

        var refused = await AnswerAsync(WithAccount(account), "SYSTEM_SERVICE_ADDRESS", new { url });

        Assert.False(refused.GetProperty("done").GetBoolean());
        Assert.Equal(why, refused.GetProperty("why").GetString());
        Assert.Empty(account.Writes);
    }

    [Fact]
    public void The_three_presses_are_a_window_s_alone_so_the_pipe_refuses_any_agent_that_asks()
    {
        foreach (var route in new[] { "SYSTEM_OPEN_FOLDER", "SYSTEM_PATH", "SYSTEM_SERVICE_ADDRESS" })
        {
            Assert.Equal(PipeClass.Window, PipeCensus.Daoris.Of(DriverModule.Name, route));
        }
    }
}
