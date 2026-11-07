using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <c>ROLES</c>, the catalogue as Agents → <i>Roles</i> shows it, and
/// <c>SET_ROLE</c>, the person's press that adds, changes, removes or gives back a working role through the role door, whose judge
/// refuses with nothing written.
/// </summary>
public sealed class DriverModuleRoleTests : DriverModuleBridge
{
    [Fact]
    public async Task A_role_added_is_read_back_with_its_staffing_and_removed_and_the_judge_refuses_at_the_press()
    {
        File.WriteAllText(DriverConfigPath, """{ "adapter": "claude-code", "intakeAdapter": "claude-code" }""");
        var staffing = RoleStaffing.Wire(Home, () => StoreSettled.Yes);
        try
        {
            Assert.Equal(StaffingImported.Done, staffing.ImportFile().How);
            var module = Module();
            // The seeded Design role takes design machine-wide; the cases below add roles that do, so it is removed first.
            await AnswerAsync(module, "SET_ROLE", new { change = "remove", id = "design" });

            await AnswerAsync(module, "SET_ROLE", new
            {
                change = "set", id = "designer",
                role = new { name = new { en = "Designer", zh = "设计师" }, takes = new[] { "design" }, scope = new { kind = "machine" } },
                choices = new object[] { new { agent = "claude-code", model = "opus", effort = "high" } },
            });

            var answer = await AnswerAsync(module, "ROLES");
            Assert.True(answer.GetProperty("kept").GetBoolean());
            Assert.Contains("design", answer.GetProperty("works").EnumerateArray().Select(work => work.GetString()));
            var designer = answer.GetProperty("roles").EnumerateArray().Single(row => row.GetProperty("id").GetString() == "designer");
            Assert.Equal(("set", "设计师", 1L), (designer.GetProperty("state").GetString(), designer.GetProperty("name").GetProperty("zh").GetString(), designer.GetProperty("revision").GetInt64()));
            Assert.Equal("opus", designer.GetProperty("choices")[0].GetProperty("model").GetString());

            // I18NOPEN1: a role named by one text, or in a third language alone, is saved and answered as written.
            await AnswerAsync(module, "SET_ROLE", new
            {
                change = "set", id = "reader", role = new { name = "Reader", takes = new[] { "design" }, scope = new { kind = "workspace", id = "w1" } },
                choices = new object[] { new { agent = "claude-code", model = "opus", effort = "high" } },
            });
            await AnswerAsync(module, "SET_ROLE", new
            {
                change = "set", id = "lecteur", role = new { name = new { fr = "Lecteur" }, takes = new[] { "design" }, scope = new { kind = "workspace", id = "w2" } },
                choices = new object[] { new { agent = "claude-code", model = "opus", effort = "high" } },
            });
            var named = (await AnswerAsync(module, "ROLES")).GetProperty("roles").EnumerateArray().ToDictionary(row => row.GetProperty("id").GetString()!);
            Assert.Equal(("Reader", JsonValueKind.Object, "Lecteur"), (named["reader"].GetProperty("name").GetString(), named["lecteur"].GetProperty("name").ValueKind, named["lecteur"].GetProperty("name").GetProperty("fr").GetString()));
            await AnswerAsync(module, "SET_ROLE", new { change = "remove", id = "reader", revision = 1 });
            await AnswerAsync(module, "SET_ROLE", new { change = "remove", id = "lecteur", revision = 1 });

            Assert.Contains(
                RoleCatalog.Of([]).Judge(RoleChange.Remove("build"))!.Said,
                await RefusalAsync(module, "SET_ROLE", new { change = "remove", id = "build" }));
            Assert.Contains("changed since", await RefusalAsync(module, "SET_ROLE", new { change = "remove", id = "designer", revision = 7 }));

            await AnswerAsync(module, "SET_ROLE", new { change = "remove", id = "designer", revision = 1 });
            Assert.DoesNotContain((await AnswerAsync(module, "ROLES")).GetProperty("roles").EnumerateArray(), row => row.GetProperty("id").GetString() == "designer");
        }
        finally
        {
            RoleStaffingPort.Unwire(DriverConfigPath);
        }
    }

    /// <summary><c>SET_ROLE</c> <c>default-all</c> gives every seeded role its default and keeps the added roles.</summary>
    [Fact]
    public async Task Restoring_every_default_through_the_press_returns_the_seeded_roles_and_keeps_an_added_one()
    {
        File.WriteAllText(DriverConfigPath, """{ "adapter": "claude-code", "intakeAdapter": "claude-code" }""");
        var staffing = RoleStaffing.Wire(Home, () => StoreSettled.Yes);
        try
        {
            Assert.Equal(StaffingImported.Done, staffing.ImportFile().How);
            var module = Module();
            await AnswerAsync(module, "SET_ROLE", new { change = "remove", id = "design" });
            await AnswerAsync(module, "SET_ROLE", new
            {
                change = "set", id = "designer",
                role = new { name = new { en = "Designer", zh = "设计师" }, takes = new[] { "design" }, scope = new { kind = "machine" } },
                choices = new object[] { new { agent = "claude-code", model = "opus", effort = "high" } },
            });
            await AnswerAsync(module, "SET_ROLE", new { change = "remove", id = "intake" });
            Assert.Equal("removed", (await AnswerAsync(module, "ROLES")).GetProperty("roles").EnumerateArray().Single(row => row.GetProperty("id").GetString() == "intake").GetProperty("state").GetString());

            await AnswerAsync(module, "SET_ROLE", new { change = "default-all" });

            var rows = (await AnswerAsync(module, "ROLES")).GetProperty("roles").EnumerateArray().ToList();
            Assert.Equal("seed", rows.Single(row => row.GetProperty("id").GetString() == "intake").GetProperty("state").GetString());
            Assert.Equal("set", rows.Single(row => row.GetProperty("id").GetString() == "designer").GetProperty("state").GetString());
        }
        finally
        {
            RoleStaffingPort.Unwire(DriverConfigPath);
        }
    }

    /// <summary>
    /// <c>SET_ROLES</c> makes several role changes as one. A batch with one refused change, or one
    /// made against a revision that moved, refuses with that change's own sentence and writes neither the catalogue nor the staffing; a batch
    /// that is judged whole writes every role with its choices.
    /// </summary>
    [Fact]
    public async Task A_batch_with_one_refused_change_writes_nothing_and_one_judged_whole_writes_every_role()
    {
        File.WriteAllText(DriverConfigPath, """{ "adapter": "claude-code", "intakeAdapter": "claude-code" }""");
        var staffing = RoleStaffing.Wire(Home, () => StoreSettled.Yes);
        try
        {
            Assert.Equal(StaffingImported.Done, staffing.ImportFile().How);
            var module = Module();
            await AnswerAsync(module, "SET_ROLE", new { change = "remove", id = "design" });
            var designer = new
            {
                change = "set", id = "designer", revision = 0,
                role = new { name = new { en = "Designer", zh = "设计师" }, takes = new[] { "design" }, scope = new { kind = "machine" } },
                choices = new object[] { new { agent = "claude-code", model = "opus", effort = "high" } },
            };
            var opinion = new
            {
                change = "set", id = "opinion", revision = 0,
                role = new { name = new { en = "Second opinion", zh = "第二意见" }, takes = new[] { "review" }, scope = new { kind = "machine" } },
                choices = new object[] { new { agent = "claude-code", model = "opus", effort = "high" } },
            };
            var before = (await AnswerAsync(module, "ROLES")).GetProperty("roles").GetRawText();

            Assert.Contains(
                RoleCatalog.Of([]).Judge(RoleChange.Remove("build"))!.Said,
                await RefusalAsync(module, "SET_ROLES", new { changes = new object[] { designer, opinion, new { change = "remove", id = "build" } } }));
            Assert.Contains(
                "changed since", await RefusalAsync(module, "SET_ROLES", new { changes = new object[] { designer, opinion with { revision = 7 } } }));
            Assert.Contains("more than once", await RefusalAsync(module, "SET_ROLES", new { changes = new object[] { opinion, opinion } }));
            Assert.Contains("no role changes", await RefusalAsync(module, "SET_ROLES", new { }));
            Assert.Contains("no role change", await RefusalAsync(module, "SET_ROLES", new { changes = Array.Empty<object>() }));

            Assert.Equal(before, (await AnswerAsync(module, "ROLES")).GetProperty("roles").GetRawText());

            await AnswerAsync(module, "SET_ROLES", new { changes = new object[] { designer, opinion } });

            var roles = (await AnswerAsync(module, "ROLES")).GetProperty("roles").EnumerateArray().ToList();
            var made = roles.Single(row => row.GetProperty("id").GetString() == "designer");
            Assert.Equal(("set", 1L, "opus"), (made.GetProperty("state").GetString(), made.GetProperty("revision").GetInt64(), made.GetProperty("choices")[0].GetProperty("model").GetString()));
            var asked = roles.Single(row => row.GetProperty("id").GetString() == "opinion");
            Assert.Equal(("set", 1L, "opus"), (asked.GetProperty("state").GetString(), asked.GetProperty("revision").GetInt64(), asked.GetProperty("choices")[0].GetProperty("model").GetString()));
        }
        finally
        {
            RoleStaffingPort.Unwire(DriverConfigPath);
        }
    }

    /// <summary><c>ROLES</c> carries each role's last seven days beside its rows; a home with no start lists none.</summary>
    [Fact]
    public async Task The_roles_answer_carries_each_role_s_week_of_starts_and_none_with_no_history()
    {
        File.WriteAllText(DriverConfigPath, """{ "adapter": "claude-code" }""");
        var module = Module();
        var none = (await AnswerAsync(module, "ROLES")).GetProperty("use");
        Assert.Empty(none.GetProperty("roles").EnumerateArray());

        var logs = DaorisHome.Entry(Home, HomeEntry.Logs);
        Directory.CreateDirectory(logs);
        var now = DateTimeOffset.UtcNow;
        File.WriteAllText(
            Path.Combine(logs, now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + ".driver.jsonl"),
            "{\"time\":\"" + now.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture)
            + "\",\"source\":\"driver\",\"level\":\"info\",\"event\":\"actor.chose\",\"data\":{\"actor\":\"build\",\"choice\":\"2\",\"agent\":\"codex\",\"model\":null,\"passed\":[\"1:claude:cooling\"]}}\n");

        var use = (await AnswerAsync(module, "ROLES")).GetProperty("use");
        var build = Assert.Single(use.GetProperty("roles").EnumerateArray().ToList());
        Assert.Equal(("build", 1, 1), (build.GetProperty("role").GetString(), build.GetProperty("starts").GetInt32(), build.GetProperty("fell").GetInt32()));
        Assert.Equal("cooling", build.GetProperty("why")[0].GetProperty("code").GetString());
        Assert.Equal(1, use.GetProperty("days").GetInt32());
    }

    [Fact]
    public async Task A_home_whose_actors_are_the_file_s_own_sets_no_role()
    {
        File.WriteAllText(DriverConfigPath, """{ "adapter": "claude-code" }""");
        var module = Module();

        Assert.False((await AnswerAsync(module, "ROLES")).GetProperty("kept").GetBoolean());
        Assert.Contains("roles are not set here", await RefusalAsync(module, "SET_ROLE", new { change = "remove", id = "intake" }));
    }

    /// <summary>
    /// <c>ROLE_STRUCTURE</c>, each place's role per work as a start finds it, its rule with
    /// where it was set, and each choice's state from the last readings alone, by name and never a path.
    /// </summary>
    [Fact]
    public async Task The_structure_answers_each_place_s_role_rule_and_choices_by_name_alone()
    {
        File.WriteAllText(DriverConfigPath, """
            { "adapter": "claude-code", "workspaceHelpers": { "aurora": { "mode": "off" } } }
            """);
        var staffing = RoleStaffing.Wire(Home, () => StoreSettled.Yes);
        try
        {
            Assert.Equal(StaffingImported.Done, staffing.ImportFile().How);
            var module = Module();
            await AnswerAsync(module, "SET_ROLE", new
            {
                change = "set", id = "engine-build",
                role = new { name = new { en = "Engine builder", zh = "引擎开发" }, takes = new[] { "build" }, scope = new { kind = "repository", id = "engine" } },
                choices = new object[] { new { agent = "claude-code", model = "opus", effort = "high" } },
            });

            var answer = await AnswerAsync(module, "ROLE_STRUCTURE", new
            {
                places = new object[] { new { }, new { workspace = "aurora", repository = "engine" } },
                works = new[] { "build", "help", "no-such-work" },
            });

            Assert.Contains("design", answer.GetProperty("works").EnumerateArray().Select(work => work.GetString()));
            Assert.Contains(answer.GetProperty("roles").EnumerateArray(), row => row.GetProperty("id").GetString() == "engine-build");
            var places = answer.GetProperty("places").EnumerateArray().ToList();
            Assert.Equal(2, places.Count);
            Assert.Equal(["build", "help"], places[1].GetProperty("works").EnumerateArray().Select(work => work.GetProperty("work").GetString()));
            Assert.Equal(("aurora", "engine"), (places[1].GetProperty("workspace").GetString(), places[1].GetProperty("repository").GetString()));

            var build = places[1].GetProperty("works")[0];
            Assert.Equal(("engine-build", "repository", "engine", 1L), (
                build.GetProperty("role").GetProperty("id").GetString(), build.GetProperty("role").GetProperty("scope").GetProperty("kind").GetString(),
                build.GetProperty("role").GetProperty("scope").GetProperty("id").GetString(), build.GetProperty("role").GetProperty("revision").GetInt64()));
            var choice = Assert.Single(build.GetProperty("choices").EnumerateArray().ToList());
            Assert.Equal(("claude-code", "opus", "high", "1"), (
                choice.GetProperty("agent").GetString(), choice.GetProperty("model").GetString(), choice.GetProperty("effort").GetString(),
                choice.GetProperty("from").GetString()));
            // No account of Daoris's in this home: the start would wait for a sign-in, said with no reading's time.
            Assert.Equal("signed-out", choice.GetProperty("state").GetProperty("code").GetString());
            Assert.Equal(("hand-over", "default"), (
                build.GetProperty("policy").GetProperty("requirement").GetProperty("kind").GetString(), build.GetProperty("policy").GetProperty("source").GetString()));

            var help = places[1].GetProperty("works")[1];
            Assert.Equal(("off", "workspace"), (help.GetProperty("policy").GetProperty("consent").GetString(), help.GetProperty("policy").GetProperty("source").GetString()));
            Assert.Equal("build", places[0].GetProperty("works")[0].GetProperty("role").GetProperty("id").GetString());
            Assert.Contains(answer.GetProperty("agents").EnumerateArray(), agent => agent.GetProperty("agent").GetString() == "claude-code" && agent.GetProperty("known").GetBoolean());

            var raw = answer.GetRawText();
            Assert.DoesNotContain(JsonSerializer.Serialize(Home).Trim('"'), raw);
        }
        finally
        {
            RoleStaffingPort.Unwire(DriverConfigPath);
        }
    }

    /// <summary>Asked for nothing, this machine's every work; a part that does not read is said on its own item.</summary>
    [Fact]
    public async Task Asked_for_nothing_it_answers_this_machine_s_every_work_and_says_a_failure_where_it_is()
    {
        File.WriteAllText(DriverConfigPath, """{ "adapter": "claude-code", "actors": { "build": [ { "agent": "claude-code" } ], "intake": "nope" } }""");

        var answer = await AnswerAsync(Module(), "ROLE_STRUCTURE");

        var place = Assert.Single(answer.GetProperty("places").EnumerateArray().ToList());
        Assert.True(None(place, "workspace") && None(place, "repository"));
        var works = place.GetProperty("works").EnumerateArray().ToList();
        Assert.Equal(RoleWork.Codes, works.Select(work => work.GetProperty("work").GetString()));
        var intake = works.Single(work => work.GetProperty("work").GetString() == "intake");
        Assert.Contains("actors.intake", intake.GetProperty("choicesFailed").GetString());
        Assert.Contains("actors.intake", intake.GetProperty("policyFailed").GetString());
        Assert.True(None(intake, "policy"));
        var build = works.Single(work => work.GetProperty("work").GetString() == "build");
        Assert.Equal("claude-code", build.GetProperty("choices")[0].GetProperty("agent").GetString());
    }

    /// <summary>A field the answer leaves none: absent where the bridge drops a null, null where it keeps one.</summary>
    private static bool None(JsonElement answer, string name) => !answer.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null;
}
