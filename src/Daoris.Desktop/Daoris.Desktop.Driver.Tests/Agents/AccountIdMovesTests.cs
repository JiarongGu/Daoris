using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An account's id is one form, <c>acct-</c> and eight hex characters: a new account draws it, and an account an earlier build named
/// <c>account-N</c> or after a word moves to it once, with every reference it has (<see cref="AccountIdMoves"/>). A fixture home holds
/// three such ids, a hash one and a lock's folder with an entry in <c>accounts.json</c>; the stores are the shapes their writers write,
/// the service's and the desktop's stores stand-ins holding only the columns a move reads.
/// </summary>
public sealed class AccountIdMovesTests : IDisposable
{
    private const string Agent = "claude-code";

    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-ids-" + Guid.NewGuid().ToString("N")[..8]);

    private string Install => Path.Combine(_root, "install");

    private string Home => Path.Combine(Install, "data");

    private string Knowledge => DaorisHome.Entry(Home, HomeEntry.Knowledge);

    private string Desktop => DaorisHome.Entry(Home, HomeEntry.DesktopDb);

    public AccountIdMovesTests()
    {
        Directory.CreateDirectory(Home);
        File.WriteAllText(Path.Combine(Install, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        foreach (var account in new[] { "account-1", "account-3", "Gmail", "acct-0000000a", "account-3.lock" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, Agent, account));
        }

        File.WriteAllText(Path.Combine(HarnessSettings.ProfileHome(Home, Agent, "account-1"), ".credentials.json"), "{}");
        Write(HomeEntry.Accounts, """
            { "claude-code": { "account-1": { "name": "someone@example.com" },
              "account-3.lock": { "name": "stray" }, "acct-0000000a": { "name": "spare" } } }
            """);
        Write(HomeEntry.Harnesses, """
            { "defaults": { "claude-code": "account-1" },
              "workspaces": { "work": { "claude-code": "account-3" } },
              "rotation": { "claude-code": ["account-1", "account-3", "Gmail", "acct-0000000a"] },
              "rotationUse": { "claude-code": { "use": "goal", "keep": "Gmail" } } }
            """);
        Write(HomeEntry.Cooling, """{ "claude-code": { "account-3": { "until": "2026-11-21T00:00:00Z" } } }""");
        Write(HomeEntry.Windows, """{ "claude-code": { "account-1": { "weekly": { "reset": "2026-11-25T00:00:00Z", "used": 0.4 } } } }""");
        Write(HomeEntry.Reads, """{ "claude-code": { "accounts": { "account-1": { "login": "in", "read": "2026-11-20T00:00:00Z" } } } }""");
        Write(HomeEntry.AgentOffers, """
            { "schemaVersion": 1, "readings": { "claude-code|Gmail": { "agent": "claude-code", "account": "Gmail", "at": "2026-11-20T00:00:00.000Z" } } }
            """);
        Write(HomeEntry.Usage, """
            [ { "session": "s1", "repository": "r", "door": "claude-code", "profile": "account-1", "used": 10, "size": 100, "when": "2026-11-20T00:00:00Z" },
              { "session": "s2", "repository": "r", "door": "claude-code", "profile": "Gmail", "used": 20, "size": 100, "when": "2026-11-20T00:00:00Z" },
              { "session": "s3", "repository": "r", "door": "codex", "profile": "account-1", "used": 30, "size": 100, "when": "2026-11-20T00:00:00Z" } ]
            """);
        var history = DaorisHome.Entry(Home, HomeEntry.UsageHistory);
        Directory.CreateDirectory(history);
        File.WriteAllText(Path.Combine(history, "epochs.json"), """{ "claude-code": { "account-3": { "epoch": 2 } } }""");
        File.WriteAllText(Path.Combine(history, "turns-2026-11.jsonl"),
            "{\"kind\":\"turn\",\"agent\":\"claude-code\",\"account\":\"account-3\",\"session\":\"s1\"}\n"
            + "{\"kind\":\"turn\",\"agent\":\"codex\",\"account\":\"account-3\",\"session\":\"s3\"}\n");
        Store(Knowledge, "CREATE TABLE sessions (id TEXT PRIMARY KEY, door TEXT NOT NULL, profile TEXT NULL);",
            "INSERT INTO sessions VALUES ('s1', 'claude-code', 'account-1'), ('s2', 'claude-code', 'Gmail'), ('s3', 'codex', 'account-1'), ('s4', 'claude-code', NULL);");
        Store(Desktop, "CREATE TABLE bindings (session TEXT PRIMARY KEY, adapter TEXT NOT NULL, conversation TEXT NOT NULL, kept TEXT NOT NULL);"
            + "CREATE TABLE binding_picks (session TEXT PRIMARY KEY, account TEXT NOT NULL, at TEXT NOT NULL);",
            "INSERT INTO bindings VALUES ('s1', 'claude-code', 'c', 'k'), ('s3', 'codex', 'c', 'k');"
            + "INSERT INTO binding_picks VALUES ('s1', 'account-1', 'now'), ('s3', 'account-1', 'now');");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>🔴 The plan names each legacy id's move and every reference it has, drops the lock's entry, and changes nothing.</summary>
    [Fact]
    public void The_plan_lists_every_rename_and_reference_and_changes_nothing()
    {
        var before = Snapshot();

        var plan = AccountIdMoves.Plan(Home);

        Assert.Equal(["Gmail", "account-1", "account-3"], plan.Moves.Select(move => move.Rename.From).Order(StringComparer.Ordinal));
        Assert.All(plan.Moves, move => Assert.True(AccountIdForm.IsHash(move.Rename.To), move.Rename.To));
        Assert.Equal(3, plan.Moves.Select(move => move.Rename.To).Distinct().Count());
        Assert.Equal([(Agent, "account-3.lock")], plan.Strays.Select(stray => (stray.Agent, stray.Entry)));

        string[] Where(string from) => [.. plan.Moves.Single(move => move.Rename.From == from).References.Select(each => $"{each.Place} {each.Where}")];
        Assert.Equal(
            [
                "profiles/agents/claude-code/account-1 folder",
                "data/accounts.json claude-code.account-1",
                "data/harnesses.json defaults.claude-code",
                "data/harnesses.json rotation.claude-code[0]",
                "data/windows.json claude-code.account-1",
                "data/reads.json claude-code.accounts.account-1",
                "data/usage.json 1 session",
                "data/knowledge.db sessions.profile 1 row",
                "data/desktop.db binding_picks.account 1 row",
            ],
            Where("account-1"));
        Assert.Equal(
            [
                "profiles/agents/claude-code/account-3 folder",
                "data/harnesses.json workspaces.work.claude-code",
                "data/harnesses.json rotation.claude-code[1]",
                "data/cooling.json claude-code.account-3",
                "data/usage-history/epochs.json claude-code.account-3",
                "data/usage-history/turns-2026-11.jsonl 1 line",
            ],
            Where("account-3"));
        Assert.Contains("data/accounts.json claude-code.Gmail", Where("Gmail"));
        Assert.Contains("data/harnesses.json rotationUse.claude-code.keep", Where("Gmail"));
        Assert.Contains("data/agent-offers.json readings.claude-code|Gmail", Where("Gmail"));
        Assert.Equal(before, Snapshot());
    }

    /// <summary>
    /// 🔴 After the move the home reads the same accounts, names, order, sessions and usage, each under its hash id; an id that is the
    /// person's word (<c>Gmail</c>) becomes the account's name where it has none, and a counter (<c>account-3</c>) never is.
    /// </summary>
    [Fact]
    public void The_apply_keeps_the_same_accounts_sessions_usage_and_order_under_hash_ids()
    {
        var to = AccountIdMoves.Plan(Home).Moves.ToDictionary(move => move.Rename.From, move => move.Rename.To);

        var done = AccountIdMoves.Apply(Home);

        Assert.Equal(3, done.Moves.Count);
        Assert.Equal(new[] { to["account-1"], to["account-3"], to["Gmail"], "acct-0000000a" }.Order(StringComparer.Ordinal),
            HarnessSettings.Profiles(Home, Agent));
        Assert.True(File.Exists(Path.Combine(HarnessSettings.ProfileHome(Home, Agent, to["account-1"]), ".credentials.json")));
        Assert.True(Directory.Exists(HarnessSettings.ProfileHome(Home, Agent, "account-3.lock")));

        var names = AccountNames.Of(Home, Agent);
        Assert.Equal("someone@example.com", names[to["account-1"]]);
        Assert.Equal("Gmail", names[to["Gmail"]]);
        Assert.Equal("spare", names["acct-0000000a"]);
        Assert.False(names.ContainsKey(to["account-3"]));
        Assert.DoesNotContain("account-3.lock", Read(HomeEntry.Accounts)[Agent]!.AsObject().Select(pair => pair.Key));

        var wiring = HarnessSettings.Load(DaorisHome.Entry(Home, HomeEntry.Harnesses));
        Assert.Equal(to["account-1"], wiring.Defaults[Agent]);
        Assert.Equal(to["account-3"], wiring.Workspaces["work"][Agent]);
        Assert.Equal([to["account-1"], to["account-3"], to["Gmail"], "acct-0000000a"], wiring.Rotation[Agent]);
        Assert.Equal(to["Gmail"], wiring.Uses[Agent]["keep"]!.Value.GetString());

        Assert.NotNull(Read(HomeEntry.Cooling)[Agent]![to["account-3"]]);
        Assert.NotNull(Read(HomeEntry.Windows)[Agent]![to["account-1"]]);
        Assert.NotNull(Read(HomeEntry.Reads)[Agent]!["accounts"]![to["account-1"]]);
        Assert.Equal(to["Gmail"], Read(HomeEntry.AgentOffers)["readings"]![$"{Agent}|{to["Gmail"]}"]!["account"]!.GetValue<string>());

        var usage = new SessionUsage(Home).Sessions.ToDictionary(entry => entry.Session, entry => entry.Profile);
        Assert.Equal(to["account-1"], usage["s1"]);
        Assert.Equal(to["Gmail"], usage["s2"]);
        Assert.Equal("account-1", usage["s3"]);

        var history = DaorisHome.Entry(Home, HomeEntry.UsageHistory);
        Assert.NotNull(JsonNode.Parse(File.ReadAllText(Path.Combine(history, "epochs.json")))![Agent]![to["account-3"]]);
        var turns = File.ReadAllLines(Path.Combine(history, "turns-2026-11.jsonl")).Select(line => JsonNode.Parse(line)!["account"]!.GetValue<string>());
        Assert.Equal([to["account-3"], "account-3"], turns);

        Assert.Equal([("s1", to["account-1"]), ("s2", to["Gmail"]), ("s3", "account-1"), ("s4", "")],
            Rows(Knowledge, "SELECT id, coalesce(profile, '') FROM sessions ORDER BY id"));
        Assert.Equal([("s1", to["account-1"]), ("s3", "account-1")], Rows(Desktop, "SELECT session, account FROM binding_picks ORDER BY session"));
    }

    /// <summary>🔴 A second apply finds nothing to move and changes nothing.</summary>
    [Fact]
    public void A_second_apply_does_nothing()
    {
        AccountIdMoves.Apply(Home);
        var after = Snapshot();

        var again = AccountIdMoves.Apply(Home);

        Assert.True(again.Nothing);
        Assert.True(AccountIdMoves.Plan(Home).Nothing);
        Assert.Equal(after, Snapshot());
    }

    /// <summary>🔴 A new account's id is the hash form, whichever way it is made; a word the person gives it is its name, never its id.</summary>
    [Fact]
    public void A_new_account_id_is_the_hash_form()
    {
        Assert.True(AccountIdForm.IsHash(AccountNames.NewId(Home, Agent)));
        Assert.False(AccountIdForm.IsHash("account-1"));
        Assert.False(AccountIdForm.IsHash("acct-0000000A"));

        var made = AccountAdd.Make(Home, Agent, "work");

        Assert.True(AccountIdForm.IsHash(made), made);
        Assert.Contains(made, HarnessSettings.Profiles(Home, Agent));
        Assert.Equal("work", AccountNames.NameOf(Home, Agent, made));
        Assert.Equal(made, AccountAdd.Make(Home, Agent, "work"));
    }

    /// <summary>🔴 A lock's folder and an entry named after one are never an account: not listed, not named, not moved.</summary>
    [Fact]
    public void A_lock_file_is_never_an_account()
    {
        Assert.DoesNotContain("account-3.lock", HarnessSettings.Profiles(Home, Agent));
        Assert.False(AccountNames.Of(Home, Agent).ContainsKey("account-3.lock"));
        Assert.DoesNotContain("account-3.lock", AccountIdMoves.Plan(Home).Moves.Select(move => move.Rename.From));
    }

    /// <summary>🔴 <c>home account-ids --plan</c> says each move with the account's name and every reference, and changes nothing.</summary>
    [Fact]
    public void The_verb_plans_each_move_with_its_name_and_changes_nothing()
    {
        var before = Snapshot();

        var exit = AccountIdsCommand.Run(["--plan"], Home, null, null, out var said);

        Assert.Equal(0, exit);
        var to = AccountIdMoves.Plan(Home).Moves.ToDictionary(move => move.Rename.From, move => move.Rename.To);
        Assert.Contains($"  claude-code  account-1 → {to["account-1"]}  (someone@example.com)", said);
        Assert.Contains($"  claude-code  account-3 → {to["account-3"]}\n", said.ReplaceLineEndings("\n"));
        Assert.Contains("    data/harnesses.json  rotation.claude-code[1]", said);
        Assert.Contains("  drop  accounts.json  claude-code.account-3.lock: no account's", said);
        Assert.StartsWith("home: 3 account ids with 22 references and 1 stray entry would move. Nothing was changed.", said);
        Assert.Equal(before, Snapshot());
    }

    /// <summary>🔴 <c>--yes</c> moves once and says so; run again, it finds nothing; from a session's terminal it moves nothing.</summary>
    [Fact]
    public void The_verb_moves_once_and_never_from_a_session_terminal()
    {
        var before = Snapshot();
        Assert.Equal(1, AccountIdsCommand.Run(["--yes"], Home, "session-1", null, out var refused));
        Assert.Contains("this one is a session's", refused);
        Assert.Equal(before, Snapshot());

        Assert.Equal(0, AccountIdsCommand.Run(["--yes"], Home, null, null, out var moved));
        Assert.StartsWith("home: 3 account ids with 22 references and 1 stray entry moved.", moved);
        Assert.Equal(0, AccountIdsCommand.Run(["--yes"], Home, null, null, out var again));
        Assert.Equal("home: every account id is the hash form already. Nothing was changed.", again.Trim());
        Assert.Equal(2, AccountIdsCommand.Run([], Home, null, null, out _));
    }

    /// <summary>🔴 A process holding an account's folder is named by the plan and the apply leaves that account whole; the next apply moves it.</summary>
    [Fact]
    public void A_folder_a_process_holds_is_named_in_the_plan_and_left_whole_by_the_apply()
    {
        if (!OperatingSystem.IsWindows()) return;
        var held = Path.Combine(HarnessSettings.ProfileHome(Home, Agent, "account-1"), ".credentials.json");
        var me = $"{System.Diagnostics.Process.GetCurrentProcess().ProcessName} (pid {Environment.ProcessId})";

        AccountIdPlan plan;
        using (new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            plan = AccountIdMoves.Plan(Home);
            Assert.Equal(1, AccountIdsCommand.Run(["--yes"], Home, null, null, out var said));
            Assert.Contains($"held by {me}", said);
            Assert.Contains("the next `daoris home account-ids --yes` moves it", said);
            Assert.True(Directory.Exists(HarnessSettings.ProfileHome(Home, Agent, "account-1")));
            Assert.Contains("account-1", HarnessSettings.Profiles(Home, Agent));
        }

        var one = plan.Moves.Single(move => move.Rename.From == "account-1");
        Assert.Equal(Environment.ProcessId, Assert.Single(one.HeldBy!).Pid);
        Assert.Contains($"held by {me}", one.Failed);
        Assert.Empty(one.References);
        Assert.All(plan.Moves.Where(move => move != one), move => Assert.Null(move.Failed));
        Assert.Equal("account-1", HarnessSettings.Load(DaorisHome.Entry(Home, HomeEntry.Harnesses)).Defaults[Agent]);
        Assert.Contains("account-1", AccountNames.Of(Home, Agent).Keys);
        Assert.Equal("account-1", new SessionUsage(Home).Sessions.Single(entry => entry.Session == "s1").Profile);
        Assert.DoesNotContain("Gmail", HarnessSettings.Profiles(Home, Agent));
        Assert.DoesNotContain("account-3", HarnessSettings.Profiles(Home, Agent));

        var done = AccountIdMoves.Apply(Home);

        Assert.Equal(one.Rename.To, Assert.Single(done.Moves).Rename.To);
        Assert.Null(done.Moves[0].Failed);
        Assert.DoesNotContain("account-1", HarnessSettings.Profiles(Home, Agent));
        Assert.Equal(one.Rename.To, HarnessSettings.Load(DaorisHome.Entry(Home, HomeEntry.Harnesses)).Defaults[Agent]);
        Assert.True(AccountIdMoves.Plan(Home).Nothing);
    }

    [Fact]
    public void The_plan_says_a_held_account_would_be_left_and_names_the_holder()
    {
        if (!OperatingSystem.IsWindows()) return;
        var held = Path.Combine(HarnessSettings.ProfileHome(Home, Agent, "account-1"), ".credentials.json");
        string said;
        using (new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Equal(0, AccountIdsCommand.Run(["--plan"], Home, null, null, out said));
        }

        Assert.Contains($"(pid {Environment.ProcessId})", said);
        Assert.Contains("not moved: held by ", said);
        Assert.Contains("the next `daoris home account-ids --yes` moves it once they end", said);
        Assert.StartsWith("home: 3 account ids with ", said);
    }

    /// <summary>🔴 The native host's manifest follows its moved account to the new folder, every other key kept; another is left as it is.</summary>
    [Fact]
    public void The_native_host_manifest_follows_its_account_to_the_moved_folder()
    {
        var old = HarnessSettings.ProfileHome(Home, Agent, "account-1");
        Directory.CreateDirectory(Path.Combine(old, "chrome"));
        File.WriteAllText(Path.Combine(old, "chrome", "chrome-native-host.bat"), "@echo off\n");
        var hosts = Path.Combine(_root, "ChromeNativeHost");
        Directory.CreateDirectory(hosts);
        var ours = Path.Combine(hosts, "com.anthropic.claude_code_browser_extension.json");
        var other = Path.Combine(hosts, "com.example.other.json");
        var elsewhere = Path.Combine(_root, "elsewhere", "chrome-native-host.bat");
        File.WriteAllText(ours, new JsonObject
        {
            ["name"] = "com.anthropic.claude_code_browser_extension",
            ["path"] = Path.Combine(old, "chrome", "chrome-native-host.bat"),
            ["type"] = "stdio",
            ["allowed_origins"] = new JsonArray("chrome-extension://abc/"),
        }.ToJsonString());
        File.WriteAllText(other, new JsonObject { ["path"] = elsewhere }.ToJsonString());
        var before = File.ReadAllText(other);

        var plan = AccountIdMoves.Plan(Home, null, [ours, other]);

        var references = plan.Moves.Single(move => move.Rename.From == "account-1").References;
        Assert.Contains(references, each => each.Place == ours.Replace('\\', '/') && each.Where == "path");
        Assert.Equal(Path.Combine(old, "chrome", "chrome-native-host.bat"), Read(ours)["path"]!.GetValue<string>());

        var done = AccountIdMoves.Apply(Home, null, [ours, other]);

        var to = done.Moves.Single(move => move.Rename.From == "account-1").Rename.To;
        var manifest = Read(ours);
        var moved = Path.Combine(HarnessSettings.ProfileHome(Home, Agent, to), "chrome", "chrome-native-host.bat");
        Assert.Equal(moved, manifest["path"]!.GetValue<string>());
        Assert.True(File.Exists(moved));
        Assert.Equal("stdio", manifest["type"]!.GetValue<string>());
        Assert.Equal("chrome-extension://abc/", manifest["allowed_origins"]![0]!.GetValue<string>());
        Assert.Equal(before, File.ReadAllText(other));
        Assert.True(AccountIdMoves.Plan(Home, null, [ours, other]).Nothing);
    }

    private void Write(HomeEntry entry, string json) => File.WriteAllText(DaorisHome.Entry(Home, entry), json);

    private JsonObject Read(HomeEntry entry) => Read(DaorisHome.Entry(Home, entry));

    private static JsonObject Read(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private static void Store(string path, string schema, string rows)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = schema + rows;
        command.ExecuteNonQuery();
    }

    private static List<(string, string)> Rows(string path, string query)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False;Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = query;
        using var reader = command.ExecuteReader();
        var rows = new List<(string, string)>();
        while (reader.Read()) rows.Add((reader.GetString(0), reader.GetString(1)));
        return rows;
    }

    /// <summary>Every file under the install with its bytes, and every folder: what a plan or a second apply must leave as it found.</summary>
    private SortedDictionary<string, string> Snapshot()
    {
        SqliteConnection.ClearAllPools();
        var seen = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var folder in Directory.EnumerateDirectories(Install, "*", SearchOption.AllDirectories)) seen[folder] = "folder";
        foreach (var file in Directory.EnumerateFiles(Install, "*", SearchOption.AllDirectories))
        {
            seen[file] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)));
        }

        return seen;
    }
}
