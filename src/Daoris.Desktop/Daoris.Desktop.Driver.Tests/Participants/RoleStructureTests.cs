using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COMPANYVIEW1c (the company-view design §4, row 3): the structure <c>ROLE_STRUCTURE</c> answers, each place's role per work held to the
/// start plan's over one shared table, <c>fixtures/role-structure.json</c>, each work's rule with where it was set, and each choice's state
/// from the last readings alone, which asks no account and no binary and counts no start (D150 point 5).
/// </summary>
/// <remarks>
/// Nothing here starts a process: the agent's binary is a file this test writes, and every question the roster could ask fails the test.
/// </remarks>
public sealed class RoleStructureTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "role-structure", Guid.NewGuid().ToString("N")[..8]);

    private readonly List<string> _asked = [];

    public RoleStructureTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
    }

    public void Dispose() => GitSeed.Remove(_home);

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Command => Path.Combine(_home, "fake-here");

    private sealed class Adapter(string name, HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => name;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    /// <summary>The fake agent, present by a file look, whose every status and usage question is written down and fails.</summary>
    private HarnessRoster Roster() =>
        new(new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = new Adapter("fake", new HarnessToolchain(
                Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true,
                LoginCheck: new LoginQuestion(["auth", "status"], @"""loggedIn""\s*:\s*true", @"""loggedIn""\s*:\s*false"))),
        }), Settings)
        {
            Clock = () => Now,
            Asking = (_, account, _) =>
            {
                lock (_asked) _asked.Add($"status {account}");
                throw new InvalidOperationException($"the structure asked {account}'s sign-in");
            },
            AskingUsage = (_, account, _) =>
            {
                lock (_asked) _asked.Add($"usage {account}");
                throw new InvalidOperationException($"the structure asked {account}'s usage");
            },
        };

    private void Accounts(params string[] names)
    {
        foreach (var name in names) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", name));
    }

    private static JsonElement Table() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "role-structure.json")))
            .RootElement.Clone();

    private static RoleCatalog Catalogue(JsonElement table) =>
        RoleCatalog.Of(table.GetProperty("stored").EnumerateArray()
            .Select(row => new StoredRole(row[0].GetString()!, row[1].GetString()!, row[2].GetString()!, row[3].GetInt64())));

    /// <summary>The table's driver.json, with the store's staffing of each added role, as a home whose roles are the store's loads it.</summary>
    private static DriverConfig Config(JsonElement table)
    {
        var config = DriverConfig.Parse(table.GetProperty("config").GetString()!);
        var actors = new Dictionary<string, IReadOnlyList<ActorChoice>>(config.Actors, StringComparer.Ordinal);
        foreach (var staffed in table.GetProperty("staffing").EnumerateObject())
        {
            actors[staffed.Name] = [.. staffed.Value.EnumerateArray().Select(choice => new ActorChoice(
                choice.GetProperty("agent").GetString()!,
                choice.TryGetProperty("model", out var model) ? model.GetString() : null,
                choice.TryGetProperty("effort", out var effort) ? effort.GetString() : null))];
        }

        return config with { Actors = actors };
    }

    private static IReadOnlyList<RolePlace> Places(JsonElement table) =>
        [.. table.GetProperty("places").EnumerateArray().Select(place => new RolePlace(
            place.TryGetProperty("workspace", out var workspace) ? workspace.GetString() : null,
            place.TryGetProperty("repository", out var repository) ? repository.GetString() : null))];

    private static StructureWork Work(RoleStructure structure, int place, string work) =>
        structure.Places[place].Works.Single(each => each.Work == work);

    [Fact]
    public void Its_role_answers_are_the_start_plan_s_over_the_shared_table()
    {
        var table = Table();
        var catalogue = Catalogue(table);
        var config = Config(table);
        var places = Places(table);
        var roster = Roster();

        var structure = RoleStructure.Of(() => catalogue, config, roster, places, RoleWork.Codes);

        foreach (var row in table.GetProperty("roles").EnumerateArray())
        {
            var work = Work(structure, row[0].GetInt32(), row[1].GetString()!);
            var expected = row[2].GetString();
            Assert.True(
                expected == "held" ? work.Held is not null && work.Role is null : work.Role?.Id == expected,
                $"place {row[0]} {row[1]}: {work.Role?.Id ?? work.Held}, the table says {expected}");
        }

        foreach (var (place, at) in places.Select((place, at) => (place, at)))
        {
            foreach (var code in RoleWork.Codes)
            {
                var work = Work(structure, at, code);
                StartPlan plan;
                try
                {
                    plan = new StartPlan(
                        code, config, roster, roster.Adapters, new PassMemory(), fence: work.Policy?.Agents is { Count: > 0 } agents ? agents : null,
                        catalogue: catalogue, place: place)
                    {
                        Subject = "structure",
                    };
                }
                catch (ActorUnreadableException unreadable)
                {
                    Assert.Equal(unreadable.Message, work.ChoicesFailed);
                    continue;
                }
                catch (DriverException held)
                {
                    Assert.Equal((null, held.Message), (work.Role, work.Held));
                    continue;
                }

                Assert.Null(work.Held);
                Assert.Equal((plan.Role, plan.Revision), (work.Role!.Id, work.Revision));
                Assert.Equal(plan.Order, work.Choices.Select(choice => choice.Choice));
            }
        }
    }

    [Fact]
    public void A_signed_out_account_overdue_for_rereading_reads_signed_out_with_its_time_and_is_not_asked()
    {
        Accounts("work");
        var read = Now - HarnessRoster.SignedOutAskedAgain - TimeSpan.FromHours(1);
        AccountReads.Keep(_home, "fake", "work", LoginState.Out, read);
        var before = File.ReadAllText(AccountReads.PathOf(_home));
        var roster = Roster();
        var config = DriverConfig.Empty with { Adapter = "fake" };

        var build = Work(RoleStructure.Of(() => RoleCatalog.Of([]), config, roster, [new RolePlace()], [RoleWork.Build]), 0, RoleWork.Build);

        var only = Assert.Single(build.Choices);
        Assert.Equal(new ChoiceState(StartPasses.SignedOut, read, "work"), only.State);
        Assert.Empty(_asked);
        Assert.Equal(before, File.ReadAllText(AccountReads.PathOf(_home)));
    }

    [Fact]
    public void Every_choice_has_its_state_not_only_the_first_that_could_run()
    {
        Accounts("work");
        var signedIn = Now.AddMinutes(-20);
        AccountReads.Keep(_home, "fake", "work", LoginState.In, signedIn);
        var refusedAt = Now.AddMinutes(-5);
        Assert.Null(AgentOffers.Refused(_home, "fake", null, "work", "m2", refusedAt));
        var config = DriverConfig.Empty with
        {
            Adapter = "fake",
            Actors = new Dictionary<string, IReadOnlyList<ActorChoice>>(StringComparer.Ordinal)
            {
                ["build"] = [new("gone"), new("fake", "m1", "high"), new("fake", "m2")],
            },
        };

        var build = Work(RoleStructure.Of(() => RoleCatalog.Of([]), config, Roster(), [new RolePlace()], [RoleWork.Build]), 0, RoleWork.Build);

        Assert.Equal(
            [
                new ChoiceState(StartPasses.Unavailable),
                new ChoiceState(ChoiceStates.Ready, signedIn, "work"),
                new ChoiceState(StartPasses.NotOffered, refusedAt, "work"),
            ],
            build.Choices.Select(choice => choice.State));
        Assert.Equal(["1", "2", "3"], build.Choices.Select(choice => choice.Choice.From));
    }

    [Fact]
    public void A_cool_off_reads_with_its_end_an_unread_account_as_not_read_and_an_agent_with_none_as_signed_out()
    {
        Accounts("work", "spare");
        var seen = Now.AddMinutes(-30);
        AccountCooling.Cool(_home, new CoolingEntry("fake", "work", Now.AddHours(2), true, null, seen, "s1"), seen);
        var config = DriverConfig.Empty with { Adapter = "fake" };

        var cooling = Work(RoleStructure.Of(() => RoleCatalog.Of([]), config, Roster(), [new RolePlace()], [RoleWork.Build]), 0, RoleWork.Build);
        Assert.Equal(new ChoiceState(ChoiceStates.NotRead, null, "spare"), Assert.Single(cooling.Choices).State);

        AccountCooling.Cool(_home, new CoolingEntry("fake", "spare", Now.AddHours(1), true, null, seen, "s2"), seen);
        var both = Work(RoleStructure.Of(() => RoleCatalog.Of([]), config, Roster(), [new RolePlace()], [RoleWork.Build]), 0, RoleWork.Build);
        Assert.Equal(new ChoiceState(StartPasses.Cooling, seen, "spare", Now.AddHours(1)), Assert.Single(both.Choices).State);

        foreach (var name in new[] { "work", "spare" }) GitSeed.Remove(HarnessSettings.ProfileHome(_home, "fake", name));
        var none = Work(RoleStructure.Of(() => RoleCatalog.Of([]), config, Roster(), [new RolePlace()], [RoleWork.Build]), 0, RoleWork.Build);
        Assert.Equal(new ChoiceState(StartPasses.SignedOut), Assert.Single(none.Choices).State);
        Assert.Empty(_asked);
    }

    [Fact]
    public void An_inherited_rule_with_no_local_override_reads_with_where_it_was_set()
    {
        var config = DriverConfig.Parse("""
            { "adapter": "fake",
              "helpers": { "engine": { "mode": "ask", "agents": ["fake"] } },
              "workspaceHelpers": { "aurora": { "mode": "off" } },
              "workspaceHandOver": { "aurora": { "mode": "on" } },
              "brainstorms": { "mode": "off" } }
            """);
        RolePlace[] places = [new(), new("aurora"), new("aurora", "web"), new("aurora", "engine")];

        var structure = RoleStructure.Of(() => RoleCatalog.Of([]), config, Roster(), places, [RoleWork.Help, RoleWork.Build, RoleWork.Brainstorm]);

        Assert.Equal(
            [("on", "default"), ("off", "workspace"), ("off", "workspace"), ("ask", "repository")],
            places.Select((_, at) => Work(structure, at, RoleWork.Help).Policy).Select(policy => (policy!.Consent, policy.Source)));
        Assert.Equal(["fake"], Work(structure, 3, RoleWork.Help).Policy!.Agents);
        Assert.Equal(
            new ActorEffective("fake", null, null, "rule-default"), Assert.Single(Work(structure, 3, RoleWork.Help).Choices).Choice);
        Assert.Equal(new HandOverRequirement(WorkConsent.On, "workspace"), Work(structure, 2, RoleWork.Build).Policy!.Requirement);
        Assert.Equal(("off", "machine"), (Work(structure, 0, RoleWork.Brainstorm).Policy!.Consent, Work(structure, 0, RoleWork.Brainstorm).Policy!.Source));
    }

    [Fact]
    public void A_failure_is_said_on_its_own_item_and_the_rest_reads()
    {
        var config = DriverConfig.Parse("""{ "adapter": "fake", "actors": { "intake": "nope" } }""");

        var structure = RoleStructure.Of(() => RoleCatalog.Of([]), config, Roster(), [new RolePlace()], [RoleWork.Intake, RoleWork.Build]);

        var intake = Work(structure, 0, RoleWork.Intake);
        var unreadable = new ActorUnreadableException("intake", config.ActorErrors["intake"]).Message;
        Assert.Equal(("intake", unreadable, unreadable), (intake.Role!.Id, intake.ChoicesFailed, intake.PolicyFailed));
        Assert.Empty(intake.Choices);
        var build = Work(structure, 0, RoleWork.Build);
        Assert.Equal((null, null, "on"), (build.ChoicesFailed, build.PolicyFailed, build.Policy!.Consent));
        Assert.Equal("fake", Assert.Single(build.Choices).Choice.Agent);

        var unread = RoleStructure.Of(
            () => throw new DriverException("the role catalogue cannot be read, so starts wait: locked"), config, Roster(), [new RolePlace()], [RoleWork.Build]);
        Assert.Equal("the role catalogue cannot be read, so starts wait: locked", unread.CatalogueFailed);
        Assert.Equal(("the role catalogue cannot be read, so starts wait: locked", "on"), (Work(unread, 0, RoleWork.Build).Held, Work(unread, 0, RoleWork.Build).Policy!.Consent));
    }

    [Fact]
    public async Task Opening_it_asks_no_account_and_no_binary_and_takes_no_reservation()
    {
        Accounts("work", "spare");
        AccountReads.Keep(_home, "fake", "work", LoginState.Out, Now.AddDays(-2));
        new HarnessSettings().WithDefault("fake", "work").Save(Settings);
        var roster = Roster();
        var mark = roster.Mark();
        var config = DriverConfig.Empty with { Adapter = "fake" };

        var structure = RoleStructure.Of(() => RoleCatalog.Of([]), config, roster, [new RolePlace(), new RolePlace("aurora")], RoleWork.Codes);

        Assert.Empty(_asked);
        Assert.Equal(mark, roster.Mark());
        Assert.Equal(new StructureAgent("fake", true, null, null), Assert.Single(structure.Agents));

        // A look of the binary is the roster's own report, never the structure's: once asked, it is said as last answered.
        await roster.ReportAsync("fake", config);
        var after = RoleStructure.Of(() => RoleCatalog.Of([]), config, roster, [new RolePlace()], [RoleWork.Build]);
        Assert.Equal(new StructureAgent("fake", true, true, null), Assert.Single(after.Agents));
        Assert.Empty(_asked);
        Assert.Equal(mark, roster.Mark());
    }
}
