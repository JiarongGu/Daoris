using System.Text.Json.Nodes;
using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// NOCOMPAT1 (D232 point 2): the release's one migration converts an older home once at its first start, a copy of each file it changes in
/// the salvage folder, each step recorded as it finishes, so a second start runs nothing and a stopped one runs only what is left.
/// </summary>
public sealed class HomeMigrationTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "home-migration", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Put(HomeEntry entry, string text, params string[] named)
    {
        var path = DaorisHome.Entry(_home, entry, named);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private JsonObject Read(string path) => (JsonObject)JsonNode.Parse(File.ReadAllText(path))!;

    private string Copy(string path) =>
        Path.Combine(DaorisHome.Entry(_home, HomeEntry.Salvage, HomeMigration.Name), Path.GetRelativePath(DaorisHome.FolderOf(_home).Folder, path));

    private IReadOnlyList<string> Recorded() =>
        [.. Read(DaorisHome.Entry(_home, HomeEntry.Migration))["steps"]!.AsArray().Select(each => each!.GetValue<string>())];

    [Fact]
    public void Every_step_has_a_name_of_its_own()
    {
        Assert.Equal(HomeMigration.Steps.Count, HomeMigration.Steps.Select(step => step.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void A_fresh_home_records_every_step_and_a_second_start_runs_none()
    {
        Directory.CreateDirectory(_home);
        var first = HomeMigration.Run(_home);
        Assert.Equal(HomeMigration.Steps.Select(step => step.Name), first.Ran);
        Assert.Equal(HomeMigration.Steps.Select(step => step.Name), Recorded());

        var again = HomeMigration.Run(_home);
        Assert.Empty(again.Ran);
        Assert.False(again.Any);
    }

    [Fact]
    public void The_old_keys_are_converted_and_the_file_as_it_was_is_kept()
    {
        const string Old = """{"adapter":"codex","intakeAdapter":"claude-code","strikes":4,"gitMoved":{"at":"2026-11-01T00:00:00Z"},"timeoutMinutes":90,"cap":2}""";
        var file = Put(HomeEntry.DriverConfig, Old);

        var migrated = HomeMigration.Run(_home);

        Assert.Null(migrated.Failed);
        var root = Read(file);
        foreach (var key in new[] { "adapter", "intakeAdapter", "strikes", "gitMoved", "timeoutMinutes" }) Assert.False(root.ContainsKey(key), key);
        Assert.Equal(2, (int)root["cap"]!);
        Assert.Equal(Old, File.ReadAllText(Copy(file)));
        Assert.Contains("data/driver.json", migrated.Changed);

        var rows = new RoleStaffing(_home, () => StoreSettled.Yes).Read()!;
        Assert.Equal("codex", rows.Actors["build"].Single().Agent);
        Assert.Equal("codex", rows.Actors["chat"].Single().Agent);
        Assert.Equal("claude-code", rows.Actors["intake"].Single().Agent);
        Assert.Empty(rows.Actors["ask-daoris"]);

        var rules = new SupervisorRuleStore(_home).Of(SupervisorRules.Machine).Rules;
        Assert.Equal(2, rules.Single(rule => rule.Count == SupervisorRules.AnswersAlone).At);
        Assert.Equal(4, rules.Single(rule => rule.Count == SupervisorRules.Failures).At);
        Assert.Equal(TransientRetry.Bound, rules.Single(rule => rule.Count == SupervisorRules.Tries).At);
        Assert.Equal(SupervisorRuleBook.MovesByDefault, rules.Single(rule => rule.Count == SupervisorRules.Moves).At);
    }

    [Fact]
    public void The_helper_key_is_converted_to_Ask_Daoris_s_order_and_taken_out_once()
    {
        var file = Put(HomeEntry.DriverConfig, """{"adapter":"codex","helperAdapter":"claude-code-acp","cap":3}""");

        var migrated = HomeMigration.Run(_home);

        Assert.Null(migrated.Failed);
        var root = Read(file);
        foreach (var key in new[] { "adapter", "intakeAdapter", "helperAdapter" }) Assert.False(root.ContainsKey(key), key);
        Assert.Equal(3, (int)root["cap"]!);
        var rows = new RoleStaffing(_home, () => StoreSettled.Yes).Read()!;
        Assert.Equal("claude-code-acp", rows.Actors["ask-daoris"].Single().Agent);
        Assert.Empty(rows.Actors["intake"]);

        Assert.DoesNotContain("data/driver.json", HomeMigration.Run(_home).Changed);
    }

    [Fact]
    public void The_landing_rules_are_taken_out_of_driver_json_once()
    {
        const string Old = """{"landings":{"engine":{"form":"branch","pattern":"feature/{quest}"}},"workspaceLandings":{"default":{"form":"merge"}},"cap":2}""";
        var file = Put(HomeEntry.DriverConfig, Old);

        var migrated = HomeMigration.Run(_home);

        Assert.Null(migrated.Failed);
        var root = Read(file);
        Assert.False(root.ContainsKey("landings"));
        Assert.False(root.ContainsKey("workspaceLandings"));
        Assert.Equal(2, (int)root["cap"]!);
        Assert.Equal(Old, File.ReadAllText(Copy(file)));
        Assert.Contains("data/driver.json", migrated.Changed);

        var again = HomeMigration.Run(_home);
        Assert.Empty(again.Ran);
        Assert.DoesNotContain("data/driver.json", again.Changed);
    }

    [Fact]
    public void A_stopped_run_records_the_steps_before_it_and_the_next_start_runs_the_rest()
    {
        Directory.CreateDirectory(_home);
        var ran = new List<string>();
        var failing = true;
        IReadOnlyList<IHomeMigrationStep> steps = [new Step("one", ran, () => false), new Step("two", ran, () => failing), new Step("three", ran, () => false)];

        var stopped = HomeMigration.Run(_home, steps);
        Assert.Equal(new[] { "one" }, stopped.Ran);
        Assert.Equal("two", stopped.Failed?.Step);
        Assert.Equal(new[] { "one" }, Recorded());

        failing = false;
        var finished = HomeMigration.Run(_home, steps);
        Assert.Equal(new[] { "two", "three" }, finished.Ran);
        Assert.Equal(new[] { "one", "two", "two", "three" }, ran);
    }

    [Fact]
    public void A_held_home_is_converted_by_the_next_start()
    {
        Directory.CreateDirectory(_home);
        using var held = HomeLease.Await(_home, LeaseKind.Verb, "test", TimeSpan.Zero, out _);
        Assert.NotNull(held);
        var migrated = HomeMigration.Run(_home, leaseWait: TimeSpan.Zero);
        Assert.NotNull(migrated.Held);
        Assert.Empty(migrated.Ran);
        Assert.False(File.Exists(DaorisHome.Entry(_home, HomeEntry.Migration)));
    }

    [Fact]
    public async Task A_home_the_launchers_swap_holds_is_converted_once_the_swap_lets_go()
    {
        Directory.CreateDirectory(_home);
        var swap = HomeLease.Await(_home, LeaseKind.Swap, null, TimeSpan.Zero, out _);
        Assert.NotNull(swap);

        var first = HomeMigration.Run(_home, leaseWait: TimeSpan.FromSeconds(30));
        Assert.NotNull(first.Held);
        Assert.Empty(first.Ran);
        Assert.False(File.Exists(DaorisHome.Entry(_home, HomeEntry.Migration)));

        swap.Dispose();
        var later = await HomeMigration.RunAfterSwapAsync(_home, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(20));
        Assert.Null(later.Held);
        Assert.NotEmpty(later.Ran);
        Assert.True(File.Exists(DaorisHome.Entry(_home, HomeEntry.Migration)));
    }

    [Fact]
    public async Task A_swap_that_never_lets_go_leaves_the_home_unconverted()
    {
        Directory.CreateDirectory(_home);
        using var swap = HomeLease.Await(_home, LeaseKind.Swap, null, TimeSpan.Zero, out _);
        Assert.NotNull(swap);

        var later = await HomeMigration.RunAfterSwapAsync(_home, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(20));
        Assert.NotNull(later.Held);
        Assert.Empty(later.Ran);
        Assert.False(File.Exists(DaorisHome.Entry(_home, HomeEntry.Migration)));
    }

    [Fact]
    public void A_waiting_say_with_now_alone_waits_with_its_level()
    {
        var path = Put(HomeEntry.Requests, """{"session":"s1","move":"say","now":true}""", "r1.json");
        HomeMigration.Run(_home);
        var root = Read(path);
        Assert.False(root.ContainsKey("now"));
        Assert.Equal(MessageLevels.Now, (string?)root["level"]);
    }

    [Fact]
    public void An_unkeyed_attempt_is_aged_past_the_horizon_and_keyed_goes()
    {
        var now = DateTimeOffset.UtcNow;
        var unkeyed = Put(HomeEntry.Helpers, $$"""{"id":"a1","state":"answered","attempted":"{{Moments.Write(now)}}"}""", "a1.state.json");
        var keyed = Put(HomeEntry.Partners, $$"""{"id":"p1","to":"s","key":"k","outcome":"attempted","at":"{{Moments.Write(now)}}","keyed":true}""", "p1.delivery.json");

        HomeMigration.Run(_home);

        Assert.Equal(SolicitedStep.Uncertain, SolicitedHand.Before(Moments.Read((string?)Read(unkeyed)["attempted"]), now));
        var receipt = Read(keyed);
        Assert.False(receipt.ContainsKey("keyed"));
        Assert.Equal(Moments.Write(now), (string?)receipt["at"]);
    }

    [Fact]
    public void A_held_press_with_no_question_is_taken_out()
    {
        var path = Put(HomeEntry.Sessions, """{"held":[{"session":"s","text":"yes","door":"page","choice":"yes"},{"session":"s","text":"hi","door":"page"}]}""", HeldWordsFile.FileName);
        HomeMigration.Run(_home);
        var held = Read(path)["held"]!.AsArray();
        Assert.Equal("hi", (string?)Assert.Single(held)!["text"]);
    }

    [Fact]
    public void The_old_harness_names_are_written_as_the_switch_and_curve()
    {
        var path = Put(HomeEntry.Harnesses, """{"rotationUse":{"claude-code":{"early":false,"prefer":"x"}}}""");
        HomeMigration.Run(_home);
        var text = File.ReadAllText(path);
        Assert.DoesNotContain("\"early\"", text);
        Assert.DoesNotContain("\"prefer\"", text);
        Assert.Contains("\"switch\": \"off\"", text);
    }

    [Fact]
    public void An_event_log_s_old_receipt_note_and_sections_are_written_as_today_s()
    {
        Assert.Equal("""{"seq":1,"kind":"receipt","id":"w1","receipt":"ended"}""", EventLogsStep.Converted("""{"seq":1,"kind":"receipt","id":"w1","receipt":"answered"}"""));
        Assert.Null(EventLogsStep.Converted("""{"seq":2,"kind":"note","parts":[{"code":"chat.told-routing","values":{}}]}"""));
        const string Today = """{"seq":3,"kind":"text","text":"\"answered\""}""";
        Assert.Same(Today, EventLogsStep.Converted(Today));

        var handed = EventLogsStep.Converted(
            """{"seq":4,"kind":"user","account":{"chars":30,"sections":[{"name":"quest","source":"quest","said":"q","chars":10},{"name":"map","source":"repository","said":"m","chars":12},{"name":"look","source":"repository","said":"l","chars":8},{"name":"indexes","source":"repository","said":"i","chars":0,"none":"absent"}]}}""");
        var sections = JsonNode.Parse(handed!)!["account"]!["sections"]!.AsArray();
        Assert.Equal(new[] { "quest", HandedSections.Block }, sections.Select(each => (string?)each!["name"]));
        Assert.Equal(20, (int)sections[1]!["chars"]!);
    }

    [Fact]
    public void A_session_record_s_old_answer_is_its_first_said_word()
    {
        var path = DaorisHome.Entry(_home, HomeEntry.Knowledge);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE sessions (id TEXT PRIMARY KEY, origin TEXT NULL, answer TEXT NULL, said TEXT NULL, updated TEXT NOT NULL);
                INSERT INTO sessions (id, answer, updated) VALUES ('old', 'go on', '2026-11-01T00:00:00Z');
                INSERT INTO sessions (id, said, updated) VALUES ('new', '[]', '2026-11-02T00:00:00Z');
                """;
            command.ExecuteNonQuery();
        }

        HomeMigration.Run(_home);

        using var read = new SqliteConnection($"Data Source={path};Pooling=False");
        read.Open();
        using var query = read.CreateCommand();
        query.CommandText = "SELECT said, answer FROM sessions WHERE id = 'old'";
        using var row = query.ExecuteReader();
        Assert.True(row.Read());
        var word = Assert.Single(JsonNode.Parse(row.GetString(0))!.AsArray())!;
        Assert.Equal(("answer", "go on", "2026-11-01T00:00:00Z"), ((string?)word["id"], (string?)word["text"], (string?)word["at"]));
        Assert.True(row.IsDBNull(1));
    }

    [Fact]
    public void The_workflow_step_reads_the_repositories_from_the_services_registry_table()
    {
        var path = DaorisHome.Entry(_home, HomeEntry.Knowledge);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE registrations (repository TEXT PRIMARY KEY, root TEXT NULL, workspace TEXT NOT NULL DEFAULT 'default', adopted INTEGER NOT NULL DEFAULT 1);
                INSERT INTO registrations (repository, workspace) VALUES ('engine', 'games');
                """;
            command.ExecuteNonQuery();
        }

        var migrated = HomeMigration.Run(_home);

        Assert.Null(migrated.Failed);
        Assert.Contains("workflows", migrated.Ran);
    }

    /// <summary>
    /// ONEWAY1x: closed six-character quests are cleared. One a remote numbered is tombstoned, its log kept with a <c>deleted</c> after the
    /// machine's highest sequence; one never numbered goes whole; an open one, a closed twelve-character one and another quest's log stay.
    /// </summary>
    [Fact]
    public void Closed_six_character_quests_are_cleared_and_the_ones_that_travelled_are_tombstoned()
    {
        var path = DaorisHome.Entry(_home, HomeEntry.Knowledge);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE quests (id TEXT PRIMARY KEY, status TEXT NOT NULL);
                CREATE TABLE quest_log (position INTEGER PRIMARY KEY, quest TEXT NOT NULL, kind TEXT NOT NULL, machine TEXT NOT NULL, sequence INTEGER NOT NULL, at TEXT NOT NULL, payload TEXT NOT NULL, remote INTEGER NULL, UNIQUE (machine, sequence));
                CREATE TABLE quest_forwards (quest TEXT NOT NULL);
                CREATE TABLE quest_machine (one INTEGER PRIMARY KEY CHECK (one = 1), id TEXT NOT NULL, sequence INTEGER NOT NULL DEFAULT 0);
                INSERT INTO quest_machine (one, id, sequence) VALUES (1, 'm1', 4);
                INSERT INTO quests VALUES ('aaaaaa', 'Done'), ('bbbbbb', 'Declined'), ('cccccc', 'Open'), ('dddddddddddd', 'Done');
                INSERT INTO quest_forwards VALUES ('bbbbbb');
                INSERT INTO quest_log (quest, kind, machine, sequence, at, payload, remote) VALUES
                  ('aaaaaa', 'published', 'm1', 1, '2026-01-01T00:00:00Z', '{}', 1),
                  ('aaaaaa', 'done', 'm1', 2, '2026-01-02T00:00:00Z', '{}', 2),
                  ('bbbbbb', 'published', 'm1', 3, '2026-01-01T00:00:00Z', '{}', NULL),
                  ('cccccc', 'published', 'm1', 4, '2026-01-01T00:00:00Z', '{}', NULL);
                """;
            command.ExecuteNonQuery();
        }

        var migrated = HomeMigration.Run(_home);

        Assert.Null(migrated.Failed);
        Assert.Contains("closed-quests", migrated.Ran);
        using var read = new SqliteConnection($"Data Source={path};Pooling=False");
        read.Open();
        Assert.Equal(["cccccc", "dddddddddddd"], Column(read, "SELECT id FROM quests ORDER BY id"));
        Assert.Equal(["aaaaaa:published", "aaaaaa:done", "cccccc:published", "aaaaaa:deleted"], Column(read, "SELECT quest || ':' || kind FROM quest_log ORDER BY position"));
        Assert.Equal(["5"], Column(read, "SELECT sequence FROM quest_log WHERE kind = 'deleted'"));
        Assert.Empty(Column(read, "SELECT quest FROM quest_forwards"));
    }

    /// <summary>ONEWAY1ac: a finished record whose note has no parts loses the note; one with parts, and one still in use, keep it.</summary>
    [Fact]
    public void A_finished_record_s_note_with_no_parts_is_cleared()
    {
        var path = DaorisHome.Entry(_home, HomeEntry.Knowledge);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE sessions (id TEXT PRIMARY KEY, state TEXT NOT NULL, note TEXT NULL, note_parts TEXT NULL, updated TEXT NOT NULL);
                INSERT INTO sessions VALUES
                  ('old-done', 'Completed', 'The quest reached done.', NULL, '2026-01-01T00:00:00Z'),
                  ('old-failed', 'Failed', 'It exited.', '[]', '2026-01-01T00:00:00Z'),
                  ('coded', 'Failed', 'It exited.', '[{"code":"ended.exit","values":{}}]', '2026-01-01T00:00:00Z'),
                  ('parked', 'AwaitingPerson', 'It stopped to ask you.', NULL, '2026-01-01T00:00:00Z');
                """;
            command.ExecuteNonQuery();
        }

        var migrated = HomeMigration.Run(_home);

        Assert.Null(migrated.Failed);
        Assert.Contains("note-records", migrated.Ran);
        using var read = new SqliteConnection($"Data Source={path};Pooling=False");
        read.Open();
        Assert.Equal(["coded:It exited.", "old-done:", "old-failed:", "parked:It stopped to ask you."], Column(read, "SELECT id || ':' || COALESCE(note, '') FROM sessions ORDER BY id"));
    }

    private static List<string> Column(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var found = new List<string>();
        while (reader.Read()) found.Add(Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!);
        return found;
    }

    private sealed class Step(string name, List<string> ran, Func<bool> fails) : IHomeMigrationStep
    {
        public string Name => name;

        public void Run(HomeMigrationRun run)
        {
            ran.Add(name);
            if (fails()) throw new IOException("stopped");
        }
    }
}
