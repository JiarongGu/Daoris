using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGT3c (D125 point 1's rule, carried from a limit to AGT3b's refused credential): a key its provider refused is read from what
/// the harness itself said of its run's ending, never from words its agent could write. The protocol door says it in its
/// failure; the native door in a failed <c>result</c>, outside its frames and on its stderr; a door that carries only text gives
/// nothing but its transcript, so there only an exit that was not 0, which the harness sets, lets the transcript be read.
/// </summary>
/// <remarks>
/// Nothing here starts a process: the native door's output is fed through the real capture from text, and the agent is present by
/// a file look that asks nobody whether an account is signed in, so only a refusal holds an account.
/// </remarks>
public sealed class RefusedByTheHarnessTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 7, 14, 0, 0, TimeSpan.FromMinutes(345));

    /// <summary>AGT3b's measured line (Claude Code 2.1.280, an invalid key).</summary>
    private const string Measured = "Failed to authenticate. API Error: 401 API key is invalid.";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-refused-own-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    public RefusedByTheHarnessTests()
    {
        Directory.CreateDirectory(Path.Combine(_home, "sessions"));
        File.WriteAllText(Command, "");
    }

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => Path.Combine(_home, "harnesses.json");

    /// <summary>A file that is there, so the agent is present by a look and nothing runs.</summary>
    private string Command => Path.Combine(_home, "agent-here");

    private string Transcript => Path.Combine(_home, "sessions", "s1.log");

    /// <summary>An agent declaring Claude Code's refused words, on the door a test names, present by a look.</summary>
    private sealed class Adapter(SessionWire wire, bool structured, HarnessToolchain toolchain) : ISessionAdapter
    {
        public string Name => "fake";

        public SessionWire Wire => wire;

        public HarnessToolchain? Toolchain => toolchain;

        public IStreamMapper? StructuredOutput() => structured ? new ClaudeStreamJson() : null;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    private HarnessRoster Roster(SessionWire wire = SessionWire.Pipe, bool structured = true) =>
        new(new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = new Adapter(wire, structured, new HarnessToolchain(
                Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true,
                Refused: "API Error: 401")),
        }), Settings)
        {
            Clock = () => Seen,
            Zone = Zone,
        };

    private Daoris.Driver.Driver Driver(ServiceClient service, HarnessRoster roster) =>
        new(service, DriverConfig.Empty with { Adapter = "fake" }, roster.Adapters, _home, harnesses: roster);

    private static DriverConfig Config => DriverConfig.Empty with { Adapter = "fake" };

    private void Account()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", "account-1"));
        new HarnessSettings().WithDefault("fake", "account-1").Save(Settings);
    }

    private const string Init = """{"type":"system","subtype":"init","cwd":"D:/fam/engine","session_id":"c1","tools":["Read"],"model":"m","permissionMode":"acceptEdits","claude_code_version":"2.1.281"}""";

    /// <summary>The agent's own words, quoting the line a test it ran expects: an error path, written by the agent.</summary>
    private const string Quoting = $$"""{"type":"assistant","message":{"id":"msg_1","content":[{"type":"text","text":"The error path now prints the provider's line, as the test expects:\n{{Measured}}"}]},"session_id":"c1"}""";

    /// <summary>The harness's own ending: the turn ended, and nothing failed.</summary>
    private const string Ended = """{"type":"result","subtype":"success","is_error":false,"result":"done","num_turns":1}""";

    /// <summary>The harness's own ending, failed, its words the measured line (the SDK's shape for a turn an API error ended).</summary>
    private const string Refused = $$"""{"type":"result","subtype":"success","is_error":true,"api_error_status":401,"result":"{{Measured}}","num_turns":1}""";

    /// <summary>A native run through the real capture, the harness's own words kept apart, ended with <paramref name="exit"/>.</summary>
    private async Task<HarnessEnding> NativeAsync(int exit, string stderr, params string[] stdout)
    {
        var own = new HarnessWords();
        await Daoris.Driver.Driver.CaptureStructuredAsync(
            new StringReader(string.Join('\n', stdout)), new StringReader(stderr), Transcript, "s1", output: null,
            events: null, new ClaudeStreamJson(), prompt: "test the error path", CancellationToken.None, own: own);
        return Daoris.Driver.Driver.Ending(protocol: false, structured: true, exit, turnFailed: null, own, Transcript);
    }

    /// <summary>The conclusion of a run that never touched its quest, read for a refused credential.</summary>
    private SessionConclusion Concluded(ServiceClient service, HarnessRoster roster, HarnessEnding ended) =>
        Driver(service, roster).AccountRefused(
            Observation.Conclude(ended.Exit ?? 0, "Open"), roster.Adapters.Resolve("fake"), new HarnessSelection(null, "account-1"), ended);

    private async Task HoldsAsync(HarnessRoster roster, SessionConclusion conclusion)
    {
        Assert.Equal("account.refused", conclusion.Parts![^1].Code);
        Assert.Equal(new AccountRead(LoginState.Out, Seen), AccountReads.Of(_home, "fake").Accounts.GetValueOrDefault("account-1"));
        Assert.Equal(AccountReadiness.Refused, (await roster.SelectAsync("fake", Config, null, null)).NotReady);
    }

    private async Task HoldsNothingAsync(HarnessRoster roster, SessionConclusion conclusion)
    {
        Assert.DoesNotContain(conclusion.Parts!, part => part.Code == "account.refused");
        Assert.Empty(AccountReads.Of(_home, "fake").Accounts);
        var next = await roster.SelectAsync("fake", Config, null, null);
        Assert.Equal(((AccountReadiness?)null, "account-1"), (next.NotReady, next.Profile));
    }

    // ——— The native door: a failed `result`, outside its frames, its stderr; never its agent's words.

    /// <summary>The hazard D125 found: the agent's words end with the phrase, on a turn its harness ended normally.</summary>
    [Fact]
    public async Task An_agent_s_words_ending_with_the_refused_phrase_on_a_turn_that_ended_normally_hold_no_account()
    {
        using var service = _ledger.Client();
        Account();
        var roster = Roster();

        var ended = await NativeAsync(0, "", Init, Quoting, Ended);

        // The hazard's shape: the transcript's last lines carry the phrase, in the agent's words.
        Assert.Contains(File.ReadLines(Transcript).TakeLast(HarnessEnding.LinesKept), line => line.Contains("API Error: 401", StringComparison.Ordinal));
        var conclusion = Concluded(service, roster, ended);
        Assert.Equal(["ended.untouched"], NoteAssert.Codes(conclusion.Parts));
        await HoldsNothingAsync(roster, conclusion);
    }

    /// <summary>An exit that was not 0 for another reason does not make the agent's words the harness's.</summary>
    [Fact]
    public async Task The_agent_s_words_hold_no_account_on_a_run_that_failed_for_another_reason()
    {
        using var service = _ledger.Client();
        Account();
        var roster = Roster();

        var ended = await NativeAsync(1, "", Init, Quoting, Ended);

        await HoldsNothingAsync(roster, Concluded(service, roster, ended));
    }

    [Fact]
    public async Task A_failed_result_in_the_harness_s_words_holds_the_account()
    {
        using var service = _ledger.Client();
        Account();
        var roster = Roster();

        var ended = await NativeAsync(1, "", Init, Refused);

        Assert.Equal(Measured, ended.Failure);
        await HoldsAsync(roster, Concluded(service, roster, ended));
    }

    [Fact]
    public async Task The_harness_s_stderr_beside_a_failed_exit_holds_the_account()
    {
        using var service = _ledger.Client();
        Account();
        var roster = Roster();

        var ended = await NativeAsync(1, $"retrying…\n{Measured}", Init);

        Assert.Equal(["retrying…", Measured], ended.Lines);
        await HoldsAsync(roster, Concluded(service, roster, ended));
    }

    [Fact]
    public async Task What_the_harness_printed_outside_its_frames_beside_a_failed_exit_holds_the_account()
    {
        using var service = _ledger.Client();
        Account();
        var roster = Roster();

        var ended = await NativeAsync(1, "", Measured);

        Assert.Equal([Measured], ended.Lines);
        await HoldsAsync(roster, Concluded(service, roster, ended));
    }

    /// <summary>A run that goes on with held words (MSG1b) begins the harness's words again, so the last run's ending is read.</summary>
    [Fact]
    public void Each_run_begins_the_harness_s_words_again()
    {
        var own = new HarnessWords();
        own.Said(Measured);
        own.Failed(Measured);

        own.Begin();

        var ended = own.Ended(0);
        Assert.Equal(((string?)null, 0), (ended.Failure, ended.Lines.Count));
    }

    /// <summary>Only the last of the harness's lines are kept, where a tool says why it gave up.</summary>
    [Fact]
    public void The_harness_s_last_lines_are_kept()
    {
        var own = new HarnessWords();
        for (var line = 0; line < HarnessEnding.LinesKept + 5; line++) own.Said($"line {line}");

        var kept = own.Ended(1).Lines;
        Assert.Equal(HarnessEnding.LinesKept, kept.Count);
        Assert.Equal(("line 5", $"line {HarnessEnding.LinesKept + 4}"), (kept[0], kept[^1]));
    }

    // ——— The protocol door: its failure.

    [Fact]
    public async Task On_the_protocol_door_only_the_door_s_failure_holds_the_account()
    {
        using var service = _ledger.Client();
        Account();
        var roster = Roster(SessionWire.Acp, structured: false);
        File.WriteAllText(Transcript, $"The error path now prints the provider's line:\n{Measured}\n");

        var quoted = Daoris.Driver.Driver.Ending(protocol: true, structured: false, 0, turnFailed: null, new HarnessWords(), Transcript);
        await HoldsNothingAsync(roster, Concluded(service, roster, quoted));

        var refused = Daoris.Driver.Driver.Ending(
            protocol: true, structured: false, 0, turnFailed: $"the ACP agent refused the call: {Measured}", new HarnessWords(), Transcript);
        await HoldsAsync(roster, Concluded(service, roster, refused));
    }

    // ——— A door that carries only text: its transcript is all it gives, read only beside an exit that was not 0.

    [Fact]
    public async Task On_a_door_that_carries_only_text_an_exit_of_0_holds_nothing_and_a_failed_exit_holds_the_account()
    {
        using var service = _ledger.Client();
        Account();
        var roster = Roster(SessionWire.Pipe, structured: false);
        File.WriteAllText(Transcript, $"working on it\n{Measured}\n");

        var clean = Daoris.Driver.Driver.Ending(protocol: false, structured: false, 0, turnFailed: null, new HarnessWords(), Transcript);
        await HoldsNothingAsync(roster, Concluded(service, roster, clean));

        // The family rehearsal's refused stub: the measured line on its stdout, then exit 1.
        var failed = Daoris.Driver.Driver.Ending(protocol: false, structured: false, 1, turnFailed: null, new HarnessWords(), Transcript);
        Assert.Equal(["working on it", Measured], failed.Lines);
        await HoldsAsync(roster, Concluded(service, roster, failed));
    }
}
