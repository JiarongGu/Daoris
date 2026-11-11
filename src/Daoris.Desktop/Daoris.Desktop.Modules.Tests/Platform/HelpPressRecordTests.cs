using System.Text.Json;
using Daoris.Driver;
using Daoris.Desktop.Driver.Tests;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>Ask Daoris's covered presses of this run: kept in memory, newest first, with their outcome, and told in the standing row.</summary>
public sealed class HelpPressRecordTests : IDisposable
{
    private readonly string _home;

    public HelpPressRecordTests()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "daoris.json"))) root = root.Parent;
        _home = Path.Combine(root?.FullName ?? throw new InvalidOperationException("the tests run in a checkout."), "_fixtures", "help-press-tests",
            Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_home);
    }

    public void Dispose() => GitSeed.Discard(_home);

    private static IpcRequest Press(string route, string payload) => new()
    {
        Id = "p", Module = "DAORIS.DRIVER", Type = route, Payload = JsonDocument.Parse(payload).RootElement.Clone(),
    };

    [Fact]
    public void A_press_is_kept_running_with_its_conversation_and_what_it_named_then_ends_with_its_outcome()
    {
        var record = new HelpPressRecord(TimeProvider.System);
        var id = record.Began(Press("WORK_PAUSE", """{"quest":"q1"}"""), "s1a2b3c4");
        var running = Assert.Single(record.Now());
        Assert.Equal(("WORK_PAUSE", "s1a2b3c4", "quest", "q1", HelpPressRecord.Running), (running.Route, running.Session, running.Scope, running.Target, running.Outcome));

        record.Ended(id, true);
        Assert.Equal(HelpPressRecord.Done, Assert.Single(record.Now()).Outcome);
        record.Ended(record.Began(Press("SET_HOLD", """{"repository":"engine","held":true}"""), "s1a2b3c4"), false);
        var now = record.Now();
        Assert.Equal(["SET_HOLD", "WORK_PAUSE"], now.Select(each => each.Route));
        Assert.Equal(("repository", "engine", HelpPressRecord.Failed), (now[0].Scope, now[0].Target, now[0].Outcome));
    }

    [Fact]
    public void Only_the_newest_are_held_and_a_new_run_starts_empty()
    {
        var record = new HelpPressRecord(TimeProvider.System);
        for (var each = 0; each < HelpPressRecord.Kept + 5; each++) record.Began(Press("SET_HOLD", $$"""{"repository":"r{{each}}"}"""), "s1");
        Assert.Equal(HelpPressRecord.Kept, record.Now().Count);
        Assert.Equal("r104", record.Now()[0].Target);
        Assert.Empty(new HelpPressRecord(TimeProvider.System).Now());
    }

    [Fact]
    public void An_answer_says_it_succeeded_only_where_it_does()
    {
        Assert.True(HelpPressRecord.Succeeded("""{"id":"p","success":true}"""));
        Assert.False(HelpPressRecord.Succeeded("""{"id":"p","success":false}"""));
        Assert.False(HelpPressRecord.Succeeded("not json"));
    }

    private static readonly PressReceipt Opened = new(PressReceipt.KindOpened, "ledger", "aurora", "/work/aurora/ledger", "main", null, ["q7"], true);

    /// <summary>A press that opened a place keeps what it made beside its outcome, and the windows are told it.</summary>
    [Fact]
    public void A_press_that_opened_a_place_keeps_what_it_made_and_tells_it()
    {
        var record = new HelpPressRecord(TimeProvider.System);
        var told = new List<HelpPressNow>();
        record.Moved += told.Add;
        var id = record.Began(Press("REGISTRY_CREATE", """{"name":"ledger","purpose":"why"}"""), "s1a2b3c4");

        record.Received(id, Opened);
        record.Ended(id, true);

        var kept = Assert.Single(record.Now());
        Assert.Equal((Opened, HelpPressRecord.Done), (kept.Receipt, kept.Outcome));
        Assert.Equal(3, told.Count);
        Assert.Equal(Opened, told[1].Receipt);
    }

    /// <summary>A press asked on a card is found by its card's id, which is all the answer of its confirmed run knows.</summary>
    [Fact]
    public void A_press_asked_on_a_card_is_found_by_its_card_for_what_it_made()
    {
        var record = new HelpPressRecord(TimeProvider.System);
        record.Asked(Press("REGISTRY_CREATE", """{"name":"ledger","purpose":"why"}"""), "s1a2b3c4", null, "card-1");
        record.Asked(Press("REGISTRY_CREATE", """{"name":"other","purpose":"why"}"""), "s1a2b3c4", null, "card-2");

        record.ReceivedByCard("card-2", Opened with { Repository = "other" });
        record.ReceivedByCard("card-gone", Opened);

        var now = record.Now();
        Assert.Equal("other", Assert.Single(now, each => each.Receipt is not null).Receipt!.Repository);
        Assert.Null(now.Single(each => each.Receipt is null).Receipt);
    }

    [Fact]
    public void The_standing_row_tells_the_presses()
    {
        var judge = new StandingConfirmation(_home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside));
        var id = judge.HelpPresses.Began(Press("WORK_PAUSE", """{"quest":"q1"}"""), "s1");
        Assert.Equal(id, Assert.Single(judge.Now().HelpPresses).Id);
    }
}
