using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// OWNSPACE1a (D175 points 1-3 and its 2026-10-17 note): every start Daoris makes runs in a configuration home Daoris owns. Where
/// a start's scope names no account, the agent's Daoris accounts on this machine are its list; with none signed in it waits, with
/// one sentence, and nothing is spawned; a record that began in the person's own tool home resumes there; a test double, whose own
/// home is nobody's, is exempt.
/// </summary>
/// <remarks>
/// Nothing here starts a process: the agent is present by a file look (<see cref="HarnessToolchain.ProbeByPresence"/>), as
/// <c>SignedOutWaitTests</c>' is, and declares no login question, so an account never read runs (SES3). Each start kind is asked
/// through the roster's door its own site asks, and its answer is applied as the spawn applies it (<see cref="HarnessProbe.Apply"/>).
/// </remarks>
public sealed class OwnSpaceTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-own-space-" + Guid.NewGuid().ToString("N")[..8]);

    public OwnSpaceTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Command => Path.Combine(_home, "agent-here");

    private const string NoAccount =
        "No `fake` account of Daoris's own is signed in, and Daoris never starts its sessions in your own `fake` sign-in";

    private sealed class Adapter(string name, HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => name;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    private HarnessToolchain Real => new(
        Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true);

    private HarnessRoster Roster() => new(
        new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = new Adapter("fake", Real),
            ["double"] = new Adapter("double", Real with { RunsInOwnHome = true }),
        }),
        Settings);

    private static DriverConfig Config => DriverConfig.Empty with { Adapter = "fake" };

    private string Account(string name)
    {
        var home = HarnessSettings.ProfileHome(_home, "fake", name);
        Directory.CreateDirectory(home);
        return home;
    }

    /// <summary>Every kind of start, by the door its own site asks the roster through.</summary>
    public static TheoryData<string> Kinds => ["driven", "carry-on", "intake", "conversation", "room", "helper", "reviewer"];

    private static async Task<HarnessSelection> StartAsync(HarnessRoster roster, string kind) => kind switch
    {
        // Driver.cs: a first start and a carry-on take the walk through the hand-over's door, naming no account.
        "driven" or "carry-on" => (await roster.SelectHandedAsync("fake", Config, "aurora", null, StartKind.Driven)).Selection,
        // DriverIntakes.cs: an intake is driven work.
        "intake" => await roster.SelectAsync("fake", Config, "aurora", chosen: null, StartKind.Driven),
        // ChatRunner.cs: a conversation the person picked no account for, and Ask Daoris's room, which has no workspace.
        "conversation" => await roster.SelectAsync("fake", Config, "aurora", chosen: null, StartKind.Conversation),
        "room" => await roster.SelectAsync("fake", Config, null, null, StartKind.Conversation),
        // HelperChoice and ReviewerChoice: the walk over the rule's agents, whose sentence says what held each where none ran.
        "helper" => Chosen(await HelperChoice.ChooseAsync(
            roster, new HelperAsk("h1", "s1", "Read this.", "fake", HelperAsk.ByPerson, DateTimeOffset.UnixEpoch), HelperRule.Default, null,
            Config, "aurora")),
        "reviewer" => Chosen(await ReviewerChoice.ChooseAsync(roster, OpinionRule.None with { Reviewers = ["fake"] }, [], Config, "aurora")),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static HarnessSelection Chosen(ReviewerChoice choice) => choice.Selection ?? new HarnessSelection(choice.Sentence);

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task A_start_that_names_no_account_runs_in_a_Daoris_account_never_the_tool_s_own_home(string kind)
    {
        var home = Account("daoris-1");

        var selection = await StartAsync(Roster(), kind);
        var info = new ProcessStartInfo();
        HarnessProbe.Apply(info, Real, selection.ProfileHome);

        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Equal(("daoris-1", home), (selection.Profile, selection.ProfileHome));
        Assert.Equal(home, info.Environment["FAKE_HOME"]);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task With_no_Daoris_account_a_start_waits_with_one_sentence_and_spawns_nothing(string kind)
    {
        var selection = await StartAsync(Roster(), kind);

        Assert.False(selection.Allowed);
        Assert.Null(selection.ProfileHome);
        Assert.Contains(NoAccount, selection.Refusal);
    }

    [Fact]
    public async Task The_hold_is_coded_and_waits_for_a_sign_in_naming_no_account()
    {
        var selection = await Roster().SelectAsync("fake", Config, "aurora", chosen: null);

        var note = selection.RefusalNote!;
        Assert.Equal(selection.Refusal, note.Note);
        var part = Assert.Single(note.Parts);
        Assert.Equal("held.no-account", part.Code);
        Assert.Equal("fake", part.Value("owner"));
        Assert.Equal(("fake", 0), (selection.SignedOut!.Agent, selection.SignedOut.Accounts.Count));
    }

    [Fact]
    public async Task Accounts_all_signed_out_hold_with_the_same_sentence()
    {
        Account("daoris-1");
        Account("daoris-2");
        var roster = Roster();
        roster.SignedOut("fake", "daoris-1");
        roster.SignedOut("fake", "daoris-2");

        var selection = await roster.SelectAsync("fake", Config, "aurora", chosen: null);

        Assert.False(selection.Allowed);
        Assert.StartsWith(NoAccount, selection.Refusal);
        Assert.Equal(["daoris-1", "daoris-2"], selection.SignedOut!.Accounts);
    }

    [Fact]
    public async Task The_walk_goes_over_the_agent_s_Daoris_accounts_past_one_signed_out()
    {
        Account("daoris-1");
        var second = Account("daoris-2");
        var roster = Roster();
        roster.SignedOut("fake", "daoris-1");

        var selection = await roster.SelectAsync("fake", Config, "aurora", chosen: null);

        Assert.Equal(("daoris-2", second), (selection.Profile, selection.ProfileHome));
    }

    [Theory]
    [InlineData(StartKind.Driven, true)]
    [InlineData(StartKind.Conversation, false)]
    public async Task A_record_that_began_in_the_tool_s_own_home_resumes_there(StartKind kind, bool walks)
    {
        Account("daoris-1");

        var resume = await Roster().ResumeAsync("fake", Config, "aurora", account: null, kind, walks: walks);
        var info = new ProcessStartInfo();
        HarnessProbe.Apply(info, Real, resume.Selection.ProfileHome);

        Assert.True(resume.Selection.Allowed, resume.Selection.Refusal);
        Assert.Equal(NextHold.Ready, resume.Own);
        Assert.Equal(((string?)null, (string?)null), (resume.Selection.Profile, resume.Selection.ProfileHome));
        Assert.False(info.Environment.ContainsKey("FAKE_HOME"));
    }

    [Fact]
    public async Task A_record_on_a_Daoris_account_resumes_on_it()
    {
        Account("daoris-1");
        var second = Account("daoris-2");

        var resume = await Roster().ResumeAsync("fake", Config, "aurora", "daoris-2", StartKind.Conversation, walks: false);

        Assert.Equal(("daoris-2", second), (resume.Selection.Profile, resume.Selection.ProfileHome));
    }

    [Fact]
    public async Task A_test_double_whose_own_home_is_nobody_s_still_starts_there()
    {
        var selection = await Roster().SelectAsync("double", Config with { Adapter = "double" }, "aurora", chosen: null);

        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Null(selection.ProfileHome);
    }

    [Fact]
    public void Only_the_stub_and_its_protocol_twin_are_exempt_among_the_build_s_agents()
    {
        var built = AdapterSet.Built();
        var exempt = built.Names
            .Where(name => built.Resolve(name).Toolchain is { RunsInOwnHome: true })
            .Order(StringComparer.Ordinal);

        Assert.Equal(["acp-stub", "stub"], exempt);
    }

    /// <summary>
    /// 🔴 A resume of a record begun in the tool's own home goes on there, but where its words are carried to a NEW record (its
    /// conversation could not go on), that record is a new start: it takes the walk (D130 §16.2), and holds where the walk holds,
    /// never a new session in the person's home.
    /// </summary>
    [Fact]
    public async Task Words_an_own_home_resume_carries_to_a_new_record_take_the_walk()
    {
        var roster = Roster();
        var resume = await roster.ResumeAsync("fake", Config, "aurora", account: null, StartKind.Driven);
        Assert.Null(resume.Selection.ProfileHome);

        var (held, _) = await roster.CarriedOnAsync(resume, "fake", Config, "aurora", null);
        Assert.False(held.Allowed);
        Assert.StartsWith(NoAccount, held.Refusal);

        var home = Account("daoris-1");
        var (runs, _) = await roster.CarriedOnAsync(resume, "fake", Config, "aurora", null);
        Assert.Equal(("daoris-1", home), (runs.Profile, runs.ProfileHome));
    }

    [Fact]
    public async Task Words_a_Daoris_account_s_resume_carries_to_a_new_record_stay_on_that_account()
    {
        Account("daoris-1");
        var second = Account("daoris-2");
        var roster = Roster();
        var resume = await roster.ResumeAsync("fake", Config, "aurora", "daoris-2", StartKind.Conversation, walks: false);

        var (carried, passed) = await roster.CarriedOnAsync(resume, "fake", Config, "aurora", null);

        Assert.Same(resume.Selection, carried);
        Assert.Equal(("daoris-2", second, (string?)null), (carried.Profile, carried.ProfileHome, passed));
    }

    /// <summary>
    /// 🔴 The page asks the next start by the accounts' OWNER, which no adapter may be named (`codex` behind `codex-acp`): its
    /// toolchain is read through its doors, so the rule holds for it as for any agent.
    /// </summary>
    [Fact]
    public void The_next_start_of_an_owner_no_adapter_is_named_after_follows_the_rule()
    {
        var roster = new HarnessRoster(AdapterSet.Built(), Settings);

        Assert.Equal(((string?)null, NextReason.Waits), roster.Next("codex", null) is var waits ? (waits.Account, waits.Reason) : default);

        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "codex", "daoris-1"));

        Assert.Equal(("daoris-1", NextReason.Named), roster.Next("codex", null) is var next ? (next.Account, next.Reason) : default);
    }

    [Fact]
    public void The_next_start_with_no_Daoris_account_waits_and_with_one_takes_it()
    {
        Assert.Equal((null, NextReason.Waits), (Roster().Next("fake", "aurora") is var waits ? (waits.Account, waits.Reason) : default));

        Account("daoris-1");

        var next = Roster().Next("fake", "aurora");
        Assert.Equal(("daoris-1", NextReason.Named), (next.Account, next.Reason));
    }
}
