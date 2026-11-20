using System.Text.Json;
using System.Text.Json.Nodes;
using Shenora.Core.Ipc;
using static Daoris.Desktop.Modules.Tests.AttentionTestRows;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Every kind the census classes is one the judge makes from facts, and every row it pushes carries its class: one set of facts makes a
/// row of each of the census's kinds, and judged with no role appointed and with every role appointed, each pushed row is the owner's
/// with one of the six kinds of wait, or the agents' with none. A kind the facts cannot reach, or a row pushed unclassed, fails here.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class AttentionEveryKindTests
{
    /// <summary>The six kinds only the owner settles, as a row carries them.</summary>
    private static readonly string[] OwnersKinds = ["question", "go-ahead", "release", "sign-in", "money", "confirm"];

    private static readonly HashSet<string> CensusKinds = AttentionOwnership.Census.Select(each => each.Kind).ToHashSet(StringComparer.Ordinal);

    private static JsonObject O(string json) => JsonNode.Parse(json)!.AsObject();

    /// <summary>
    /// One set of facts that makes at least one row of every kind of the census, each by an id of its own; each input's shape is the one
    /// the tests of its kind make it with. Its start held on accounts is a signed-out one, so with no role appointed every row is the owner's.
    /// </summary>
    internal static AttentionFacts EveryKind() => new()
    {
        Sessions =
        [
            // An intake parked asking on its live ask (`intake`), and a session parked to ask (`parked`).
            Session("""{"id":"s-intake","kind":"intake","state":"awaiting-person","updated":"2026-09-21T10:05:00Z"}"""),
            Session("""{"id":"s-park","state":"awaiting-person","updated":"2026-09-21T10:10:00Z"}"""),
            // Runs placed in Needs you for their Accept (`review`) and for work left (`left`).
            Session("""{"id":"s-review","quest":"q-review","state":"completed","updated":"2026-09-21T11:00:00Z"}"""),
            Session("""{"id":"s-left","quest":"q-left","state":"completed","updated":"2026-09-21T11:05:00Z"}"""),
        ],
        Quests =
        [
            // A done its departure holds for the owner's yes (`departure`).
            O("""{"id":"q-held","from":"platform","to":"engine","status":"Done","held":true,"answers":[{"requirement":1,"departed":"The cap is per chunk."}],"filed":"2026-09-14T09:00:00Z","updated":"2026-09-21T09:00:00Z"}"""),
            // A set-up step shown for the owner's look (`set-up`).
            O("""{"id":"q-setup","from":"platform","to":"engine","status":"Done","setUpIn":"staging","setUps":[{"machine":"m1","sequence":1,"at":"2026-09-21T09:30:00Z","shows":"The menu."}],"verdicts":[],"filed":"2026-09-14T09:00:00Z","updated":"2026-09-21T09:30:00Z"}"""),
            // An open quest to a repository this deployment does not hold (`unanswerable`).
            O("""{"id":"q-nowhere","from":"platform","to":"nowhere","status":"Open","filed":"2026-09-14T09:00:00Z","updated":"2026-09-14T09:00:00Z"}"""),
        ],
        Registry = [Registration("engine")],
        Asks =
        [
            O("""{"id":"a-intake","workspace":"default","state":"Open","intake":"s-intake","asked":"2026-09-21T08:30:00Z","updated":"2026-09-21T08:30:00Z"}"""),
            // A proposed split (`proposal`) with a go-ahead still asked (`go-ahead`) and a plan's question (`plan-question`).
            O("""
              {"id":"a-proposed","workspace":"default","state":"Proposed","asked":"2026-09-21T08:40:00Z","updated":"2026-09-21T08:40:00Z",
               "proposal":[{"repository":"engine"}],
               "goAheads":[{"number":1,"kind":"push","on":"prod","act":"the release","state":"asked","asked":[{"session":"s-asker","at":"2026-09-21T09:10:00Z","why":"It ships."}]}],
               "planQuestions":[{"key":"scope","question":"Which scope?"}]}
              """),
        ],
        Brainstorms = [O("""{"id":"b-design","pass":"design","workspace":"default","updated":"2026-09-21T09:00:00Z","answered":[],"comparison":{"questions":[{"key":"store","question":"One store?"}]}}""")],
        Untrusted = [O("""{"folder":"trees/engine","trustFile":"trust.json"}""")],
        Proposals = [O("""{"id":"p-rule","state":"waiting","scope":"machine","proposed":"2026-09-21T09:00:00Z","why":"Run the tests unasked."}""")],
        Considered =
        [
            O("""{"quest":"q-exhausted","verdict":"Exhausted","repository":"engine","since":"2026-09-21T09:20:00Z"}"""),
            O("""{"quest":"q-policy","verdict":"PolicyHeld","repository":"engine","since":"2026-09-21T09:21:00Z","said":{"code":"moves-bound"}}"""),
            O("""{"quest":"q-unclosed","verdict":"Unclosed","repository":"engine","since":"2026-09-21T09:22:00Z","said":{"session":"s-unclosed"}}"""),
            O("""{"quest":"q-stopped","verdict":"Stopped","repository":"engine","since":"2026-09-21T09:23:00Z","said":{"code":"steward","session":"s-steward"}}"""),
        ],
        Groups =
        [
            O("""{"session":"s-review","group":"needs-you","reason":"review","shown":"completed","archived":false,"teammate":false}"""),
            O("""{"session":"s-left","group":"needs-you","state":"needs-you","reason":"left","shown":"completed","archived":false,"teammate":false}"""),
        ],
        Opinions = [O("""{"session":"s-opinion","repository":"engine","workspace":"default","since":"2026-09-21T09:24:00Z"}""")],
        Helpers = [O("""{"id":"h-1","session":"s-helper","question":"May a helper read the logs?","at":"2026-09-21T09:25:00Z"}""")],
        Quiet = [O("""{"session":"s-quiet","quest":"q-quiet","since":"2026-09-21T09:26:00Z","minutes":30,"busy":false,"kind":"driven"}""")],
        Backs = [O("""{"session":"s-back","quest":"q-back","words":"The move back was refused.","at":"2026-09-21T09:27:00Z"}""")],
        HandOvers = [O("""{"id":"ho-1","session":"s-handover","note":"Hand it to another agent.","agent":"codex","at":"2026-09-21T09:28:00Z"}""")],
        Held = [O("""{"unit":"u-held","node":"close","attempt":1,"at":"2026-09-21T09:29:00Z"}""")],
        Requests = [O("""{"run":"run-declined","node":"ask","quest":"q-request","to":"platform","work":"w-1","repository":"engine","workspace":"default","since":"2026-09-21T09:30:00Z"}""")],
        Steps = [O("""{"run":"run-step","node":"sign","why":"task","role":"acceptor","holder":"person","repository":"engine","workspace":"default","since":"2026-09-21T09:31:00Z","presses":[]}""")],
        Integration = [O("""{"id":"int-decide","wait":"decide","repository":"engine","workspace":"default","line":"main","candidate":"c-1","since":"2026-09-21T09:32:00Z"}""")],
        Permissions = [O("""{"session":"s-permission","request":"r-1","since":"2026-09-21T09:33:00Z"}""")],
        Answers = [O("""{"session":"s-answer","since":"2026-09-21T09:34:00Z"}""")],
        Memory = [O("""{"session":"s-memory","ends":1,"replacements":0,"staged":0,"since":"2026-09-21T09:35:00Z"}""")],
        // An account of Daoris's signed out with no start naming it (`signed-out`), and a start held on another agent's signed-out account (`account-wait`).
        Doors = [O("""{"door":"claude-code","accountOf":"claude","product":"Claude Code","present":true,"profiles":[{"name":"work","home":"homes/work","login":"out","read":"2026-09-21T07:00:00Z"}]}""")],
        Waits = [O("""{"agent":"codex","signedOut":["spare"],"quests":["q-account"],"workspace":"default"}""")],
        Confirmations = [O("""{"id":"c-1","module":"sessions","route":"STOP","asker":{"process":"daoris","pid":4242},"asked":"2026-09-21T09:36:00Z","expires":"2026-09-21T09:41:00Z","shownAt":null}""")],
        Space = [O("""{"volume":"vol-1","root":"vol-1","free":1,"total":100,"floor":5,"freed":0,"could":0,"said":"2026-09-21T09:37:00Z"}""")],
        Git = [O("""{"repository":"engine","workspace":"default","remote":"origin","host":"git.example","class":"refused","holds":"push","words":"Permission denied.","since":"2026-09-21T09:38:00Z"}""")],
        GitAsks = [O("""{"id":"ga-1","repository":"engine","workspace":"default","remote":"origin","host":"git.example","class":"ask","session":"s-git","at":"2026-09-21T09:39:00Z"}""")],
        SecondCheck = [O("""{"session":"s-ask-daoris","range":"0-3","index":0,"says":"A flag.","certainty":"high","at":"2026-09-21T09:40:00Z"}""")],
    };

    /// <summary>The kinds the census holds that <paramref name="made"/> lacks, and those it has that the census does not hold.</summary>
    private static string Mismatch(IEnumerable<string> made)
    {
        var kinds = made.ToHashSet(StringComparer.Ordinal);
        var missing = CensusKinds.Except(kinds).Order(StringComparer.Ordinal);
        var extra = kinds.Except(CensusKinds).Order(StringComparer.Ordinal);
        return $"missing: [{string.Join(", ", missing)}]; extra: [{string.Join(", ", extra)}]";
    }

    [Fact]
    public void Every_kind_of_the_census_is_made_by_the_every_kind_facts()
    {
        var made = NeedsAPerson.Rows(EveryKind()).Select(row => row["kind"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        Assert.True(made.SetEquals(CensusKinds), Mismatch(made));
    }

    [Fact]
    public void Every_row_judged_has_a_class_and_the_owner_s_a_kind_of_wait()
    {
        static List<JsonObject> Judged(AttentionFacts facts) =>
        [
            .. JsonSerializer.SerializeToNode(AttentionStateSlice.Judge(facts)!.Single(each => each.Partition == AttentionStateSlice.Rows).Data, IpcJson.Options)!
                .AsObject().Select(each => each.Value!.AsObject()),
        ];

        var alone = Judged(EveryKind());
        var appointed = Judged(EveryKind() with { Takers = new AttentionTakers((_, _, _) => true, steward: true) });

        foreach (var rows in new[] { alone, appointed })
        {
            var kinds = rows.Select(row => row["kind"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
            Assert.True(CensusKinds.IsSubsetOf(kinds), Mismatch(kinds));
            foreach (var row in rows)
            {
                var name = $"{row["kind"]}:{row["id"]}";
                var side = row["class"]?.GetValue<string>();
                Assert.True(side is "owner" or "agents", $"{name} is pushed with class {side ?? "none"}");
                if (side == "owner")
                {
                    Assert.True(row["waitKind"] is JsonValue wait && OwnersKinds.Contains(wait.GetValue<string>()), $"{name} is the owner's with no kind of wait");
                }
                else
                {
                    Assert.True(row["waitKind"] is null, $"{name} is the agents' and carries a kind of wait");
                    Assert.True(row["untaken"] is null, $"{name} is the agents' and is said as no role's");
                }
            }
        }

        // No role appointed: every row is the owner's, its start on accounts a signed-out one, which no cool-off clears.
        Assert.All(alone, row => Assert.Equal("owner", row["class"]!.GetValue<string>()));

        // Every role appointed: the agents' are the kinds a role takes, and the kinds no role's shape takes yet stay the owner's.
        Assert.Equal(
            ["answer", "opinion", "parked", "parked-quest", "permission", "policy-hold", "review", "rule", "steward-stopped", "work"],
            appointed.Where(row => row["class"]!.GetValue<string>() == "agents").Select(row => row["kind"]!.GetValue<string>())
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    }
}
