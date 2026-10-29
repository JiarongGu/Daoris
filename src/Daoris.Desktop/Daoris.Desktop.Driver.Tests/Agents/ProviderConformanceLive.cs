using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A stand-in's live cases (the agent-provider design §10): its doors' programs run as real model-free processes, through the door's own spawn
/// and today's transports, a protocol door's through <see cref="AcpSession"/> and a native one's through the plain capture. They run on the
/// registry's <c>StandIn</c> providers alone, in <see cref="ProviderConformanceProcessTests"/>.
/// </summary>
/// <remarks>
/// The program a case runs is a scenario it writes into its scratch, in the stand-in's vocabulary, which is Claude Code's and Codex's by
/// reference: until part 3 lays a stand-in's program out in its folder, nothing here is that program, and nothing names a stand-in by its id.
/// </remarks>
internal static partial class ConformanceCases
{
    /// <summary>How long a live case's process is waited on: a generous bound, since the Process half runs several classes at once.</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(90);

    private static IEnumerable<ConformanceCase> LiveCases =>
    [
        Live("live.start", "a start: the door's program started by its spawn and read by its transport", LiveStart),
        Live("live.turn", "a turn: the program's words and its turn's end read", LiveTurn),
        Live("live.resume-own", "a resume of its own conversation; a door that resumes nothing answers the unsupported resume's fallback", LiveResumeOwn),
        Live("live.missing-conversation", "a missing conversation refused at the open, nothing prompted", LiveMissing, ResumesOnTheWire),
        Live("live.other-writer", "another active writer refused at the open, nothing prompted", LiveOtherWriter, ResumesOnTheWire),
        Live("live.word-before-wire", "a word attempted before the wire and acknowledged after it, never replayed once uncertain",
            _ => Pending("DoorInput with Course's attempt port, a word's attempt and acknowledgement read around the wire")),
        Live("live.limit-cools", "a limit cooling the bound account with no strike",
            _ => Pending("the one failure channel a limit cools the bound account through")),
        Live("live.transient-retried", "a transient error tried again in the same conversation within D210's bounds", LiveTransient, Protocol),
        Live("live.access-and-fatal", "access and fatal on their paths: each read as its kind and ended, never tried again", LiveAccessAndFatal),
        Live("live.child-seen-ended", "a child seen and ended, and duplicate child signals merged",
            _ => Pending("the provider's IChildVocabulary, its children's readings reaching the lineage")),
        Live("live.unknown-parent-end", "an unknown parent and an unknown end kept unknown",
            _ => Pending("the provider's IChildVocabulary, saying whether it says a child's end")),
        Live("live.usage-null-computed", "usage with a null count where none was said, and computed context marked computed",
            _ => Pending("the readings' usage, a count said apart from one computed")),
        Live("live.reported-model", "a reported model distinct from the one asked",
            _ => Pending("the model asked, carried, acknowledged and reported kept apart")),
        Live("live.refused-model", "a refused model passing its account first, then the choice: on the protocol door before any prompt, on the native one at the next start (O2-F11)",
            _ => Pending("the offers store keeping a refused model by door, version and account")),
        Live("live.reask-later-choice", "the open's re-ask answered through IChoiceReask, a later choice carried on the same session and the refused attempt's changes set back (O4-F5)",
            _ => Pending("IChoiceReask, the open's re-ask"), Protocol),
        Live("live.reask-none", "the open's re-ask answered none, the open ending refused as today",
            _ => Pending("IChoiceReask, the open's re-ask"), Protocol),
        Live("live.held-request", "a request held for the person answered through HeldPermissions, the response written once with their answer (O3-F5)",
            _ => Pending("HeldPermissions as the transport's two-way port"), Protocol),
        Live("live.sign-in", "a sign-in through the roster's sign-in and login question", LiveSignIn),
        Live("live.pin-controlled", "a pin from controlled infrastructure",
            _ => Pending("each provider's program as Install's ToolSpec, its release source a controlled fixture")),
        Live("live.listing-configured", "a listing with a command configured, and none without one", LiveListing),
    ];

    private static ConformanceCase Live(string name, string says, Func<ConformanceRun, Task> body, Func<AgentDoor, bool>? applies = null) =>
        new(name, ConformanceHalf.Live, ConformanceScope.Door, says,
            (_, provider, door) => door is not null && provider.Identity.StandIn && (applies?.Invoke(door) ?? true), body);

    private static Task Pending(string reader) => Task.FromException(new NotAnsweredYet(reader));

    private static bool ResumesOnTheWire(AgentDoor door) => Protocol(door) && door.Abilities.Resumes is { IsKnown: true, Value: true };

    // ---- The scenarios, in the stand-in's vocabulary ----

    /// <summary>
    /// A protocol agent: it keeps each method it was sent in a file, answers the spec's calls, and plays the scenario its second argument
    /// names on a resume or a prompt.
    /// </summary>
    private const string ProtocolAgent = """
        import { appendFileSync } from 'node:fs';
        import { createInterface } from 'node:readline';
        const [seen, scenario] = process.argv.slice(2);
        let prompts = 0;
        const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
        createInterface({ input: process.stdin }).on('line', (line) => {
          const frame = JSON.parse(line);
          if (!frame.method) return;
          appendFileSync(seen, frame.method + '\n');
          const answer = (result) => frame.id === undefined ? 0 : send({ jsonrpc: '2.0', id: frame.id, result });
          const refuse = (error) => send({ jsonrpc: '2.0', id: frame.id, error });
          if (frame.method === 'initialize') answer({ protocolVersion: 1, agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } });
          else if (frame.method === 'session/new') answer({ sessionId: 's-1' });
          else if (frame.method === 'session/resume' && scenario === 'missing') refuse({ code: -32002, message: 'Resource not found' });
          else if (frame.method === 'session/resume' && scenario === 'elsewhere') refuse({ code: -32603, message: 'Internal error: another client holds the thread', data: { reason: 'thread_active_writer' } });
          else if (frame.method === 'session/prompt') {
            prompts += 1;
            if (scenario === 'transient' && prompts === 1) return refuse({ code: -32603, message: 'Internal error: API Error: 529 Overloaded.', data: { errorKind: 'overloaded' } });
            if (scenario === 'access') return refuse({ code: -32603, message: 'Internal error: API Error: 401 API key is invalid.' });
            if (scenario === 'fatal') return refuse({ code: -32000, message: 'the turn failed' });
            send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: frame.params.sessionId, update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'carrying on' } } } });
            answer({ stopReason: 'end_turn' });
          } else answer({});
        }).on('close', () => process.exit(0));
        """;

    /// <summary>A native agent of plain lines: a start says what it was handed, a conversation repeats what it hears, and it answers the sign-in.</summary>
    private const string TextAgent = """
        import { createInterface } from 'node:readline';
        const args = process.argv.slice(2);
        if (args.includes('--version')) { console.log('stand-in 0.0.1'); process.exit(0); }
        if (args.includes('--login-state')) { console.log('logged-in as alice@example.org'); console.log('org: Acme'); console.log('plan: max'); process.exit(0); }
        if (args.includes('--login')) { console.log('signed in'); process.exit(0); }
        if (args[0] === 'start') { console.log('started on ' + (process.env.DAORIS_REPOSITORY ?? 'nothing') + ' with a target of ' + ((process.env.DAORIS_TARGET ?? '').length > 0 ? 'words' : 'none')); process.exit(0); }
        if (args[0] === 'resume') { console.log('resumed with ' + args.slice(1).join(' ')); process.exit(0); }
        const lines = createInterface({ input: process.stdin });
        lines.on('line', (line) => console.log('heard: ' + line));
        lines.on('close', () => process.exit(0));
        """;

    private static string Script(ConformanceRun run, string name, string text)
    {
        var path = Path.Combine(run.Scratch, name);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>What a protocol run gave: its outcome or its failure, the methods the agent was sent, and the lines the session said.</summary>
    private sealed record ProtocolRan(AcpOutcome? Outcome, Exception? Failed, IReadOnlyList<string> Methods, IReadOnlyList<string> Lines);

    /// <summary>The door's program started by the door's own spawn, and a run of the protocol transport over it.</summary>
    private static async Task<ProtocolRan> OverTheWire(
        ConformanceRun run, string scenario, string? resume = null, TransientRetry? retry = null, Func<Task>? meanwhile = null)
    {
        var agent = Script(run, "protocol-agent.mjs", ProtocolAgent);
        var seen = Path.Combine(run.Scratch, $"seen-{scenario}.txt");
        using var process = Process.Start(run.Adapter.Prepare(run.Target(), ["node", agent, seen, scenario]))!;
        var lines = new List<string>();
        var session = new AcpSession(process.StandardOutput, process.StandardInput, line =>
        {
            lock (lines) lines.Add(line);
        });

        AcpOutcome? outcome = null;
        Exception? failed = null;
        try
        {
            var running = session.RunAsync(run.Root(), "Go on.", CancellationToken.None, resume: resume, retry: retry);
            if (meanwhile is not null) await meanwhile();
            outcome = await running.WaitAsync(Bound);
        }
        catch (Exception error) when (error is not TimeoutException)
        {
            failed = error;
        }
        finally
        {
            try
            {
                process.StandardInput.Close();
            }
            catch (IOException)
            {
            }

            if (!process.WaitForExit(10_000)) process.Kill(entireProcessTree: true);
        }

        lock (lines) return new(outcome, failed, File.Exists(seen) ? [.. File.ReadAllLines(seen)] : [], [.. lines]);
    }

    private static async Task LiveStart(ConformanceRun run)
    {
        if (Protocol(run.TheDoor))
        {
            var ran = await OverTheWire(run,"open");
            Assert.Null(ran.Failed);
            Assert.Equal("s-1", ran.Outcome!.SessionId);
            Assert.Equal(["initialize", "session/new", "session/prompt", "session/close"], ran.Methods);
            return;
        }

        // The plain capture: what the program says reaches its transcript, and its end is its exit.
        var agent = Script(run, "text-agent.mjs", TextAgent);
        using var process = Process.Start(run.Adapter.Prepare(run.Target(), ["node", agent, "start"]))!;
        var transcript = Path.Combine(run.Scratch, "transcript.txt");
        await HarnessCapture.CaptureAsync(process, transcript, "s-1", null, CancellationToken.None).WaitAsync(Bound);
        await process.WaitForExitAsync().WaitAsync(Bound);

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("started on engine with a target of words", File.ReadAllText(transcript));
    }

    private static async Task LiveTurn(ConformanceRun run)
    {
        if (Protocol(run.TheDoor))
        {
            var ran = await OverTheWire(run,"open");
            Assert.Null(ran.Failed);
            Assert.Equal("end_turn", ran.Outcome!.StopReason);
            Assert.Contains(ran.Lines, line => line.Contains("carrying on", StringComparison.Ordinal));
            return;
        }

        // A conversation's turn: the person's words framed as the door frames them, the program's answer read.
        var agent = Script(run, "text-agent.mjs", TextAgent);
        using var process = Process.Start(run.Adapter.PrepareChat(run.Chat(), ["node", agent, "chat"]))!;
        await process.StandardInput.WriteLineAsync(run.Adapter.FrameMessage("hello"));
        process.StandardInput.Close();
        var said = await process.StandardOutput.ReadToEndAsync().WaitAsync(Bound);
        await process.WaitForExitAsync().WaitAsync(Bound);

        Assert.Contains("heard: hello", said);
        Assert.Equal(0, process.ExitCode);
    }

    private static async Task LiveResumeOwn(ConformanceRun run)
    {
        var (door, adapter) = (run.TheDoor, run.Adapter);
        if (Protocol(door))
        {
            var ran = await OverTheWire(run,"open", resume: "s-0");
            Assert.Null(ran.Failed);
            Assert.Equal("s-0", ran.Outcome!.SessionId);
            Assert.Equal(["initialize", "session/resume", "session/prompt", "session/close"], ran.Methods);
            return;
        }

        var agent = Script(run, "text-agent.mjs", TextAgent);
        var resumed = adapter.PrepareResume(run.Target(), ["node", agent, "resume"], "c-1", "go on");
        if (door.Abilities.Resumes is not { IsKnown: true, Value: true })
        {
            // The unsupported resume's fallback, answered as today: nothing to resume with, so the continuation hands the brief.
            Assert.False(adapter.Resumes);
            Assert.Null(resumed);
            return;
        }

        using var process = Process.Start(resumed!)!;
        var said = await process.StandardOutput.ReadToEndAsync().WaitAsync(Bound);
        await process.WaitForExitAsync().WaitAsync(Bound);
        Assert.Contains("c-1", said);
    }

    private static async Task LiveMissing(ConformanceRun run)
    {
        var ran = await OverTheWire(run,"missing", resume: "s-gone");

        Assert.Equal(ContinueWhy.Gone, Assert.IsType<AcpResumeRefused>(ran.Failed).Why.Code);
        Assert.DoesNotContain("session/prompt", ran.Methods);
    }

    private static async Task LiveOtherWriter(ConformanceRun run)
    {
        var ran = await OverTheWire(run,"elsewhere", resume: "s-held");

        Assert.Equal(ContinueWhy.Elsewhere, Assert.IsType<AcpResumeRefused>(ran.Failed).Why.Code);
        Assert.DoesNotContain("session/prompt", ran.Methods);
    }

    private static async Task LiveTransient(ConformanceRun run)
    {
        var clock = new MovedClock();
        var waits = 0;
        var retry = new TransientRetry
        {
            Read = refused => AgentErrors.Read(run.Subject.Adapters, run.Adapter, DoorFailure.OfPrompt(refused), clock.GetUtcNow(), TimeZoneInfo.Utc),
            Clock = clock,
            Waits = waiting =>
            {
                if (waiting) Interlocked.Increment(ref waits);
            },
        };

        var ran = await OverTheWire(run,"transient", retry: retry, meanwhile: async () =>
        {
            await Poll.Until(() => Volatile.Read(ref waits) >= 1, () => "the run never waited to try again", Bound);
            clock.Advance(TransientRetry.Delays[0]);
        });

        Assert.Null(ran.Failed);
        Assert.Equal("end_turn", ran.Outcome!.StopReason);
        // Tried again in the same conversation, once: no second session/new.
        Assert.Equal(["initialize", "session/new", "session/prompt", "session/prompt", "session/close"], ran.Methods);
        Assert.Equal(1, retry.Episode!.Tries);
    }

    private static async Task LiveAccessAndFatal(ConformanceRun run)
    {
        if (!Protocol(run.TheDoor)) await Pending("the native door's failure read on the one failure channel, its harness's ending as readings");

        foreach (var (scenario, kind) in new[] { ("access", AgentErrorKind.Access), ("fatal", AgentErrorKind.Fatal) })
        {
            var read = (AgentError?)null;
            var retry = new TransientRetry
            {
                Read = refused => read = AgentErrors.Read(run.Subject.Adapters, run.Adapter, DoorFailure.OfPrompt(refused), DateTimeOffset.UtcNow, TimeZoneInfo.Utc),
            };

            var ran = await OverTheWire(run,scenario, retry: retry);

            Assert.NotNull(ran.Failed);
            Assert.Equal(kind, read?.Kind);
            Assert.Single(ran.Methods, method => method == "session/prompt");
        }
    }

    private static async Task LiveSignIn(ConformanceRun run)
    {
        var agent = Script(run, "text-agent.mjs", TextAgent);
        var home = run.Home();
        IReadOnlyList<string> command = ["node", agent];
        var config = DriverConfig.Empty with
        {
            Commands = run.Provider.Doors.ToDictionary(door => door.Id.Value, _ => command, StringComparer.OrdinalIgnoreCase),
        };
        var roster = new HarnessRoster(run.Subject.Adapters, DaorisHome.Entry(home, HomeEntry.Harnesses));
        var door = run.TheDoor.Id.Value;
        var toolchain = roster.AccountToolchain(door);
        var signIn = roster.SignInCommand(door, config);
        Assert.NotNull(toolchain);
        Assert.NotNull(signIn);

        var profile = HarnessSettings.ProfileHome(home, run.Registry.OwnerOf(run.TheDoor.Id).Value, "account-1");
        var said = new List<string>();
        Assert.Equal(0, await HarnessActions.LoginAsync(toolchain, signIn, profile, said.Add, fresh: true).WaitAsync(Bound));
        var read = await roster.LoginOfAsync(door, config, "account-1").WaitAsync(Bound);

        Assert.Equal((LoginState.In, "alice@example.org", "Acme", "max"), (read.Login, read.Account, read.Org, read.Plan));
    }

    private static async Task LiveListing(ConformanceRun run)
    {
        var agent = Script(run, "text-agent.mjs", TextAgent);
        var door = run.TheDoor.Id.Value;
        // The stand-in's own doors, so the roster asks no other agent's program on this machine.
        var standIn = new AdapterSet(run.Provider.Doors.ToDictionary(
            each => each.Id.Value, each => run.Subject.Adapters.Resolve(each.Id.Value), StringComparer.OrdinalIgnoreCase));
        var roster = new HarnessRoster(standIn, DaorisHome.Entry(run.Home(), HomeEntry.Harnesses));

        var none = await roster.RosterAsync(DriverConfig.Empty).WaitAsync(Bound);
        var configured = await roster.RosterAsync(DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { [door] = ["node", agent] },
        }).WaitAsync(Bound);

        Assert.DoesNotContain(none, report => report.Adapter == door);
        Assert.Single(configured, report => report.Adapter == door);
    }
}
