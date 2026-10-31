using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// FOUNDATION1y-i (the foundation design §2.7): <c>ROLES</c>, the catalogue as Agents → <i>Roles</i> shows it, and
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
            var suggested = Assert.Single((await AnswerAsync(module, "ROLES")).GetProperty("suggested").EnumerateArray().ToList());
            Assert.Equal(("designer", "opus"), (suggested.GetProperty("id").GetString(), suggested.GetProperty("choices")[0].GetProperty("model").GetString()));

            await AnswerAsync(module, "SET_ROLE", new
            {
                change = "set", id = "designer",
                role = new { name = new { en = "Designer", zh = "设计师" }, takes = new[] { "design" }, scope = new { kind = "machine" } },
                choices = new object[] { new { agent = "claude-code", model = "opus", effort = "high" } },
            });

            var answer = await AnswerAsync(module, "ROLES");
            Assert.True(answer.GetProperty("kept").GetBoolean());
            Assert.Empty(answer.GetProperty("suggested").EnumerateArray());
            Assert.Contains("design", answer.GetProperty("works").EnumerateArray().Select(work => work.GetString()));
            var designer = answer.GetProperty("roles").EnumerateArray().Single(row => row.GetProperty("id").GetString() == "designer");
            Assert.Equal(("set", "设计师", 1L), (designer.GetProperty("state").GetString(), designer.GetProperty("nameZh").GetString(), designer.GetProperty("revision").GetInt64()));
            Assert.Equal("opus", designer.GetProperty("choices")[0].GetProperty("model").GetString());

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

    [Fact]
    public async Task A_home_whose_actors_are_the_file_s_own_sets_no_role()
    {
        File.WriteAllText(DriverConfigPath, """{ "adapter": "claude-code" }""");
        var module = Module();

        Assert.False((await AnswerAsync(module, "ROLES")).GetProperty("kept").GetBoolean());
        Assert.Contains("roles are not set here", await RefusalAsync(module, "SET_ROLE", new { change = "remove", id = "intake" }));
    }

    /// <summary>
    /// COMPANYVIEW1c (the company-view design §4): <c>ROLE_STRUCTURE</c>, each place's role per work as a start finds it, its rule with
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

    /// <summary>COMPANYVIEW1c: asked for nothing, this machine's every work; a part that does not read is said on its own item.</summary>
    [Fact]
    public async Task Asked_for_nothing_it_answers_this_machine_s_every_work_and_says_a_failure_where_it_is()
    {
        File.WriteAllText(DriverConfigPath, """{ "adapter": "claude-code", "actors": { "intake": "nope" } }""");

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
