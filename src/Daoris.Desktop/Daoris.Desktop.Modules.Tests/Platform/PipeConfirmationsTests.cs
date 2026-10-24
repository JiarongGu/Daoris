using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A terminal's press waits until the person answers (CONFIRMWAIT1a-i, D156's CONFIRMWAIT1 notes, the confirm-wait design §2, §5.2, §6,
/// §9): no clock ends it, shown or not, however long; a Confirm is judged against its grounds while its card stays, checking; a read that
/// fails then leaves it waiting; its asker takes back only what still waits; ten wait at once; and the press's own event names it by its
/// id and its balloon names no route (NOTIFY2a's §3.3).
/// </summary>
/// <remarks>
/// The confirmations alone, on a <see cref="HeldClock"/> the test moves (<c>held-clocks.md</c>): before CONFIRMWAIT1a-i a press asked was
/// ended expired ten minutes after its ask unshown, or two after its showing. The pipe's end of the same, with the terminal's answer and
/// nothing run, is <c>PipeConfirmTests</c>'.
/// </remarks>
public sealed class PipeConfirmationsTests : Bridge
{
    private const string Land = "LAND_SESSION_TREE";

    private static readonly PipeConfirmationField[] Values = [new("/id", "string", "s1a2b3c4")];

    private static readonly PipeAsker Asker = new("daoris-driver", 4242);

    [Fact]
    public async Task A_press_shown_or_not_still_waits_a_day_later_and_the_person_s_answer_is_taken()
    {
        using var log = new MachineLog(Home, "desktop");
        var clock = new HeldClock();
        using var confirmations = new PipeConfirmations(Bus, log, clock);
        var unshown = confirmations.Ask(DriverModule.Name, Land, Values, Asker)!.Value;
        var shown = confirmations.Ask(DriverModule.Name, Land, Values, Asker)!.Value;
        Assert.True(confirmations.Show(shown.Shown.Id));

        // A day passes, every timer the clock made fired: nothing ended either.
        clock.Advance(TimeSpan.FromDays(1));

        Assert.Equal(2, confirmations.Now().Waiting.Count);
        Assert.All(confirmations.Now().Waiting, each => Assert.Null(each.Expires));
        Assert.Equal(new PipeAnswer(PipeAnswered.Answered), await confirmations.AnswerAsync(unshown.Shown.Id, confirm: true));
        Assert.Equal(PipeConfirmationEnd.Confirmed, (await unshown.Ended).End);
        Assert.Equal(new PipeAnswer(PipeAnswered.Answered), await confirmations.AnswerAsync(shown.Shown.Id, confirm: false));
        Assert.Equal(PipeConfirmationEnd.Refused, (await shown.Ended).End);
        Assert.Equal(["asked", "asked", "shown", "confirmed", "refused"], States());
    }

    [Fact]
    public void A_window_s_word_that_its_card_is_shown_is_kept_once_and_moves_nothing()
    {
        using var log = new MachineLog(Home, "desktop");
        var clock = new HeldClock();
        using var confirmations = new PipeConfirmations(Bus, log, clock);
        var asked = confirmations.Ask(DriverModule.Name, Land, Values, Asker)!.Value;
        clock.Advance(TimeSpan.FromHours(3));

        Assert.True(confirmations.Show(asked.Shown.Id));
        clock.Advance(TimeSpan.FromHours(3));
        Assert.True(confirmations.Show(asked.Shown.Id));

        var now = Assert.Single(confirmations.Now().Waiting);
        Assert.Equal(asked.Shown.Asked + TimeSpan.FromHours(3), now.ShownAt);
        Assert.Null(now.Expires);
        Assert.Equal(["asked", "shown"], States());
    }

    [Fact]
    public async Task A_Confirm_keeps_the_card_checking_until_its_grounds_are_judged_and_its_asker_takes_back_nothing_meanwhile()
    {
        using var log = new MachineLog(Home, "desktop");
        using var confirmations = new PipeConfirmations(Bus, log, new HeldClock());
        var judging = new TaskCompletionSource<PipeJudgement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = confirmations.Ask(DriverModule.Name, Land, Values, Asker, judge: _ => judging.Task, checkWithin: TimeSpan.FromSeconds(30))!.Value;
        Assert.Equal(30_000, asked.Shown.CheckWithin);

        var answer = confirmations.AnswerAsync(asked.Shown.Id, confirm: true);

        var checking = Assert.Single(confirmations.Now().Waiting);
        Assert.True(checking.Checking);
        Assert.Equal(PipeWithdrawal.Checking, confirmations.Withdraw(asked.Shown.Id));
        Assert.Equal(new PipeAnswer(PipeAnswered.Gone), await confirmations.AnswerAsync(asked.Shown.Id, confirm: false));
        Assert.False(answer.IsCompleted);

        judging.SetResult(PipeJudgement.Holding);

        Assert.Equal(new PipeAnswer(PipeAnswered.Answered), await answer);
        Assert.Equal(PipeConfirmationEnd.Confirmed, (await asked.Ended).End);
        Assert.Empty(confirmations.Now().Waiting);
        Assert.Equal(["asked", "checking", "confirmed"], States());
    }

    [Fact]
    public async Task A_Confirm_whose_ground_moved_ends_it_changed_with_its_reason()
    {
        using var log = new MachineLog(Home, "desktop");
        using var confirmations = new PipeConfirmations(Bus, log, new HeldClock());
        var moved = new PipeGroundChange("session", new Dictionary<string, string> { ["then"] = "awaiting-person", ["now"] = "completed" });
        var asked = confirmations.Ask(DriverModule.Name, Land, Values, Asker, judge: _ => Task.FromResult(new PipeJudgement(PipeJudged.Changed, moved)))!.Value;

        var answer = await confirmations.AnswerAsync(asked.Shown.Id, confirm: true);

        Assert.Equal(PipeAnswered.Changed, answer.Outcome);
        Assert.Same(moved, answer.Change);
        var ending = await asked.Ended;
        Assert.Equal(PipeConfirmationEnd.Changed, ending.End);
        Assert.Same(moved, ending.Change);
        Assert.Empty(confirmations.Now().Waiting);
        Assert.Equal(["asked", "checking", "changed"], States());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_Confirm_whose_grounds_cannot_be_read_leaves_it_waiting_with_its_buttons_back(bool throws)
    {
        using var log = new MachineLog(Home, "desktop");
        using var confirmations = new PipeConfirmations(Bus, log, new HeldClock());
        var mended = false;
        var asked = confirmations.Ask(DriverModule.Name, Land, Values, Asker, judge: _ => mended
            ? Task.FromResult(PipeJudgement.Holding)
            : throws ? throw new IOException("the service is reconnecting") : Task.FromResult(new PipeJudgement(PipeJudged.Unread)))!.Value;

        Assert.Equal(new PipeAnswer(PipeAnswered.Unread), await confirmations.AnswerAsync(asked.Shown.Id, confirm: true));

        var still = Assert.Single(confirmations.Now().Waiting);
        Assert.False(still.Checking);
        Assert.False(asked.Ended.IsCompleted);

        // A second Confirm with the reader mended runs it.
        mended = true;
        Assert.Equal(new PipeAnswer(PipeAnswered.Answered), await confirmations.AnswerAsync(asked.Shown.Id, confirm: true));
        Assert.Equal(PipeConfirmationEnd.Confirmed, (await asked.Ended).End);
        Assert.Equal(["asked", "checking", "unread", "checking", "confirmed"], States());
    }

    [Fact]
    public async Task A_withdrawal_ends_only_what_waits_and_a_notice_is_kept_only_where_a_window_showed_it()
    {
        using var log = new MachineLog(Home, "desktop");
        using var confirmations = new PipeConfirmations(Bus, log, new HeldClock());
        var noticed = new List<(string Id, PipeConfirmationEnd End)>();
        confirmations.Noticed = (press, end) => noticed.Add((press.Id, end));
        var unseen = confirmations.Ask(DriverModule.Name, Land, Values, Asker)!.Value;
        var seen = confirmations.Ask(DriverModule.Name, Land, Values, Asker)!.Value;
        Assert.True(confirmations.Show(seen.Shown.Id));

        Assert.Equal(PipeWithdrawal.Withdrawn, confirmations.Withdraw(unseen.Shown.Id));
        Assert.Equal(PipeWithdrawal.Withdrawn, confirmations.Withdraw(seen.Shown.Id));
        Assert.Equal(PipeWithdrawal.Gone, confirmations.Withdraw(seen.Shown.Id));

        Assert.Equal(PipeConfirmationEnd.Withdrawn, (await unseen.Ended).End);
        Assert.Equal(PipeConfirmationEnd.Withdrawn, (await seen.Ended).End);
        Assert.Equal([(seen.Shown.Id, PipeConfirmationEnd.Withdrawn)], noticed);
        Assert.Equal(new PipeAnswer(PipeAnswered.Gone), await confirmations.AnswerAsync(seen.Shown.Id, confirm: true));
    }

    /// <summary>
    /// CONFIRMWAIT1a-ii (the confirm-wait design §7.2): a press carrying a key is lost as Daoris closes, whatever it waits on, and the
    /// person is told; the confirmations no longer end anything themselves as they are disposed.
    /// </summary>
    [Fact]
    public async Task A_lost_press_ends_lost_and_is_noticed_and_disposing_ends_nothing()
    {
        using var log = new MachineLog(Home, "desktop");
        var confirmations = new PipeConfirmations(Bus, log, new HeldClock());
        var noticed = new List<PipeConfirmationEnd>();
        confirmations.Noticed = (_, end) => noticed.Add(end);
        var keyed = confirmations.Ask(confirmations.Hold()!, "DAORIS.REMOTES", "SET", Values, Asker, @as: new PipeAsAsked(Guid.NewGuid().ToString("N"), Keyed: true));
        var kept = confirmations.Ask(DriverModule.Name, Land, Values, Asker)!.Value;
        Assert.True(keyed.Shown.Keyed);

        Assert.True(confirmations.Lose(keyed.Shown.Id));
        Assert.False(confirmations.Lose(keyed.Shown.Id));
        confirmations.Dispose();

        Assert.Equal(PipeConfirmationEnd.Lost, (await keyed.Ended).End);
        Assert.Equal([PipeConfirmationEnd.Lost], noticed);
        Assert.False(kept.Ended.IsCompleted);
        Assert.Equal(kept.Shown.Id, Assert.Single(confirmations.Now().Waiting).Id);
        Assert.Equal(["asked", "asked", "lost"], States());
    }

    /// <summary>
    /// CONFIRMWAIT1a-ii (§7.2 step 6, §8.1): a press restored from its record waits again under its record's id, asked and shown as the
    /// record says, no terminal on its line, said as restored; it raises no balloon of its own, and the restore's one word is one balloon
    /// for every press waiting again and one notice for those lost and those a newer Daoris wrote.
    /// </summary>
    [Fact]
    public void A_restored_press_waits_again_as_its_record_says_and_the_restart_says_one_word()
    {
        using var log = new MachineLog(Home, "desktop");
        var clock = new HeldClock();
        using var confirmations = new PipeConfirmations(Bus, log, clock);
        var told = new List<(int Lost, int Newer)>();
        confirmations.Restarted = (lost, newer) => told.Add((lost, newer));
        var id = Guid.NewGuid().ToString("N");
        var asked = clock.GetUtcNow() - TimeSpan.FromHours(5);

        var restored = confirmations.Ask(confirmations.Hold()!, DriverModule.Name, Land, Values, Asker,
            @as: new PipeAsAsked(id, asked, asked + TimeSpan.FromHours(1), Restored: true, WaitedOn: false));
        confirmations.Restore(waiting: 1, lost: 2, newer: 1);
        confirmations.Restore(waiting: 0, lost: 0, newer: 0);

        var card = Assert.Single(confirmations.Now().Waiting);
        Assert.Equal((id, asked, asked + TimeSpan.FromHours(1), true, false), (card.Id, card.Asked, card.ShownAt, card.Restored, card.WaitedOn));
        Assert.Same(restored.Shown, card);
        Assert.Equal([(2, 1)], told);
        var attention = Raised.Where(each => each.Type == PipeConfirmations.AttentionType).Select(each => each.Payload).ToList();
        var balloon = Assert.Single(attention);
        Assert.Equal(PipeConfirmations.RestoredKind, balloon!.GetType().GetProperty("Kind")!.GetValue(balloon));
        Assert.Equal(1, balloon.GetType().GetProperty("Count")!.GetValue(balloon));
    }

    /// <summary>
    /// CONFIRMWAIT1a-ii (§7.2): a Confirm while Daoris closes or an update drains it takes nothing, and the press still waits; a
    /// refusal is still the person's word, and a Confirm once the drain is over runs it.
    /// </summary>
    [Fact]
    public async Task A_Confirm_while_Daoris_drains_takes_nothing_and_the_press_still_waits()
    {
        using var log = new MachineLog(Home, "desktop");
        using var confirmations = new PipeConfirmations(Bus, log, new HeldClock());
        var draining = true;
        confirmations.Holding = () => draining;
        var asked = confirmations.Ask(DriverModule.Name, Land, Values, Asker, judge: _ => Task.FromResult(PipeJudgement.Holding))!.Value;

        Assert.Equal(new PipeAnswer(PipeAnswered.Draining), await confirmations.AnswerAsync(asked.Shown.Id, confirm: true));

        Assert.False(Assert.Single(confirmations.Now().Waiting).Checking);
        Assert.False(asked.Ended.IsCompleted);
        draining = false;
        Assert.Equal(new PipeAnswer(PipeAnswered.Answered), await confirmations.AnswerAsync(asked.Shown.Id, confirm: true));
        Assert.Equal(PipeConfirmationEnd.Confirmed, (await asked.Ended).End);
    }

    /// <summary>CONFIRMWAIT1a-ii (§7.2): a drain that begins while a Confirm's grounds are read gives the press its buttons back.</summary>
    [Fact]
    public async Task A_drain_begun_while_a_Confirm_is_judged_gives_its_buttons_back()
    {
        using var log = new MachineLog(Home, "desktop");
        using var confirmations = new PipeConfirmations(Bus, log, new HeldClock());
        var draining = false;
        confirmations.Holding = () => draining;
        var judging = new TaskCompletionSource<PipeJudgement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = confirmations.Ask(DriverModule.Name, Land, Values, Asker, judge: _ => judging.Task)!.Value;
        var answer = confirmations.AnswerAsync(asked.Shown.Id, confirm: true);
        Assert.True(Assert.Single(confirmations.Now().Waiting).Checking);

        draining = true;
        judging.SetResult(PipeJudgement.Holding);

        Assert.Equal(new PipeAnswer(PipeAnswered.Draining), await answer);
        Assert.False(Assert.Single(confirmations.Now().Waiting).Checking);
        Assert.False(asked.Ended.IsCompleted);
    }

    [Fact]
    public void Ten_wait_at_once_a_place_held_for_a_read_counts_and_one_let_go_frees_it()
    {
        using var log = new MachineLog(Home, "desktop");
        using var confirmations = new PipeConfirmations(Bus, log, new HeldClock());
        Assert.Equal(10, confirmations.Bound);
        for (var each = 0; each < 9; each++) Assert.NotNull(confirmations.Ask(DriverModule.Name, Land, Values, Asker));
        var place = confirmations.Hold();
        Assert.NotNull(place);

        Assert.Null(confirmations.Hold());
        Assert.Null(confirmations.Ask(DriverModule.Name, Land, Values, Asker));
        // Told to no window while its grounds are read.
        Assert.Equal(9, confirmations.Now().Waiting.Count);

        confirmations.Let(place);
        confirmations.Let(place);
        Assert.NotNull(confirmations.Hold());
        Assert.Null(confirmations.Hold());
    }

    [Fact]
    public void The_pipe_takes_more_connections_than_presses_may_wait()
    {
        var limits = new PipeLimits();

        Assert.Equal(10, limits.Waiting);
        Assert.Equal(16, limits.Connections);
        Assert.True(limits.Connections > limits.Waiting);
    }

    [Fact]
    public void Whether_a_terminal_waits_on_it_is_told_to_every_window_as_it_changes()
    {
        using var log = new MachineLog(Home, "desktop");
        using var confirmations = new PipeConfirmations(Bus, log, new HeldClock());
        var asked = confirmations.Ask(DriverModule.Name, Land, Values, Asker)!.Value;
        Assert.True(asked.Shown.WaitedOn);
        var told = Raised.Count(each => each.Type == PipeConfirmations.Event);

        Assert.True(confirmations.WaitedOn(asked.Shown.Id, false));
        Assert.True(confirmations.WaitedOn(asked.Shown.Id, false));

        Assert.False(Assert.Single(confirmations.Now().Waiting).WaitedOn);
        Assert.Equal(told + 1, Raised.Count(each => each.Type == PipeConfirmations.Event));
    }

    [Fact]
    public void A_press_s_own_event_names_it_by_its_id()
    {
        using var log = new MachineLog(Home, "desktop");
        using var confirmations = new PipeConfirmations(Bus, log, new HeldClock());

        var asked = confirmations.Ask(DriverModule.Name, Land, Values, Asker)!.Value;

        var told = Assert.Single(Raised, each => each.Module == StatePublisher.Module && each.Type == PipeConfirmations.AttentionType);
        // As the page reads it, the field added beside the rest (§3.3): the id the window's cards and toasts key on.
        var read = JsonSerializer.SerializeToElement(told.Payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(PipeConfirmations.AttentionKind, read.GetProperty("kind").GetString());
        Assert.Equal(asked.Shown.Id, read.GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, read.GetProperty("session").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(read.GetProperty("headline").GetString()));
    }

    /// <summary>
    /// The balloon behind the window names no route's constant (§3.3, the owner's report of 2026-10-21: <c>SESSION_INPUT</c> beside
    /// its sentence), for every route a terminal's press of which waits for the window, and no value; the terminal as the system
    /// names it stays.
    /// </summary>
    [Fact]
    public void The_balloon_names_no_route_and_no_value_for_any_press_that_waits_for_the_window()
    {
        using var log = new MachineLog(Home, "desktop");
        using var confirmations = new PipeConfirmations(Bus, log, new HeldClock());
        var person = PipeCensus.Daoris.Routes
            .Where(each => each.Class == PipeClass.Person || each.When?.Class == PipeClass.Person)
            .ToList();
        Assert.NotEmpty(person);

        foreach (var each in person)
        {
            var route = each.Route ?? "WRITE";
            var asked = confirmations.Ask(each.Module, route, [new PipeConfirmationField("/id", "string", "s9f8e7d6")], Asker)!.Value;
            Assert.Equal(PipeWithdrawal.Withdrawn, confirmations.Withdraw(asked.Shown.Id));
        }

        var balloons = Raised.Where(each => each.Type == PipeConfirmations.AttentionType).Select(each => each.Payload!).ToList();
        Assert.Equal(person.Count, balloons.Count);
        foreach (var (balloon, each) in balloons.Zip(person))
        {
            var words = $"{Read(balloon, "Headline")} {Read(balloon, "Detail")}";
            Assert.DoesNotContain(each.Route ?? "WRITE", words, StringComparison.Ordinal);
            Assert.DoesNotContain(each.Module, words, StringComparison.Ordinal);
            Assert.DoesNotContain("s9f8e7d6", words, StringComparison.Ordinal);
            Assert.Contains("daoris-driver (4242)", words, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// NOTIFY2d (§3.3): as the application's notifier words it from the press's event, in the window's language or with no words handed,
    /// the balloon names the press's act by the route's words and never the route's constant, its module or a value it carries.
    /// </summary>
    [Fact]
    public void The_balloon_as_worded_names_its_act_and_no_route_or_value_in_either_language()
    {
        using var log = new MachineLog(Home, "desktop");
        using var confirmations = new PipeConfirmations(Bus, log, new HeldClock());
        var person = PipeCensus.Daoris.Routes
            .Where(each => each.Class == PipeClass.Person || each.When?.Class == PipeClass.Person)
            .ToList();
        foreach (var each in person)
        {
            var asked = confirmations.Ask(each.Module, each.Route ?? "WRITE", [new PipeConfirmationField("/id", "string", "s9f8e7d6")], Asker)!.Value;
            Assert.Equal(PipeWithdrawal.Withdrawn, confirmations.Withdraw(asked.Shown.Id));
        }

        var balloons = Raised.Where(each => each.Type == PipeConfirmations.AttentionType).Select(each => each.Payload!).ToList();
        Assert.Equal(person.Count, balloons.Count);
        var handed = new[] { SessionBalloonTests.Handed("en"), SessionBalloonTests.Handed("zh"), null };
        foreach (var (balloon, each) in balloons.Zip(person))
        {
            var route = each.Route ?? "WRITE";
            Assert.Equal((each.Module, route), (Read(balloon, "Module"), Read(balloon, "Route")));
            foreach (var words in handed)
            {
                var said = SessionBalloon.Words(
                    new BalloonFacts(Read(balloon, "Kind"), Read(balloon, "Headline"), Read(balloon, "Detail")) { Module = Read(balloon, "Module"), Route = Read(balloon, "Route") },
                    words);
                var text = $"{said.Title} {said.Body}";
                Assert.DoesNotContain(route, text, StringComparison.Ordinal);
                Assert.DoesNotContain(each.Module, text, StringComparison.Ordinal);
                Assert.DoesNotContain("s9f8e7d6", text, StringComparison.Ordinal);
                if (words is not null) Assert.Contains(words[PipeWording.Key(each.Module, route)], text, StringComparison.Ordinal);
            }
        }
    }

    // As the application's notifier reads it (`SessionNotifier.Read`): a field off the payload the bus carries.
    private static string? Read(object payload, string name) => payload.GetType().GetProperty(name)?.GetValue(payload) as string;

    /// <summary>The states of the machine log's confirmation lines, in order.</summary>
    private List<string?> States()
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.GetFiles(folder).SelectMany(path =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            })
            .Select(line => JsonDocument.Parse(line).RootElement)
            .Where(line => line.GetProperty("event").GetString() == PipeConfirmations.LogEvent)
            .Select(line => line.GetProperty("data").GetProperty("state").GetString()),
        ];
    }
}
