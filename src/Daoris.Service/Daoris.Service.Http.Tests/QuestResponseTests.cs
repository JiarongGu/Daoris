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
}
