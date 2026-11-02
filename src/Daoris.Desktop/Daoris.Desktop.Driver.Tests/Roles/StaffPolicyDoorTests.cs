using System.Text.Json;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STAFFPOLICY1b (D226; the staff policy design §2.1, §6, §11 points 2, 3 and 6): the role door carries a role's policy. It is judged once
/// with the record, the staffing and the revision it was read at, and published with them in one store transaction; the staffing's writers
/// re-key it in their own; every other writer leaves it stale; a field this build does not enforce is refused; <c>ROLES</c>' next new start
/// is a probe.
/// </summary>
public sealed class StaffPolicyDoorTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "staff-policy-door", Guid.NewGuid().ToString("N")[..8]);

    private static readonly ActorChoice Sonnet = new("claude", "sonnet", "high");

    private static readonly ActorChoice Opus = new("claude", "opus", "high");

    private static readonly ActorChoice Codex = new("codex", null, "medium");

    private string File_ => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    public StaffPolicyDoorTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        File.WriteAllText(File_,
            """
            { "adapter": "claude", "intakeAdapter": "codex", "actors": {
              "build": [ { "agent": "claude", "model": "sonnet", "effort": "high" }, { "agent": "claude", "model": "opus", "effort": "high" } ],
              "intake": [ { "agent": "codex", "effort": "medium" }, { "agent": "claude", "model": "sonnet", "effort": "high" } ] } }
            """);
        Staffing = Make();
        Assert.Equal(StaffingImported.Done, Staffing.ImportFile().How);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private RoleStaffing Staffing { get; }

    private RoleStaffing Make(Action? beforeCommit = null) => new(_home, () => StoreSettled.Yes) { BeforeCommit = beforeCommit, FileWait = TimeSpan.FromMilliseconds(200) };

    private RoleCatalog Catalogue() => RoleCatalog.Read(Staffing.Location);

    private RoleRecord Role(string id) => Catalogue().Slots.Single(slot => slot.Id == id).Role!;

    private DriverConfig Config() => StaffingProjection.Config(Staffing.Read()!);

    private static string Entry(ActorChoice choice, string extra = "") =>
        $$"""{"agent":"{{choice.Agent}}"{{(choice.Model is { } m ? $",\"model\":\"{m}\"" : "")}}{{(choice.Effort is { } e ? $",\"effort\":\"{e}\"" : "")}}{{extra}}}""";

    /// <summary>A record: the rule, then each choice with the fields after its tuple, then the rest.</summary>
    private static StaffPolicy Policy(string rule, (ActorChoice Choice, string Extra)[] choices, string rest = "") =>
        StaffPolicy.Parse($$"""{"rule":"{{rule}}","choices":[{{string.Join(",", choices.Select(each => Entry(each.Choice, each.Extra)))}}]{{rest}}}""").Policy
        ?? throw new InvalidOperationException("the test's record does not read");

    private static StaffPolicy ThreeToOne() => Policy("weight", [(Sonnet, ",\"weight\":3"), (Opus, ",\"weight\":1")]);

    private long SetBuildPolicy(StaffPolicy policy, long? read = null) =>
        RoleDoor.Apply(Staffing, RoleChange.Set(Role("build")), null, null, new RolePolicyPress(policy, Read: read));

    private IReadOnlyList<T> Query<T>(string sql, Func<SqliteDataReader, T> map)
    {
        using var reader = DesktopStoreReader.Open(Staffing.Location);
        return reader is null ? [] : reader.Query(sql, map);
    }

    private string Snapshot() => string.Join(
        "|",
        Catalogue().Slots.Select(slot => $"{slot.Id}:{slot.State}:{slot.Revision}"),
        string.Join(",", Query("SELECT role, choices FROM role_staffing ORDER BY role", row => $"{row.GetString(0)}={row.GetString(1)}")),
        string.Join(",", Query("SELECT role, revision, policy FROM role_policy_revisions ORDER BY role, revision", row => $"{row.GetString(0)}{row.GetInt64(1)}")));

    // ——— A policy is saved with the role and read back.

    [Fact]
    public void A_policy_set_through_the_door_is_kept_and_read_back_with_its_revision()
    {
        Assert.Equal(StaffPolicyRead.Default, Staffing.Policy("build").State);

        SetBuildPolicy(ThreeToOne());

        var read = Staffing.Policy("build");
        Assert.Equal((StaffPolicyRead.Set, 1L), (read.State, read.Revision));
        Assert.Equal(ThreeToOne().Write(), read.Policy!.Write());
    }

    [Fact]
    public void A_preset_fills_the_record_for_the_roles_choices_with_its_moves_and_bound_now_that_the_course_enforces_them()
    {
        RoleDoor.Apply(Staffing, RoleChange.Set(Role("build")), null, null, new RolePolicyPress(Preset: "even"));

        var read = Staffing.Policy("build");
        Assert.Equal((StaffRules.Weight, 1, 3), (read.Policy!.Rule, read.Policy.Moves.Count, read.Policy.Most));
        Assert.Equal(StaffActions.Weighted, read.Policy.Moves[StaffConditions.Limit].Do);
        Assert.All(read.Policy.Choices, choice => Assert.Equal(1, choice.Weight));

        RoleDoor.Apply(Staffing, RoleChange.Set(Role("build")), null, null, new RolePolicyPress(Preset: "first", Read: read.Revision));
        Assert.Equal(StaffRules.Order, Staffing.Policy("build").Policy!.Rule);

        RoleDoor.Apply(Staffing, RoleChange.Set(Role("build")), null, null, new RolePolicyPress(Clear: true, Read: 2));
        Assert.Equal(StaffPolicyRead.Default, Staffing.Policy("build").State);
        Assert.Equal(3, Staffing.Policy("build").Revision);
        Assert.Equal(3, Query("SELECT COUNT(*) FROM role_policy_revisions WHERE role = 'build'", row => row.GetInt32(0))[0]);
    }

    [Fact]
    public void A_preset_that_is_not_one_is_refused_in_the_policy_code()
    {
        var refused = Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, RoleChange.Set(Role("build")), null, null, new RolePolicyPress(Preset: "cheapest")));
        Assert.Equal(StaffPolicy.Code, refused.Code);
        Assert.Contains("first, even, weighted", refused.Message);
    }

    // ——— Every refusal is the policy's code, and nothing is written.

    public static TheoryData<string, string> Refusals() => new()
    {
        { "a weight above ten", """{"rule":"weight","choices":[{"agent":"claude","model":"sonnet","effort":"high","weight":11}]}""" },
        { "every weight zero", """{"rule":"weight","choices":[{"agent":"claude","model":"sonnet","effort":"high","weight":0},{"agent":"claude","model":"opus","effort":"high","weight":0}]}""" },
        { "a share of 100", """{"rule":"weight","choices":[{"agent":"claude","model":"sonnet","effort":"high","share":100}]}""" },
        { "a budget of no starts", """{"rule":"order","choices":[{"agent":"claude","model":"sonnet","effort":"high","budget":{"starts":0}}]}""" },
        { "a work the role does not take", """{"rule":"order","choices":[{"agent":"claude","model":"sonnet","effort":"high","takes":["review"]}]}""" },
        { "a work no choice takes", """{"rule":"order","choices":[{"agent":"claude","model":"sonnet","effort":"high","takes":["design"]},{"agent":"claude","model":"opus","effort":"high","takes":["design"]}]}""" },
        { "a choice the staffing does not list", """{"rule":"order","choices":[{"agent":"codex","effort":"medium"}]}""" },
        { "an action its condition does not allow", """{"rule":"order","choices":[],"moves":{"quiet":"wait"}}""" },
        { "a count of tries out of range", """{"rule":"order","choices":[],"moves":{"transient":{"do":"next","after":9}}}""" },
        { "a bound out of range", """{"rule":"order","choices":[],"most":9}""" },
        { "a refused sign-in held, not enforced yet", """{"rule":"order","choices":[],"moves":{"signed-out":"hold"}}""" },
        { "a refused model asked about, not enforced yet", """{"rule":"order","choices":[],"moves":{"model-refused":"ask"}}""" },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public void A_refused_policy_is_the_policy_code_and_writes_nothing(string name, string record)
    {
        var before = Snapshot();
        var policy = StaffPolicy.Parse(record).Policy ?? throw new InvalidOperationException($"{name}: the record does not read");

        var refused = Assert.Throws<RoleRefusedException>(() => SetBuildPolicy(policy));

        Assert.Equal(StaffPolicy.Code, refused.Code);
        Assert.Equal(before, Snapshot());
        Assert.Equal(StaffPolicyRead.Default, Staffing.Policy("build").State);
    }

    [Fact]
    public void A_field_this_build_does_not_enforce_says_what_it_does_enforce()
    {
        var refused = Assert.Throws<RoleRefusedException>(
            () => SetBuildPolicy(Policy("order", [(Sonnet, ""), (Opus, "")], ",\"moves\":{\"model-refused\":\"hold\"}")));

        Assert.Contains("`moves.model-refused` as `hold`", refused.Message);
        Assert.Equal(
            ["rule", "weight", "share", "budget.starts", "budget.tokens", "takes", "moves.limit", "moves.transient", "moves.failed", "moves.quiet", "most"],
            StaffCapabilities.Fields);
    }

    [Fact]
    public void A_role_that_takes_brainstorm_keeps_no_policy_and_a_role_with_no_choices_of_its_own_keeps_none()
    {
        RoleDoor.Apply(Staffing, RoleChange.Set(Role("build") with { Id = "voices", Takes = [RoleWork.Brainstorm], Scope = new RoleScope(RoleScope.Workspace, "w1") }), [Sonnet, Codex]);
        var onBrainstorm = Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(
            Staffing, RoleChange.Set(Role("voices")), null, null, new RolePolicyPress(Policy("order", [(Sonnet, "")]))));
        Assert.Equal(StaffPolicy.Code, onBrainstorm.Code);
        Assert.Contains("brainstorm", onBrainstorm.Message);

        RoleDoor.Apply(Staffing, RoleChange.Set(Role("build") with { Id = "night", Takes = [RoleWork.Intake], Scope = new RoleScope(RoleScope.Workspace, "w2") }), []);
        var none = Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(
            Staffing, RoleChange.Set(Role("night")), null, null, new RolePolicyPress(Policy("order", [(Sonnet, "")]))));
        Assert.Contains("no choices of its own", none.Message);
    }

    // ——— Publication is one transaction at one revision.

    [Fact]
    public void A_policy_revision_that_moved_since_it_was_read_is_refused_with_nothing_written_and_the_role_unchanged()
    {
        SetBuildPolicy(ThreeToOne());
        var before = Snapshot();

        var refused = Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(
            Staffing, RoleChange.Set(Role("build") with { Name = AuthoredText.Of("Renamed") }), [Opus, Sonnet], null,
            new RolePolicyPress(Policy("order", [(Sonnet, ""), (Opus, "")]), Read: 0)));

        Assert.Equal(StaffPolicy.Code, refused.Code);
        Assert.Contains("changed since you read it", refused.Message);
        Assert.Equal(before, Snapshot());
        Assert.Equal("Build", Role("build").Name.In("en"));
    }

    [Fact]
    public void A_role_staffing_and_policy_are_published_together_or_not_at_all_when_the_commit_is_cut()
    {
        var before = Snapshot();
        var killed = Make(() => throw new IOException("the kill"));

        Assert.Throws<DriverException>(() => RoleDoor.Apply(
            killed, RoleChange.Set(Role("build") with { Name = AuthoredText.Of("Renamed") }), [Opus, Sonnet], null, new RolePolicyPress(Policy("weight", [(Opus, ",\"weight\":2"), (Sonnet, "")]))));

        Assert.Equal(before, Snapshot());
        Assert.Equal(StaffPolicyRead.Default, Staffing.Policy("build").State);
        Assert.Equal([Sonnet, Opus], Config().Actors["build"]);
    }

    [Fact]
    public void An_added_role_a_policy_and_its_staffing_are_cut_with_the_commit_too()
    {
        var before = Snapshot();
        var killed = Make(() => throw new IOException("the kill"));
        var night = Role("build") with { Id = "night", Takes = [RoleWork.Chat], Scope = new RoleScope(RoleScope.Workspace, "w2") };

        Assert.Throws<IOException>(() => RoleDoor.Apply(killed, RoleChange.Set(night), [Sonnet, Opus], null, new RolePolicyPress(Policy("order", [(Sonnet, ""), (Opus, "")]))));

        Assert.Equal(before, Snapshot());
        Assert.DoesNotContain(Catalogue().Slots, slot => slot.Id == "night");
    }

    [Theory]
    [InlineData("""{"limit":"ask"}""", "")]
    [InlineData("""{"limit":{"do":"wait"}}""", "")]
    [InlineData("""{"limit":"next"}""", ",\"most\":2")]
    public void A_role_taking_a_conversations_work_is_refused_the_limit_waits_asks_and_bound_its_conversation_does_not_read(string moves, string rest)
    {
        var night = Role("build") with { Id = "night", Takes = [RoleWork.Chat], Scope = new RoleScope(RoleScope.Workspace, "w2") };
        var policy = Policy("order", [(Sonnet, ""), (Opus, "")], $",\"moves\":{moves}{rest}");

        var refused = Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, RoleChange.Set(night), [Sonnet, Opus], null, new RolePolicyPress(policy)));

        Assert.Contains("takes a conversation's work", refused.Message);
        Assert.DoesNotContain(Catalogue().Slots, slot => slot.Id == "night");
    }

    [Fact]
    public void A_role_the_catalogue_refuses_writes_neither_its_staffing_nor_its_policy()
    {
        var before = Snapshot();
        var clash = Role("build") with { Id = "second" };

        // Two roles take build on the machine: the catalogue's judge refuses, before the staffing or the policy is written.
        var refused = Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(
            Staffing, RoleChange.Set(clash), [Sonnet], null, new RolePolicyPress(Policy("order", [(Sonnet, "")]))));

        Assert.Equal("two-roles-one-work", refused.Code);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public void A_batch_with_one_refusal_is_refused_whole_and_writes_nothing()
    {
        var before = Snapshot();
        var good = new RolePress(RoleChange.Set(Role("build") with { Name = AuthoredText.Of("Renamed") }), null, null, new RolePolicyPress(ThreeToOne()));
        var bad = new RolePress(RoleChange.Set(Role("intake")), null, null, new RolePolicyPress(Policy("order", [(Codex, "")], ",\"moves\":{\"model-refused\":\"hold\"}")));

        var refused = Assert.Throws<RoleRefusedException>(() => RoleDoor.ApplyAll(Staffing, [good, bad]));

        Assert.Equal(StaffPolicy.Code, refused.Code);
        Assert.Equal(before, Snapshot());

        RoleDoor.ApplyAll(Staffing, [good]);
        Assert.Equal("Renamed", Role("build").Name.In("en"));
        Assert.Equal(StaffPolicyRead.Set, Staffing.Policy("build").State);
    }

    [Fact]
    public void A_home_that_keeps_no_staffing_in_its_store_refuses_a_policy_in_words()
    {
        var elsewhere = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "staff-policy-door", Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var file = DaorisHome.Entry(elsewhere, HomeEntry.DriverConfig);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, """{ "adapter": "claude", "actors": { "build": [ { "agent": "claude", "model": "sonnet", "effort": "high" } ] } }""");
            var staffing = new RoleStaffing(elsewhere, () => StoreSettled.Yes);

            var refused = Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(
                staffing, RoleChange.Set(Role("build")), null, null, new RolePolicyPress(Policy("order", [(Sonnet, "")]))));

            Assert.Equal(StaffPolicy.Code, refused.Code);
            Assert.Contains("kept in this machine's store, which this home does not use yet", refused.Message);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            GitSeed.Discard(elsewhere);
        }
    }

    [Fact]
    public void A_policy_goes_with_a_set_alone()
    {
        var refused = Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, RoleChange.Default("build"), null, null, new RolePolicyPress(Clear: true)));

        Assert.Equal(StaffPolicy.Code, refused.Code);
    }

    // ——— The staffing's writers re-key the policy in their own transaction; every other writer leaves it stale.

    [Fact]
    public void A_reorder_an_added_choice_and_a_removed_choice_re_key_the_policy_and_keep_its_one_revision()
    {
        SetBuildPolicy(Policy("weight", [(Sonnet, ",\"weight\":3,\"share\":60"), (Opus, ",\"weight\":2")]));

        Staffing.Set("build", [Opus, Sonnet]);
        var reordered = Staffing.Policy("build");
        Assert.Equal((StaffPolicyRead.Set, 1L), (reordered.State, reordered.Revision));
        Assert.Equal([StaffKey.Of(Opus), StaffKey.Of(Sonnet)], reordered.Policy!.Choices.Select(choice => choice.Key));
        Assert.Equal((2, 3, 60), (reordered.Policy.Choices[0].Weight, reordered.Policy.Choices[1].Weight, reordered.Policy.Choices[1].Share));

        Staffing.Set("build", [Opus, Sonnet, Codex]);
        var added = Staffing.Policy("build").Policy!;
        Assert.Equal(3, added.Choices.Count);
        Assert.Equal((1, null), (added.Of(StaffKey.Of(Codex))!.Weight, added.Of(StaffKey.Of(Codex))!.Share));

        Staffing.Set("build", [Sonnet]);
        var removed = Staffing.Policy("build");
        Assert.Equal((StaffPolicyRead.Set, 1L), (removed.State, removed.Revision));
        Assert.Equal([StaffKey.Of(Sonnet)], removed.Policy!.Choices.Select(choice => choice.Key));
        Assert.Equal(1, Query("SELECT COUNT(*) FROM role_policy_revisions WHERE role = 'build'", row => row.GetInt32(0))[0]);
    }

    [Fact]
    public void A_role_made_to_run_its_choices_in_order_again_or_given_its_seed_loses_its_policy_with_its_revisions_kept()
    {
        SetBuildPolicy(ThreeToOne());

        Staffing.Set("build", null);

        Assert.Equal(StaffPolicyRead.Default, Staffing.Policy("build").State);
        Assert.Equal(2, Query("SELECT COUNT(*) FROM role_policy_revisions WHERE role = 'build'", row => row.GetInt32(0))[0]);
    }

    [Fact]
    public void An_added_role_s_policy_is_re_keyed_with_its_choices_and_deleted_with_the_role()
    {
        var night = Role("build") with { Id = "night", Takes = [RoleWork.Chat], Scope = new RoleScope(RoleScope.Workspace, "w2") };
        RoleDoor.Apply(Staffing, RoleChange.Set(night), [Sonnet, Opus], null, new RolePolicyPress(Policy("weight", [(Sonnet, ",\"weight\":3"), (Opus, "")])));
        Assert.Equal(StaffPolicyRead.Set, Staffing.Policy("night").State);

        RoleDoor.Apply(Staffing, RoleChange.Set(night), [Opus, Codex]);
        var rekeyed = Staffing.Policy("night");
        Assert.Equal(StaffPolicyRead.Set, rekeyed.State);
        Assert.Equal([StaffKey.Of(Opus), StaffKey.Of(Codex)], rekeyed.Policy!.Choices.Select(choice => choice.Key));

        RoleDoor.Apply(Staffing, RoleChange.Remove("night"));
        Assert.Equal(StaffPolicyRead.Default, Staffing.Policy("night").State);
        Assert.Equal(2, Query("SELECT COUNT(*) FROM role_policy_revisions WHERE role = 'night'", row => row.GetInt32(0))[0]);
    }

    [Fact]
    public void Taking_what_the_check_found_and_the_check_s_turn_off_leave_the_policy_stale_and_the_role_in_order()
    {
        SetBuildPolicy(ThreeToOne());
        RoleDoor.Apply(Staffing, RoleChange.Set(Role("intake")), null, null, new RolePolicyPress(Policy("order", [(Codex, ""), (Sonnet, "")])));

        // An older build writes the file: build's choices changed, intake turned off.
        var older = DriverConfig.Parse(File.ReadAllText(File_));
        older = ActorChoices.With(older, "build", [Codex]);
        older = ActorChoices.With(older, "intake", []);
        AtomicFile.WriteText(File_, older.ToJson());
        Assert.Equal(StaffingChecks.Found, Staffing.Check(StaffingCheckAt.Look).How);

        Assert.Equal(StaffPolicyRead.Set, Staffing.Policy("build").State);
        var intake = Staffing.Policy("intake");
        Assert.Equal(StaffPolicyRead.Stale, intake.State);
        Assert.Contains("no choices of its own", intake.Why);

        Staffing.Take(Assert.Single(Staffing.Found()).Id);

        var build = Staffing.Policy("build");
        Assert.Equal(StaffPolicyRead.Stale, build.State);
        Assert.Contains("set for other choices", build.Why);
        Assert.Null(build.Applied);
    }

    // ——— ROLES says each role's policy and next start, and a preview writes nothing.

    [Fact]
    public void A_roles_next_new_start_is_a_probe_that_reserves_nothing_and_writes_nothing()
    {
        SetBuildPolicy(ThreeToOne());
        var before = Snapshot();
        var revisions = Query("SELECT family, revision FROM store_revisions ORDER BY family", row => $"{row.GetString(0)}={row.GetInt64(1)}");

        var rows = RolePolicyRow.Of(Staffing.Ledger, Catalogue(), Config());

        var build = rows.Single(row => row.Role == "build");
        Assert.Equal(StaffPolicyRead.Set, build.PolicyState);
        Assert.Equal(("1", StaffBy.Weight), (build.Next!.Choice, build.Next.By));
        Assert.Equal(StaffRules.Weight, build.StaffPolicy!["rule"]!.GetValue<string>());
        Assert.Equal(StaffPolicyRead.Default, rows.Single(row => row.Role == "intake").PolicyState);
        Assert.Empty(Query("SELECT 1 FROM role_starts", row => row.GetInt32(0)));
        Assert.Equal(before, Snapshot());
        Assert.Equal(revisions, Query("SELECT family, revision FROM store_revisions ORDER BY family", row => $"{row.GetString(0)}={row.GetInt64(1)}"));
        Assert.NotEmpty(JsonSerializer.Serialize(rows));
    }

    [Fact]
    public void The_payload_reads_a_preset_a_record_a_clear_and_the_revision()
    {
        using var preset = JsonDocument.Parse("""{ "change": "set", "id": "build", "role": { "name": "Build", "takes": ["build"] }, "policy": "even", "policyRevision": 4 }""");
        using var record = JsonDocument.Parse("""{ "change": "set", "id": "build", "role": { "name": "Build", "takes": ["build"] }, "policy": { "rule": "order", "choices": [] } }""");
        using var clear = JsonDocument.Parse("""{ "change": "set", "id": "build", "role": { "name": "Build", "takes": ["build"] }, "policy": null }""");
        using var none = JsonDocument.Parse("""{ "change": "set", "id": "build", "role": { "name": "Build", "takes": ["build"] } }""");
        using var odd = JsonDocument.Parse("""{ "change": "set", "id": "build", "role": { "name": "Build", "takes": ["build"] }, "policy": 3 }""");

        Assert.Equal(("even", 4L), (RoleDoor.ReadPress(preset.RootElement).Policy!.Preset, RoleDoor.ReadPress(preset.RootElement).Policy!.Read));
        Assert.Equal(StaffRules.Order, RoleDoor.ReadPress(record.RootElement).Policy!.Policy!.Rule);
        Assert.True(RoleDoor.ReadPress(clear.RootElement).Policy!.Clear);
        Assert.Null(RoleDoor.ReadPress(none.RootElement).Policy);
        Assert.Equal(StaffPolicy.Code, Assert.Throws<RoleRefusedException>(() => RoleDoor.ReadPress(odd.RootElement)).Code);
    }
}
