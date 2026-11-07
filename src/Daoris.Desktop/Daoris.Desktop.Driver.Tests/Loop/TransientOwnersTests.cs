using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Each kind of pass hands its retry its own authority as the check, so no try reopens what is
/// already done: a helper's answer stored, an intake's ask settled, an opinion recorded, a partner's piece ending.
/// </summary>
public sealed class TransientOwnersTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "transient-owners", Guid.NewGuid().ToString("N")[..8]);

    public TransientOwnersTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    [Fact]
    public async Task A_helper_is_tried_again_until_its_answer_is_stored()
    {
        var files = new HelperFiles(_home);
        var wanted = TransientOwners.Helper(new SessionProcesses(), "s1", files, "a1");
        Assert.True(await wanted(CancellationToken.None));

        Directory.CreateDirectory(Path.GetDirectoryName(files.PathOf("a1", "answer"))!);
        File.WriteAllText(
            files.PathOf("a1", "answer"),
            new JsonObject { ["id"] = "a1", ["helper"] = "codex-acp", ["answer"] = "Use the config.", ["at"] = Moments.Write(At) }.ToJsonString());

        Assert.False(await wanted(CancellationToken.None));
    }

    [Theory]
    [InlineData("started", true)]
    [InlineData("ending", false)]
    [InlineData("answered", false)]
    public async Task A_partner_is_tried_again_only_while_its_piece_has_not_ended(string state, bool tried)
    {
        var files = new PartnerFiles(_home);
        files.WriteState(new PartnerState("p1", state, null, At));

        Assert.Equal(tried, await TransientOwners.Partner(new SessionProcesses(), "s1", files, "p1")(CancellationToken.None));
    }

    [Fact]
    public async Task An_opinion_is_tried_again_until_the_host_holds_it_given_and_an_unreadable_host_costs_a_try()
    {
        var reading = new OpinionView("o1", "landing", "first", "s1", "r1", "web-app", "0000", "1111", "codex-acp", ReviewerLabels.AnotherMaker, OpinionViews.Reading);
        var given = reading with { State = OpinionViews.GivenState, Findings = [] };

        Assert.True(await TransientOwners.Opinion(new SessionProcesses(), "s1", _ => Task.FromResult<OpinionView?>(reading))(CancellationToken.None));
        Assert.False(await TransientOwners.Opinion(new SessionProcesses(), "s1", _ => Task.FromResult<OpinionView?>(given))(CancellationToken.None));
        Assert.True(await TransientOwners.Opinion(new SessionProcesses(), "s1", _ => throw new DriverException("host gone"))(CancellationToken.None));
    }

    [Theory]
    [InlineData("Proposed", 0, false)]
    [InlineData("Proposed", 1, true)]
    [InlineData("Closed", 0, true)]
    public void An_intakes_ask_is_settled_where_it_was_closed_or_gained_quests(string state, int quests, bool settled)
    {
        var ask = new AskView("a1", "work", "use the config", state, "intake") { Quests = [.. Enumerable.Range(0, quests).Select(at => $"q{at}")] };

        Assert.Equal(settled, IntakeObservation.Settled(0, ask));
        Assert.True(IntakeObservation.Settled(0, null));
    }
}
