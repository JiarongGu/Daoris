using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A terminal's press answered or shown past its time runs nothing (NOTIFY2a, D196 point 4, the notification design §3.4): the bound
/// the card states is the bound kept, whenever the timer fires. An answer, or a window's word that the card is shown, at or past the
/// press's time ends it expired, the clock read first; and the press's own event names it by its id and its balloon names no route
/// (§3.3).
/// </summary>
/// <remarks>
/// The confirmations alone, on a <see cref="HeldClock"/> whose timers are held late (<see cref="HeldClock.Pass"/>), as a loaded pool
/// runs them (<c>held-clocks.md</c>): before NOTIFY2a only the timer's firing read the clock, so a Confirm taken while a late timer
/// had not fired ran past the two minutes, and a word that the card was shown past its ten gave it two more. The pipe's end of the
/// same, with the terminal's answer and nothing run, is <c>PipeConfirmTests</c>'.
/// </remarks>
public sealed class PipeConfirmationsTests : Bridge
{
    private const string Land = "LAND_SESSION_TREE";

    private static readonly PipeConfirmationField[] Values = [new("/id", "string", "s1a2b3c4")];

    private static readonly PipeAsker Asker = new("daoris-driver", 4242);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_answer_at_or_past_its_two_minutes_ends_it_expired_though_its_timer_is_late(bool confirm)
    {
        using var log = new MachineLog(Home, "desktop");
        var clock = new HeldClock();
        using var confirmations = new PipeConfirmations(Bus, log, clock);
        var late = confirmations.Ask(DriverModule.Name, Land, Values, Asker)!.Value;
        var inTime = confirmations.Ask(DriverModule.Name, Land, Values, Asker)!.Value;
        Assert.True(confirmations.Show(late.Shown.Id));
        clock.Pass(TimeSpan.FromSeconds(1));
        Assert.True(confirmations.Show(inTime.Shown.Id));

        // Two minutes from the first card's showing, its timer held late: it still waits, and the person answers it.
        clock.Pass(TimeSpan.FromMinutes(2) - TimeSpan.FromSeconds(1));
        Assert.Equal(2, confirmations.Now().Waiting.Count);

        Assert.False(confirmations.Answer(late.Shown.Id, confirm));
        Assert.Equal(PipeConfirmationEnd.Expired, await late.Ended);
        // The second, a second inside its own two minutes, is the person's answer as given.
        Assert.True(confirmations.Answer(inTime.Shown.Id, confirm));
        Assert.Equal(confirm ? PipeConfirmationEnd.Confirmed : PipeConfirmationEnd.Refused, await inTime.Ended);
        Assert.Empty(confirmations.Now().Waiting);
        Assert.Equal(["asked", "asked", "shown", "shown", "expired", confirm ? "confirmed" : "refused"], States());
    }

    [Fact]
    public async Task A_word_that_its_card_is_shown_past_its_ten_minutes_ends_it_expired_and_gives_it_no_more_time()
    {
        using var log = new MachineLog(Home, "desktop");
        var clock = new HeldClock();
        using var confirmations = new PipeConfirmations(Bus, log, clock);
        var asked = confirmations.Ask(DriverModule.Name, Land, Values, Asker)!.Value;

        clock.Pass(PipeConfirmations.FindWithin);
        Assert.Single(confirmations.Now().Waiting);

        Assert.False(confirmations.Show(asked.Shown.Id));
        Assert.Equal(PipeConfirmationEnd.Expired, await asked.Ended);
        Assert.Empty(confirmations.Now().Waiting);
        Assert.False(confirmations.Answer(asked.Shown.Id, confirm: true));
        Assert.Equal(["asked", "expired"], States());
    }

    [Fact]
    public async Task A_word_for_a_card_shown_before_past_its_two_minutes_ends_it_expired()
    {
        using var log = new MachineLog(Home, "desktop");
        var clock = new HeldClock();
        using var confirmations = new PipeConfirmations(Bus, log, clock);
        var asked = confirmations.Ask(DriverModule.Name, Land, Values, Asker)!.Value;
        Assert.True(confirmations.Show(asked.Shown.Id));
        clock.Pass(TimeSpan.FromMinutes(1));
        // Within its two minutes, a second window's word moves nothing.
        Assert.True(confirmations.Show(asked.Shown.Id));

        clock.Pass(TimeSpan.FromMinutes(1));

        Assert.False(confirmations.Show(asked.Shown.Id));
        Assert.Equal(PipeConfirmationEnd.Expired, await asked.Ended);
        Assert.Empty(confirmations.Now().Waiting);
        Assert.Equal(["asked", "shown", "expired"], States());
    }

    [Fact]
    public async Task A_late_timer_that_fires_after_its_press_ended_ends_nothing_again()
    {
        using var log = new MachineLog(Home, "desktop");
        var clock = new HeldClock();
        using var confirmations = new PipeConfirmations(Bus, log, clock);
        var asked = confirmations.Ask(DriverModule.Name, Land, Values, Asker)!.Value;
        Assert.True(confirmations.Show(asked.Shown.Id));
        clock.Pass(TimeSpan.FromMinutes(3));
        Assert.False(confirmations.Answer(asked.Shown.Id, confirm: true));

        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(PipeConfirmationEnd.Expired, await asked.Ended);
        Assert.Equal(["asked", "shown", "expired"], States());
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
            Assert.True(confirmations.Withdraw(asked.Shown.Id));
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
            Assert.True(confirmations.Withdraw(asked.Shown.Id));
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
