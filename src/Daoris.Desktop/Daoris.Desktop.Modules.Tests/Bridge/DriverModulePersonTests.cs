using Shenora;
using Shenora.Core.Ipc;
using Microsoft.Extensions.DependencyInjection;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The person's door over the bridge (`PersonRoutes.cs`, PERSONDOOR1f and RPC1c, D156 points 2 and 4): the window reads what a
/// terminal asked it to confirm and answers one, and reads the person key the shell holds for its host, which none holds before
/// PERSONDOOR1g. Each route is the window's alone: the pipe's census refuses all three to a terminal (<c>PipeCensusTests</c>).
/// </summary>
public sealed class DriverModulePersonTests : DriverModuleBridge
{
    private (DriverModule Module, InstancePipe Pipe, ServiceProvider Services) Composed()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Shenora.Core.Events.IEventBus>(Bus);
        services.UseMessageDispatcher((_, _) => { });
        var provider = services.BuildServiceProvider();
        var loop = Loop();
        var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home);
        loop.Pipe = pipe;
        return (new DriverModule(Bus, loop), pipe, provider);
    }

    [Fact]
    public async Task The_window_reads_what_waits_and_confirms_or_refuses_one_once()
    {
        var (module, pipe, services) = Composed();
        using var composed = services;
        using var served = pipe;
        var archive = pipe.Confirmations.Ask(DriverModule.Name, "SESSION_ARCHIVE",
            [new PipeConfirmationField("/ids/0", "string", "s1a2b3c4"), new PipeConfirmationField("/archived", "boolean", "true")],
            new PipeAsker("daoris-driver", 4242))!.Value;
        var delete = pipe.Confirmations.Ask(DriverModule.Name, "SESSION_DELETE",
            [new PipeConfirmationField("/id", "string", "s5e6f7a8")], new PipeAsker(null, null))!.Value;

        var read = await AnswerAsync(module, "PERSON_CONFIRMATIONS");

        Assert.Equal(2, read.GetProperty("seq").GetInt64());
        var shown = read.GetProperty("waiting").EnumerateArray().ToList();
        Assert.Equal(2, shown.Count);
        Assert.Equal(archive.Shown.Id, shown[0].GetProperty("id").GetString());
        Assert.Equal("SESSION_ARCHIVE", shown[0].GetProperty("route").GetString());
        Assert.Equal("DAORIS.DRIVER", shown[0].GetProperty("module").GetString());
        Assert.Equal("/ids/0", shown[0].GetProperty("fields")[0].GetProperty("name").GetString());
        Assert.Equal("boolean", shown[0].GetProperty("fields")[1].GetProperty("type").GetString());
        Assert.Equal("daoris-driver", shown[0].GetProperty("asker").GetProperty("process").GetString());
        Assert.Equal(4242, shown[0].GetProperty("asker").GetProperty("pid").GetInt32());
        // CONFIRMWAIT1a-i: a terminal's press has no time, and its card says whether a terminal waits on it.
        Assert.True(!shown[0].TryGetProperty("expires", out var expires) || expires.ValueKind == System.Text.Json.JsonValueKind.Null);
        Assert.True(shown[0].GetProperty("waitedOn").GetBoolean());

        // The person's answers: each once, and the terminal's press hears how it ended.
        var confirmed = await AnswerAsync(module, "PERSON_CONFIRM", new { id = archive.Shown.Id, confirm = true });
        Assert.True(confirmed.GetProperty("answered").GetBoolean());
        Assert.True(confirmed.GetProperty("confirmed").GetBoolean());
        Assert.Equal(PipeConfirmationEnd.Confirmed, (await archive.Ended).End);
        var refused = await AnswerAsync(module, "PERSON_CONFIRM", new { id = delete.Shown.Id, confirm = false });
        Assert.False(refused.GetProperty("confirmed").GetBoolean());
        Assert.Equal(PipeConfirmationEnd.Refused, (await delete.Ended).End);

        // One no longer waiting is said as data, by its code, for the card to word: answered elsewhere or taken back.
        var again = await AnswerAsync(module, "PERSON_CONFIRM", new { id = archive.Shown.Id, confirm = true });
        Assert.False(again.GetProperty("answered").GetBoolean());
        Assert.Equal("gone", again.GetProperty("why").GetString());
        Assert.False(string.IsNullOrEmpty(again.GetProperty("message").GetString()));
        Assert.Empty((await AnswerAsync(module, "PERSON_CONFIRMATIONS")).GetProperty("waiting").EnumerateArray());
    }

    /// <summary>
    /// CONFIRMWAIT1a-ii (the confirm-wait design §7.2, §8.1): a Confirm while an update drains Daoris is answered as data, <c>draining</c>,
    /// for the card to say, and the press still waits; a press restored after a restart says so and names its asker as its record's, and
    /// one carrying a key says it does not wait across a restart.
    /// </summary>
    [Fact]
    public async Task A_Confirm_during_the_drain_is_answered_draining_and_a_restored_or_keyed_card_says_so()
    {
        var (module, pipe, services) = Composed();
        using var composed = services;
        using var served = pipe;
        pipe.Draining = () => true;
        var waits = pipe.Confirmations.Ask(DriverModule.Name, "SESSION_DELETE",
            [new PipeConfirmationField("/id", "string", "s5e6f7a8")], new PipeAsker("pwsh", 7))!.Value;
        pipe.Confirmations.Ask(pipe.Confirmations.Hold()!, DriverModule.Name, "SESSION_ARCHIVE", [], new PipeAsker("pwsh", 7),
            @as: new PipeAsAsked(Guid.NewGuid().ToString("N"), Restored: true, WaitedOn: false));
        pipe.Confirmations.Ask(pipe.Confirmations.Hold()!, "DAORIS.REMOTES", "SET", [], new PipeAsker("pwsh", 7),
            @as: new PipeAsAsked(Guid.NewGuid().ToString("N"), Keyed: true));

        var answer = await AnswerAsync(module, "PERSON_CONFIRM", new { id = waits.Shown.Id, confirm = true });

        Assert.False(answer.GetProperty("answered").GetBoolean());
        Assert.Equal("draining", answer.GetProperty("why").GetString());
        Assert.Equal("Daoris is about to restart; this still waits.", answer.GetProperty("message").GetString());
        Assert.False(waits.Ended.IsCompleted);
        var shown = (await AnswerAsync(module, "PERSON_CONFIRMATIONS")).GetProperty("waiting").EnumerateArray().ToList();
        Assert.Equal([(false, false), (true, false), (false, true)],
            shown.Select(each => (each.GetProperty("restored").GetBoolean(), each.GetProperty("keyed").GetBoolean())));
    }

    /// <summary>
    /// NOTIFY2j: the window reads what waits at the service's door as the loop was last told it, the service's own shape whole and
    /// numbered; none before the first telling.
    /// </summary>
    [Fact]
    public async Task The_window_reads_what_waits_at_the_service_as_the_loop_was_told_it()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        var none = await AnswerAsync(module, "SERVICE_CONFIRMATIONS");
        Assert.Equal(0, none.GetProperty("seq").GetInt64());
        Assert.Empty(none.GetProperty("waiting").EnumerateArray());

        using var waiting = System.Text.Json.JsonDocument.Parse(
            """[{"id":"a1b2c3d4e5f60718","state":"waiting","method":"POST","route":"/api/asks","act":"an ask","values":[],"fields":[{"name":"/sentence","type":"string","value":"Rename it."}],"asked":"2026-10-22T12:00:00+00:00","expires":"2026-10-22T12:02:00+00:00"}]""");
        Assert.True(loop.ServiceConfirmations.Tell([.. waiting.RootElement.EnumerateArray()]));
        Assert.False(loop.ServiceConfirmations.Tell([.. waiting.RootElement.EnumerateArray()]));

        var read = await AnswerAsync(module, "SERVICE_CONFIRMATIONS");
        Assert.Equal(1, read.GetProperty("seq").GetInt64());
        var shown = Assert.Single(read.GetProperty("waiting").EnumerateArray());
        Assert.Equal("an ask", shown.GetProperty("act").GetString());
        Assert.Equal("Rename it.", shown.GetProperty("fields")[0].GetProperty("value").GetString());
    }

    /// <summary>
    /// CONFIRMSEEN1: the window says a card is in front of the person, kept once; it moves no clock (CONFIRMWAIT1a-i). One no longer
    /// waiting is said as data, by its code.
    /// </summary>
    [Fact]
    public async Task The_window_says_a_card_is_shown_once_and_it_moves_no_clock()
    {
        var (module, pipe, services) = Composed();
        using var composed = services;
        using var served = pipe;
        var archive = pipe.Confirmations.Ask(DriverModule.Name, "SESSION_ARCHIVE",
            [new PipeConfirmationField("/ids/0", "string", "s1a2b3c4")], new PipeAsker("daoris-driver", 4242))!.Value;
        var unshown = (await AnswerAsync(module, "PERSON_CONFIRMATIONS")).GetProperty("waiting")[0];
        Assert.True(!unshown.TryGetProperty("shownAt", out var none) || none.ValueKind == System.Text.Json.JsonValueKind.Null);

        var shown = await AnswerAsync(module, "PERSON_CONFIRM_SHOWN", new { id = archive.Shown.Id });

        Assert.True(shown.GetProperty("shown").GetBoolean());
        var waiting = (await AnswerAsync(module, "PERSON_CONFIRMATIONS")).GetProperty("waiting")[0];
        var at = waiting.GetProperty("shownAt").GetDateTimeOffset();
        Assert.True(!waiting.TryGetProperty("expires", out var expires) || expires.ValueKind == System.Text.Json.JsonValueKind.Null);
        Assert.True((await AnswerAsync(module, "PERSON_CONFIRM_SHOWN", new { id = archive.Shown.Id })).GetProperty("shown").GetBoolean());
        Assert.Equal(at, (await AnswerAsync(module, "PERSON_CONFIRMATIONS")).GetProperty("waiting")[0].GetProperty("shownAt").GetDateTimeOffset());

        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(archive.Shown.Id, confirm: false)).Outcome);
        var gone = await AnswerAsync(module, "PERSON_CONFIRM_SHOWN", new { id = archive.Shown.Id });
        Assert.False(gone.GetProperty("shown").GetBoolean());
        Assert.Equal("gone", gone.GetProperty("why").GetString());
    }

    /// <summary>
    /// TERMTRUST1a (D201 point 5, the terminal-trust design §4.1 and §8 case 9): the window grants the standing confirmation for the
    /// presses ticked and turns it off with none; the record is written and read back as STATE's `standingConfirmation`; a press the
    /// build does not name is refused as data and writes nothing; and a grant this build does not offer yet is refused `not-offered`.
    /// </summary>
    [Fact]
    public async Task The_window_grants_and_revokes_the_standing_confirmation_and_STATE_says_it()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Shenora.Core.Events.IEventBus>(Bus);
        services.UseMessageDispatcher((_, _) => { });
        using var provider = services.BuildServiceProvider();
        var loop = Loop();
        using var pipe = new InstancePipe(
            provider.GetRequiredService<IMessageDispatcher>(), Bus, Home,
            standing: new StandingConfirmation(Home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), offered: true));
        loop.Pipe = pipe;
        var module = new DriverModule(Bus, loop);

        var unknown = await AnswerAsync(module, "SET_STANDING_CONFIRMATION", new { presses = new[] { "stop", "decline" } });
        Assert.Equal((false, "unknown-press"), (unknown.GetProperty("done").GetBoolean(), unknown.GetProperty("why").GetString()));
        Assert.False(File.Exists(pipe.StandingConfirmation.RecordPath));

        var granted = await AnswerAsync(module, "SET_STANDING_CONFIRMATION", new { presses = new[] { "hold", "stop" } });
        Assert.True(granted.GetProperty("done").GetBoolean());
        Assert.True(File.Exists(pipe.StandingConfirmation.RecordPath));
        var state = (await AnswerAsync(module, "STATE")).GetProperty("standingConfirmation");
        Assert.True(state.GetProperty("offered").GetBoolean());
        Assert.True(state.GetProperty("on").GetBoolean());
        Assert.Equal(["stop", "hold"], state.GetProperty("presses").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal(StandingPresses.Built, state.GetProperty("built").EnumerateArray().Select(each => each.GetString()!));
        Assert.Equal(0, state.GetProperty("used").GetInt32());

        // Read back as the next start reads it.
        var next = new StandingConfirmation(Home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), offered: true);
        next.Load();
        Assert.Equal([StandingPresses.Stop, StandingPresses.Hold], next.Now().Presses);

        var revoked = await AnswerAsync(module, "SET_STANDING_CONFIRMATION", new { presses = Array.Empty<string>() });
        Assert.True(revoked.GetProperty("done").GetBoolean());
        Assert.False((await AnswerAsync(module, "STATE")).GetProperty("standingConfirmation").GetProperty("on").GetBoolean());

        // This build does not offer it yet (D201's note on order).
        using var unoffered = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home);
        loop.Pipe = unoffered;
        var refused = await AnswerAsync(module, "SET_STANDING_CONFIRMATION", new { presses = new[] { "stop" } });
        Assert.Equal((false, StandingConfirmation.NotOffered), (refused.GetProperty("done").GetBoolean(), refused.GetProperty("why").GetString()));
        Assert.False((await AnswerAsync(module, "STATE")).GetProperty("standingConfirmation").GetProperty("offered").GetBoolean());
    }

    [Fact]
    public async Task A_shell_with_no_pipe_reads_none_waiting_and_answers_none()
    {
        var module = Module();

        var read = await AnswerAsync(module, "PERSON_CONFIRMATIONS");
        Assert.Equal(0, read.GetProperty("seq").GetInt64());
        Assert.Empty(read.GetProperty("waiting").EnumerateArray());
        Assert.Equal("gone", (await AnswerAsync(module, "PERSON_CONFIRM", new { id = "nothing", confirm = true })).GetProperty("why").GetString());
        Assert.Equal("gone", (await AnswerAsync(module, "PERSON_CONFIRM_SHOWN", new { id = "nothing" })).GetProperty("why").GetString());
    }

    /// <summary>
    /// The page asks the shell for the person key as it loads, and keeps it in memory (D156 point 2). The shell mints none until
    /// PERSONDOOR1g, so it answers none, and the page sends its writes as it did.
    /// </summary>
    [Fact]
    public async Task The_key_the_shell_holds_for_its_host_is_none_until_the_shell_mints_one()
    {
        var key = await AnswerAsync(Module(), "PERSON_KEY");

        Assert.Equal(System.Text.Json.JsonValueKind.Null, key.GetProperty("key").ValueKind);
    }
}
