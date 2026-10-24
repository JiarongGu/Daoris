using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MODELROLE1c3 (D205 §3.3's pipe exception, §3.5(a)): the pipe door has no offer list and its prompt rides its arguments, so a model it
/// refuses is read only from row a's measured words, the <c>assistant</c> frame's <c>error: "model_not_found"</c> on a turn with no output
/// and no tool use (<c>docs/2026-10-23-model-effort-doors-evidence.md</c>). Such a run's account keeps a <c>refused</c> reading for that
/// model, and its part ends with <c>account.refused-model</c>, which no strike counts; on a go-on run its words wait for the next look, whose
/// resume judges the reading. Never from words its agent wrote.
/// </summary>
/// <remarks>Nothing here starts a process: the native door's output is fed through the real capture from text.</remarks>
public sealed class PipeModelRefusalTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-pipe-refusal-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    public PipeModelRefusalTests()
    {
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));
    }

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Transcript => DaorisHome.Entry(_home, HomeEntry.Sessions, "s1.log");

    private const string Init = """{"type":"system","subtype":"init","cwd":"D:/fam/engine","session_id":"c1","tools":["Read"],"model":"opus","permissionMode":"acceptEdits","claude_code_version":"2.1.281"}""";

    /// <summary>Row a's measured refusal: the tool's own message, as <c>&lt;synthetic&gt;</c>, with the frame's error word.</summary>
    private const string Refusal = """{"type":"assistant","message":{"id":"msg_0","model":"<synthetic>","content":[{"type":"text","text":"There's an issue with the selected model (opus). It may not exist or you may not have access to it. Run --model to pick a different model."}]},"error":"model_not_found","session_id":"c1"}""";

    private const string RefusedResult = """{"type":"result","subtype":"success","is_error":true,"api_error_status":404,"num_turns":1,"modelUsage":{},"result":"There's an issue with the selected model (opus). It may not exist or you may not have access to it. Run --model to pick a different model."}""";

    /// <summary>A model that answered: words of its own on the same turn.</summary>
    private const string Spoke = """{"type":"assistant","message":{"id":"msg_1","model":"claude-opus-5-5","content":[{"type":"text","text":"Reading the gate first."}]},"session_id":"c1"}""";

    private const string Called = """{"type":"assistant","message":{"id":"msg_2","model":"claude-opus-5-5","content":[{"type":"tool_use","id":"t1","name":"Read","input":{"file_path":"README.md"}}]},"session_id":"c1"}""";

    /// <summary>The agent's own words quoting the error word, on a turn its harness ended normally.</summary>
    private const string Quoting = """{"type":"assistant","message":{"id":"msg_3","model":"claude-opus-5-5","content":[{"type":"text","text":"The stub now answers \"error\":\"model_not_found\" as the test expects."}]},"session_id":"c1"}""";

    private const string Ended = """{"type":"result","subtype":"success","is_error":false,"result":"done","num_turns":1}""";

    private async Task<HarnessEnding> NativeAsync(int exit, params string[] stdout)
    {
        var own = new HarnessWords();
        await HarnessCapture.CaptureStructuredAsync(
            new StringReader(string.Join('\n', stdout)), new StringReader(""), Transcript, "s1", output: null,
            events: null, new ClaudeStreamJson(), prompt: "fix the gate", CancellationToken.None, own: own);
        return Daoris.Driver.Driver.Ending(protocol: false, structured: true, exit, turnFailed: null, own, Transcript);
    }

    private sealed class Adapter : ISessionAdapter
    {
        public string Name => "fake";

        public IStreamMapper? StructuredOutput() => new ClaudeStreamJson();

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    private Daoris.Driver.Driver Driver(ServiceClient service)
    {
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase) { ["fake"] = new Adapter() });
        return new(service, DriverConfig.Empty with { Adapter = "fake" }, adapters, _home,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    private static readonly HarnessSelection OnAccount1 = new(null, "account-1", Version: "2.1.281");

    /// <summary>The measured frames on a silent turn read as the model refused, never as an ordinary failure.</summary>
    [Fact]
    public async Task The_measured_refusal_on_a_silent_turn_reads_as_the_model_refused()
    {
        var ended = await NativeAsync(1, Init, Refusal, RefusedResult);

        Assert.True(ended.ModelRefused);
        Assert.True(ended.Failed);
    }

    /// <summary>A turn with words of a model's own, or a tool call, ran: its refusal word is not the door's model refusal (§3.3).</summary>
    [Theory]
    [InlineData(Spoke)]
    [InlineData(Called)]
    public async Task A_turn_with_output_or_a_tool_call_is_no_model_refusal(string ran)
    {
        var ended = await NativeAsync(1, Init, ran, Refusal, RefusedResult);

        Assert.False(ended.ModelRefused);
    }

    /// <summary>The agent's words quoting the error word, on a turn its harness ended normally, are its words (D125 point 1.4).</summary>
    [Fact]
    public async Task The_agent_s_words_quoting_the_error_word_are_no_refusal()
    {
        var ended = await NativeAsync(0, Init, Quoting, Ended);

        Assert.False(ended.ModelRefused);
    }

    /// <summary>A limit is told apart the same way, by its own word: it is no model refusal.</summary>
    [Fact]
    public async Task A_limit_on_a_silent_turn_is_no_model_refusal()
    {
        const string Limit = """{"type":"assistant","message":{"id":"msg_0","model":"<synthetic>","content":[{"type":"text","text":"You've hit your weekly limit · resets 4pm"}]},"error":"rate_limit","session_id":"c1"}""";
        const string LimitResult = """{"type":"result","subtype":"success","is_error":true,"api_error_status":429,"num_turns":1,"result":"You've hit your weekly limit · resets 4pm"}""";

        var ended = await NativeAsync(1, Init, Limit, LimitResult);

        Assert.False(ended.ModelRefused);
    }

    /// <summary>
    /// 🔴 The part ends with the account's line, no strike's: the reading is kept for the model asked, so the walk passes that account for it
    /// at the next start, and the line names the agent and the model but no account, since the note travels.
    /// </summary>
    [Fact]
    public async Task A_refused_run_keeps_the_account_s_reading_and_ends_with_the_account_s_line()
    {
        using var service = _ledger.Client();
        var ended = await NativeAsync(1, Init, Refusal, RefusedResult);

        var conclusion = Driver(service).ModelRefused(
            Observation.Conclude(1, "Open"), "fake", OnAccount1, "opus", ended, "s1");

        Assert.Equal("failed", conclusion.State);
        Assert.Equal(["ended.untouched-exit", "account.refused-model"], NoteAssert.Codes(conclusion.Parts));
        var line = conclusion.Parts![^1];
        Assert.Equal(("fake", "opus"), (line.Value("agent")?.ToString(), line.Value("model")?.ToString()));
        Assert.DoesNotContain("account-1", conclusion.Note, StringComparison.Ordinal);
        Assert.NotNull(AgentOffers.Judge(AgentOffers.Read(_home), "fake", "2.1.281", "account-1", "opus", DateTimeOffset.UtcNow));
        // Only that model, on that account.
        Assert.Null(AgentOffers.Judge(AgentOffers.Read(_home), "fake", "2.1.281", "account-1", "sonnet", DateTimeOffset.UtcNow));
        Assert.Null(AgentOffers.Judge(AgentOffers.Read(_home), "fake", "2.1.281", "account-2", "opus", DateTimeOffset.UtcNow));
    }

    /// <summary>A run that was not refused, or that asked no model of its actor's, keeps its conclusion and writes no reading.</summary>
    [Fact]
    public async Task A_run_not_refused_or_asking_no_model_keeps_its_conclusion()
    {
        using var service = _ledger.Client();
        var failed = await NativeAsync(1, Init, Spoke, RefusedResult);
        var refused = await NativeAsync(1, Init, Refusal, RefusedResult);
        var driver = Driver(service);

        Assert.Equal(["ended.untouched-exit"], NoteAssert.Codes(driver.ModelRefused(Observation.Conclude(1, "Open"), "fake", OnAccount1, "opus", failed, "s1").Parts));
        Assert.Equal(["ended.untouched-exit"], NoteAssert.Codes(driver.ModelRefused(Observation.Conclude(1, "Open"), "fake", OnAccount1, null, refused, "s1").Parts));
        Assert.Empty(AgentOffers.Read(_home));
    }

    /// <summary>
    /// A go-on run refused so (§3.5(a)): its words wait for the next look, whose resume judges the reading, and never wait for the person
    /// as a second failure's do, since the refusal is the account's and the next run goes on another.
    /// </summary>
    [Fact]
    public void A_go_on_run_s_words_wait_for_the_next_look_after_a_model_refusal_even_after_a_failure_before()
    {
        var refused = Observation.Conclude(1, "Taken", resumed: true)
            .Then(" ", Noted.Of(NoteCodes.AccountRefusedModel, "`fake` refused `opus` on the account it ran on.", ("agent", "fake"), ("model", "opus")));
        var waitedBefore = new PriorSession("s1", null, "failed") { NoteParts = [new NotePart { Code = NoteCodes.EndedWordsWait.Code }] };
        var ended = new HarnessEnding(1, "There's an issue with the selected model (opus).", []) { ModelRefused = true };

        Assert.Equal(WordsAfter.Wait, FailedTurnWords.Judge(refused, null, ended, waitedBefore));
        // An ordinary second failure still waits for the person.
        Assert.Equal(WordsAfter.Held, FailedTurnWords.Judge(Observation.Conclude(1, "Taken", resumed: true), null, ended with { ModelRefused = false }, waitedBefore));
    }
}
