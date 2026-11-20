using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.Modules.Tests.AttentionTestRows;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Whose each attention row is (the window design §5.2): the census classes every kind the judge makes as the owner's or the agents',
/// with its reason, and fails a kind it does not class. An agents' kind is counted as the owner's until a role is appointed to take it, as
/// the roles stand at each judgement: Daoris is not made to block progress, so no row waits on a role nobody holds. The rules are held
/// on sources made in memory, one case each, and then run over every source of the modules that makes rows; the slice pushes each row's
/// class.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed partial class AttentionOwnershipTests : IDisposable
{
    /// <summary>The six kinds only the owner settles, as a row carries them.</summary>
    private static readonly string[] OwnersKinds = ["question", "go-ahead", "release", "sign-in", "money", "confirm"];

    /// <summary>The roles that take a kind's agents' side.</summary>
    private static readonly string[] Takers = ["supervisor", "steward", "reviewer", "approver", "holder", "clock"];

    /// <summary>Every role appointed: each supervisor admitted at every place for every act, and the steward held.</summary>
    private static readonly AttentionTakers Everyone = new((_, _, _) => true, steward: true);

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "attention-ownership", Guid.NewGuid().ToString("N")[..8]);

    public AttentionOwnershipTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>A kind a judge makes: a row made with its kind, a row's kind set, or a rows' maker's own kind.</summary>
    [GeneratedRegex("""Row\("(?<kind>[a-z][a-z-]*)"|\["kind"\] = "(?<kind>[a-z][a-z-]*)"|const string Kind = "(?<kind>[a-z][a-z-]*)";""")]
    private static partial Regex Made();

    /// <summary>
    /// A source that makes attention rows: it reads <c>AttentionJson</c>'s row builder, adds to <c>NeedsAPerson</c>'s rows, or is a row
    /// builder of its own judging from <c>AttentionFacts</c> (the accounts' rows).
    /// </summary>
    [GeneratedRegex(@"\bAttentionJson\b|\bNeedsAPerson\b|\bAttentionFacts\b")]
    private static partial Regex MakesRows();

    /// <summary>What the census breaks in <paramref name="sources"/>: a kind made with no class, and a class no source makes.</summary>
    private static List<string> Breaches(IEnumerable<(string Path, string Text)> sources)
    {
        var made = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, text) in sources)
        {
            foreach (Match match in Made().Matches(text)) made.TryAdd(match.Groups["kind"].Value, path);
        }

        var classed = AttentionOwnership.Census.Select(each => each.Kind).ToHashSet(StringComparer.Ordinal);
        return
        [
            .. made.Where(each => !classed.Contains(each.Key))
                .Select(each => $"{each.Value}: `{each.Key}` has no class in AttentionOwnership: say whose it is and why"),
            .. classed.Where(kind => !made.ContainsKey(kind)).Order(StringComparer.Ordinal)
                .Select(kind => $"`{kind}` is classed and no judge makes it: take it out of the census"),
        ];
    }

    /// <summary>Every source of the modules (its tests apart, which are another project) that makes attention rows, wherever it lives.</summary>
    private static List<(string Path, string Text)> RowMakers()
    {
        var project = SourceFiles.Project(SourceFiles.ModulesProject);
        return
        [
            .. Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
                .Where(file => !Path.GetRelativePath(project, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(part => part is "bin" or "obj"))
                .Select(file => (Path: Path.GetRelativePath(project, file).Replace('\\', '/'), Text: File.ReadAllText(file)))
                .Where(source => MakesRows().IsMatch(source.Text))
                .OrderBy(source => source.Path, StringComparer.Ordinal),
        ];
    }

    [Fact]
    public void A_kind_made_with_no_class_and_a_class_no_judge_makes_each_break_the_census()
    {
        var classed = string.Join("\n", AttentionOwnership.Census.Select(each => $"Row(\"{each.Kind}\", id, since);"));
        Assert.Empty(Breaches([("Loop/All.cs", classed)]));

        Assert.Equal(
            [
                "Loop/Fresh.cs: `brand-new` has no class in AttentionOwnership: say whose it is and why",
                "Loop/Fresh.cs: `made-twice` has no class in AttentionOwnership: say whose it is and why",
                "Loop/Fresh.cs: `own-maker` has no class in AttentionOwnership: say whose it is and why",
            ],
            Breaches([("Loop/All.cs", classed), ("Loop/Fresh.cs",
                "var row = Row(\"brand-new\", id, since);\nrow = new JsonObject { [\"kind\"] = \"made-twice\" };\npublic const string Kind = \"own-maker\";")]));
        Assert.Equal(["`parked` is classed and no judge makes it: take it out of the census"],
            Breaches([("Loop/All.cs", classed.Replace("Row(\"parked\", id, since);", "", StringComparison.Ordinal))]));
    }

    /// <summary>
    /// The census reaches every source that makes rows, not the loop's folder alone: a maker moved or added elsewhere in the modules is
    /// read, and a source that makes no row (a steward's act's own <c>Kind</c>) is not.
    /// </summary>
    [Fact]
    public void Every_kind_a_source_of_the_modules_makes_has_one_class()
    {
        var sources = RowMakers();
        Assert.Contains(sources, source => source.Path == "Loop/NeedsAPerson.cs");
        Assert.Contains(sources, source => source.Path == "Loop/AccountAttention.cs");
        Assert.DoesNotContain(sources, source => source.Path.EndsWith("/StewardDesk.cs", StringComparison.Ordinal));
        Assert.Empty(Breaches(sources));
        // The walk still reaches the judges: today's 36 kinds (design §5.2).
        Assert.Equal(36, AttentionOwnership.Census.Count);
        Assert.Equal(AttentionOwnership.Census.Count, AttentionOwnership.Census.Select(each => each.Kind).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// Each class says whose its kind is and why: every kind the owner's kind of wait, since every kind can be theirs; a kind with an
    /// agents' side the role that takes it, what tells a row's side and why the other side is the agents'.
    /// </summary>
    [Fact]
    public void Each_class_says_whose_with_its_reason_and_an_agents_side_names_its_role()
    {
        foreach (var each in AttentionOwnership.Census)
        {
            Assert.False(string.IsNullOrWhiteSpace(each.Reason), each.Kind);
            Assert.Contains(each.Wait, OwnersKinds);
            if (each.Takes is { } takes) Assert.Contains(takes, Takers);
            var agentsSide = each.Takes is not null;
            Assert.Equal(agentsSide, each.OwnersWhen is not null);
            Assert.Equal(agentsSide, !string.IsNullOrWhiteSpace(each.Otherwise));
            if (each.TakenWhen is not null) Assert.True(agentsSide, $"{each.Kind} is taken by a role and names none");
        }
    }

    /// <summary>The design's short list (§5.2), kind by kind: the owner's kind of wait, and the role that takes its agents' side.</summary>
    [Theory]
    [InlineData("signed-out", "sign-in", null)]
    [InlineData("git-held", "sign-in", null)]
    [InlineData("confirmation", "confirm", null)]
    [InlineData("intake", "question", null)]
    [InlineData("departure", "question", null)]
    [InlineData("plan-question", "question", null)]
    [InlineData("design-question", "question", null)]
    [InlineData("unanswerable", "question", null)]
    [InlineData("memory", "go-ahead", null)]
    [InlineData("go-ahead", "go-ahead", "approver")]
    [InlineData("git-ask", "go-ahead", "approver")]
    [InlineData("trust", "go-ahead", "approver")]
    [InlineData("space", "go-ahead", "approver")]
    [InlineData("step", "release", "holder")]
    [InlineData("set-up", "release", "holder")]
    [InlineData("integration", "release", "holder")]
    [InlineData("account-wait", "sign-in", "clock")]
    [InlineData("policy-hold", "go-ahead", "supervisor")]
    [InlineData("work", "go-ahead", "steward")]
    [InlineData("permission", "go-ahead", "supervisor")]
    [InlineData("review", "release", "supervisor")]
    [InlineData("rule", "go-ahead", "supervisor")]
    [InlineData("parked", "question", "supervisor")]
    [InlineData("left", "question", "supervisor")]
    [InlineData("parked-quest", "question", "supervisor")]
    [InlineData("unclosed", "question", "supervisor")]
    [InlineData("opinion", "go-ahead", "supervisor")]
    [InlineData("second-check", "question", "reviewer")]
    [InlineData("steward-stopped", "go-ahead", "steward")]
    [InlineData("proposal", "question", "supervisor")]
    public void Classes_each_kind_as_the_design_says(string kind, string wait, string? takes)
    {
        var each = Assert.Single(AttentionOwnership.Census, one => one.Kind == kind);
        Assert.Equal((wait, takes), (each.Wait, each.Takes));
    }

    // ---- a row's side ----

    private static AttentionOwnership.Side Of(string row, AttentionTakers? takers = null) =>
        AttentionOwnership.Of(JsonNode.Parse(row)!.AsObject(), takers ?? Everyone);

    private static AttentionOwnership.Side Owners(string wait, bool untaken = false) => new("owner", wait, untaken);

    private static readonly AttentionOwnership.Side Agents = new("agents", null);

    /// <summary>
    /// 🔴 Measured in review on 2026-11-20: the kinds classed as the agents' on every row (a held call, a review, a widening, a policy hold,
    /// a park, left work, a parked quest, an unclosed quest…) left every count while no role answered them, so nobody would. Each is the
    /// owner's, counted with its kind of wait and said as no role's yet, until a role is appointed to take it.
    /// </summary>
    [Theory]
    [InlineData("permission", "go-ahead")]
    [InlineData("review", "release")]
    [InlineData("rule", "go-ahead")]
    [InlineData("policy-hold", "go-ahead")]
    [InlineData("parked", "question")]
    [InlineData("left", "question")]
    [InlineData("parked-quest", "question")]
    [InlineData("unclosed", "question")]
    [InlineData("steward-stopped", "go-ahead")]
    [InlineData("request-declined", "question")]
    [InlineData("proposal", "question")]
    [InlineData("opinion", "go-ahead")]
    [InlineData("second-check", "question")]
    [InlineData("helper", "go-ahead")]
    [InlineData("hand-over", "go-ahead")]
    [InlineData("helper-ended", "question")]
    [InlineData("quiet", "question")]
    public void An_agents_kind_is_the_owner_s_while_no_role_is_appointed_to_take_it(string kind, string wait)
    {
        var row = $$"""{"kind":"{{kind}}","id":"x","workspace":"work","where":"engine"}""";
        Assert.Equal(Owners(wait, untaken: true), Of(row, AttentionTakers.None));
    }

    /// <summary>Every kind with an agents' side but the ones no role's shape takes yet is the agents' once a role is appointed to take it.</summary>
    [Fact]
    public void An_agents_kind_is_the_agents_once_a_role_is_appointed_to_take_it_and_one_no_role_takes_stays_the_owner_s()
    {
        string[] taken = ["permission", "review", "rule", "policy-hold", "parked", "parked-quest", "opinion", "steward-stopped", "answer"];
        string[] untaken = ["left", "unclosed", "request-declined", "proposal", "second-check", "helper", "hand-over", "helper-ended", "quiet"];
        foreach (var kind in taken) Assert.Equal(Agents, Of($$"""{"kind":"{{kind}}","id":"x","workspace":"work","where":"engine"}"""));
        foreach (var kind in untaken)
        {
            var wait = AttentionOwnership.Census.Single(each => each.Kind == kind).Wait;
            Assert.Equal(Owners(wait, untaken: true), Of($$"""{"kind":"{{kind}}","id":"x","workspace":"work","where":"engine"}"""));
        }

        Assert.Equal(
            taken.Concat(untaken).Order(StringComparer.Ordinal),
            AttentionOwnership.Census.Where(each => each.Takes is not null && each.OwnersWhen!(new JsonObject()) == false && each.Kind is not ("work" or "account-wait"))
                .Select(each => each.Kind).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The appointment is read from the roles as they stand, through the decisions' admission: a question supervisor an agent holds, granted
    /// its answer, takes a park in its workspace; its grant taken away, or a supervisor of another kind, takes none, and the park is the owner's.
    /// </summary>
    [Fact]
    public void A_role_is_appointed_where_the_admission_answers_a_supervisor_for_the_kind_s_act_at_the_row_s_place()
    {
        const string parked = """{"kind":"parked","id":"s1","workspace":"work","where":"engine"}""";
        Assert.Equal(Agents, Of(parked, AttentionTakers.Of(Standing(RoleKit.Supervisor(acts: ["question.answer"], holder: RoleHolders.Agent)))));
        Assert.Equal(Owners("question", untaken: true), Of(parked, AttentionTakers.Of(Standing(RoleKit.Supervisor(acts: [], holder: RoleHolders.Agent)))));
        Assert.Equal(Owners("question", untaken: true), Of(parked, AttentionTakers.Of(Standing(
            RoleKit.Supervisor(acts: ["allow.once"], holder: RoleHolders.Agent, decides: [SupervisorKinds.Allow])))));
        // Held by the person's rules, nobody answers it: the owner's.
        Assert.Equal(Owners("question", untaken: true), Of(parked, AttentionTakers.Of(Standing(RoleKit.Supervisor(acts: ["question.answer"], holder: RoleHolders.Rules)))));
        // A held call takes the allow supervisor; the steward, its holder.
        Assert.Equal(Agents, Of("""{"kind":"permission","id":"s1#r","workspace":"work","where":"engine"}""",
            AttentionTakers.Of(Standing(RoleKit.Supervisor(acts: ["allow.once"], holder: RoleHolders.Agent, decides: [SupervisorKinds.Allow])))));
        Assert.False(AttentionTakers.Of(Standing(RoleKit.Supervisor(holder: RoleHolders.Agent))).Steward);
        Assert.True(AttentionTakers.Of(Standing(RoleKit.Supervisor(holder: RoleHolders.Agent), RoleKit.Steward([], RoleHolders.Agent))).Steward);
        Assert.False(AttentionTakers.Of(null).Supervises(SupervisorKinds.Question, new RolePlace("work", "engine"), SupervisorKinds.Answer));
    }

    private StandingConfirmation Standing(params RoleRecord[] roles) =>
        new(_home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), walk: new StandingConfirmationTests.Walked())
        {
            Wider = () => RuleWiderPlaces.None,
            Slots = () => [.. roles.Select(role => RoleKit.Slot(role) with { Revision = 1 })],
            Risk = () => PlaceRiskBook.Unwritten.Apply(new PlaceRiskEdit { Workspace = "work", Risk = PlaceRisks.Low }),
        };

    [Fact]
    public void A_session_branch_s_clean_up_is_the_agents_and_one_nothing_names_may_be_the_owner_s_data()
    {
        // A tree its session recorded, and a standing `daoris/` branch nothing recorded: the agents' own bookkeeping.
        Assert.Equal(Agents, Of("""{"kind":"work","id":"s1","session":"s1","title":"daoris/s1","blocked":{"unknown":false,"entries":[{"kind":"tree","code":"unfollowed","facts":{"branch":"daoris/s1"}}]}}"""));
        Assert.Equal(Agents, Of("""{"kind":"work","id":"u1","title":"daoris/s-c4a7c4a7","blocked":{"unknown":true,"entries":[{"kind":"branch","code":"unrecorded","facts":{"branch":"daoris/s-c4a7c4a7"}}]}}"""));
        // A held attempt, a record that does not read and a close in part are the agents' too.
        Assert.Equal(Agents, Of("""{"kind":"work","id":"u#n#1","held":{"unit":"u"}}"""));
        Assert.Equal(Agents, Of("""{"kind":"work","id":"record#f","unreadable":{"file":"f"}}"""));
        // A branch nothing records and nothing names the agents': deleting it may delete the owner's own work.
        Assert.Equal(Owners("go-ahead"), Of("""{"kind":"work","id":"u2","title":"feature/mine","blocked":{"unknown":true,"entries":[{"kind":"branch","code":"unknown-branch","facts":{"branch":"feature/mine"}}]}}"""));
    }

    /// <summary>
    /// A unit whose owner is recorded, on a branch not named <c>daoris/</c>: a session's (its session recorded) is the agents' clean-up,
    /// whatever its branch is called; one no session made is the person's own branch, the owner's.
    /// </summary>
    [Fact]
    public void A_recorded_unit_on_a_branch_of_its_own_name_is_the_agents_where_its_session_is_recorded_and_the_owner_s_where_none_is()
    {
        Assert.Equal(Agents, Of("""{"kind":"work","id":"s7","session":"s7","title":"fix/login","blocked":{"unknown":false,"entries":[{"kind":"tree","code":"unfollowed","facts":{"branch":"fix/login"}}]}}"""));
        Assert.Equal(Owners("go-ahead"), Of("""{"kind":"work","id":"u7","title":"fix/login","blocked":{"unknown":false,"entries":[{"kind":"branch","code":"unfollowed","facts":{"branch":"fix/login"}}]}}"""));
        // The steward held by nobody, the session's clean-up is the owner's until a holder holds it.
        Assert.Equal(Owners("go-ahead", untaken: true), Of(
            """{"kind":"work","id":"s7","session":"s7","title":"fix/login","blocked":{"unknown":false,"entries":[]}}""", new AttentionTakers((_, _, _) => true, steward: false)));
    }

    /// <summary>An allow-list: a start waiting out a cool-off or on an account not read yet clears by itself; every other state is a sign-in, the owner's.</summary>
    [Theory]
    [InlineData("out", true)]
    [InlineData("refused", true)]
    [InlineData("keyed", true)]
    [InlineData("in", true)]
    [InlineData("expired", true)]
    [InlineData("revoked", true)]
    [InlineData("error", true)]
    [InlineData("noAccount-kind-this-build-does-not-know", true)]
    [InlineData("cooling", false)]
    [InlineData("unknown", false)]
    public void A_start_waiting_on_accounts_is_the_agents_only_while_each_clears_by_itself(string state, bool owners)
    {
        var side = Of($$$"""{"kind":"account-wait","id":"a","account":{"named":[{"id":"x","state":"{{{state}}}"}]}}""", AttentionTakers.None);
        Assert.Equal(owners ? Owners("sign-in") : Agents, side);
    }

    [Fact]
    public void A_start_waiting_on_no_account_on_none_named_or_on_one_signed_out_beside_a_cool_off_is_the_owner_s()
    {
        Assert.Equal(Owners("sign-in"), Of("""{"kind":"account-wait","id":"a","account":{"named":[],"noAccount":true}}"""));
        Assert.Equal(Owners("sign-in"), Of("""{"kind":"account-wait","id":"a","account":{"named":[]}}"""));
        Assert.Equal(Owners("sign-in"), Of("""{"kind":"account-wait","id":"a"}"""));
        Assert.Equal(Owners("sign-in"), Of("""{"kind":"account-wait","id":"a","account":{"named":[{"id":"x","state":"cooling"},{"id":"y","state":"out"}]}}"""));
        Assert.Equal(Owners("sign-in"), Of("""{"kind":"account-wait","id":"a","account":{"named":[{"id":"x"}]}}"""));
    }

    [Fact]
    public void A_held_answer_is_the_owner_s_where_their_own_rule_held_it()
    {
        Assert.Equal(Owners("go-ahead"), Of("""{"kind":"answer","id":"s1","answer":{"session":"s1","rule":{"place":"machine","n":3,"at":3}}}"""));
        Assert.Equal(Agents, Of("""{"kind":"answer","id":"s1","answer":{"session":"s1"}}"""));
        Assert.Equal(Owners("go-ahead", untaken: true), Of("""{"kind":"answer","id":"s1","answer":{"session":"s1"}}""", AttentionTakers.None));
    }

    /// <summary>A policy hold whose verdict names money (a budget, a per-use account) is the owner's money whoever is appointed; any other, the limit supervisor's.</summary>
    [Fact]
    public void A_policy_hold_naming_money_is_the_owner_s_money_and_another_the_limit_supervisor_s_where_one_is_appointed()
    {
        Assert.Equal(Owners("money"), Of("""{"kind":"policy-hold","id":"q","sitting":{"verdict":"PolicyHeld","said":{"code":"policy-ask","budget":"monthly"}}}"""));
        Assert.Equal(Owners("money"), Of("""{"kind":"policy-hold","id":"q","sitting":{"verdict":"PolicyHeld","said":{"code":"policy-ask","perUse":true}}}"""));
        Assert.Equal(Agents, Of("""{"kind":"policy-hold","id":"q","sitting":{"verdict":"PolicyHeld","said":{"code":"moves-bound"}}}"""));
        Assert.Equal(Owners("go-ahead", untaken: true),
            Of("""{"kind":"policy-hold","id":"q","sitting":{"verdict":"PolicyHeld","said":{"code":"moves-bound"}}}""", AttentionTakers.None));
    }

    /// <summary>
    /// Each pushed row carries whose it is: the owner's with its kind of wait, the agents' with none, so the page counts the owner's
    /// alone and lists the agents' apart. The rows the judge itself makes carry none: the class is the push's, read from the row whole
    /// and the roles the facts hand.
    /// </summary>
    [Fact]
    public void Each_pushed_row_carries_its_class_and_the_owner_s_its_kind_of_wait()
    {
        var facts = new AttentionFacts
        {
            Sessions = [Session("""{"state":"awaiting-person"}""")],
            Quests = [JsonNode.Parse("""{"id":"7a82cc","from":"platform","to":"nowhere","title":"A budget","status":"Open","filed":"2026-09-14T09:00:00Z","updated":"2026-09-14T09:00:00Z"}""")!.AsObject()],
            Registry = [Registration("engine")],
        };

        JsonObject Pushed(AttentionFacts judged) => JsonSerializer.SerializeToNode(AttentionStateSlice.Judge(judged)![0].Data, IpcJson.Options)!.AsObject();

        // No role appointed: the park is the owner's question, said as no role's yet.
        var alone = Pushed(facts);
        Assert.Equal(("owner", "question", true),
            (alone["parked:s1a2b3c4"]!["class"]!.GetValue<string>(), alone["parked:s1a2b3c4"]!["waitKind"]!.GetValue<string>(), alone["parked:s1a2b3c4"]!["untaken"]!.GetValue<bool>()));
        Assert.Equal("owner", alone["unanswerable:7a82cc"]!["class"]!.GetValue<string>());
        Assert.Equal("question", alone["unanswerable:7a82cc"]!["waitKind"]!.GetValue<string>());
        Assert.Null(alone["unanswerable:7a82cc"]!["untaken"]);

        // A question supervisor appointed: the park is the agents'.
        var supervised = Pushed(facts with { Takers = Everyone });
        Assert.Equal("agents", supervised["parked:s1a2b3c4"]!["class"]!.GetValue<string>());
        Assert.Null(supervised["parked:s1a2b3c4"]!["waitKind"]);
        Assert.Null(supervised["parked:s1a2b3c4"]!["untaken"]);
        Assert.All(Rows(facts), row => Assert.Null(row["class"]));
    }

    /// <summary>
    /// The full path to the bell starts at rows the judge makes, never rows made with a class preset: what it pushes for these facts (a
    /// park a question supervisor takes, a parked quest no course supervisor takes, a quest nobody can be asked for) is the page's fixture
    /// <c>work/attention/judged.fixture.json</c>, which the page's own test feeds through its filter to the bell, the taskbar and the places.
    /// </summary>
    [Fact]
    public void The_rows_the_judge_pushes_are_the_page_s_fixture_for_its_bell()
    {
        var facts = new AttentionFacts
        {
            Sessions = [Session("""{"state":"awaiting-person"}""")],
            Quests = [JsonNode.Parse("""{"id":"7a82cc","from":"platform","to":"nowhere","title":"A budget","status":"Open","filed":"2026-09-14T09:00:00Z","updated":"2026-09-14T09:00:00Z"}""")!.AsObject()],
            Considered = [JsonNode.Parse("""{"quest":"q2","verdict":"Exhausted","repository":"engine","reason":"three sessions failed","since":"2026-09-14T10:00:00Z"}""")!.AsObject()],
            Registry = [Registration("engine")],
            Takers = new AttentionTakers((kind, _, _) => kind == SupervisorKinds.Question, steward: false),
        };

        var pushed = JsonSerializer.SerializeToNode(AttentionStateSlice.Judge(facts)!.Single(each => each.Partition == AttentionStateSlice.Rows).Data, IpcJson.Options)!;
        var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(SourceFiles.WorkspaceFolder(), "src", "Daoris.Web", "src", "work", "attention", "judged.fixture.json")))!;

        Assert.True(JsonNode.DeepEquals(fixture, pushed), $"the page's fixture is not what the judge pushes; the judge pushes:\n{pushed.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}");
        Assert.Equal(["owner", "agents", "owner"], new[] { "unanswerable:7a82cc", "parked:s1a2b3c4", "parked-quest:q2" }.Select(key => pushed[key]!["class"]!.GetValue<string>()));
    }

    [Fact]
    public void A_kind_the_census_does_not_hold_is_counted_as_the_owner_s_rather_than_hidden()
    {
        Assert.Equal(Agents, Of("""{"kind":"parked","id":"s1"}"""));
        Assert.Equal(Owners("sign-in"), Of("""{"kind":"signed-out","id":"signed-out:claude/x"}"""));
        Assert.Equal(new AttentionOwnership.Side("owner", null), Of("""{"kind":"brand-new","id":"x"}"""));
    }
}
