using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// When each account's sign-in state was last read, as the application saw it (UX6e, D150 §5.3): what the Agents place
/// says beside each state. Pure: a report in, the moments out, so no probe runs here.
/// </summary>
public sealed class RosterReadsTests
{
    private static readonly DateTimeOffset Ten = new(2026, 10, 4, 10, 0, 0, TimeSpan.Zero);

    private static HarnessReport Report(
        LoginState own = LoginState.Unknown, string? ownWho = null, params ProfileReport[] profiles) =>
        new("claude-code", true, "2.1.4", null, "CLAUDE_CONFIG_DIR", null, profiles, own, ownWho);

    private static ProfileReport Account(string name, LoginState login, string? who = null) =>
        new(name, $"home/{name}", login, who);

    /// <summary>The first answer since the application started is the probe that just ran, so a word it gave was read now.</summary>
    [Fact]
    public void A_first_answer_dates_each_word_it_gives_and_leaves_an_unanswered_one_never_read()
    {
        var reads = new RosterReads();

        var stamps = reads.Observe(Report(profiles: [Account("account-1", LoginState.Out), Account("account-2", LoginState.Unknown)]), Ten, asked: false);

        Assert.Equal(Ten, stamps.Accounts["account-1"]);
        Assert.Null(stamps.Accounts["account-2"]);
        // The tool's own sign-in is asked only at a person's press (TOOL6g): unknown here is never read.
        Assert.Null(stamps.Own);
    }

    /// <summary>The same report served again is the same reading: nothing was asked, so nothing moves.</summary>
    [Fact]
    public void The_same_report_served_again_keeps_every_moment()
    {
        var reads = new RosterReads();
        var report = Report(profiles: [Account("account-1", LoginState.In, "you@work")]);
        reads.Observe(report, Ten, asked: false);

        var stamps = reads.Observe(report, Ten.AddHours(1), asked: false);

        Assert.Equal(Ten, stamps.Accounts["account-1"]);
    }

    /// <summary>
    /// A person's press asks every account again, and the tool's own sign-in: each is read now, an answer that failed too,
    /// which then says when the last read failed.
    /// </summary>
    [Fact]
    public void A_press_dates_every_account_and_the_tools_own_sign_in_now()
    {
        var reads = new RosterReads();
        reads.Observe(Report(profiles: [Account("account-1", LoginState.In)]), Ten, asked: false);

        var later = Ten.AddMinutes(42);
        var stamps = reads.Observe(Report(LoginState.In, "me@home", Account("account-1", LoginState.In), Account("account-2", LoginState.Unknown)), later, asked: true);

        Assert.Equal(later, stamps.Accounts["account-1"]);
        Assert.Equal(later, stamps.Accounts["account-2"]);
        Assert.Equal(later, stamps.Own);
    }

    /// <summary>
    /// An account a session of Daoris's runs on is not asked by the probe (TOOL6g), which keeps its last word: so a press
    /// keeps that word's moment too, and claims no fresher reading than was made.
    /// </summary>
    [Fact]
    public void A_press_keeps_the_moment_of_an_account_a_session_runs_on()
    {
        var reads = new RosterReads();
        reads.Observe(Report(profiles: [Account("account-1", LoginState.In), Account("account-2", LoginState.In)]), Ten, asked: false);

        var later = Ten.AddMinutes(5);
        var stamps = reads.Observe(
            Report(LoginState.Unknown, null, Account("account-1", LoginState.In), Account("account-2", LoginState.In)),
            later, asked: true, busy: account => account == "account-2");

        Assert.Equal(later, stamps.Accounts["account-1"]);
        Assert.Equal(Ten, stamps.Accounts["account-2"]);
    }

    /// <summary>
    /// A report the driver made on its own since the last answer (a start asking one account again) is dated where its word
    /// changed, when the application first heard it; an unchanged word keeps its moment rather than claim a newer one.
    /// </summary>
    [Fact]
    public void A_report_the_driver_made_by_itself_dates_only_the_words_that_changed()
    {
        var reads = new RosterReads();
        reads.Observe(Report(profiles: [Account("account-1", LoginState.Out), Account("account-2", LoginState.In)]), Ten, asked: false);

        var later = Ten.AddMinutes(20);
        var stamps = reads.Observe(Report(profiles: [Account("account-1", LoginState.In, "you@work"), Account("account-2", LoginState.In)]), later, asked: false);

        Assert.Equal(later, stamps.Accounts["account-1"]);
        Assert.Equal(Ten, stamps.Accounts["account-2"]);
    }

    /// <summary>An account removed since is forgotten, so one made later under its name starts never read.</summary>
    [Fact]
    public void An_account_gone_from_the_report_is_forgotten()
    {
        var reads = new RosterReads();
        reads.Observe(Report(profiles: [Account("account-1", LoginState.In)]), Ten, asked: false);
        reads.Observe(Report(), Ten.AddMinutes(1), asked: false);

        var stamps = reads.Observe(Report(profiles: [Account("account-1", LoginState.Unknown)]), Ten.AddMinutes(2), asked: false);

        Assert.Null(stamps.Accounts["account-1"]);
    }

    /// <summary>Each door is its own reading: a door onto the same accounts keeps its own moments.</summary>
    [Fact]
    public void Each_door_keeps_its_own_moments()
    {
        var reads = new RosterReads();
        reads.Observe(Report(profiles: [Account("account-1", LoginState.In)]), Ten, asked: false);
        var protocol = Report(profiles: [Account("account-1", LoginState.In)]) with { Adapter = "claude-code-acp" };

        var stamps = reads.Observe(protocol, Ten.AddHours(2), asked: false);

        Assert.Equal(Ten.AddHours(2), stamps.Accounts["account-1"]);
        Assert.Equal(Ten, reads.Observe(Report(profiles: [Account("account-1", LoginState.In)]), Ten.AddHours(3), asked: false).Accounts["account-1"]);
    }
}
