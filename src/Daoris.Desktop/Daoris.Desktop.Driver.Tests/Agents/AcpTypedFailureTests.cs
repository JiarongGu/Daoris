using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CODEXSTATUS1 (D210, the agent-errors design §4.3): <c>codex-acp</c> marks a terminal failure only for a client that asked for AIR's
/// typed session failures, and then answers the prompt <c>end_turn</c> with <c>_meta.jetbrains.air.sessionFailure</c>. Against a fake
/// agent over in-memory streams, shaped as <c>codex-acp</c> 2.1.1 sends it (<c>index.js</c>, <c>createSessionFailureMeta</c>), on a clock
/// the test moves: no process and no model, so the fast half. What the agent writes as its own message is never a failure.
/// </summary>
public sealed class AcpTypedFailureTests
{
    private const string Capacity = "Selected model is at capacity. Please try a different model.";

    private static readonly AdapterSet Built = AdapterSet.Built();

    /// <summary>The terminal answer <c>codex-acp</c> sends a typed client: <c>end_turn</c>, the failure in <c>_meta</c> (source-derived).</summary>
    private static string Typed(JsonElement request, string category, string title, string severity = "error") =>
        AcpTransientTests.Ok(
            request,
            JsonSerializer.Serialize(new
            {
                stopReason = "end_turn",
                _meta = new
                {
                    jetbrains = new
                    {
                        air = new
                        {
                            version = 1,
                            sessionFailure = new { id = "turn-1:error", revision = 1, category, severity, title, actions = new[] { "retry" } },
                        },
                    },
                },
            }));

    private static TransientRetry Retry(MovedClock clock, AcpTransientTests.Told told) => new()
    {
        Read = refused => AgentErrors.Read(
            Built, Built.Resolve("codex-acp"), DoorFailure.OfPrompt(refused), clock.GetUtcNow(), TimeZoneInfo.Utc),
        Clock = clock,
        Began = (_, next) => told.Add($"began:{(next - clock.GetUtcNow()).TotalMinutes:0}"),
        Tried = (_, number, next) => told.Add($"try:{number}:{(next is { } at ? (at - clock.GetUtcNow()).TotalMinutes.ToString("0") : "last")}"),
        Waits = waiting => told.Add(waiting ? "wait" : "waited"),
    };

    /// <summary>The air capabilities the client's <c>initialize</c> asked for.</summary>
    private static string[] Asked(AcpTransientTests.Agent agent)
    {
        string first;
        lock (agent.Sent) first = agent.Sent[0];
        return [.. JsonDocument.Parse(first).RootElement.GetProperty("params").GetProperty("clientCapabilities")
            .GetProperty("_meta").GetProperty("jetbrains").GetProperty("air").GetProperty("capabilities")
            .EnumerateArray().Select(each => each.GetString()!)];
    }

    /// <summary>The typed marker is asked for by a door whose toolchain declares it, and by no other (the design §10).</summary>
    [Fact]
    public async Task The_typed_marker_is_asked_for_only_where_the_door_is_declared_to_need_it()
    {
        var plain = AcpTransientTests.Scripted((_, frame, _) => AcpTransientTests.Answer(frame, "end_turn"));
        await new AcpSession(plain.Incoming, plain.Outgoing, _ => { }).RunAsync("D:/fam/Game", "serve the report", CancellationToken.None);
        var typed = AcpTransientTests.Scripted((_, frame, _) => AcpTransientTests.Answer(frame, "end_turn"));
        await new AcpSession(typed.Incoming, typed.Outgoing, _ => { }) { TypedFailures = true }
            .RunAsync("D:/fam/Game", "serve the report", CancellationToken.None);

        Assert.DoesNotContain("sessionFailure", Asked(plain));
        Assert.Contains("sessionFailure", Asked(typed));
        Assert.True(Built.Resolve("codex-acp").Toolchain!.ErrorWords!.TypedFailures);
        Assert.All(new[] { "claude-code-acp", "acp-stub", "dsh" }, name => Assert.NotEqual(true, Built.Resolve(name).Toolchain?.ErrorWords?.TypedFailures));
    }

    /// <summary>
    /// 🔴 The install's capacity failure, typed: the turn fails transient, so the run waits and tries again in the SAME session on D210's
    /// schedule, and the person's word said meanwhile is held and goes with the try, beside a go-on that carries the provider's words as
    /// they were written.
    /// </summary>
    [Fact]
    public async Task A_typed_capacity_failure_is_tried_again_in_the_same_session_with_the_person_s_word_and_the_provider_s_words()
    {
        var clock = new MovedClock();
        var told = new AcpTransientTests.Told();
        var agent = AcpTransientTests.Scripted((n, frame, _) =>
            n == 1 ? Typed(frame, "service", Capacity) : AcpTransientTests.Answer(frame, "end_turn"));
        var inbox = new DrivenInbox(_ => { });
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }) { TypedFailures = true };

        var run = session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None, inbox: inbox, retry: Retry(clock, told));
        await Poll.Until(() => session.WaitsOnTransient, () => "the run never waited", Poll.Patience);
        Assert.True(inbox.Hold(new ChatMessage("use port 9090", [])));
        var outcome = await run.WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal([["serve the report"], ["use port 9090", TransientRetry.GoOnText(Capacity)]], agent.Prompts);
        Assert.Contains(Capacity, TransientRetry.GoOnText(Capacity));
        Assert.Equal(["began:1", "wait", "waited", "try:1:2"], told.Lines);
    }

    /// <summary>Typed failures every try: the bound holds (five tries, 31 minutes of waits) and the run ends exhausted, as D210 says.</summary>
    [Fact]
    public async Task A_typed_failure_on_every_try_ends_past_the_bound()
    {
        var clock = new MovedClock();
        var told = new AcpTransientTests.Told();
        var agent = AcpTransientTests.Scripted((_, frame, _) => Typed(frame, "service", Capacity));
        var retry = Retry(clock, told);
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }) { TypedFailures = true };

        var run = session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None, retry: retry);
        await AcpTransientTests.PassAsync(told, clock, run, 1, 2, 4, 8, 16);
        var exhausted = await Assert.ThrowsAsync<AcpTransientExhausted>(() => run.WaitAsync(Poll.Patience));

        Assert.Equal((5, TimeSpan.FromMinutes(31), true), (exhausted.Episode.Tries, exhausted.Episode.Waited, exhausted.Episode.Exhausted));
        Assert.Equal(6, agent.Prompts.Count);
    }

    /// <summary>
    /// The agent's own message with the same words, and a turn that ends clean, is no failure: only the typed marker is read, never what
    /// the agent wrote. A warning (<c>willRetry</c>'s) is no terminal failure either.
    /// </summary>
    [Fact]
    public async Task An_agent_message_with_the_same_words_and_a_warning_are_not_failures()
    {
        var clock = new MovedClock();
        var told = new AcpTransientTests.Told();
        var agent = AcpTransientTests.Scripted((n, frame, self) =>
        {
            self.Push(AcpTransientTests.Said(Capacity));
            return n == 1 ? AcpTransientTests.Answer(frame, "end_turn") : Typed(frame, "service", Capacity, severity: "warning");
        });
        var retry = Retry(clock, told);
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }) { TypedFailures = true };

        var outcome = await session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None, retry: retry);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Single(agent.Prompts);
        Assert.Null(retry.Episode);
        Assert.Empty(told.Lines);
    }

    /// <summary>The same typed answer to a client that did not ask is read as it always was: a clean turn (the marker is the asking's).</summary>
    [Fact]
    public async Task A_marker_is_read_only_by_a_client_that_asked_for_it()
    {
        var agent = AcpTransientTests.Scripted((_, frame, _) => Typed(frame, "service", Capacity));
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });

        var outcome = await session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None);

        Assert.Equal("end_turn", outcome.StopReason);
    }
}
