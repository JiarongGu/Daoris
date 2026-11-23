using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>The owner's card for applying a change to the policy shows the proposal it names, its edits and the waits they open or release, and refuses what is not a waiting proposal.</summary>
public sealed class PolicyApplyViewTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "policy-apply-view", Guid.NewGuid().ToString("N")[..8]);

    public PolicyApplyViewTests() => Directory.CreateDirectory(Path.GetDirectoryName(DaorisHome.Entry(_home, HomeEntry.DriverConfig))!);

    public void Dispose() => GitSeed.Discard(_home);

    private StubGrounds Grounds()
    {
        var grounds = new StubGrounds();
        grounds.Facts = grounds.Facts with { Home = _home };
        return grounds;
    }

    private PolicyProposal Proposed(params PolicyEdit[] edits) =>
        new PolicyChanges(_home, () => DateTimeOffset.UnixEpoch, new OwnerTurns(_ => [])).Propose(PolicyChannel.Of("s-ask"), edits).Proposal!;

    private Task<PressViewed> View(string proposal, string digest) =>
        new PolicyApplyView().ViewAsync(Grounds(), JsonDocument.Parse($$"""{"proposal":"{{proposal}}","digest":"{{digest}}"}""").RootElement, CancellationToken.None);

    [Fact]
    public async Task A_waiting_proposal_is_shown_with_each_edit_and_each_wait_it_opens()
    {
        var proposal = Proposed(new PolicyEdit("repository/Aurora", "release.start", RiskClasses.Owner, null, null), new PolicyEdit("machine", "line.land", null, null, null));

        var viewed = await View(proposal.Id, proposal.Digest);

        Assert.Equal(("policy-apply", $"daoris settings risk apply {proposal.Id} {proposal.Digest}"), (viewed.View!.Kind, viewed.View.Command));
        Assert.Equal(("s-ask", proposal.BaseRevision), (viewed.View.Facts["session"], viewed.View.Facts["revision"]));
        var also = viewed.View.Also!;
        Assert.Contains(also, said => said.Kind == "policy-apply-set" && said.Facts["place"] == "repository/Aurora" && said.Facts["class"] == "owner");
        Assert.Contains(also, said => said.Kind == "policy-apply-remove" && said.Facts["act"] == "line.land");
        Assert.Contains(also, said => said.Kind == "policy-apply-opens" && said.Facts["act"] == "release.start" && said.Facts["to"] == "owner");
    }

    [Fact]
    public async Task A_cast_and_an_access_mode_are_shown_by_their_own_sentences()
    {
        var proposal = Proposed(PolicyEdit.Cast("repository/Aurora", "reviewer", "agent"), PolicyEdit.Access("machine", "full"));

        var viewed = await View(proposal.Id, proposal.Digest);

        Assert.Contains(viewed.View!.Also!, said => said.Kind == "policy-apply-cast" && said.Facts["role"] == "reviewer" && said.Facts["holder"] == "agent");
        Assert.Contains(viewed.View.Also!, said => said.Kind == "policy-apply-access" && said.Facts["setting"] == "full");
        Assert.Contains(viewed.View.Also!, said => said.Kind == "policy-apply-releases" && said.Facts["act"] == "command.change");
    }

    [Fact]
    public async Task A_proposal_that_opens_and_releases_nothing_says_so()
    {
        var proposal = Proposed(new PolicyEdit("machine", "spend.money", RiskClasses.None, null, null));

        var viewed = await View(proposal.Id, proposal.Digest);

        Assert.Contains(viewed.View!.Also!, said => said.Kind == "policy-apply-none");
    }

    [Theory]
    [InlineData("pc-nothing", "000000000000", "is held here")]
    public async Task An_unknown_proposal_is_refused_with_no_card(string id, string digest, string says)
    {
        var viewed = await View(id, digest);

        Assert.Null(viewed.View);
        Assert.Contains(says, viewed.Refusal);
    }

    [Fact]
    public async Task Another_digest_than_the_proposals_is_refused_with_no_card()
    {
        var proposal = Proposed(new PolicyEdit("machine", "line.land", RiskClasses.Owner, null, null));

        var viewed = await View(proposal.Id, "000000000000");

        Assert.Null(viewed.View);
        Assert.Contains("is not the digest", viewed.Refusal);
    }
}
