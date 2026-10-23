using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LIMITRESUME1j (D206; the limit-resume design §3.6, D155 point 4): the agents that wrote the work an opinion reads are every agent
/// the line ran on, never only its last part's. A line cut off by a limit on one maker's agent and carried on by another's has two
/// makers, and a reviewer of either is the work's own family, never another maker's.
/// </summary>
public sealed class OpinionWritersTests
{
    private static PriorSession Record(
        string id, string adapter, string? quest = "q1", string? from = null, string state = "completed", int minute = 0) =>
        new(id, null, state)
        {
            Adapter = adapter, Quest = quest, From = from, Created = new DateTimeOffset(2026, 10, 24, 9, minute, 0, TimeSpan.Zero),
        };

    private static IReadOnlyDictionary<string, PriorSession> Records(params PriorSession[] records) =>
        records.ToDictionary(record => record.Session, StringComparer.Ordinal);

    /// <summary>🔴 Every part of the candidate's line by its <c>from</c>, oldest first, each agent once.</summary>
    [Fact]
    public void The_candidates_line_names_every_agent_it_ran_on()
    {
        var records = Records(
            Record("s0", "codex-acp", quest: null, minute: 0),
            Record("s1", "claude-code-acp", quest: null, from: "s0", minute: 5),
            Record("s2", "claude-code-acp", quest: null, from: "s1", minute: 9));

        Assert.Equal(["codex-acp", "claude-code-acp"], OpinionWriters.Agents(records, "s2", []));
    }

    /// <summary>
    /// 🔴 Every record of this machine's on the chain's quests here, with each one's line, so a part that names no <c>from</c> (one from
    /// before the link) is still counted; a stand-down, which ran nothing of the work, is not; a record of another quest is not.
    /// </summary>
    [Fact]
    public void Every_record_on_the_chains_quests_is_counted_and_a_stand_down_is_not()
    {
        var records = Records(
            Record("s0", "codex-acp", minute: 0),
            Record("s1", "claude-code-acp", minute: 5),
            Record("s3", "dsh", state: "stood-down", minute: 6),
            Record("s4", "stub", quest: "q9", minute: 7),
            Record("p1", "claude-code", quest: null, minute: 1),
            Record("s5", "claude-code-acp", quest: "q2", from: "p1", minute: 8));

        Assert.Equal(["claude-code-acp", "codex-acp", "claude-code"], OpinionWriters.Agents(records, "s1", ["q1", "Q2"]));
    }

    /// <summary>A candidate this machine holds no record of names none of its own; nothing held names nothing.</summary>
    [Fact]
    public void A_candidate_with_no_record_here_names_none_of_its_own()
    {
        Assert.Empty(OpinionWriters.Agents(Records(), "s1", ["q1"]));
        Assert.Equal(["codex-acp"], OpinionWriters.Agents(Records(Record("s0", "codex-acp")), "gone", ["q1"]));
    }

    /// <summary>
    /// 🔴 The row's proof at the walk (§3.6): an opinion asked on a line that ran on two makers refuses a reviewer of either as
    /// another maker's. Every listed reviewer is the work's own family, so none may be independent: <c>not-independent</c>.
    /// </summary>
    [Theory]
    [InlineData("codex-acp")]
    [InlineData("claude-code")]
    [InlineData("codex-acp,claude-code-acp")]
    public async Task An_opinion_on_a_line_of_two_makers_refuses_a_reviewer_of_either(string reviewers)
    {
        var records = Records(
            Record("s0", "codex-acp", minute: 0),
            Record("s1", "claude-code-acp", from: "s0", minute: 5));
        var asked = new List<(string Reviewer, string Label)>();

        var choice = await ReviewerChoice.WalkAsync(
            AdapterSet.Built(), new OpinionRule([.. reviewers.Split(',')], [OpinionRules.Landing]), OpinionWriters.Agents(records, "s1", ["q1"]),
            entry =>
            {
                asked.Add((entry.Family.Agent, entry.Label));
                return Task.FromResult<(HarnessSelection?, ReviewerTried?)>(
                    (null, new ReviewerTried(entry.Family.Agent, entry.Label, ReviewerHeld.Refused, "refused here")));
            });

        Assert.Equal(ReviewerUnavailable.NotIndependent, choice.Code);
        Assert.All(asked, each => Assert.Equal(ReviewerLabels.SameAgent, each.Label));
        Assert.Equal(["OpenAI", "Anthropic"], choice.Working.Select(family => family.Maker));
    }
}
