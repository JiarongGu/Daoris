using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Http;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// QUESTOP1: what a quest carries is answered on every quest route. A kind of operation that gives a quest new state adds
/// a property to <see cref="Quest"/>, and the HTTP answer is one of the places <c>.claude/knowledge/quest-operations.md</c>
/// names for it: a property no route answers is state the page, the driver and a terminal never learn.
/// </summary>
public sealed class QuestResponseTests
{
    /// <summary>What a quest has that the answer leaves out on purpose, each with why.</summary>
    private static readonly Dictionary<string, string> LeftOut = new(StringComparer.Ordinal)
    {
        [nameof(Quest.Name)] = "answered as `short`: what a list calls it, the publisher's short title or a name read from its words",
        [nameof(Quest.EvidenceWanted)] = "read from `requirements` and `answers`, both answered",
        [nameof(Quest.SubmissionCommit)] = "answered inside `submission`, as its `commit` (FABRIC-CLOSE1c5)",
    };

    /// <summary>Every property of a quest is answered under its own name, or left out here with the reason.</summary>
    [Fact]
    public void Every_property_of_a_quest_is_answered_or_left_out_saying_why()
    {
        var answered = typeof(QuestResponse).GetProperties().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        var carried = typeof(Quest).GetProperties().Select(property => property.Name).ToList();

        var unanswered = carried.Where(name => !answered.Contains(name) && !LeftOut.ContainsKey(name)).ToList();
        Assert.True(unanswered.Count == 0,
            $"A quest carries {string.Join(", ", unanswered)}, which no quest route answers: QuestResponse and ToQuest, "
            + "or a reason here (.claude/knowledge/quest-operations.md, the HTTP answer).");
        // A reason for something a quest no longer has, or that the answer now carries, is a stale reason.
        Assert.All(LeftOut.Keys, name => Assert.Contains(name, carried));
        Assert.All(LeftOut.Keys, name => Assert.DoesNotContain(name, answered));
    }

    /// <summary>
    /// SYNCMERGE1b0: an older host answers none of a bring-forward's fields, and its answer reads as it did: a conflict neither lost to the
    /// reopen nor brought forward, nothing settled, nobody named as holding the step, no machine named as this one, and a claim with no
    /// bring-forward. Each field is written only when set, so a newer host's answer without them is the same.
    /// </summary>
    [Fact]
    public void An_older_hosts_answer_reads_with_nothing_settled_held_or_brought_forward()
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        const string Older = """
            {"id":"abcdefabcdef","from":"Asker","to":"Keeper","title":"t","body":"b","status":"Taken","note":null,
             "filed":"2026-10-19T00:00:00+00:00","updated":"2026-10-19T00:00:00+00:00","workspace":"default",
             "links":[],"attachments":[],"then":[],"parent":null,
             "conflicts":[{"machine":"m1","attempted":"Taken","note":null,"at":"2026-10-19T00:00:00+00:00","sequence":3}]}
            """;

        var read = JsonSerializer.Deserialize<QuestResponse>(Older, web)!;

        Assert.Null(read.Settled);
        Assert.Null(read.Holder);
        Assert.Null(read.Here);
        var conflict = Assert.Single(read.Conflicts);
        Assert.Equal((null, null, null), (conflict.LostToTheReopen, conflict.Ended, conflict.Forward));
        Assert.Null(JsonSerializer.Deserialize<QuestClaimResponse>("""{"quest":"abcdefabcdef","claim":"held"}""", web)!.Forward);
    }
}
