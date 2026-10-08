using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEWENV1b2: the review on the record (REVIEWENV1b, D154 points 3, 4, 8 and 9; the review environment design §2.6, §3.5) as
/// the driver reads it from the service: a quest's chain choice, a set-up step's environment, its set-ups and the person's
/// verdicts, and an ask's choices and its intake's proposals. Each is an optional field, absent where there is none and from a
/// host before them, so a quest or an ask no review touches reads exactly as it did. Runs no process: the fast half (MOD8).
/// </summary>
public sealed class ServiceClientReviewTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private const string Newer = "89abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void A_set_up_step_s_review_is_read_as_the_service_answers_it()
    {
        var quests = ServiceClient.ReadQuests(
            $$"""
            [{ "id": "q2", "from": "ask #a1", "to": "engine", "title": "Build the column", "body": "", "status": "Done",
               "review": { "choice": "local", "words": "show me it running" },
               "then": [{ "to": "engine", "title": "Show {parent} in local for review", "body": "Set it up.", "setUpIn": "local" }] },
             { "id": "q3", "from": "ask #a1", "to": "engine", "title": "Show q2 in local for review", "body": "", "status": "Done",
               "held": true, "hold": "unreviewed", "setUpIn": "local",
               "setUps": [
                 { "commit": "{{Commit}}", "look": "http://localhost:4200/reports", "shows": "the new column", "again": "open reports",
                   "served": "dist/app", "session": "s7", "local": true, "at": "2026-10-08T09:00:00+00:00", "machine": "desk", "sequence": 41 },
                 { "commit": "{{Newer}}", "shows": "the column, its total put right", "again": "open reports, then the totals tab",
                   "run": "npm run serve", "local": false, "at": "2026-10-08T10:00:00+00:00", "machine": "desk", "sequence": 44 }],
               "verdicts": [
                 { "said": "not-yet", "setUp": { "machine": "desk", "sequence": 41 }, "commit": "{{Commit}}", "words": "the total is off",
                   "at": "2026-10-08T09:30:00+00:00", "machine": "desk" },
                 { "said": "skipped", "words": "seen enough", "at": "2026-10-08T11:00:00+00:00", "machine": "laptop" }] }]
            """);

        var build = quests[0];
        Assert.Equal(new QuestReviewChoiceView("local", "show me it running"), build.Review);
        Assert.Null(build.SetUpIn);
        Assert.Equal("local", Assert.Single(build.Then).SetUpIn);

        var step = quests[1];
        Assert.Equal(("local", "unreviewed", true), (step.SetUpIn, step.Hold, step.Held));
        Assert.Null(step.Review);
        Assert.Equal(
            [
                new QuestSetUpView(Commit)
                {
                    Look = "http://localhost:4200/reports", Shows = "the new column", Again = "open reports", Served = "dist/app",
                    Session = "s7", Local = true, At = new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero), Machine = "desk", Sequence = 41,
                },
                new QuestSetUpView(Newer)
                {
                    Shows = "the column, its total put right", Again = "open reports, then the totals tab", Run = "npm run serve",
                    At = new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero), Machine = "desk", Sequence = 44,
                },
            ],
            step.SetUps);
        Assert.Equal(
            [
                new QuestReviewVerdictView(AskWordView.NotYet)
                {
                    SetUpMachine = "desk", SetUpSequence = 41, Commit = Commit, Words = "the total is off",
                    At = new DateTimeOffset(2026, 10, 8, 9, 30, 0, TimeSpan.Zero), Machine = "desk",
                },
                new QuestReviewVerdictView(AskWordView.Skipped)
                {
                    Words = "seen enough", At = new DateTimeOffset(2026, 10, 8, 11, 0, 0, TimeSpan.Zero), Machine = "laptop",
                },
            ],
            step.Verdicts);
    }

    /// <summary>A quest no review touches, or one from a host before the review, answers none of the fields: none is read.</summary>
    [Fact]
    public void A_quest_no_review_touches_reads_as_it_did()
    {
        var quest = Assert.Single(ServiceClient.ReadQuests(
            """
            [{ "id": "q1", "from": "game", "to": "engine", "title": "Cap it", "body": "", "status": "Done",
               "then": [{ "to": "game", "title": "Report on {parent}", "body": "Say what was done." }] }]
            """));

        Assert.Null(quest.Review);
        Assert.Null(quest.SetUpIn);
        Assert.Empty(quest.SetUps);
        Assert.Empty(quest.Verdicts);
        Assert.Null(Assert.Single(quest.Then).SetUpIn);
    }

    /// <summary>
    /// Half a set-up or half a verdict is none, as the service passes over what it cannot read: a set-up with no full commit,
    /// which is what the gate asks git about, and a verdict that says nothing. A choice without one is no choice.
    /// </summary>
    [Fact]
    public void A_set_up_a_verdict_or_a_choice_that_is_not_whole_is_passed_over()
    {
        var quest = Assert.Single(ServiceClient.ReadQuests(
            $$"""
            [{ "id": "q3", "to": "engine", "status": "Done", "setUpIn": "local", "review": { "words": "no choice" },
               "setUps": [{ "shows": "no commit" }, { "commit": "abc123", "shows": "a short commit" }, "not a set-up",
                          { "commit": "{{Commit}}", "shows": "whole" }],
               "verdicts": [{ "words": "said nothing" }, 7, { "said": "reviewed", "setUp": { "machine": "desk", "sequence": 3 } }] }]
            """));

        Assert.Null(quest.Review);
        Assert.Equal("whole", Assert.Single(quest.SetUps).Shows);
        var verdict = Assert.Single(quest.Verdicts);
        Assert.Equal((AskWordView.Reviewed, "desk", (long?)3), (verdict.Said, verdict.SetUpMachine, verdict.SetUpSequence));
    }

    [Fact]
    public void An_ask_s_review_choices_and_its_intake_s_proposals_are_read_as_the_service_answers_them()
    {
        var ask = ServiceClient.ReadAskJson(
            """
            {"id":"a1","workspace":"work","sentence":"s","state":"Published","tier":"intake",
             "reviewChoices":[{"choice":"on","at":"2026-10-08T08:00:00+00:00"},
                              {"choice":"off","at":"2026-10-08T08:30:00+00:00","words":"it is only the readme"},
                              {"choice":"local"}],
             "reviewProposals":[{"choice":"off","reason":"the work changes only a document","at":"2026-10-08T07:50:00+00:00",
                                 "session":"s1","quest":"q1"},
                                {"choice":"on","at":"2026-10-08T07:55:00+00:00"}]}
            """);

        Assert.Equal(
            [
                new AskReviewChoiceView("on", new DateTimeOffset(2026, 10, 8, 8, 0, 0, TimeSpan.Zero)),
                new AskReviewChoiceView("off", new DateTimeOffset(2026, 10, 8, 8, 30, 0, TimeSpan.Zero), "it is only the readme"),
            ],
            ask.ReviewChoices);
        Assert.Equal(
            [
                new AskReviewProposalView(
                    "off", "the work changes only a document", new DateTimeOffset(2026, 10, 8, 7, 50, 0, TimeSpan.Zero), "s1", "q1"),
            ],
            ask.ReviewProposals);
    }

    [Fact]
    public void An_ask_no_review_touches_reads_none()
    {
        var ask = ServiceClient.ReadAskJson("""{"id":"a1","workspace":"w","sentence":"s","state":"Open","tier":"named"}""");

        Assert.Empty(ask.ReviewChoices);
        Assert.Empty(ask.ReviewProposals);
    }
}
