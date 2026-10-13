using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ACCT2b (D125's ACCT2 and ACCT2b notes): what a person reads names an account by the name they gave it, looked up when it is
/// said, and by its id where it has none. A held start's sentence, rotation's waits, a picked account's refusal, a refused
/// credential's hold and a resume's wait each say the name, and the facts *What needs you* says them from carry it beside the
/// id. A record keeps the id, which never moves: the record's opening line, a note's English, which travels, and the machine
/// log. A command keeps the id too, since a terminal takes it whatever the account is called by then.
/// </summary>
/// <remarks>
/// Nothing here starts a process. The walk runs on an agent present by a file look (<see cref="HarnessToolchain.ProbeByPresence"/>),
/// as <c>AccountRotationTests</c>' does, and the tick on the stand-in service, as <c>AccountLimitHoldTests</c>' does.
/// </remarks>
public sealed class AccountNamesSaidTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));

    private static readonly DateTimeOffset Until = new(2026, 10, 3, 16, 2, 0, TimeSpan.FromMinutes(345));

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    /// <summary>A fresh id the person named (ACCT2), an old <c>account-N</c> they named, and a fresh one they never named.</summary>
    private const string Work = "acct-3f9c1a2b";
    private const string Personal = "account-2";
    private const string Spare = "acct-77aa00ff";

    private static readonly IReadOnlyDictionary<string, string> Names =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [Work] = "work", [Personal] = "personal" };

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-names-said-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private DateTimeOffset _now = Seen;

    public AccountNamesSaidTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
        _ledger.Register("engine", Path.Combine(_home, "engine"));
    }

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => Path.Combine(_home, "harnesses.json");

    private string Command => Path.Combine(_home, "agent-here");

    private sealed class Adapter(string name, HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => name;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    private HarnessRoster Fake() =>
        new(new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = new Adapter("fake", new HarnessToolchain(
                Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true)),
        }), Settings)
        {
            Clock = () => _now,
            Zone = Zone,
        };

    private HarnessRoster Built() => new(AdapterSet.Built(), Settings) { Clock = () => _now, Zone = Zone };

    private static DriverConfig FakeConfig => DriverConfig.Empty with { Adapter = "fake" };

    private static DriverConfig Config => DriverConfig.Empty with
    {
        Drivable = ["engine"],
        Trees = ["engine"],
        Adapter = "acp-stub",
        Strikes = 1,
        PollSeconds = 1,
    };

    private Daoris.Driver.Driver Driver(ServiceClient service, HarnessRoster roster) =>
        new(service, Config, AdapterSet.Built(), _home, harnesses: roster);

    /// <summary>The agent's accounts, a folder each, and the names the person gave them, by the driver's own writer.</summary>
    private void Accounts(string agent, params string[] ids)
    {
        foreach (var id in ids) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, agent, id));
        foreach (var (id, name) in Names.Where(pair => ids.Contains(pair.Key)))
        {
            AccountNames.Rename(_home, agent, HarnessSettings.Profiles(_home, agent), id, name);
        }
    }

    private void Wire(Func<HarnessSettings, HarnessSettings> edit) => edit(new HarnessSettings()).Save(Settings);

    private CoolingEntry Cool(string agent, string? account, DateTimeOffset? until = null)
    {
        var entry = new CoolingEntry(agent, account, until ?? Until, true, "weekly", Seen, "s1");
        AccountCooling.Cool(_home, entry, _now);
        return entry;
    }

    // ——— The one rule: the person's name, else the id.

    [Theory]
    [InlineData(Work, "work")]
    [InlineData("ACCT-3F9C1A2B", "work")]
    [InlineData(Personal, "personal")]
    [InlineData(Spare, Spare)]
    public void An_account_is_said_by_its_name_and_by_its_id_where_it_has_none(string account, string said)
    {
        Assert.Equal(said, AccountNames.Said(Names, account));
        Assert.Equal(account, AccountNames.Said(null, account));
    }

    // ——— A held start (D125 §4, TOOL6g): said where the person reads it, so by name.

    [Fact]
    public async Task A_cooling_account_s_hold_names_it_by_its_name()
    {
        Accounts("fake", Work);
        Wire(s => s.WithDefault("fake", Work));
        var cooling = Cool("fake", Work);

        var held = await Fake().SelectAsync("fake", FakeConfig, null, null);

        Assert.Equal(cooling, held.Cooling);
        Assert.Equal(
            $"the `fake` account `work` is cooling until Oct 3, 16:02 ({Zone.Id}), as the agent said. Daoris starts nothing on it until then.",
            held.Refusal);
        Assert.Equal(held.Refusal, CoolingWords.Hold(cooling, Zone, Names));
    }

    [Fact]
    public void A_wait_over_an_order_names_each_account_by_its_name_and_each_sign_in_by_its_id()
    {
        var cooling = new CoolingEntry("fake", Personal, Until, false, null, Seen, "s1");

        Assert.Equal(
            $"no `fake` account this start may use is ready: `work` is not signed in, `personal` is cooling until Oct 3, 16:02 "
            + $"({Zone.Id}), `{Spare}` was refused by its provider; the first ready, `personal`, at Oct 3, 16:02 ({Zone.Id}), "
            + "Daoris's default: the agent named no time. Daoris starts nothing on them until then. A sign-in starts it sooner: "
            + $"`daoris agent login fake --profile {Work}`, or Agents → the agent's page → Accounts.",
            RotationWords.Wait("fake", [
                new(Work, AccountReadiness.SignedOut), new(Personal, AccountReadiness.Cooling, cooling), new(Spare, AccountReadiness.Refused),
            ], Zone, Names));
    }

    [Fact]
    public void A_hold_with_nothing_cooling_names_each_account_by_its_name()
    {
        Assert.Equal(
            "no `claude-code` account this start may use is ready: `work` is not signed in, `personal` was refused by its provider. "
            + $"A sign-in starts it: `daoris agent login claude-code --profile {Work}`, or Agents → the agent's page → Accounts.",
            RotationWords.NoneReady("claude-code", [new(Work, AccountReadiness.SignedOut), new(Personal, AccountReadiness.Refused)], Names));
    }

    [Fact]
    public async Task A_pick_refused_names_the_picked_account_and_the_ready_ones_by_their_names()
    {
        Accounts("fake", Work, Personal, Spare);
        var cooling = Cool("fake", Work);

        var held = await Fake().SelectAsync("fake", FakeConfig, null, chosen: Work);

        Assert.Equal($"{CoolingWords.Hold(cooling, Zone, Names)} Ready now: `personal`, `{Spare}`.", held.Refusal);
        Assert.Contains("account `work`", held.Refusal);
    }

    [Fact]
    public void What_a_wait_adds_names_the_accounts_by_their_names_and_the_door_by_their_ids()
    {
        Assert.Equal("`work` is kept for conversations.", RotationWords.KeptAside(Work, Names));
        Assert.Equal(
            "Not cooling, and not among the accounts `forge` may use: `personal`, `" + Spare + "` — Daoris starts nothing on them "
            + $"unless a list names them; `daoris agent profile order fake {Work} {Personal} --workspace forge` adds `personal`.",
            RotationWords.Outside("fake", "forge", [Work], [Personal, Spare], Names));
    }

    [Fact]
    public async Task A_signed_out_account_s_hold_names_it_and_its_sign_in_by_its_id_and_its_facts_carry_both()
    {
        Accounts("fake", Work);
        Wire(s => s.WithDefault("fake", Work));
        var roster = Fake();
        roster.SignedOut("fake", Work);

        var held = await roster.SelectAsync("fake", FakeConfig, null, null);

        Assert.Equal(
            $"the `fake` account `work` is not signed in, so a session would have nothing to run as — `daoris agent login fake "
            + $"--profile {Work}` runs the agent's own sign-in into it. Daoris manages the directory and the name; the credential "
            + "stays in the agent's own store.",
            held.Refusal);
        Assert.Equal([Work], held.SignedOut!.Accounts);
        Assert.Equal(["work"], held.SignedOut.Names);
    }

    [Fact]
    public async Task A_list_s_facts_name_each_account_not_signed_in_and_null_where_it_has_no_name()
    {
        Accounts("fake", Work, Spare);
        Wire(s => s.WithDefault("fake", Work).WithRotation("fake", [Work, Spare]));
        var roster = Fake();
        roster.SignedOut("fake", Work);
        roster.SignedOut("fake", Spare);

        var held = await roster.SelectAsync("fake", FakeConfig, null, null);

        Assert.Equal([Work, Spare], held.SignedOut!.Accounts);
        Assert.Equal(["work", null], held.SignedOut.Names);
        Assert.StartsWith($"no `fake` account this start may use is ready: `work` is not signed in, `{Spare}` is not signed in.", held.Refusal);
    }

    [Fact]
    public void A_resume_s_wait_names_its_account_by_its_name()
    {
        var cooling = new CoolingEntry("claude-code", Work, Until, true, "weekly", Seen, "s1");

        Assert.StartsWith("the `claude-code` account `work` is cooling until", ResumeWords.Waits(cooling, Zone, Names));
        Assert.StartsWith($"the `claude-code` account `{Work}` is cooling until", ResumeWords.Waits(cooling, Zone));
    }

    // ——— A record keeps the id (D125's ACCT2 note): what reproduces it never moves.

    [Fact]
    public async Task A_rotated_start_s_record_opening_keeps_the_ids()
    {
        Accounts("fake", Work, Personal);
        Wire(s => s.WithDefault("fake", Work).WithRotation("fake", [Work, Personal]));
        Cool("fake", Work);

        var selection = await Fake().SelectAsync("fake", FakeConfig, null, null);

        Assert.Equal((Personal, Work), (selection.Profile, selection.Rotated!.From));
        Assert.Equal($"opened on `{Personal}`: the `fake` account `{Work}` is cooling until Oct 3, 16:02 ({Zone.Id}), as the agent said.",
            RotationWords.Opened(selection.Profile!, selection.Rotated));
    }

    [Fact]
    public async Task A_refused_credential_s_hold_says_its_name_and_its_note_which_travels_keeps_its_id()
    {
        using var service = _ledger.Client();
        Accounts("stub", "account-1");
        AccountNames.Rename(_home, "stub", HarnessSettings.Profiles(_home, "stub"), "account-1", "work");
        var roster = Built();
        var transcript = Path.Combine(_home, "s1.log");
        File.WriteAllText(transcript, "working on it\nFailed to authenticate. API Error: 401 API key is invalid.\n");

        var conclusion = Driver(service, roster).AccountRefused(
            new SessionConclusion("failed", "exit 1 before taking its quest."), AdapterSet.Built().Resolve("stub"),
            new HarnessSelection(null, "account-1"), HarnessEnding.Transcript(1, transcript));

        Assert.Contains("the `stub` account `account-1` (401)", conclusion.Note);
        Assert.DoesNotContain("work", conclusion.Note);
        Assert.Equal(["owner"], conclusion.Parts![^1].Values.Select(value => value.Key));
        Wire(s => s.WithDefault("stub", "account-1"));
        var held = await roster.SelectAsync("stub", DriverConfig.Empty with { Adapter = "stub" }, null, null);
        Assert.Equal(
            "an earlier session found that its provider refused the `stub` account `work` (401). Replace the key or sign in again — "
            + "on Settings, or `daoris agent` — and Daoris will start sessions on it again.",
            held.Refusal);
    }

    [Fact]
    public void A_refused_sign_in_s_note_keeps_the_id_of_a_named_account()
    {
        using var service = _ledger.Client();
        Accounts("stub", "account-1");
        AccountNames.Rename(_home, "stub", HarnessSettings.Profiles(_home, "stub"), "account-1", "work");
        const string refusal = "the ACP agent refused the call: Authentication required";

        var conclusion = Driver(service, Built()).AccountSignedOut(
            Observation.Conclude(0, "Open", turnFailed: refusal), AdapterSet.Built().Resolve("acp-stub"),
            new HarnessSelection(null, "account-1"), refusal);

        Assert.Contains("the `stub` account `account-1` for its sign-in", conclusion.Note);
        Assert.DoesNotContain("`work`", conclusion.Note);
        Assert.Equal(("account.signed-out", "stub"), (conclusion.Parts![^1].Code, (string?)conclusion.Parts[^1].Value("owner")));
    }

    // ——— A look (TOOL4g): the wait carries the name beside the id, and the log keeps the id alone.

    [Fact]
    public async Task A_wait_on_a_named_account_says_its_name_carries_it_beside_the_id_and_the_log_keeps_the_id()
    {
        using var service = _ledger.Client();
        var lines = new List<AccountLine>();
        service.AccountLined += lines.Add;
        Accounts("stub", "account-1");
        AccountNames.Rename(_home, "stub", HarnessSettings.Profiles(_home, "stub"), "account-1", "work");
        Wire(s => s.WithDefault("stub", "account-1"));
        var roster = Built();
        _ledger.Publish("q1", "engine");
        var (cut, _) = await service.OpenSessionAsync("q1", "acp-stub", null, "account-1", Path.Combine(_home, "engine"), null);
        await service.AdvanceAsync(cut!, "working");
        _ledger.Move("q1", "Taken");
        const string refusal = "the ACP agent refused the call: You've hit your individual spend limit · … · your weekly limit resets Oct 3, 4pm (Asia/Kathmandu)";
        await service.AdvanceAsync(cut!, "failed", note: $"the agent's turn failed with the quest still taken: {refusal}", limit: true);
        roster.Limited("acp-stub", "account-1", refusal, cut!);

        var look = await Driver(service, roster).TickAsync().WaitAsync(Bound);

        var sitting = Assert.Single(look.Considerations);
        Assert.Equal(StartVerdict.Blocked, sitting.Verdict);
        Assert.StartsWith("the `stub` account `work` is cooling until", sitting.Reason);
        var wait = Assert.Single(look.Waits);
        Assert.Equal(("account-1", "work"), (wait.Account, wait.Name));
        var line = Assert.Single(lines, l => l.Event == "starts.waiting");
        Assert.Equal("account-1", line.Data.Single(field => field.Key == "account").Value);
    }

    // ——— A command spells its ids for any shell (ACCTQUOTE1b, D125's ACCTQUOTE1 note): an id `profile add` was given may hold a
    // space, kept whole in double quotes, or a character no spelling keeps, named by its placeholder. The sentence around it
    // names the account as it is.

    /// <summary>An id a person gave, with a space, and one no shell can be handed whole.</summary>
    private const string Spaced = "my acct";
    private const string Ampersand = "R&D";

    [Fact]
    public void A_wait_s_and_a_hold_s_sign_ins_spell_each_id_for_any_shell()
    {
        var cooling = new CoolingEntry("fake", Personal, Until, false, null, Seen, "s1");

        var wait = RotationWords.Wait("fake", [
            new(Spaced, AccountReadiness.SignedOut), new(Personal, AccountReadiness.Cooling, cooling), new(Ampersand, AccountReadiness.SignedOut),
        ], Zone);
        var hold = RotationWords.NoneReady("claude-code", [new(Spaced, AccountReadiness.SignedOut), new(Ampersand, AccountReadiness.SignedOut)]);

        Assert.EndsWith(
            "A sign-in starts it sooner: `daoris agent login fake --profile \"my acct\"`, `daoris agent login fake --profile <account>`, "
            + "or Agents → the agent's page → Accounts.",
            wait);
        Assert.Equal(
            "no `claude-code` account this start may use is ready: `my acct` is not signed in, `R&D` is not signed in. A sign-in starts it: "
            + "`daoris agent login claude-code --profile \"my acct\"`, `daoris agent login claude-code --profile <account>`, or Agents → "
            + "the agent's page → Accounts.",
            hold);
    }

    [Fact]
    public void What_a_wait_adds_spells_its_door_s_ids_and_workspace_for_any_shell()
    {
        Assert.Equal(
            "Not cooling, and not among the accounts `my team` may use: `R&D`, `work` — Daoris starts nothing on them unless a list "
            + "names them; `daoris agent profile order fake \"my acct\" <account> --workspace \"my team\"` adds `R&D`.",
            RotationWords.Outside("fake", "my team", [Spaced], [Ampersand, Work], Names));
        Assert.EndsWith(
            "`daoris agent profile order fake work <account> --workspace <workspace>` adds `R&D`.",
            RotationWords.Outside("fake", "R&D", ["work"], [Ampersand]));
    }

    [Fact]
    public async Task A_signed_out_account_s_hold_spells_its_sign_in_s_id_for_any_shell()
    {
        Accounts("fake", Spaced);
        Wire(s => s.WithDefault("fake", Spaced));
        var roster = Fake();
        roster.SignedOut("fake", Spaced);

        var held = await roster.SelectAsync("fake", FakeConfig, null, null);

        Assert.Equal(
            "the `fake` account `my acct` is not signed in, so a session would have nothing to run as — `daoris agent login fake "
            + "--profile \"my acct\"` runs the agent's own sign-in into it. Daoris manages the directory and the name; the credential "
            + "stays in the agent's own store.",
            held.Refusal);
    }

    [Theory]
    [InlineData(Spaced, "`daoris agent login fake --profile \"my acct\"`")]
    [InlineData(Ampersand, "`daoris agent login fake --profile <account>`")]
    public void A_refused_sign_in_s_line_spells_its_id_for_any_shell(string account, string command)
    {
        var said = Daoris.Driver.Driver.SignedOutNote("fake", account);

        Assert.Equal(
            $"The agent refused the `fake` account `{account}` for its sign-in, so it reads signed out and Daoris starts nothing more on "
            + $"it until it is signed in: {command}, or Agents → the agent's page → Accounts.",
            said.Note);
    }

    [Theory]
    [InlineData(Spaced, "--profile \"my acct\" --yes")]
    [InlineData(Ampersand, "--profile <account> --yes")]
    public void A_trust_hold_s_command_spells_its_account_for_any_shell(string account, string command)
    {
        Assert.Contains(command, ClaudeTrust.Refusal(@"D:\fam\engine", account));
    }
}
