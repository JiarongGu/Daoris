using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// FOUNDATION1y-i (the foundation design §2.7): <c>ROLES</c>, the catalogue as Settings → AI features → <i>Roles</i> shows it, and
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
}
