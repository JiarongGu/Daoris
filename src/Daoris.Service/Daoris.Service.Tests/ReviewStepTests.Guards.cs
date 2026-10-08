using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// REVIEWENV1b3 (D154's REVIEWENV1b3 note; the review environment design §1.5, §3.5): the review on the record cannot be
/// decided by an agent or by a stale press. Each test here was seen failing on the defect a read-only review claimed, before
/// its fix: a verdict that named no set-up approved one the person never saw, an agent's quote set `off`, a set-up said again
/// at another address held the step on one machine and not on the next, and the late set-up door judged the wrong part of
/// the chain.
/// </summary>
public sealed partial class ReviewStepTests
{
    // ——— (2) A verdict answers the set-up the person's view showed, never the newest by default.

    /// <summary>
    /// 🔴 The person looked at the first set-up; the step's session then posted a second. A `reviewed` or a `not-yet` that names
    /// no set-up is refused as a shape, so it cannot approve the second unseen; one naming the first is refused as stale; and
    /// nothing is kept. A skip names none, and needs none.
    /// </summary>
    [Fact]
    public async Task A_verdict_that_names_no_set_up_is_refused_and_one_naming_an_older_set_up_is_stale()
    {
        var (_, _, setUp) = await Built();
        var (session, shown) = await Shown(setUp);
        var looked = shown.SetUps[0].Ref!;
        await _ledger.ReadyAsync(session.Id, Look, "Shown again, by the session.", "Open it.", null, Now);
        var newer = await _exchange.PostSetUpAsync(setUp.Id, new QuestSetUpPost(Later, "local", Session: session.Id), Now);
        Assert.Equal(2, newer.Quest!.SetUps.Count);

        foreach (var said in new[] { Reviews.Reviewed, Reviews.NotYet })
        {
            var unnamed = await _exchange.ReviewAsync(setUp.Id, said, "the words", null, Now);
            Assert.Equal(QuestRespondRefusal.BadReviewVerdict, unnamed.Refusal);
            Assert.Contains("names the set-up you looked at", unnamed.Message);
        }

        var stale = await _exchange.ReviewAsync(setUp.Id, Reviews.Reviewed, null, looked, Now);
        Assert.Equal(QuestRespondRefusal.ReviewRefused, stale.Refusal);
        Assert.Contains("shown again since", stale.Message);
        var stands = (await _quests.FindAsync(setUp.Id))!;
        Assert.Empty(stands.Verdicts);
        Assert.Equal(QuestHold.Unreviewed, stands.Hold);

        var reviewed = await _exchange.ReviewAsync(setUp.Id, Reviews.Reviewed, null, newer.Quest.SetUps[^1].Ref, Now);
        Assert.Equal(QuestRespondRefusal.None, reviewed.Refusal);
        Assert.Equal(Later, Assert.Single(reviewed.Quest!.Verdicts).Commit);
    }

    // ——— (3), (4) An agent never sets `off`: from an agent it is a proposal awaiting the person's press.

    /// <summary>
    /// 🔴 An intake quoting the person's words exactly, with `off`, sets no choice: `off` lowers a gate, so it is only the
    /// person's own press. The publish goes ahead, and the choice is kept on the ask as a proposal, with their words as its
    /// reason and the session that made it. A quoted environment still sets the chain's choice, as designed.
    /// </summary>
    [Fact]
    public async Task An_agents_off_is_kept_as_a_proposal_however_it_quotes_the_person()
    {
        var ask = await Asked();

        var published = await Publish(ask, [], new QuestReview(Reviews.Off, "add the compare setting"), session: "s1a2b3c4", title: "Quoted off");

        Assert.Equal(AskRefusal.None, published.Refusal);
        Assert.Null(published.Quest!.Review);
        Assert.Contains("only the person's own press", published.Message);
        var kept = (await _desk.FindAsync(ask.Id))!;
        Assert.Empty(kept.ReviewChoices);
        var proposal = Assert.Single(kept.ReviewProposals);
        Assert.Equal((Reviews.Off, "s1a2b3c4", published.Quest.Id), (proposal.Choice, proposal.Session, proposal.Quest));
        Assert.Contains("\"add the compare setting\"", proposal.Reason);
        Assert.True(proposal.Reason.Length <= Reviews.ReasonLimit);

        // The exchange's own door says the same of a session publishing as the ask, whichever door it came through.
        var direct = await _exchange.PublishAsync(
            new QuestAsk(AskDesk.SenderOf(ask.Id), "reports", "Direct off", "b")
            {
                Review = new QuestReview(Reviews.Off, "run it locally against dev data"), PublishedBy = "s1a2b3c4", Workspace = "work",
            },
            Now);
        Assert.Equal(QuestPublishRefusal.None, direct.Refusal);
        Assert.Null(direct.Quest!.Review);
        Assert.Equal(2, (await _desk.FindAsync(ask.Id))!.ReviewProposals.Count);

        var environment = await Publish(ask, [], new QuestReview("local", "run it locally against dev data"), session: "s1a2b3c4", title: "Quoted local");
        Assert.Equal(new QuestReview("local", "run it locally against dev data"), environment.Quest!.Review);
    }

    /// <summary>
    /// 🔴 Whoever publishes is an agent unless it is the person's own door, whether or not it names a session: an agent's door
    /// that names none still sets no choice off the person's words, and its `off` is refused unquoted as any agent's is.
    /// </summary>
    [Fact]
    public async Task An_agent_that_names_no_session_has_no_persons_authority()
    {
        var ask = await Asked();

        var refused = await _desk.PublishAsync(
            ask.Id, "reports", Now,
            draft: new AskDraft("Unquoted off", "b") { Review = new QuestReview(Reviews.Off) },
            byAgent: true);

        Assert.Equal(AskRefusal.QuestRefused, refused.Refusal);
        Assert.Contains("only on the person's own words", refused.Message);
        Assert.Empty((await _desk.FindAsync(ask.Id))!.Quests);

        var direct = await _exchange.PublishAsync(
            new QuestAsk("checker", "reports", "A connector's off", "b") { Review = new QuestReview(Reviews.Off), ByAgent = true }, Now);
        Assert.Equal(QuestPublishRefusal.BadReview, direct.Refusal);
    }

    // ——— (5) A set-up's identity survives the wire, so every machine dedupes one set-up alike.

    /// <summary>
    /// 🔴 The person's own local set-up, reviewed, then a second at another address with the same commit and words. Here it is
    /// a second set-up and holds the step again; across the wire, which leaves a local address behind, it must read the same,
    /// and a remote must take the push whole rather than refuse it as a set-up already kept.
    /// </summary>
    [Fact]
    public async Task A_set_up_said_again_at_another_address_reads_alike_here_and_across_the_wire()
    {
        var (_, _, setUp) = await Built();
        var first = await _exchange.PostSetUpAsync(
            setUp.Id, new QuestSetUpPost(Commit, "local", Look: "http://localhost:4200/reports/7", Shows: "The report."), Now);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.PersonDoneAsync(setUp.Id, null, Now)).Refusal);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.ReviewAsync(setUp.Id, Reviews.Reviewed, null, first.Quest!.SetUps[0].Ref, Now)).Refusal);

        var again = await _exchange.PostSetUpAsync(
            setUp.Id, new QuestSetUpPost(Commit, "local", Look: "http://localhost:4300/reports/7", Shows: "The report."), Now);
        var retried = await _exchange.PostSetUpAsync(
            setUp.Id, new QuestSetUpPost(Commit, "local", Look: "http://localhost:4300/reports/7", Shows: "The report."), Now);
        Assert.Contains("already holds this set-up", retried.Message);

        var here = (await _quests.FindAsync(setUp.Id))!;
        Assert.Equal((2, QuestHold.Unreviewed), (here.SetUps.Count, here.Hold));
        Assert.Equal(2, again.Quest!.SetUps.Count);

        var crossed = QuestWire.ReadPush(QuestWire.Push(0, await _quests.HistoryAsync(setUp.Id)))!.Value.Operations;
        var there = QuestLog.Replay(crossed)!;
        Assert.Equal((here.SetUps.Count, here.Hold), (there.SetUps.Count, there.Hold));

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var remote = await QuestStore.OpenAsync(connection);
        var push = await remote.ReceiveAsync(0, crossed, _ => null, _ => "work");
        Assert.Empty(push.Refused);
        Assert.Equal(QuestHold.Unreviewed, (await remote.FindAsync(setUp.Id))!.Hold);
    }

    /// <summary>
    /// Each set-up this machine says carries its identity, made from what it says with its local address, and the same
    /// identity crosses the wire. A set-up a build from before kept carries none and reads back as it was; beside another, it
    /// is compared by what crosses, so a local address, which the wire drops, never decides.
    /// </summary>
    [Fact]
    public async Task A_set_up_carries_its_identity_across_and_one_from_before_reads_as_it_was()
    {
        var (_, _, setUp) = await Built();
        var kept = await _exchange.PostSetUpAsync(
            setUp.Id, new QuestSetUpPost(Commit, "local", Look: "http://localhost:4200/reports/7", Shows: "The report."), Now);
        var made = Assert.Single(kept.Quest!.SetUps);
        Assert.Equal(Reviews.IdentityOf(made), made.Id);
        var crossed = QuestWire.ReadPush(QuestWire.Push(0, await _quests.HistoryAsync(setUp.Id)))!.Value.Operations;
        Assert.Equal(made.Id, crossed[^1].SetUp!.Id);
        Assert.Null(crossed[^1].SetUp!.Look);

        // As a build from before wrote one: no identity.
        var old = QuestWire.ReadPush($$"""
            { "base": 0, "operations": [{ "machine": "m1", "sequence": 9, "quest": "{{setUp.Id}}", "at": "2026-10-08T09:00:00Z",
              "kind": "setup", "setUp": { "commit": "{{Commit}}", "shows": "The report.", "local": true } }] }
            """)!.Value.Operations[0].SetUp!;
        Assert.Equal(new QuestSetUp(Commit) { Shows = "The report.", Local = true }, old);
        Assert.True(old.Same(new QuestSetUp(Commit) { Look = "http://localhost:4300/other", Shows = "The report.", Local = true }));
        Assert.True(old.Same(made));
        Assert.False(new QuestSetUp(Commit) { Look = "https://dev.example.test/a" }.Same(new QuestSetUp(Commit) { Look = "https://dev.example.test/b" }));
    }

    // ——— (6) The late set-up door reads the repository's part of the whole chain.

    /// <summary>
    /// 🔴 A chain whose later part composes a set-up step for another repository leaves this repository's own *Set it up*
    /// free: the set-up step it publishes is this repository's, following its last step.
    /// </summary>
    [Fact]
    public async Task Another_repositorys_set_up_step_in_the_chain_leaves_this_ones_door_open()
    {
        var ask = await Asked();
        var published = await Publish(ask, [new QuestStep("checker", "Check {parent}", "Look."), SetUpStep("checker")], title: "Two repositories");
        Assert.Equal(AskRefusal.None, published.Refusal);
        var build = published.Quest!;
        await _exchange.RespondAsync(build.Id, "take", null, Now);
        await _exchange.RespondAsync(build.Id, "done", "Built.", Now, answers: [Met]);

        var pressed = await _exchange.PublishSetUpStepAsync(build.Id, "local", Now);

        Assert.Equal(QuestRespondRefusal.None, pressed.Refusal);
        Assert.Equal(("reports", build.Id, "local"), (pressed.Quest!.To, pressed.Quest.Parent, pressed.Quest.SetUpIn));
    }

    /// <summary>
    /// 🔴 A set-up step follows the repository's last work step in the chain, so its tree holds all of it, and there is one per
    /// repository per chain: a press after work that a later step of the same repository adds to is refused naming that
    /// step, and so is a second set-up step once one follows the last step, whichever quest of the chain the press follows.
    /// </summary>
    [Fact]
    public async Task The_late_set_up_door_follows_the_repositorys_last_step_and_is_one_per_chain()
    {
        var ask = await Asked();
        var published = await Publish(ask, [new QuestStep("reports", "Polish {parent}", "More of it.")], title: "Two steps here");
        var first = published.Quest!;
        await _exchange.RespondAsync(first.Id, "take", null, Now);
        await _exchange.RespondAsync(first.Id, "done", "Built.", Now, answers: [Met]);
        var second = (await FollowUps(ask, first)).Single();

        var early = await _exchange.PublishSetUpStepAsync(first.Id, "local", Now);
        Assert.Equal(QuestRespondRefusal.ReviewRefused, early.Refusal);
        Assert.Contains($"`#{second.Id}` asks `reports` after it", early.Message);

        await _exchange.RespondAsync(second.Id, "take", null, Now);
        await _exchange.RespondAsync(second.Id, "done", "Polished.", Now, answers: [Met]);
        var last = await _exchange.PublishSetUpStepAsync(second.Id, "local", Now);
        Assert.Equal(QuestRespondRefusal.None, last.Refusal);

        var again = await _exchange.PublishSetUpStepAsync(first.Id, "dev", Now);
        Assert.Equal(QuestRespondRefusal.ReviewRefused, again.Refusal);
        Assert.Single(await _quests.ListAsync(includeClosed: true), quest => quest.SetUpIn is not null && quest.To == "reports");
    }

    /// <summary>
    /// 🔴 A later step of the same repository still to come, not yet published, holds the door the same way, named by its
    /// place in the chain.
    /// </summary>
    [Fact]
    public async Task A_later_step_of_the_same_repository_still_to_come_holds_the_late_door()
    {
        var ask = await Asked();
        var published = await Publish(
            ask, [new QuestStep("checker", "Check {parent}", "Look."), new QuestStep("reports", "Polish {parent}", "More of it.")],
            title: "Back to reports");
        var build = published.Quest!;
        await _exchange.RespondAsync(build.Id, "take", null, Now);
        await _exchange.RespondAsync(build.Id, "done", "Built.", Now, answers: [Met]);

        var refused = await _exchange.PublishSetUpStepAsync(build.Id, "local", Now);

        Assert.Equal(QuestRespondRefusal.ReviewRefused, refused.Refusal);
        Assert.Contains("step 2 of its chain asks `reports` after it", refused.Message);
    }
}
