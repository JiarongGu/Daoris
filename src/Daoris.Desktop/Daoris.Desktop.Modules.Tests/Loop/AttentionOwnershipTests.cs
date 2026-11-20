using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Shenora.Core.Ipc;
using static Daoris.Desktop.Modules.Tests.AttentionTestRows;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Whose each attention row is (the window design §5.2): the census classes every kind the judge makes as the owner's or the agents',
/// with its reason, a conditional kind naming both its sides, and fails a kind it does not class. The rules are held on sources made in
/// memory, one case each, and then run over the loop's own sources, as the structure gate is; the slice pushes each row's class.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed partial class AttentionOwnershipTests
{
    /// <summary>The six kinds only the owner settles, as a row carries them.</summary>
    private static readonly string[] OwnersKinds = ["question", "go-ahead", "release", "sign-in", "money", "confirm"];

    /// <summary>A kind a judge makes: a row made with its kind, a row's kind set, or a rows' maker's own kind.</summary>
    [GeneratedRegex("""Row\("(?<kind>[a-z][a-z-]*)"|\["kind"\] = "(?<kind>[a-z][a-z-]*)"|const string Kind = "(?<kind>[a-z][a-z-]*)";""")]
    private static partial Regex Made();

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

    private static List<(string Path, string Text)> LoopSources()
    {
        var loop = Path.Combine(SourceFiles.Project(SourceFiles.ModulesProject), "Loop");
        return [.. Directory.EnumerateFiles(loop, "*.cs").Select(file => ($"Loop/{Path.GetFileName(file)}", File.ReadAllText(file)))];
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

    [Fact]
    public void Every_kind_the_loop_makes_has_one_class()
    {
        var sources = LoopSources();
        Assert.Empty(Breaches(sources));
        // The walk still reaches the judges: today's 36 kinds (design §5.2).
        Assert.Equal(36, AttentionOwnership.Census.Count);
        Assert.Equal(AttentionOwnership.Census.Count, AttentionOwnership.Census.Select(each => each.Kind).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// Each class says whose its kind is and why: the owner's one of the six kinds, the agents' the role that takes it, and a conditional
    /// kind both, with what tells a row's side and why the other side is the agents'.
    /// </summary>
    [Fact]
    public void Each_class_says_whose_with_its_reason_and_a_conditional_kind_names_both_sides()
    {
        foreach (var each in AttentionOwnership.Census)
        {
            Assert.False(string.IsNullOrWhiteSpace(each.Reason), each.Kind);
            Assert.True(each.Wait is not null || each.Takes is not null, $"{each.Kind} says neither side");
            if (each.Wait is { } wait) Assert.Contains(wait, OwnersKinds);
            if (each.Takes is { } takes) Assert.Contains(takes, (string[])["supervisor", "steward", "reviewer", "approver", "holder"]);
            var conditional = each.Wait is not null && each.Takes is not null;
            Assert.Equal(conditional, each.OwnersWhen is not null);
            Assert.Equal(conditional, !string.IsNullOrWhiteSpace(each.Otherwise));
        }
    }

    /// <summary>The design's short list (§5.2), kind by kind: who settles each, the owner by which kind of wait.</summary>
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
    [InlineData("account-wait", "sign-in", "supervisor")]
    [InlineData("policy-hold", "money", "supervisor")]
    [InlineData("work", "go-ahead", "steward")]
    [InlineData("parked", null, "supervisor")]
    [InlineData("left", null, "supervisor")]
    [InlineData("parked-quest", null, "supervisor")]
    [InlineData("opinion", null, "reviewer")]
    [InlineData("second-check", null, "reviewer")]
    [InlineData("steward-stopped", null, "steward")]
    [InlineData("proposal", null, "supervisor")]
    public void Classes_each_kind_as_the_design_says(string kind, string? wait, string? takes)
    {
        var each = Assert.Single(AttentionOwnership.Census, one => one.Kind == kind);
        Assert.Equal((wait, takes), (each.Wait, each.Takes));
    }

    // ---- a row's side ----

    private static (string Class, string? Wait) Of(string row) => AttentionOwnership.Of(JsonNode.Parse(row)!.AsObject());

    [Fact]
    public void A_session_branch_s_clean_up_is_the_agents_and_one_nothing_names_may_be_the_owner_s_data()
    {
        // A tree its session recorded, and a standing `daoris/` branch nothing recorded: the agents' own bookkeeping.
        Assert.Equal(("agents", null), Of("""{"kind":"work","id":"s1","session":"s1","title":"daoris/s1","blocked":{"unknown":false,"entries":[{"kind":"tree","code":"unfollowed","facts":{"branch":"daoris/s1"}}]}}"""));
        Assert.Equal(("agents", null), Of("""{"kind":"work","id":"u1","title":"daoris/s-c4a7c4a7","blocked":{"unknown":true,"entries":[{"kind":"branch","code":"unrecorded","facts":{"branch":"daoris/s-c4a7c4a7"}}]}}"""));
        // A held attempt, a record that does not read and a close in part are the agents' too.
        Assert.Equal(("agents", null), Of("""{"kind":"work","id":"u#n#1","held":{"unit":"u"}}"""));
        Assert.Equal(("agents", null), Of("""{"kind":"work","id":"record#f","unreadable":{"file":"f"}}"""));
        // A branch nothing records and nothing names the agents': deleting it may delete the owner's own work.
        Assert.Equal(("owner", "go-ahead"), Of("""{"kind":"work","id":"u2","title":"feature/mine","blocked":{"unknown":true,"entries":[{"kind":"branch","code":"unknown-branch","facts":{"branch":"feature/mine"}}]}}"""));
    }

    [Fact]
    public void A_start_waiting_on_accounts_is_the_owner_s_only_where_a_sign_in_clears_it()
    {
        Assert.Equal(("owner", "sign-in"), Of("""{"kind":"account-wait","id":"a","account":{"named":[{"id":"x","state":"out"}]}}"""));
        Assert.Equal(("owner", "sign-in"), Of("""{"kind":"account-wait","id":"a","account":{"named":[{"id":"x","state":"refused"}]}}"""));
        Assert.Equal(("owner", "sign-in"), Of("""{"kind":"account-wait","id":"a","account":{"named":[],"noAccount":true}}"""));
        Assert.Equal(("agents", null), Of("""{"kind":"account-wait","id":"a","account":{"named":[{"id":"x","state":"cooling","until":"2026-11-20T12:00:00Z"}]}}"""));
    }

    [Fact]
    public void A_held_answer_is_the_owner_s_where_their_own_rule_held_it()
    {
        Assert.Equal(("owner", "go-ahead"), Of("""{"kind":"answer","id":"s1","answer":{"session":"s1","rule":{"place":"machine","n":3,"at":3}}}"""));
        Assert.Equal(("agents", null), Of("""{"kind":"answer","id":"s1","answer":{"session":"s1"}}"""));
    }

    /// <summary>
    /// Each pushed row carries whose it is: the owner's with its kind of wait, the agents' with none, so the page counts the owner's
    /// alone and lists the agents' apart. The rows the judge itself makes carry none: the class is the push's, read from the row whole.
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

        var rows = JsonSerializer.SerializeToNode(AttentionStateSlice.Judge(facts)![0].Data, IpcJson.Options)!.AsObject();
        Assert.Equal("agents", rows["parked:s1a2b3c4"]!["class"]!.GetValue<string>());
        Assert.Null(rows["parked:s1a2b3c4"]!["waitKind"]);
        Assert.Equal("owner", rows["unanswerable:7a82cc"]!["class"]!.GetValue<string>());
        Assert.Equal("question", rows["unanswerable:7a82cc"]!["waitKind"]!.GetValue<string>());
        Assert.All(Rows(facts), row => Assert.Null(row["class"]));
    }

    [Fact]
    public void A_kind_the_census_does_not_hold_is_counted_as_the_owner_s_rather_than_hidden()
    {
        Assert.Equal(("agents", null), Of("""{"kind":"parked","id":"s1"}"""));
        Assert.Equal(("owner", "sign-in"), Of("""{"kind":"signed-out","id":"signed-out:claude/x"}"""));
        Assert.Equal(("owner", null), Of("""{"kind":"brand-new","id":"x"}"""));
    }
}
