using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// An account's name and where it runs, over the bridge (ACCT1, ACCT2; D125's ACCT1 and ACCT2 notes): the routes the agent's
/// page will call — <c>profile-rename</c>, <c>profile-join</c>, and a sign-in's <c>join</c> and <c>name</c> — and the
/// roster's and <c>ACCOUNTS</c>' facts for each account: the person's name for it, and the lists and defaults that hold it.
/// </summary>
/// <remarks>
/// The fast half (MOD8): each answer is a file read or written under the test's home, and each refusal comes before any
/// process starts. A sign-in that runs is the Process half's (<c>HarnessProfileTests</c>, <c>DriverModuleAgentsTests</c>).
/// </remarks>
public sealed class DriverModuleAccountNamesTests : DriverModuleBridge
{
    private void Accounts(string agent, params string[] names)
    {
        foreach (var name in names) Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, agent, name));
    }

    private void Wiring(string json) => File.WriteAllText(HarnessSettingsPath, json);

    private static JsonElement Account(JsonElement answer, string agent, string name) =>
        answer.GetProperty("agents").EnumerateArray().Single(each => each.GetProperty("agent").GetString() == agent)
            .GetProperty("accounts").EnumerateArray().Single(each => each.GetProperty("name").GetString() == name);

    /// <summary>
    /// ACCT2: a rename writes the person's name beside the account and nothing else, so the rotation, the defaults and the
    /// workspace's list — all naming its id — keep it; <c>ACCOUNTS</c> answers the name beside the id.
    /// </summary>
    [Fact]
    public async Task A_rename_names_the_account_and_leaves_every_list_and_default_naming_its_id()
    {
        Accounts("claude-code", "account-1", "account-2");
        Wiring("""
            { "defaults": { "claude-code": "account-1" }, "rotation": { "claude-code": ["account-1", "account-2"] },
              "workspaceRotation": { "work": { "claude-code": ["account-2", "account-1"] } } }
            """);
        var wiring = File.ReadAllText(HarnessSettingsPath);

        var answer = await AnswerAsync(Module(), "HARNESS_ACTION",
            new { harness = "claude-code-acp", action = "profile-rename", profile = "account-1", name = "you@work.example" });

        Assert.Equal("you@work.example", answer.GetProperty("name").GetString());
        Assert.Equal(wiring, File.ReadAllText(HarnessSettingsPath));
        Assert.Equal("you@work.example", AccountNames.NameOf(Home, "claude-code", "account-1"));
        var settings = HarnessSettings.Load(HarnessSettingsPath);
        Assert.Equal("account-1", settings.ResolveScope("claude-code", null).Begins);
        Assert.Equal(["account-2", "account-1"], settings.ResolveScope("claude-code", "work").List);

        var accounts = await AnswerAsync(Module(), "ACCOUNTS");
        Assert.Equal("you@work.example", Account(accounts, "claude-code", "account-1").GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, Account(accounts, "claude-code", "account-2").GetProperty("displayName").ValueKind);
    }

    /// <summary>
    /// ACCT2: Daoris's session records name an account by its id, so the sessions running on it are counted for it across a
    /// rename, beside its new name.
    /// </summary>
    [Fact]
    public async Task A_renamed_account_keeps_the_sessions_its_records_name_by_its_id()
    {
        Accounts("claude-code", "account-1");
        await AnswerAsync(Module(), "HARNESS_ACTION",
            new { harness = "claude-code", action = "profile-rename", profile = "account-1", name = "seat" });
        var loop = Loop();
        var running = DriverModule.RunningOn(
            """[{"id":"s1a2b3c4","adapter":"claude-code-acp","profile":"account-1","state":"running"}]""",
            adapter => loop.Harnesses.Toolchain(adapter)?.Owner(adapter) ?? adapter);

        var answer = JsonSerializer.SerializeToElement(DriverModule.AccountsAnswer(loop.Harnesses, running, DateTimeOffset.UtcNow));

        var account = answer.GetProperty("Agents").EnumerateArray().Single(each => each.GetProperty("Agent").GetString() == "claude-code")
            .GetProperty("Accounts").EnumerateArray().Single();
        Assert.Equal("account-1", account.GetProperty("Name").GetString());
        Assert.Equal("seat", account.GetProperty("DisplayName").GetString());
        Assert.Equal(1, account.GetProperty("Running").GetInt32());
    }

    /// <summary>ACCT2: its own id, or no name at all, gives it none again; another account's name or id is refused, writing nothing.</summary>
    [Fact]
    public async Task A_rename_to_its_own_id_clears_it_and_another_account_s_name_is_refused()
    {
        Accounts("claude-code", "account-1", "account-2");
        AccountNames.Rename(Home, "claude-code", ["account-1", "account-2"], "account-2", "seat");

        var refusal = await RefusalAsync(Module(), "HARNESS_ACTION",
            new { harness = "claude-code", action = "profile-rename", profile = "account-1", name = "SEAT" });
        Assert.Contains("already names `account-2`", refusal);
        Assert.Null(AccountNames.NameOf(Home, "claude-code", "account-1"));

        var cleared = await AnswerAsync(Module(), "HARNESS_ACTION",
            new { harness = "claude-code", action = "profile-rename", profile = "account-2", name = "account-2" });
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("name").ValueKind);
        Assert.Null(AccountNames.NameOf(Home, "claude-code", "account-2"));
    }

    /// <summary>ACCT1: an account put into the lists named — a workspace's, and this machine's by null — answers where it runs.</summary>
    [Fact]
    public async Task A_join_puts_the_account_in_each_list_named_and_answers_where_it_runs()
    {
        Accounts("claude-code", "account-1", "account-3");
        Wiring("""{ "workspaces": { "work": { "claude-code": "account-1" } } }""");

        var answer = await AnswerAsync(Module(), "HARNESS_ACTION",
            new { harness = "claude-code", action = "profile-join", profile = "account-3", join = new string?[] { "work", null } });

        var settings = HarnessSettings.Load(HarnessSettingsPath);
        Assert.Equal(["account-1", "account-3"], settings.WorkspaceRotation["work"]["claude-code"]);
        Assert.Equal(["account-3"], settings.Rotation["claude-code"]);
        var places = answer.GetProperty("places").EnumerateArray().ToList();
        Assert.Equal(2, places.Count);
        Assert.Equal(JsonValueKind.Null, places[0].GetProperty("workspace").ValueKind);
        Assert.Equal("work", places[1].GetProperty("workspace").GetString());
    }

    /// <summary>
    /// ACCT1: a workspace that names no list and no default of its own takes this machine's list, so joining it is refused,
    /// naming both ways on, and nothing is written — not even the lists named beside it.
    /// </summary>
    [Fact]
    public async Task A_join_to_a_workspace_that_takes_this_machine_s_list_is_refused_and_writes_nothing()
    {
        Accounts("claude-code", "account-1");
        Wiring("""{ "rotation": { "claude-code": ["account-1"] } }""");
        Accounts("claude-code", "account-3");
        var wiring = File.ReadAllText(HarnessSettingsPath);

        var refusal = await RefusalAsync(Module(), "HARNESS_ACTION",
            new { harness = "claude-code", action = "profile-join", profile = "account-3", join = new string?[] { null, "forge" } });

        Assert.Contains("`forge` names no `claude-code` account or list of its own", refusal);
        Assert.Equal(wiring, File.ReadAllText(HarnessSettingsPath));
    }

    /// <summary>
    /// 🔴 ACCT1: a sign-in into an account that is not here is refused before anything starts, naming the accounts there,
    /// and no folder is made — the install's owner signed in to bring a signed-out account back and got a new account no
    /// list held. None named and no default is refused the same way.
    /// </summary>
    [Fact]
    public async Task A_sign_in_into_an_account_that_is_not_here_is_refused_and_makes_no_folder()
    {
        Accounts("claude-code", "account-1");
        AccountNames.Rename(Home, "claude-code", ["account-1"], "account-1", "seat");

        var refusal = await RefusalAsync(Module(), "HARNESS_ACTION", new { harness = "claude-code", action = "login", profile = "account-2" });

        Assert.Contains("no account `account-2` on this machine, so nothing was signed in and no account was made", refusal);
        Assert.Contains("seat (account-1)", refusal);
        Assert.False(Directory.Exists(HarnessSettings.ProfileHome(Home, "claude-code", "account-2")));

        var none = await RefusalAsync(Module(), "HARNESS_ACTION", new { harness = "claude-code", action = "login" });
        Assert.Contains("name the `claude-code` account to sign in to", none);
        Assert.False(Directory.Exists(HarnessSettings.ProfileHome(Home, "claude-code", "default")));
        Assert.Equal(["account-1"], HarnessSettings.Profiles(Home, "claude-code"));
    }

    /// <summary>ACCT1, ACCT2: a new account's join that cannot be kept, or a name another account has, is refused before anything starts.</summary>
    [Fact]
    public async Task A_new_account_s_join_or_name_that_cannot_be_kept_is_refused_before_anything_starts()
    {
        Accounts("claude-code", "account-1");
        AccountNames.Rename(Home, "claude-code", ["account-1"], "account-1", "seat");

        var join = await RefusalAsync(Module(), "HARNESS_ACTION",
            new { harness = "claude-code", action = "login-new", join = new[] { "forge" } });
        var name = await RefusalAsync(Module(), "HARNESS_ACTION",
            new { harness = "claude-code", action = "login-new", name = "Seat" });

        Assert.Contains("`forge` names no `claude-code` account or list of its own", join);
        Assert.Contains("already names `account-1`, so nothing was signed in", name);
        Assert.Equal(["account-1"], HarnessSettings.Profiles(Home, "claude-code"));
    }

    /// <summary>
    /// ACCT1, ACCT2: one account on the roster says its id, the person's name for it, and where it runs; an account no list
    /// and no default holds says <c>nowhere</c>, which the page shows as *no workspace*.
    /// </summary>
    [Fact]
    public void An_account_on_the_roster_says_its_name_and_where_it_runs_or_that_it_runs_nowhere()
    {
        var settings = HarnessSettings.Load(Path.Combine(Home, "none.json"))
            .WithRotation("claude-code", ["account-1"])
            .WithDefault("claude-code", "account-1");

        var placed = JsonSerializer.SerializeToElement(DriverModule.ProfileShown(
            new ProfileReport("account-1", Path.Combine(Home, "a"), LoginState.In, DisplayName: "seat"),
            settings.PlacesOf("claude-code", "account-1"), settingsFile: null));
        var nowhere = JsonSerializer.SerializeToElement(DriverModule.ProfileShown(
            new ProfileReport("acct-3f9c2a71", Path.Combine(Home, "b"), LoginState.Unknown),
            settings.PlacesOf("claude-code", "acct-3f9c2a71"), settingsFile: null));

        Assert.Equal("account-1", placed.GetProperty("Name").GetString());
        Assert.Equal("seat", placed.GetProperty("DisplayName").GetString());
        Assert.False(placed.GetProperty("Nowhere").GetBoolean());
        var place = Assert.Single(placed.GetProperty("Places").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, place.GetProperty("Workspace").ValueKind);
        Assert.True(place.GetProperty("List").GetBoolean());
        Assert.True(place.GetProperty("Default").GetBoolean());

        Assert.True(nowhere.GetProperty("Nowhere").GetBoolean());
        Assert.Empty(nowhere.GetProperty("Places").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, nowhere.GetProperty("DisplayName").ValueKind);
    }
}
