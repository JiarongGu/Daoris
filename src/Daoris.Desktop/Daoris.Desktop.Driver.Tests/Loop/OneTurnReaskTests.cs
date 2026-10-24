using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MODELROLE1e (D205 §3.5(f)): a one-turn owner's refusal at the open is handled inside its own call. The owner's walk is asked again with
/// the refused (entry, account) passed: a later entry of the same agent on the same account is set on the open session, anything else ends
/// the refused record and the walk's answer opens next, and nothing left is the walk's own "no reviewer could run" with the pass reasons.
/// Nothing here starts a process or asks the machine's accounts: the owner's walk is a table.
/// </summary>
public sealed class OneTurnReaskTests
{
    private static readonly ActorEffective Opus = new("codex-acp", "opus", null, "1");
    private static readonly ActorEffective Sonnet = new("codex-acp", "sonnet", null, "2");

    private static ReviewerChoice Chose(ActorEffective entry, string? account) =>
        new("codex-acp", ReviewerLabels.AnotherMaker, null, "chosen")
        {
            Selection = new HarnessSelection(null, account), Effective = entry,
        };

    private static ReviewerChoice None() => new(null, null, ReviewerUnavailable.Refused, "No second opinion: every listed reviewer was refused.");

    /// <summary>The owner's walk as a table: entry 1 on account-1 and -2, then entry 2 on account-1, each unless passed.</summary>
    private static Func<IReadOnlyList<ReviewerPassed>, Task<ReviewerChoice>> Walk(List<IReadOnlyList<ReviewerPassed>> asked) => passed =>
    {
        asked.Add(passed);
        bool Left(ActorEffective entry, string account) => !passed.Any(each => each.Entry == entry && each.Account == account);
        if (Left(Opus, "account-1")) return Task.FromResult(Chose(Opus, "account-1"));
        if (Left(Opus, "account-2")) return Task.FromResult(Chose(Opus, "account-2"));
        if (Left(Sonnet, "account-1")) return Task.FromResult(Chose(Sonnet, "account-1"));
        return Task.FromResult(None());
    };

    /// <summary>A refusal on the account the entry runs on moves a later entry of the same agent only on that very account.</summary>
    [Fact]
    public async Task A_later_entry_on_the_same_account_is_set_on_the_open_session()
    {
        var asked = new List<IReadOnlyList<ReviewerPassed>>();
        var first = Chose(Opus, "account-1");
        var reask = new OneTurnReask(first, Walk(asked));

        // The walk answers entry 1 on account-2 (choice-major: another account before another entry): not the same account, so no move.
        Assert.Null(await reask.SameAccountAsync());
        Assert.Equal([new ReviewerPassed(Opus, "account-1")], asked.Single());

        // Both accounts of entry 1 refused: entry 2 answers on account-1, which is the account that refused entry 1 first, not account-2.
        var single = new OneTurnReask(Chose(Opus, "account-2"), Walk(asked));
        Assert.Null(await single.SameAccountAsync());
    }

    [Fact]
    public async Task A_same_account_answer_is_taken_and_becomes_the_choice_that_runs()
    {
        // One account only: entry 2 is the walk's answer on the very account that refused entry 1.
        var walk = new Func<IReadOnlyList<ReviewerPassed>, Task<ReviewerChoice>>(passed =>
            Task.FromResult(passed.Any(each => each.Entry == Opus) ? Chose(Sonnet, "account-1") : Chose(Opus, "account-1")));
        var reask = new OneTurnReask(Chose(Opus, "account-1"), walk);

        var moved = await reask.SameAccountAsync();

        Assert.Equal(("2", "sonnet"), (moved!.Value.Choice, moved.Value.Asked.NamedModel));
        Assert.Equal(Sonnet, reask.Current.Effective);
        Assert.Equal("account-1", reask.Current.Selection!.Profile);
    }

    /// <summary>The record has ended: the walk's answer for what is left is the next open, the refused (entry, account) passed once.</summary>
    [Fact]
    public async Task The_next_answer_leaves_out_what_was_passed_and_ends_with_the_walk_s_own_no_reviewer()
    {
        var asked = new List<IReadOnlyList<ReviewerPassed>>();
        var reask = new OneTurnReask(Chose(Opus, "account-1"), Walk(asked));

        var second = await reask.NextAsync();
        Assert.Equal((Opus, "account-2"), (second.Effective, second.Selection!.Profile));

        var third = await reask.NextAsync();
        Assert.Equal((Sonnet, "account-1"), (third.Effective, third.Selection!.Profile));

        var last = await reask.NextAsync();
        Assert.False(last.Chosen);
        Assert.Equal(ReviewerUnavailable.Refused, last.Code);
        Assert.Equal(
            [new ReviewerPassed(Opus, "account-1"), new ReviewerPassed(Opus, "account-2"), new ReviewerPassed(Sonnet, "account-1")],
            reask.Passed);
    }

    /// <summary>The open's callback may already have passed the account: passing it again adds nothing, so a walk is never asked twice for one pass.</summary>
    [Fact]
    public async Task A_pass_is_kept_once()
    {
        var asked = new List<IReadOnlyList<ReviewerPassed>>();
        var reask = new OneTurnReask(Chose(Opus, "account-1"), Walk(asked));

        await reask.SameAccountAsync();
        await reask.NextAsync();

        Assert.Equal([new ReviewerPassed(Opus, "account-1")], reask.Passed);
        Assert.Equal("account-2", reask.Current.Selection!.Profile);
    }

    /// <summary>An agent under its own sign-in names no account: its entry is passed whole, so the walk moves to the agent's next entry.</summary>
    [Fact]
    public async Task An_entry_on_the_agent_s_own_sign_in_is_passed_whole()
    {
        var walk = new Func<IReadOnlyList<ReviewerPassed>, Task<ReviewerChoice>>(passed =>
            Task.FromResult(passed.Any(each => each.Entry == Opus && each.Account is null) ? Chose(Sonnet, null) : Chose(Opus, null)));
        var reask = new OneTurnReask(Chose(Opus, null), walk);

        var moved = await reask.SameAccountAsync();

        Assert.Equal("2", moved!.Value.Choice);
        Assert.Equal([new ReviewerPassed(Opus, null)], reask.Passed);
    }

    /// <summary>The opening note's actor line is said only where a later entry ran (§4.6), its passes the walk's own.</summary>
    [Fact]
    public void The_actor_line_names_a_later_entry_and_what_it_passed()
    {
        var ran = new ReviewerChoice("codex-acp", ReviewerLabels.AnotherMaker, null, "chosen")
        {
            Effective = Sonnet,
            Tried = [new ReviewerTried("codex-acp", ReviewerLabels.AnotherMaker, ReviewerHeld.NotOffered, "`codex-acp` did not offer `opus`.") { Entry = Opus }],
        };

        var line = RotatedOpening.ActorLine("opinion", ran.Effective!, [.. ran.Tried.Select(each => new BuildPass(each.Entry!, each.Held, each.Refusal))]);

        Assert.Equal("Ran as Second opinion's choice 2, codex-acp · sonnet: choice 1, codex-acp · opus: `codex-acp` did not offer `opus`.", line!.Note);
    }
}
