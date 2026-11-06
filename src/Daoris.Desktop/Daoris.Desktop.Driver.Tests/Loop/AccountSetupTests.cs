using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The set-up presses of Settings → This Daoris (WIRING1e, the this-Daoris design §4): <c>AccountPath</c>, <c>AccountServiceAddress</c>
/// and <c>WiringFolderPaths</c>. Each runs against <see cref="AccountStandIn"/>, so nothing here reads or changes the machine's
/// account, its registry or its <c>PATH</c>; each refusal is data, with a code, and changes nothing.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class AccountSetupTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-setup-tests", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A scratch folder something still holds is left for the OS to clean.
        }
    }

    /// <summary>An install's shape (D93): the marker at its root, the launchers under <c>app/bin</c>, its home <c>data/</c>.</summary>
    private (string Install, string Bin, string Home) Install()
    {
        var install = Path.Combine(_root, "install");
        var bin = Path.Combine(install, "app", "bin");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(install, DaorisHome.InstallMarker), "# Daoris\n");
        var home = Path.Combine(install, DaorisHome.InstallData);
        Directory.CreateDirectory(home);
        return (install, bin, home);
    }

    [Fact]
    public void Putting_Daoris_on_the_path_appends_the_launchers_folder_to_the_account_s_PATH_and_keeps_the_rest_as_written()
    {
        var (install, bin, _) = Install();
        var account = new AccountStandIn().With("PATH", @"%USERPROFILE%\tools;C:\other");

        var result = AccountPath.Set(install, account, on: true);

        Assert.True(result.Done);
        Assert.True(result.Changed);
        var write = Assert.Single(account.Writes);
        Assert.Equal("PATH", write.Name);
        Assert.Equal(@$"%USERPROFILE%\tools;C:\other;{bin}", write.Value);
    }

    [Fact]
    public void Putting_it_on_twice_changes_nothing_the_second_time()
    {
        var (install, _, _) = Install();
        var account = new AccountStandIn();

        Assert.True(AccountPath.Set(install, account, on: true).Changed);
        var again = AccountPath.Set(install, account, on: true);

        Assert.True(again.Done);
        Assert.False(again.Changed);
        Assert.Single(account.Writes);
    }

    [Fact]
    public void Taking_it_off_removes_exactly_the_entry_an_on_would_add_and_nothing_else()
    {
        var (install, bin, _) = Install();
        var account = new AccountStandIn().With("PATH", @$"%USERPROFILE%\tools;""{bin}"";C:\other");

        var result = AccountPath.Set(install, account, on: false);

        Assert.True(result.Changed);
        Assert.Equal(@"%USERPROFILE%\tools;C:\other", Assert.Single(account.Writes).Value);
    }

    [Fact]
    public void Taking_it_off_when_it_is_not_there_changes_nothing()
    {
        var (install, _, _) = Install();
        var account = new AccountStandIn().With("PATH", @"C:\other");

        var result = AccountPath.Set(install, account, on: false);

        Assert.True(result.Done);
        Assert.False(result.Changed);
        Assert.Empty(account.Writes);
    }

    [Fact]
    public void Taking_off_the_last_entry_removes_the_variable_rather_than_storing_an_empty_one()
    {
        var (install, bin, _) = Install();
        var account = new AccountStandIn().With("PATH", bin);

        AccountPath.Set(install, account, on: false);

        Assert.Equal(("PATH", (string?)null), Assert.Single(account.Writes));
    }

    [Fact]
    public void A_development_run_with_no_launchers_is_refused_as_PATH_NOT_INSTALL_and_nothing_is_written()
    {
        var account = new AccountStandIn();

        Assert.Equal("PATH_NOT_INSTALL", AccountPath.Set(null, account, on: true).Why);
        // An install whose launchers folder is not there is no install to put on a PATH either.
        Directory.CreateDirectory(Path.Combine(_root, "bare"));
        Assert.Equal("PATH_NOT_INSTALL", AccountPath.Set(Path.Combine(_root, "bare"), account, on: true).Why);
        Assert.Empty(account.Writes);
    }

    [Fact]
    public void A_machine_with_no_account_variables_is_refused_as_PATH_NOT_WINDOWS()
    {
        var (install, _, _) = Install();

        var result = AccountPath.Set(install, null, on: true);

        Assert.False(result.Done);
        Assert.Equal("PATH_NOT_WINDOWS", result.Why);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void A_PATH_that_cannot_be_read_is_never_written_over_and_a_write_that_fails_says_so()
    {
        var (install, _, _) = Install();
        var unreadable = new AccountStandIn { Unreadable = true };

        Assert.Equal("PATH_UNREADABLE", AccountPath.Set(install, unreadable, on: true).Why);
        Assert.Empty(unreadable.Writes);

        var unwritable = new AccountStandIn { Unwritable = true };
        var failed = AccountPath.Set(install, unwritable, on: true);
        Assert.False(failed.Done);
        Assert.Equal("PATH_NOT_WRITTEN", failed.Why);
        Assert.False(failed.Changed);
    }

    [Theory]
    [InlineData("http://127.0.0.1:5198", "http://127.0.0.1:5198")]
    [InlineData("  http://localhost:5198/  ", "http://localhost:5198")]
    [InlineData("http://[::1]:5198", "http://[::1]:5198")]
    public void The_service_s_address_is_set_as_the_account_s_variable_for_an_address_of_this_machine(string asked, string stored)
    {
        var account = new AccountStandIn();

        var result = AccountServiceAddress.Set(account, asked);

        Assert.True(result.Changed);
        Assert.Equal(("DAORIS_SERVICE_URL", (string?)stored), Assert.Single(account.Writes));
    }

    [Fact]
    public void A_null_address_clears_the_variable_for_the_default_and_an_unset_one_is_not_a_change()
    {
        var set = new AccountStandIn().With("DAORIS_SERVICE_URL", "http://127.0.0.1:9");

        Assert.True(AccountServiceAddress.Set(set, null).Changed);
        Assert.Equal(("DAORIS_SERVICE_URL", (string?)null), Assert.Single(set.Writes));

        var unset = new AccountStandIn();
        Assert.False(AccountServiceAddress.Set(unset, null).Changed);
        Assert.Empty(unset.Writes);
    }

    [Theory]
    [InlineData("not an address")]
    [InlineData("ftp://127.0.0.1:21")]
    [InlineData("http://user:secret@127.0.0.1:5198")]
    [InlineData("http://127.0.0.1:5198/?key=x")]
    [InlineData("127.0.0.1:5198")]
    [InlineData("")]
    public void A_malformed_address_is_refused_as_SERVICE_MALFORMED_and_nothing_is_written(string asked)
    {
        var account = new AccountStandIn();

        Assert.Equal("SERVICE_MALFORMED", AccountServiceAddress.Set(account, asked).Why);
        Assert.Empty(account.Writes);
    }

    [Theory]
    [InlineData("http://example.com:5198")]
    [InlineData("https://daoris.example.org")]
    [InlineData("http://192.168.1.20:5198")]
    [InlineData("http://localhost.example.com:5198")]
    public void An_address_that_is_not_this_machine_s_is_refused_as_SERVICE_NOT_LOOPBACK_since_the_local_service_is_sent_this_machine_s_paths(string asked)
    {
        var account = new AccountStandIn();

        Assert.Equal("SERVICE_NOT_LOOPBACK", AccountServiceAddress.Set(account, asked).Why);
        Assert.Empty(account.Writes);
    }

    [Fact]
    public void An_account_that_cannot_be_read_or_written_or_does_not_exist_refuses_the_address()
    {
        Assert.Equal("SERVICE_NOT_WINDOWS", AccountServiceAddress.Set(null, "http://127.0.0.1:1").Why);
        Assert.Equal("SERVICE_UNREADABLE", AccountServiceAddress.Set(new AccountStandIn { Unreadable = true }, "http://127.0.0.1:1").Why);
        Assert.Equal("SERVICE_NOT_WRITTEN", AccountServiceAddress.Set(new AccountStandIn { Unwritable = true }, "http://127.0.0.1:1").Why);
    }

    [Fact]
    public void A_folder_id_resolves_to_the_install_s_folder_and_a_file_id_to_the_folder_it_is_in()
    {
        var (install, _, home) = Install();
        var world = new WiringWorld { Home = home, BaseDirectory = install, Process = _ => null };

        Assert.Equal(home, WiringFolderPaths.Resolve(world, "home").Folder);
        Assert.Equal(Path.Combine(install, "logs"), WiringFolderPaths.Resolve(world, "logs").Folder);
        var driver = WiringFolderPaths.Resolve(world, HomeEntry.DriverConfig.Name);
        Assert.Null(driver.Why);
        Assert.Equal(Path.GetDirectoryName(DaorisHome.Entry(home, HomeEntry.DriverConfig)), driver.Folder);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nothing-by-this-name")]
    [InlineData("../../Windows")]
    [InlineData(@"C:\Windows")]
    public void An_id_the_read_never_gave_is_FOLDER_UNKNOWN_and_a_path_is_never_taken_for_one(string? id)
    {
        var (install, _, home) = Install();
        var world = new WiringWorld { Home = home, BaseDirectory = install, Process = _ => null };

        var resolved = WiringFolderPaths.Resolve(world, id);

        Assert.Null(resolved.Folder);
        Assert.Equal("FOLDER_UNKNOWN", resolved.Why);
    }
}
