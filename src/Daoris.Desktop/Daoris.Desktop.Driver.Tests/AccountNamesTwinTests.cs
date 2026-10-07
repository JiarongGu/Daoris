using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An account's name and the account a person means (ACCT2, D125's ACCT2 note): <c>accounts.json</c> under the home holds the
/// name a person gave each account, by agent and its stable id, the folder's name. The driver's half of a TWIN with the
/// CLI's <c>accountnames.ts</c>, whose <c>accountnames.test.ts</c> holds the same tables, row for row and in the same order,
/// and parses these theories to hold them to its own, cell for cell.
/// </summary>
/// <remarks>
/// <para>A file is the text on disk, <c>null</c> for none; accounts are the folders on disk, as a JSON list. An <c>after</c>
/// is the file's JSON once the name is given, or <c>refused &lt;kind&gt;</c> with the account it collides with, where the door
/// refuses it and writes nothing.</para>
/// <para>🔴 <b>Keep each row on one line, its cells literals</b>: the CLI's test reads them.</para>
/// </remarks>
public sealed class AccountNamesTwinTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-names-twin-" + Guid.NewGuid().ToString("N")[..8]);

    public AccountNamesTwinTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string File_ => AccountNames.PathOf(_home);

    private static List<string> Names(string list) => [.. JsonNode.Parse(list)!.AsArray().Select(name => name!.GetValue<string>())];

    private void Holding(string? file)
    {
        if (file is not null) File.WriteAllText(File_, file);
    }

    // ——— Reading: missing or unreadable is no name; a name is text, trimmed, not blank; agents and accounts compare
    // without case, as the readings compare them.

    [Theory]
    [InlineData("missing is no name", null, "claude-code", "account-1", null)]
    [InlineData("not JSON is no name", "not json", "claude-code", "account-1", null)]
    [InlineData("an account named", """{"claude-code":{"account-1":{"name":"you@work.example"}}}""", "claude-code", "account-1", "you@work.example")]
    [InlineData("another account's name is not this one's", """{"claude-code":{"account-2":{"name":"seat"}}}""", "claude-code", "account-1", null)]
    [InlineData("another agent's name is not this one's", """{"codex":{"account-1":{"name":"seat"}}}""", "claude-code", "account-1", null)]
    [InlineData("a name is read trimmed", """{"claude-code":{"account-1":{"name":" work "}}}""", "claude-code", "account-1", "work")]
    [InlineData("a blank name is none", """{"claude-code":{"account-1":{"name":"  "}}}""", "claude-code", "account-1", null)]
    [InlineData("a name that is not text is none", """{"claude-code":{"account-1":{"name":7}}}""", "claude-code", "account-1", null)]
    [InlineData("an entry that is not an object is none", """{"claude-code":{"account-1":"work"}}""", "claude-code", "account-1", null)]
    [InlineData("an agent that is not an object is none", """{"claude-code":[1]}""", "claude-code", "account-1", null)]
    [InlineData("agents and accounts compare without case", """{"claude-code":{"account-1":{"name":"work"}}}""", "Claude-Code", "ACCOUNT-1", "work")]
    [InlineData("a name in Chinese is read as written", """{"claude-code":{"acct-3f9c2a71":{"name":"工作"}}}""", "claude-code", "acct-3f9c2a71", "工作")]
    [InlineData("a letter whose capital is two letters is not those two: straße is not STRASSE", """{"claude-code":{"straße":{"name":"work"}}}""", "claude-code", "STRASSE", null)]
    [InlineData("a dotted capital I is not an i with a dot above", """{"claude-code":{"İzmir":{"name":"work"}}}""", "claude-code", "i\u0307zmir", null)]
    public void A_name_reads_as_the_cli_reads_it(string why, string? file, string agent, string account, string? name)
    {
        Holding(file);

        var read = AccountNames.NameOf(_home, agent, account);

        Assert.True(name == read, $"{why}: {read ?? "none"}");
    }

    // ——— The account a person means: the one whose id it is, exactly; else the one whose name it is, in any case.

    [Theory]
    [InlineData("an id names its account", """["account-1","account-2"]""", null, "claude-code", "account-2", "account-2")]
    [InlineData("a name names its account", """["account-1","account-2"]""", """{"claude-code":{"account-1":{"name":"work"}}}""", "claude-code", "work", "account-1")]
    [InlineData("a name names it in any case", """["account-1","account-2"]""", """{"claude-code":{"account-1":{"name":"work"}}}""", "claude-code", "WORK", "account-1")]
    [InlineData("what is named is read trimmed", """["account-1"]""", """{"claude-code":{"account-1":{"name":"work"}}}""", "claude-code", " work ", "account-1")]
    [InlineData("an id compares exactly, as the wiring compares it", """["account-1"]""", null, "claude-code", "Account-1", null)]
    [InlineData("an id wins over a name written by hand to match it", """["account-1","work"]""", """{"claude-code":{"account-1":{"name":"work"}}}""", "claude-code", "work", "work")]
    [InlineData("a name whose account is gone names nothing", """["account-1"]""", """{"claude-code":{"account-9":{"name":"work"}}}""", "claude-code", "work", null)]
    [InlineData("another agent's name names nothing here", """["account-1"]""", """{"codex":{"account-1":{"name":"work"}}}""", "claude-code", "work", null)]
    [InlineData("nothing named is nothing", """["account-1"]""", null, "claude-code", "seat", null)]
    [InlineData("a letter whose capital is two letters is not those two: straße is not STRASSE", """["account-1"]""", """{"claude-code":{"account-1":{"name":"straße"}}}""", "claude-code", "STRASSE", null)]
    [InlineData("a dotted capital I is not an i with a dot above", """["account-1"]""", """{"claude-code":{"account-1":{"name":"İzmir"}}}""", "claude-code", "i\u0307zmir", null)]
    public void An_account_resolves_as_the_cli_resolves_it(string why, string accounts, string? file, string agent, string given, string? id)
    {
        Holding(file);

        var resolved = AccountNames.Resolve(Names(accounts), AccountNames.Of(_home, agent), given);

        Assert.True(id == resolved, $"{why}: {resolved ?? "none"}");
    }

    // ——— Naming one (ACCT2): the person's word, kept beside the account; its own id, or none, clears it. A name is one word
    // a terminal can type: no space, no backtick, no leading dash, at most 64 characters; never another account's id or
    // name, in any case. What has no field is kept, and an entry or an agent left with nothing goes.

    [Theory]
    [InlineData("an account named", """["account-1","account-2"]""", null, "claude-code", "account-1", "work", """{"claude-code":{"account-1":{"name":"work"}}}""")]
    [InlineData("a name is kept trimmed", """["account-1"]""", null, "claude-code", "account-1", " work ", """{"claude-code":{"account-1":{"name":"work"}}}""")]
    [InlineData("a name replaced", """["account-1"]""", """{"claude-code":{"account-1":{"name":"work"}}}""", "claude-code", "account-1", "seat", """{"claude-code":{"account-1":{"name":"seat"}}}""")]
    [InlineData("its own name in another case", """["account-1"]""", """{"claude-code":{"account-1":{"name":"work"}}}""", "claude-code", "account-1", "Work", """{"claude-code":{"account-1":{"name":"Work"}}}""")]
    [InlineData("its own id clears its name, and an agent left with none goes", """["account-1"]""", """{"claude-code":{"account-1":{"name":"work"}}}""", "claude-code", "account-1", "account-1", "{}")]
    [InlineData("none clears its name", """["account-1"]""", """{"claude-code":{"account-1":{"name":"work"}},"codex":{"account-1":{"name":"seat"}}}""", "claude-code", "account-1", null, """{"codex":{"account-1":{"name":"seat"}}}""")]
    [InlineData("a clear keeps what the entry has no field for", """["account-1"]""", """{"claude-code":{"account-1":{"name":"work","later":true}}}""", "claude-code", "account-1", null, """{"claude-code":{"account-1":{"later":true}}}""")]
    [InlineData("what has no field is kept", """["account-1","account-2"]""", """{"codex":[1],"claude-code":{"account-1":{"name":"work","later":true},"account-2":{"name":"seat"}}}""", "claude-code", "account-1", "desk", """{"codex":[1],"claude-code":{"account-1":{"name":"desk","later":true},"account-2":{"name":"seat"}}}""")]
    [InlineData("an entry is found in any case, and written under its own", """["account-1"]""", """{"Claude-Code":{"Account-1":{"name":"work"}}}""", "claude-code", "account-1", "seat", """{"Claude-Code":{"Account-1":{"name":"seat"}}}""")]
    [InlineData("a name in Chinese is kept as written", """["acct-3f9c2a71"]""", null, "claude-code", "acct-3f9c2a71", "工作", """{"claude-code":{"acct-3f9c2a71":{"name":"工作"}}}""")]
    [InlineData("an account not on this machine is refused", """["account-1"]""", null, "claude-code", "account-9", "work", "refused missing")]
    [InlineData("a blank name is refused", """["account-1"]""", null, "claude-code", "account-1", "  ", "refused blank")]
    [InlineData("a name with a space is refused", """["account-1"]""", null, "claude-code", "account-1", "my seat", "refused characters")]
    [InlineData("a name with a backtick is refused", """["account-1"]""", null, "claude-code", "account-1", "wo`rk", "refused characters")]
    [InlineData("a name that starts with a dash is refused", """["account-1"]""", null, "claude-code", "account-1", "-work", "refused characters")]
    [InlineData("a name longer than 64 characters is refused", """["account-1"]""", null, "claude-code", "account-1", "a234567890b234567890c234567890d234567890e234567890f234567890g2345", "refused long")]
    [InlineData("another account's name is refused, in any case", """["account-1","account-2"]""", """{"claude-code":{"account-2":{"name":"work"}}}""", "claude-code", "account-1", "WORK", "refused taken account-2")]
    [InlineData("another account's id is refused, in any case", """["account-1","work"]""", null, "claude-code", "account-1", "Work", "refused taken work")]
    [InlineData("a name a gone account kept is free", """["account-1"]""", """{"claude-code":{"account-9":{"name":"work"}}}""", "claude-code", "account-1", "work", """{"claude-code":{"account-9":{"name":"work"},"account-1":{"name":"work"}}}""")]
    [InlineData("a file that does not read is refused and kept", """["account-1"]""", "not json", "claude-code", "account-1", "work", "refused unreadable")]
    [InlineData("a letter whose capital is two letters is not those two: straße is not STRASSE", """["account-1","account-2"]""", """{"claude-code":{"account-2":{"name":"straße"}}}""", "claude-code", "account-1", "STRASSE", """{"claude-code":{"account-2":{"name":"straße"},"account-1":{"name":"STRASSE"}}}""")]
    [InlineData("a dotted capital I is not an i with a dot above", """["account-1","account-2"]""", """{"claude-code":{"account-2":{"name":"İzmir"}}}""", "claude-code", "account-1", "i\u0307zmir", """{"claude-code":{"account-2":{"name":"İzmir"},"account-1":{"name":"i\u0307zmir"}}}""")]
    public void A_name_is_given_as_the_cli_gives_it(string why, string accounts, string? file, string agent, string account, string? name, string after)
    {
        Holding(file);

        string said;
        try
        {
            AccountNames.Rename(_home, agent, Names(accounts), account, name);
            said = File.ReadAllText(File_);
        }
        catch (AccountNameException refused)
        {
            said = $"refused {refused.Problem.Kind.ToString().ToLowerInvariant()}{(refused.Problem.Other is { } other ? $" {other}" : "")}";
        }

        if (after.StartsWith("refused", StringComparison.Ordinal))
        {
            Assert.True(after == said, $"{why}: {said}");
            Assert.True(file is null ? !File.Exists(File_) : File.ReadAllText(File_) == file, $"{why}: written");
        }
        else
        {
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(after), JsonNode.Parse(said)), $"{why}: {said}");
            Assert.True(said.EndsWith("}\n", StringComparison.Ordinal) && !said.Contains('\r'), $"{why}: LF and a final newline");
        }
    }

    // ——— 🔴 Both twins write the same bytes for the same names: agents and ids in the order found, a name first in its entry,
    // two spaces an indent, LF and a final newline. An `after` is that file's JSON in its order, indented as both write it.

    [Theory]
    [InlineData("one agent, two accounts, the new one after", """{"claude-code":{"account-2":{"name":"seat"}}}""", "claude-code", "account-1", "work", """{"claude-code":{"account-2":{"name":"seat"},"account-1":{"name":"work"}}}""")]
    [InlineData("a name goes first in its entry", """{"claude-code":{"account-1":{"later":true}}}""", "claude-code", "account-1", "work", """{"claude-code":{"account-1":{"name":"work","later":true}}}""")]
    [InlineData("another agent first stays first", """{"codex":{"account-1":{"name":"seat"}}}""", "claude-code", "account-2", "work", """{"codex":{"account-1":{"name":"seat"}},"claude-code":{"account-2":{"name":"work"}}}""")]
    [InlineData("the last name cleared leaves an empty file", """{"claude-code":{"account-1":{"name":"work"}}}""", "claude-code", "account-1", null, "{}")]
    public void Both_twins_write_the_same_file(string why, string? file, string agent, string account, string? name, string after)
    {
        Holding(file);

        AccountNames.Rename(_home, agent, ["account-1", "account-2"], account, name);

        var wanted = JsonNode.Parse(after)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true, NewLine = "\n" }) + "\n";
        Assert.True(wanted == File.ReadAllText(File_), $"{why}:\n{File.ReadAllText(File_)}");
    }

    // ——— A sign-in into an account that is here (ACCT1): the one named, by its id or its name; none named is the machine's
    // default; never one that is not here, and never a new one.

    [Theory]
    [InlineData("an account by its id", """["account-1","account-2"]""", null, "{}", "claude-code", "account-2", "account-2")]
    [InlineData("an account by its name", """["account-1","account-2"]""", """{"claude-code":{"account-2":{"name":"seat"}}}""", "{}", "claude-code", "seat", "account-2")]
    [InlineData("none named is the machine's default", """["account-1","account-2"]""", null, """{"defaults":{"claude-code":"account-1"}}""", "claude-code", null, "account-1")]
    [InlineData("an account named wins over the default", """["account-1","account-2"]""", null, """{"defaults":{"claude-code":"account-1"}}""", "claude-code", "account-2", "account-2")]
    [InlineData("an account not here is refused, never made", """["account-1"]""", null, "{}", "claude-code", "account-3", "refused missing account-3")]
    [InlineData("none named and no default is refused", """["account-1"]""", null, "{}", "claude-code", null, "refused none")]
    [InlineData("a default naming an account not here is refused", """["account-1"]""", null, """{"defaults":{"claude-code":"account-9"}}""", "claude-code", null, "refused missing account-9")]
    [InlineData("another agent's default is not this one's", """["account-1"]""", null, """{"defaults":{"codex":"account-1"}}""", "claude-code", null, "refused none")]
    public void A_sign_in_reaches_the_account_the_cli_reaches(string why, string accounts, string? file, string wiring, string agent, string? given, string reached)
    {
        Holding(file);
        var path = Path.Combine(_home, "harnesses.json");
        File.WriteAllText(path, wiring);

        var target = AccountNames.SignInTarget(Names(accounts), AccountNames.Of(_home, agent), HarnessSettings.Load(path), agent, given);

        var said = target.Account ?? $"refused {(target.Missing is { } missing ? $"missing {missing}" : "none")}";
        Assert.True(reached == said, $"{why}: {said}");
    }

    /// <summary>
    /// ACCT1: a sign-in into an account is refused, before anything starts and with nothing made, where its folder is not
    /// there; only a sign-in to a new account opens one.
    /// </summary>
    [Fact]
    public async Task A_sign_in_into_an_account_that_is_not_there_makes_nothing()
    {
        var toolchain = new HarnessToolchain(["no-such-agent-binary"], ["--version"], "AGENT_HOME", LoginArguments: ["login"]);
        var missing = HarnessSettings.ProfileHome(_home, "claude-code", "account-3");

        var refused = await Assert.ThrowsAsync<DriverException>(
            () => HarnessActions.LoginAsync(toolchain, new HarnessCommand("agent", toolchain.Binary, null, null), missing, _ => { }));

        Assert.Contains("nothing was signed in and no account was made", refused.Message);
        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public void A_new_account_takes_a_fresh_id_its_name_never_shifts_from()
    {
        var drawn = new Queue<string>(["3f9c2a71", "0b7e4d22"]);
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "claude-code", "acct-3f9c2a71"));

        var id = AccountNames.NewId(_home, "claude-code", () => drawn.Dequeue());

        // A drawn id an account already has is drawn again: an id is never reused, so a reading, a cool-off or a usage total
        // kept under one never lands on another account.
        Assert.Equal("acct-0b7e4d22", id);
        Assert.Matches("^acct-[0-9a-f]{8}$", AccountNames.NewId(_home, "claude-code"));
    }

    /// <summary>
    /// ACCT2: a rename touches <c>accounts.json</c> alone. The rotation, the defaults, the readings, the cool-offs, the session
    /// records' count and the usage totals all name the account's id, so each keeps it across the rename, and the name a
    /// person types reaches the same account.
    /// </summary>
    [Fact]
    public void A_rename_is_kept_by_rotation_defaults_readings_cool_offs_records_and_usage()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var account in new[] { "account-1", "account-2" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "claude-code", account));
        var wiring = Path.Combine(_home, "harnesses.json");
        File.WriteAllText(wiring, """
            { "defaults": { "claude-code": "account-1" }, "workspaces": { "work": { "claude-code": "account-1" } },
              "rotation": { "claude-code": ["account-1", "account-2"] }, "workspaceRotation": { "work": { "claude-code": ["account-1"] } } }
            """);
        AccountReads.Keep(_home, "claude-code", "account-1", LoginState.Out, now);
        AccountCooling.Cool(_home, new CoolingEntry("claude-code", "account-1", now.AddHours(2), true, "weekly", now, "s1a2b3c4"), now);
        var usage = new SessionUsage(_home);
        usage.Record(new UsageEntry("s1a2b3c4", "engine", "claude-code", "account-1", 1200, 200000, now));
        var before = File.ReadAllText(wiring);

        AccountNames.Rename(_home, "claude-code", HarnessSettings.Profiles(_home, "claude-code"), "account-1", "you@work.example");

        Assert.Equal(before, File.ReadAllText(wiring));
        var settings = HarnessSettings.Load(wiring);
        Assert.Equal("account-1", settings.ResolveScope("claude-code", null).Begins);
        Assert.Equal(["account-1"], settings.ResolveScope("claude-code", "work").List);
        Assert.Equal("account-1", settings.Resolve("claude-code", "work", null));
        Assert.Equal(LoginState.Out, AccountReads.Of(_home, "claude-code").Accounts["account-1"].Login);
        Assert.NotNull(AccountCooling.Of(_home, "claude-code", "account-1", now));
        Assert.Equal(1200, usage.ByAccount().Single(each => each.Profile == "account-1").Used);
        // The name the person types reaches the account. (A session record's count by its id is the modules' table.)
        Assert.Equal("account-1", AccountNames.Resolve(HarnessSettings.Profiles(_home, "claude-code"), AccountNames.Of(_home, "claude-code"), "You@Work.example"));
        Assert.Equal(new AccountPlace(null, List: true, Default: true), settings.PlacesOf("claude-code", "account-1")[0]);
    }

    [Fact]
    public void An_account_reads_as_its_name_and_else_its_id()
    {
        Holding("""{"claude-code":{"account-1":{"name":"work"}}}""");

        Assert.Equal("work", AccountNames.Shown(_home, "claude-code", "account-1"));
        Assert.Equal("account-2", AccountNames.Shown(_home, "claude-code", "account-2"));
    }

    [Fact]
    public void An_account_removed_forgets_its_name_and_no_other()
    {
        Holding("""{"claude-code":{"account-1":{"name":"work"},"account-2":{"name":"seat"}}}""");

        AccountNames.Forget(_home, "claude-code", "account-1");

        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("""{"claude-code":{"account-2":{"name":"seat"}}}"""), JsonNode.Parse(File.ReadAllText(File_))));
        // Nothing to forget writes nothing.
        AccountNames.Forget(_home, "codex", "account-1");
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("""{"claude-code":{"account-2":{"name":"seat"}}}"""), JsonNode.Parse(File.ReadAllText(File_))));
    }

    [Fact]
    public void Each_refusal_says_what_was_wrong_and_what_is_there()
    {
        Holding("""{"claude-code":{"account-2":{"name":"work"}}}""");

        var taken = Assert.Throws<AccountNameException>(
            () => AccountNames.Rename(_home, "claude-code", ["account-1", "account-2"], "account-1", "work"));
        Assert.Contains("`work` already names `account-2`", taken.Message);

        var missing = Assert.Throws<AccountNameException>(
            () => AccountNames.Rename(_home, "claude-code", ["account-1", "account-2"], "account-9", "desk"));
        Assert.Contains("no account `account-9`", missing.Message);
        Assert.Contains("account-1, work (account-2)", missing.Message);

        var spaced = Assert.Throws<AccountNameException>(
            () => AccountNames.Rename(_home, "claude-code", ["account-1"], "account-1", "my seat"));
        Assert.Contains("one word", spaced.Message);
        Assert.IsAssignableFrom<DriverException>(spaced);
    }

    [Fact]
    public void A_name_is_never_a_reading_of_who_signed_in()
    {
        // D66 §3: who signed in is written nowhere without the person. A name is written only by a rename, never by a read.
        Assert.False(File.Exists(File_));
        Assert.Null(AccountNames.NameOf(_home, "claude-code", "account-1"));
        Assert.False(File.Exists(File_));
        Assert.Empty(JsonSerializer.Serialize(AccountNames.Of(_home, "claude-code")).Trim('{', '}'));
    }
}
