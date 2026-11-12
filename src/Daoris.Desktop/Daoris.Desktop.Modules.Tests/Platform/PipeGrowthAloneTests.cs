using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The growth switch is the person's alone: <c>SET_GROWTH</c> is refused to a press that proves a session's key, in
/// every mode, with nothing asked or run, since a switch an agent could turn back on would hold nothing; and to a process inside an agents' job. The
/// person's own press, from a terminal outside every agent, is asked on a card and runs on their yes.
/// </summary>
public sealed class PipeGrowthAloneTests : PipeDelegateHarness
{
    private static readonly object Sets = new { workspace = "aurora", value = "mode" };

    private async Task AssertRefusedAsync(string session, Action prepare, string? proofOver = null)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, nonce, driver, services, log) = await ServeAsync();
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var key = _delegates.Draw(session);
        prepare();

        var answer = await client.AnswerAsync(await SendProvedAsync(client, "SET_GROWTH", Sets, session, key, nonce, proofOver));

        Assert.Equal((PipeOutsideOnly.PersonAlone, PipeOutsideOnly.PersonAloneSentence), (Code(answer), answer.GetProperty("error").GetProperty("message").GetString()));
        Assert.Equal(0, driver.Ran("SET_GROWTH"));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Empty(_said);
        Assert.Empty(pipe.StandingConfirmation.Now().HelpPresses);
        Assert.Empty(Logged(PipeDelegate.PressedEvent));
        Assert.Empty(Logged(PipeDelegate.SessionPressedEvent));
    }

    [Fact]
    public Task Ask_Daoris_in_full_access_cannot_press_it() => AssertRefusedAsync("h1", () => _modes["h1"] = HelpModes.Full);

    [Fact]
    public Task Ask_Daoris_in_restricted_mode_cannot_press_it() => AssertRefusedAsync("h1", () => _modes["h1"] = HelpModes.Restricted);

    [Fact]
    public Task Ask_Daoris_in_restricted_mode_with_the_settings_family_granted_cannot_press_it() =>
        AssertRefusedAsync("h1", () =>
        {
            _modes["h1"] = HelpModes.Restricted;
            _families["h1"] = [ActFamilies.Settings];
        });

    [Fact]
    public Task A_working_session_in_an_unrestricted_setup_cannot_press_it() => AssertRefusedAsync("s-quest", () => _access["s-quest"] = AccessRules.Unrestricted);

    [Fact]
    public Task A_working_session_that_asks_cannot_press_it_either() => AssertRefusedAsync("s-quest", () => _access["s-quest"] = AccessRules.Asking);

    /// <summary>A proof that does not hold is a session's press all the same, so it is refused as the person's alone, not asked.</summary>
    [Fact]
    public Task A_press_naming_a_session_with_a_proof_that_does_not_hold_is_refused_the_same() =>
        AssertRefusedAsync("h1", () => _modes["h1"] = HelpModes.Full, proofOver: "{\"workspace\":\"other\"}");

    [Fact]
    public async Task A_press_from_a_process_inside_an_agents_job_with_no_proof_is_refused_the_same()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, _, driver, services, log) = await ServeAsync(callerAt: StandingPlace.InAgentJob);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "SET_GROWTH", Sets));

        Assert.Equal(PipeOutsideOnly.PersonAlone, Code(answer));
        Assert.Equal(0, driver.Ran("SET_GROWTH"));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
    }

    [Theory]
    [InlineData(StandingPlace.Outside)]
    [InlineData(StandingPlace.Untold)]
    public async Task The_persons_own_press_from_a_terminal_outside_every_agent_asks_on_a_card_and_runs_on_their_yes(StandingPlace place)
    {
        if (!OperatingSystem.IsWindows()) return;
        var (pipe, client, _, driver, services, log) = await ServeAsync(callerAt: place);
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;

        var id = await client.SendAsync(DriverModule.Name, "SET_GROWTH", Sets);
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var card = Assert.Single(pipe.Confirmations.Now().Waiting);

        Assert.Equal(((string?)null, false), (card.Asker.Session, card.Asker.Help));
        Assert.Equal(0, driver.Ran("SET_GROWTH"));
        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(card.Id, confirm: true)).Outcome);
        Assert.True((await client.AnswerAsync(id)).GetProperty("success").GetBoolean());
        Assert.Equal(1, driver.Ran("SET_GROWTH"));
    }
}
