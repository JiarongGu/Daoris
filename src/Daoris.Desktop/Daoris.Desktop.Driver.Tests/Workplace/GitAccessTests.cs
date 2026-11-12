using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The tables: the keys and their resolution at
/// each level with the default's two doors, the judge by value, an edit's widenings, the go-ahead held at a door and its late grant, the
/// rules composed for every value, and the terminal's verbs.
/// </summary>
public sealed class GitAccessTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-git-access-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "data");

    public GitAccessTests()
    {
        Directory.CreateDirectory(Home);
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
    }

    public void Dispose() => GitSeed.Discard(_root);

    private void Put(string json)
    {
        var path = DaorisHome.Entry(Home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private JsonObject Driver() => (JsonObject)JsonNode.Parse(File.ReadAllText(DaorisHome.Entry(Home, HomeEntry.DriverConfig)))!;

    private static string Show(GitAccess access) => string.Join(" ", access.Classes.Select(each => $"{each.Class}={each.Value}@{each.Source}"));

    // ── Resolution (§6.2) ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Nothing_set_is_daoris_default_door_by_door()
    {
        var access = GitAccessKeys.Resolve(Home, "engine", "Games");

        Assert.Equal("read=allow@default write=as-today@default dangerous=deny@default", Show(access));
        Assert.True(access.Default);
    }

    [Fact]
    public void Each_class_resolves_on_its_own_from_the_nearest_level()
    {
        Put("""
            {"machineGitAccess":{"read":"ask","write":"deny"},
             "workspaceGitAccess":{"Games":{"write":"ask"}},
             "gitAccess":{"engine":{"read":"allow"}}}
            """);

        Assert.Equal("read=allow@repository write=ask@workspace dangerous=deny@default", Show(GitAccessKeys.Resolve(Home, "engine", "Games")));
        Assert.Equal("read=ask@machine write=ask@workspace dangerous=deny@default", Show(GitAccessKeys.Resolve(Home, "other", "Games")));
        Assert.Equal("read=ask@machine write=deny@machine dangerous=deny@default", Show(GitAccessKeys.Resolve(Home, "other", "Elsewhere")));
        // A repository in no workspace is in `default`, and names match in any case.
        Assert.Equal("read=allow@repository write=deny@machine dangerous=deny@default", Show(GitAccessKeys.Resolve(Home, "ENGINE", null)));
    }

    [Fact]
    public void A_class_or_value_this_build_does_not_know_is_passed_over_and_never_reads_as_looser()
    {
        Put("""{"machineGitAccess":{"read":"maybe","write":"ask","future":"allow"},"gitAccess":{"engine":"full"}}""");

        Assert.Equal("read=allow@default write=ask@machine dangerous=deny@default", Show(GitAccessKeys.Resolve(Home, "engine", null)));
    }

    // ── The judge, by value (§6.5) ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(GitAct.Read, "allow", false, true, "access")]
    [InlineData(GitAct.Read, "deny", false, false, "access-denied")]
    [InlineData(GitAct.Read, "ask", false, false, "ask")]
    [InlineData(GitAct.Write, "allow", false, true, "access")]
    [InlineData(GitAct.Write, "deny", true, false, "access-denied")]
    [InlineData(GitAct.Write, "ask", false, false, "ask")]
    [InlineData(GitAct.Write, "ask", true, true, "granted")]
    [InlineData(GitAct.Write, "as-today", false, false, "denied")]
    [InlineData(GitAct.Write, "as-today", true, true, "granted")]
    public void The_judge_answers_the_value_a_class_resolved_to(GitAct act, string value, bool granted, bool allowed, string code)
    {
        var verdict = GitAccessRules.Judge(act, value, granted);

        Assert.Equal((allowed, code), (verdict.Allowed, verdict.Code));
        Assert.Equal(code == "ask", verdict.Ask);
    }

    [Fact]
    public void A_read_only_identity_denies_a_write_whatever_the_access_and_the_grant_say_in_its_words()
    {
        var refused = GitAccessRules.Judge(GitAct.Write, "allow", granted: true, readOnlyIdentity: "Deploy");

        Assert.Equal(("read-only", false), (refused.Code, refused.Allowed));
        Assert.Equal(GitAccessRules.ReadOnlyLine("Deploy"), refused.Line);
        Assert.Contains("Deploy is read only on its host", refused.Line, StringComparison.Ordinal);
        Assert.True(GitAccessRules.Judge(GitAct.Read, "allow", granted: false, readOnlyIdentity: "Deploy").Allowed);
    }

    /// <summary>The judge reads git access alone; a permissions file that lists <c>no-push</c> off moves nothing.</summary>
    [Fact]
    public void The_judge_reads_git_access_alone_and_no_push_off_in_the_rules_file_lets_no_push_through()
    {
        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.Permissions), """{"defaultsOff":["no-push"]}""");

        var value = GitAccessKeys.Resolve(Home, null, null).Of(GitAccessKeys.Write).Value;
        var verdict = GitAccessRules.Judge(GitAct.Write, value, granted: false);

        Assert.Equal(GitAccessKeys.AsToday, value);
        Assert.Equal(("denied", false), (verdict.Code, verdict.Allowed));
    }

    // ── An edit (§6.2, §8) ──────────────────────────────────────────────────────────────────────────

    private static GitAccessEdit Set(string repository, params (string Class, string? Value)[] pairs) =>
        new() { Repository = repository, Set = [.. pairs.Select(each => new KeyValuePair<string, string?>(each.Class, each.Value))] };

    [Fact]
    public void An_edit_writes_only_its_key_paths_and_keeps_every_other_key_as_written()
    {
        Put("""{"gitMoved":{"at":"2026-11-02T00:00:00Z"},"gitAccess":{"engine":{"read":"ask","future":"x"}},"access":{"engine":"full"}}""");

        GitAccessActs.Apply(Home, Set("Engine", ("write", "ask"), ("read", null)));

        var root = Driver();
        Assert.Equal("ask", (string?)root["gitAccess"]!["engine"]!["write"]);
        Assert.Null(root["gitAccess"]!["engine"]!["read"]);
        Assert.Equal("x", (string?)root["gitAccess"]!["engine"]!["future"]);
        Assert.NotNull(root["gitMoved"]);
        Assert.Equal("full", (string?)root["access"]!["engine"]);
        Assert.Null(root["gitAccess"]!["Engine"]);
    }

    [Fact]
    public void An_edit_that_leaves_a_level_naming_nothing_takes_the_level_and_then_the_key_away()
    {
        Put("""{"gitAccess":{"engine":{"write":"ask"}},"machineGitAccess":{"read":"deny"}}""");

        GitAccessActs.Apply(Home, new GitAccessEdit { Repository = "engine", Clear = true });
        GitAccessActs.Apply(Home, new GitAccessEdit { Machine = true, Set = [new("read", "")] });

        Assert.Null(Driver()["gitAccess"]);
        Assert.Null(Driver()["machineGitAccess"]);
    }

    [Fact]
    public void An_edit_is_refused_whole_for_no_level_two_levels_not_one_change_dangerous_or_a_word_there_is_not()
    {
        Assert.Contains("name exactly one", Assert.Throws<DriverException>(() => GitAccessActs.Apply(Home, new GitAccessEdit { Set = [new("read", "ask")] })).Message);
        Assert.Contains("name exactly one", Assert.Throws<DriverException>(() => GitAccessActs.Apply(Home, new GitAccessEdit { Repository = "a", Machine = true, Clear = true })).Message);
        Assert.Contains("one change at a time", Assert.Throws<DriverException>(() => GitAccessActs.Apply(Home, new GitAccessEdit { Repository = "a", Clear = true, OneValue = "ask" })).Message);
        Assert.Contains("dangerous-no-ask", Assert.Throws<DriverException>(() => GitAccessActs.Apply(Home, Set("a", ("dangerous", "ask")))).Message);
        Assert.Contains("not a class", Assert.Throws<DriverException>(() => GitAccessActs.Apply(Home, Set("a", ("push", "allow")))).Message);
        Assert.Contains("not `sometimes`", Assert.Throws<DriverException>(() => GitAccessActs.Apply(Home, Set("a", ("read", "sometimes")))).Message);
        Assert.False(File.Exists(DaorisHome.Entry(Home, HomeEntry.DriverConfig)));
    }

    [Fact]
    public void One_value_for_both_sets_read_and_write_together()
    {
        GitAccessActs.Apply(Home, new GitAccessEdit { Machine = true, OneValue = "ASK" });

        Assert.Equal("read=ask@machine write=ask@machine dangerous=deny@default", Show(GitAccessKeys.Resolve(Home, "engine", null)));
    }

    [Theory]
    [InlineData("""{"machineGitAccess":{"write":"deny"}}""", "write=ask", "write")]
    [InlineData("""{"machineGitAccess":{"write":"ask"}}""", "write=deny", "")]
    [InlineData("""{}""", "write=ask", "write")]
    [InlineData("""{}""", "write=deny", "")]
    [InlineData("""{}""", "read=deny", "")]
    [InlineData("""{"machineGitAccess":{"read":"deny"}}""", "read=allow", "read")]
    public void A_class_loosened_is_a_widening_and_a_narrowing_is_not(string file, string change, string widened)
    {
        Put(file);
        var (cls, value) = (change.Split('=')[0], change.Split('=')[1]);

        Assert.Equal(widened, string.Join(",", GitAccessActs.Widened(GitAccessKeys.Load(Home), Set("engine", (cls, value)))));
    }

    [Fact]
    public void Clearing_a_level_where_the_level_above_is_looser_is_a_widening()
    {
        Put("""{"machineGitAccess":{"write":"allow"},"gitAccess":{"engine":{"write":"deny"}}}""");

        Assert.Equal("write", string.Join(",", GitAccessActs.Widened(GitAccessKeys.Load(Home), new GitAccessEdit { Repository = "engine", Clear = true })));
    }

    [Fact]
    public void One_value_for_both_is_a_widening_where_it_loosens()
    {
        Put("""{"machineGitAccess":{"read":"allow","write":"deny"}}""");

        Assert.Equal("write", string.Join(",", GitAccessActs.Widened(GitAccessKeys.Load(Home), new GitAccessEdit { Machine = true, OneValue = "ask" })));
        Assert.Empty(GitAccessActs.Widened(GitAccessKeys.Load(Home), new GitAccessEdit { Machine = true, OneValue = "deny" }).Where(each => each == "write"));
    }

    [Fact]
    public void The_route_spells_an_edit_as_a_level_and_its_classes()
    {
        var set = GitAccessActs.EditOf(JsonDocument.Parse("""{"repository":"engine","access":{"write":"ask","read":null}}""").RootElement);
        var one = GitAccessActs.EditOf(JsonDocument.Parse("""{"machine":true,"oneValue":"ask"}""").RootElement);
        var clear = GitAccessActs.EditOf(JsonDocument.Parse("""{"workspace":"Games"}""").RootElement);

        Assert.Equal(("engine", 2, null), (set.Repository, set.Set!.Count, set.OneValue));
        Assert.Equal((true, "ask"), (one.Machine, one.OneValue));
        Assert.True(clear.Clear);
    }

    // ── The go-ahead held at a door (§6.6) ──────────────────────────────────────────────────────────

    private sealed class Clock
    {
        public DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public void A_door_holds_the_act_until_the_person_allows_it()
    {
        var clock = new Clock();
        var allowed = GitAsks.Hold(Home, "engine", "Games", "origin", "github.com", "write", "s1", "github.com/o/engine", () => clock.Now, _ =>
        {
            var ask = Assert.Single(GitAsks.List(Home, clock.Now));
            Assert.False(ask.Gone);
            Assert.Equal(("engine", "origin", "write", "s1"), (ask.Repository, ask.Remote, ask.Class, ask.Session));
            Assert.True(GitAsks.Answer(Home, ask.Id, allow: true, clock.Now));
        });

        Assert.True(allowed);
        Assert.Empty(GitAsks.List(Home, clock.Now));
    }

    [Fact]
    public void A_refusal_or_an_unanswered_ask_refuses_the_act_and_leaves_nothing()
    {
        var clock = new Clock();
        Assert.False(GitAsks.Hold(Home, "engine", null, "origin", "h", "write", "s1", "h/o/e", () => clock.Now,
            _ => GitAsks.Answer(Home, GitAsks.List(Home, clock.Now)[0].Id, allow: false, clock.Now)));
        Assert.False(GitAsks.Hold(Home, "engine", null, "origin", "h", "write", "s1", "h/o/e", () => clock.Now, _ => clock.Now += TimeSpan.FromMinutes(6)));

        Assert.Empty(GitAsks.List(Home, clock.Now));
        Assert.Empty(Directory.EnumerateFiles(DaorisHome.Entry(Home, HomeEntry.GitAsks)));
    }

    [Fact]
    public void An_allow_for_an_act_that_has_gone_is_kept_as_a_grant_the_identical_retry_takes_once()
    {
        var clock = new Clock();
        // The door that raised it is gone: a raised go-ahead of a process that no longer runs.
        var id = GitAsks.Raise(Home, "engine", null, "origin", "h", "write", "s1", "h/o/e", clock.Now);
        var path = Path.Combine(DaorisHome.Entry(Home, HomeEntry.GitAsks), id + ".json");
        var record = (JsonObject)JsonNode.Parse(File.ReadAllText(path))!;
        record["pid"] = int.MaxValue;
        File.WriteAllText(path, record.ToJsonString());

        Assert.True(Assert.Single(GitAsks.List(Home, clock.Now)).Gone);
        Assert.False(GitAsks.Answer(Home, id, allow: true, clock.Now));

        Assert.False(GitAsks.TakeGrant(Home, "s2", "h/o/e", "write", clock.Now));
        Assert.False(GitAsks.TakeGrant(Home, "s1", "h/o/e", "read", clock.Now));
        Assert.True(GitAsks.TakeGrant(Home, "s1", "H/o/e", "write", clock.Now));
        Assert.False(GitAsks.TakeGrant(Home, "s1", "h/o/e", "write", clock.Now));
    }

    [Fact]
    public void A_grant_stands_only_within_the_bound_and_a_refusal_keeps_none()
    {
        var clock = new Clock();
        var id = GitAsks.Raise(Home, null, null, null, "h", "write", "s1", "h/o/e", clock.Now);
        var path = Path.Combine(DaorisHome.Entry(Home, HomeEntry.GitAsks), id + ".json");
        var record = (JsonObject)JsonNode.Parse(File.ReadAllText(path))!;
        record["pid"] = int.MaxValue;
        File.WriteAllText(path, record.ToJsonString());
        GitAsks.Answer(Home, id, allow: true, clock.Now);

        Assert.False(GitAsks.TakeGrant(Home, "s1", "h/o/e", "write", clock.Now + TimeSpan.FromMinutes(16)));

        var refused = GitAsks.Raise(Home, null, null, null, "h", "write", "s1", "h/o/e", clock.Now);
        File.WriteAllText(Path.Combine(DaorisHome.Entry(Home, HomeEntry.GitAsks), refused + ".json"), record.ToJsonString().Replace(id, refused));
        GitAsks.Answer(Home, refused, allow: false, clock.Now);
        Assert.False(GitAsks.TakeGrant(Home, "s1", "h/o/e", "write", clock.Now));
    }

    [Fact]
    public void Answering_a_go_ahead_there_is_not_is_refused()
    {
        Assert.Contains("(no-ask)", Assert.Throws<DriverException>(() => GitAsks.Answer(Home, new string('a', 32), true, DateTimeOffset.UtcNow)).Message);
    }

    // ── The rules composed for every value (§6.8) ───────────────────────────────────────────────────

    private static readonly RuleLists Shipped = new([], [], ["Bash(git push)", "Bash(git push:*)", "Bash(git -* push)", "Bash(git -* push *)"]);

    [Theory]
    [InlineData("deny")]
    [InlineData("as-today")]
    public void Deny_and_the_default_leave_the_four_push_forms_denied(string write)
    {
        Assert.Equal(Shipped, GitAccessPolicy.Compose(Shipped, write));
    }

    [Theory]
    [InlineData("ask")]
    [InlineData("allow")]
    public void Ask_and_allow_allow_the_plain_forms_and_deny_the_option_first_and_dangerous_forms(string write)
    {
        var composed = GitAccessPolicy.Compose(Shipped, write);

        Assert.Equal(["Bash(git push)", "Bash(git push:*)"], composed.Allow);
        Assert.DoesNotContain("Bash(git push:*)", composed.Deny);
        Assert.Contains("Bash(git -* push)", composed.Deny);
        Assert.Contains("Bash(git push --force:*)", composed.Deny);
        Assert.Contains("Bash(git push --delete:*)", composed.Deny);
        Assert.Contains("Bash(git push --tags:*)", composed.Deny);
    }

    [Fact]
    public void The_classifier_says_a_push_as_git_access_does_and_publishing_keeps_its_own_sentence()
    {
        var told = PermissionRules.HardDeny(PermissionFile.Empty with { DefaultsOn = [PermissionRules.NoPushId, PermissionRules.NoPublishId] });

        Assert.Equal(told, GitAccessPolicy.HardDeny(told, "deny"));
        var judged = GitAccessPolicy.HardDeny(told, "ask");
        Assert.Contains(GitAccessPolicy.Judged, judged);
        Assert.Contains("Publishing a package or creating a release: these stay the person's.", judged);
        Assert.DoesNotContain(judged, sentence => sentence.StartsWith("Pushing to any remote", StringComparison.Ordinal));
    }

    // ── The terminal (§8) ───────────────────────────────────────────────────────────────────────────

    /// <summary>The presses sent to the running Daoris's window, each its route and payload as JSON.</summary>
    private readonly List<(string Route, string Payload)> _pressed = [];

    private (int Code, string Out, string Err) Run(bool window, string session, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        GitPress press = (route, payload, alone, words) =>
        {
            if (!window) return alone();
            _pressed.Add((route.Route, JsonSerializer.Serialize(payload)));
            return Task.FromResult(words(JsonDocument.Parse("""{"waiting":true}""").RootElement));
        };
        var world = new GitAccessWorld(Home, press, name => name == "DAORIS_SESSION_ID" ? session : null, _ => Task.FromResult<string?>("Games"));
        var code = GitAccessCommand.RunAsync(args, world, output, error).GetAwaiter().GetResult();
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public void Git_access_says_each_class_with_its_source_and_the_default_in_the_landing_s_words()
    {
        GitAccessActs.Apply(Home, new GitAccessEdit { Machine = true, Set = [new("read", "ask")] });

        var (code, text, _) = Run(false, "", "access", "engine");

        Assert.Equal(0, code);
        Assert.Contains("read       ask  (set for the machine)", text);
        Assert.Contains("the landing pushes on your yes; sessions do not push", text);
    }

    [Fact]
    public void Git_access_changes_a_level_and_a_session_changes_none()
    {
        Assert.Equal(0, Run(false, "", "access", "engine", "write=ask").Code);
        Assert.Equal("ask", GitAccessKeys.Resolve(Home, "engine", null).Of("write").Value);

        var (code, _, error) = Run(false, "s9", "access", "engine", "write=allow");
        Assert.Equal(1, code);
        Assert.Contains("git:", error);
        Assert.Equal("ask", GitAccessKeys.Resolve(Home, "engine", null).Of("write").Value);
    }

    /// <summary>
    /// While the window runs, a widening and an answer are pressed there as the page presses them, the
    /// window's confirmation before each, and nothing is done here; a narrowing runs here; with no window each runs here.
    /// </summary>
    [Fact]
    public void While_the_window_runs_a_widening_and_an_answer_are_pressed_there_and_a_narrowing_is_not()
    {
        var (code, text, error) = Run(true, "", "access", "--machine", "write=allow");
        Assert.True(code == 0, error);
        Assert.Contains("confirmed in Daoris's window", text);
        Assert.True(GitAccessKeys.Resolve(Home, "engine", null).Default);
        Assert.Equal(("SET_GIT_ACCESS", """{"machine":true,"access":{"write":"allow"}}"""), Assert.Single(_pressed));

        Assert.Equal(0, Run(true, "", "access", "engine", "--one-value", "allow").Code);
        Assert.Equal("""{"repository":"engine","oneValue":"allow"}""", _pressed[1].Payload);

        Assert.Equal(0, Run(true, "", "access", "--machine", "write=deny").Code);
        Assert.Equal(2, _pressed.Count);
        Assert.Equal("deny", GitAccessKeys.Resolve(Home, "engine", null).Of("write").Value);

        var id = GitAsks.Raise(Home, "engine", null, "origin", "h", "write", null, "h/o/e", DateTimeOffset.UtcNow);
        Assert.Equal(0, Run(true, "", "answer", id, "allow").Code);
        Assert.Equal(("GIT_ANSWER", $$"""{"ask":"{{id}}","answer":"allow"}"""), _pressed[2]);
        Assert.Null(GitAsks.Answered(Home, id));
        Assert.Equal(0, Run(false, "", "answer", id, "refuse").Code);
        Assert.False(GitAsks.Answered(Home, id));
    }

    [Fact]
    public void A_class_named_twice_on_the_command_line_is_taken_as_its_last_value_pressed_and_applied_alike()
    {
        var (code, _, error) = Run(true, "", "access", "--machine", "write=deny", "write=allow");
        Assert.True(code == 0, error);
        Assert.Equal("""{"machine":true,"access":{"write":"allow"}}""", Assert.Single(_pressed).Payload);

        GitSessionAccess.Begin(Home, "s7", "engine", "Games");
        var (sessionCode, _, sessionError) = Run(false, "", "access", "--session", "s7", "write=ask", "write=deny");
        Assert.True(sessionCode == 0, sessionError);
        Assert.Equal("deny", GitSessionAccess.Read(Home, "s7")!.Classes.Single(each => each.Class == "write").Value);
    }

    [Fact]
    public void A_full_access_session_pushes_where_write_is_unset_and_keeps_what_the_person_set()
    {
        GitSessionAccess.Begin(Home, "restricted", "engine", "Games");
        GitSessionAccess.Begin(Home, "full", "engine", "Games", unrestricted: true);

        Assert.Equal("as-today", GitSessionAccess.Held(Home, "restricted", "engine", "Games").Of(GitAccessKeys.Write).Value);
        Assert.Equal("allow", GitSessionAccess.Held(Home, "full", "engine", "Games").Of(GitAccessKeys.Write).Value);
        Assert.Equal("allow", GitSessionAccess.Held(Home, "full", "engine", "Games").Of(GitAccessKeys.Dangerous).Value);
        Assert.Equal("deny", GitSessionAccess.Held(Home, "restricted", "engine", "Games").Of(GitAccessKeys.Dangerous).Value);

        Put("""{"machineGitAccess":{"write":"deny","dangerous":"deny"}}""");
        Assert.Equal("deny", GitSessionAccess.Held(Home, "full", "engine", "Games").Of(GitAccessKeys.Write).Value);
        Assert.Equal("deny", GitSessionAccess.Held(Home, "full", "engine", "Games").Of(GitAccessKeys.Dangerous).Value);
    }

    [Fact]
    public void Git_asks_lists_the_go_aheads_that_wait()
    {
        Assert.Contains("no git go-ahead waits", Run(false, "", "asks").Out);
        var id = GitAsks.Raise(Home, "engine", null, "origin", "h", "write", "s1", "h/o/e", DateTimeOffset.UtcNow);

        var text = Run(false, "", "asks").Out;
        var json = JsonDocument.Parse(Run(false, "", "asks", "--json").Out).RootElement;

        Assert.Contains($"{id}  a push to origin in engine by session s1", text);
        Assert.Equal(id, json[0].GetProperty("id").GetString());
    }

    [Fact]
    public void Dangerous_is_set_from_the_terminal_to_allow_or_deny_and_never_to_ask()
    {
        var (asked, _, error) = Run(false, "", "access", "--machine", "dangerous=ask");
        Assert.Equal(1, asked);
        Assert.Contains("dangerous-no-ask", error);

        var (set, _, _) = Run(false, "", "access", "--machine", "dangerous=allow");
        Assert.Equal(0, set);
        Assert.Equal(GitAccessKeys.Allow, GitAccessKeys.Resolve(Home, "engine", null).Of(GitAccessKeys.Dangerous).Value);
    }
}
