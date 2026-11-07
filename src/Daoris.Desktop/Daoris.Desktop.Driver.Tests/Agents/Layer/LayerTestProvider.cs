using System.Runtime.CompilerServices;
using System.Text.Json;
using Daoris.Driver;
using Lyntai.Agents;
using Lyntai.Inference;
using Lyntai.Processes;
using Microsoft.Extensions.DependencyInjection;
using LayerDoor = Lyntai.Agents.AgentDoor;
using LayerPlanEntry = Lyntai.Agents.PlanEntry;
using SessionEnded = Lyntai.Agents.SessionEnded;
using SessionStarted = Lyntai.Agents.SessionStarted;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The tests' own provider (the agent-lyntai design §6.3): a layer provider no production door is, registered as a host registers an agent
/// of its own, whose one <c>cli</c> door runs a scenario program through its container's runner. Each stdout line that is a JSON object is
/// read in the scenario's own vocabulary into the layer's events; any other line is the runner's to keep. A failed exit after no terminal
/// is the turn's fatal terminal, as the layer's CLI loop makes one.
/// </summary>
internal static class LayerTestProvider
{
    public const string Id = "layer-stub";

    /// <summary>The provider as registered under <paramref name="id"/>, the Daoris door it is bound to.</summary>
    public static AgentProviderDescriptor Descriptor(string id = Id) => new()
    {
        Id = id,
        Doors =
        [
            new LayerDoor
            {
                Id = AgentDoorKinds.Cli,
                Kind = AgentDoorKinds.Cli,
                Abilities =
                [
                    new AgentAbility(AgentAbilities.Resume, AgentSupport.Supported, AgentAbilitySource.Declared) { Evidence = "the scenario" },
                    new AgentAbility(AgentAbilities.Model, AgentSupport.Unsupported, AgentAbilitySource.Declared),
                ],
            },
        ],
    };

    /// <summary>
    /// The provider's registration under <paramref name="door"/>, its program <paramref name="command"/>; <paramref name="plant"/>, where
    /// given, rewrites what its reader yields, a regression planted on the layer path alone for the oracle to catch (§6.3 point 5).
    /// </summary>
    public static LayerRegistration Registration(
        IReadOnlyList<string> command, string door = Id, Func<IReadOnlyList<AgentStreamEvent>, IEnumerable<AgentStreamEvent>>? plant = null)
    {
        var descriptor = Descriptor(door);
        return new(door, builder => builder.AddAgentProvider(
            descriptor, services => new AgentProvider(descriptor, _ => new Session(services.GetRequiredService<IProcessRunner>(), command, plant))))
        {
            ModelRefusals = new HashSet<string>(StringComparer.Ordinal) { RefusedBy },
        };
    }

    /// <summary>The entry the tests' door spells a refused model with.</summary>
    public const string RefusedBy = "tests:refused";

    /// <summary>A scenario's program in <paramref name="scratch"/>, saying what <paramref name="spec"/> says, as a command.</summary>
    /// <remarks>
    /// The spec's fields: <c>out</c>, lines said at once (a string as written, an object as its JSON); <c>err</c>, stderr lines first;
    /// <c>wait</c>, milliseconds before <c>after</c>'s lines; <c>hang</c>, never ending by itself; <c>echo</c>, its stdin said back;
    /// <c>argv</c>, its arguments after the spec said back; <c>env</c>, the variable it names said back; <c>exit</c>, its code. A resume's conversation is said first, as
    /// <c>resumed id</c>.
    /// </remarks>
    public static IReadOnlyList<string> Scenario(string scratch, object spec, string name = "scenario")
    {
        Directory.CreateDirectory(scratch);
        var program = Path.Combine(scratch, "scenario.mjs");
        if (!File.Exists(program)) File.WriteAllText(program, Program);
        var file = Path.Combine(scratch, name + ".json");
        File.WriteAllText(file, JsonSerializer.Serialize(spec));
        return ["node", program, file];
    }

    /// <summary>
    /// A production door's layer registration spawns one program with its own arguments: a shim in <paramref name="scratch"/> running
    /// <paramref name="command"/> (a scenario's) before them.
    /// </summary>
    public static string Shim(string scratch, IReadOnlyList<string> command, string name)
    {
        var quoted = string.Join(' ', command.Select(each => $"\"{each}\""));
        if (OperatingSystem.IsWindows())
        {
            var cmd = Path.Combine(scratch, name + ".cmd");
            File.WriteAllText(cmd, $"@{quoted} %*\r\n");
            return cmd;
        }

        var sh = Path.Combine(scratch, name + ".sh");
        File.WriteAllText(sh, $"#!/bin/sh\nexec {quoted} \"$@\"\n");
        File.SetUnixFileMode(sh, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return sh;
    }

    private const string Program = """
        import { readFileSync } from 'node:fs';
        const args = process.argv.slice(2);
        const spec = JSON.parse(readFileSync(args[0], 'utf8'));
        const say = (line) => console.log(typeof line === 'string' ? line : JSON.stringify(line));
        const resume = args.indexOf('--resume');
        let heard = '';
        process.stdin.setEncoding('utf8');
        process.stdin.on('data', (chunk) => { heard += chunk; });
        process.stdin.on('end', async () => {
          if (resume >= 0) say(`resumed ${args[resume + 1]}`);
          if (spec.argv) say(`argv ${JSON.stringify(args.slice(1))}`);
          if (spec.echo) say(`heard ${heard}`);
          if (spec.env) say(`env ${process.env[spec.env] ?? ''}`);
          for (const line of spec.err ?? []) console.error(line);
          for (const line of spec.out ?? []) say(line);
          if (spec.wait) await new Promise((done) => setTimeout(done, spec.wait));
          for (const line of spec.after ?? []) say(line);
          if (spec.hang) setInterval(() => {}, 1000);
          else process.exitCode = spec.exit ?? 0;
        });
        """;

    private sealed class Session(
        IProcessRunner runner, IReadOnlyList<string> command, Func<IReadOnlyList<AgentStreamEvent>, IEnumerable<AgentStreamEvent>>? plant)
        : IAgentSession
    {
        public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
            AgentSessionOptions options, [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (plant is null)
            {
                await foreach (var each in ReadAsync(options, ct).ConfigureAwait(false)) yield return each;
                yield break;
            }

            var read = new List<AgentStreamEvent>();
            await foreach (var each in ReadAsync(options, ct).ConfigureAwait(false)) read.Add(each);
            foreach (var each in plant(read)) yield return each;
        }

        private async IAsyncEnumerable<AgentStreamEvent> ReadAsync(AgentSessionOptions options, [EnumeratorCancellation] CancellationToken ct)
        {
            List<string> args = [.. command.Skip(1)];
            if (options.ResumeToken is { } token) args.AddRange(["--resume", token]);
            var lines = runner.StreamLinesAsync(
                command[0], args, stdin: options.Prompt, inactivityTimeout: TimeSpan.FromSeconds(options.TimeoutSeconds ?? 120),
                workingDirectory: options.WorkingDirectory, ct: ct).GetAsyncEnumerator(ct);
            SessionEnded? ended = null;
            SessionEnded? faulted = null;
            string? conversation = null;
            List<AgentShownKind> shown = [];
            await using (lines.ConfigureAwait(false))
            {
                while (true)
                {
                    string line;
                    try
                    {
                        if (!await lines.MoveNextAsync().ConfigureAwait(false)) break;
                        line = lines.Current;
                    }
                    catch (ProcessRunException fault)
                    {
                        faulted = new SessionEnded(ProviderVerdict.Failed, true, null, conversation, null, fault.Message)
                        {
                            Failure = new AgentFailure(AgentFailureKind.Fatal, "process.exit"),
                        };
                        break;
                    }

                    foreach (var each in Read(line))
                    {
                        if (each is SessionStarted started) conversation ??= started.SessionId;
                        if (Shown(each) is { } kind && !shown.Contains(kind)) shown.Add(kind);
                        if (each is SessionEnded terminal)
                        {
                            if (ended is not null) continue;
                            ended = terminal;
                            yield return Stamped(terminal, shown);
                            continue;
                        }

                        yield return each;
                    }
                }
            }

            if (ended is null)
            {
                yield return Stamped(faulted ?? new SessionEnded(
                    ProviderVerdict.Failed, true, null, conversation, null, "no output produced (no terminal result)")
                {
                    Failure = new AgentFailure(AgentFailureKind.Fatal, "no-terminal-event"),
                }, shown);
            }
        }

        /// <summary>The kind of event a turn showed, as the layer's loop counts one after the write (a start is none).</summary>
        private static AgentShownKind? Shown(AgentStreamEvent each) => each switch
        {
            TextDelta => AgentShownKind.Text,
            Thinking => AgentShownKind.Thinking,
            ToolCall => AgentShownKind.ToolCall,
            ToolResult => AgentShownKind.ToolResult,
            UsageLive or UsageFinal or UsageReading => AgentShownKind.Usage,
            PlanUpdated => AgentShownKind.Plan,
            _ => null,
        };

        /// <summary>The terminal with what the turn showed, and the layer's own delivery stamp, which counts a meter as the prompt taken.</summary>
        private static SessionEnded Stamped(SessionEnded terminal, List<AgentShownKind> shown)
        {
            var delivery = shown.Any(kind => kind is AgentShownKind.ToolCall or AgentShownKind.ToolResult) ? AgentDelivery.Uncertain
                : shown.Count > 0 ? AgentDelivery.Taken : AgentDelivery.NeverSent;
            return terminal with
            {
                Turn = new AgentTurnEvidence(null, [.. shown]),
                Failure = terminal.Failure is null ? null : new AgentFailure(terminal.Failure.Kind, terminal.Failure.By)
                {
                    Restriction = terminal.Failure.Restriction, Delivery = delivery,
                },
            };
        }

        private static IEnumerable<AgentStreamEvent> Read(string line)
        {
            JsonElement frame;
            try
            {
                using var document = JsonDocument.Parse(line);
                frame = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                yield break;
            }

            if (frame.ValueKind != JsonValueKind.Object) yield break;
            string? Text(string name) => frame.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            long Count(string name) => frame.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : 0;

            switch (Text("t"))
            {
                case "start":
                    // A start the agent did not announce is a later frame that only carries an id (AG35).
                    yield return new SessionStarted(Text("id")!)
                    {
                        Announced = !frame.TryGetProperty("announced", out var announced) || announced.ValueKind != JsonValueKind.False,
                    };
                    break;
                case "text":
                    yield return new TextDelta(Text("text")!);
                    break;
                case "think":
                    yield return new Thinking(Text("text")!);
                    break;
                case "call":
                    yield return new ToolCall(Text("name")!, Text("args") ?? "{}", Text("id"));
                    break;
                case "result":
                    yield return new ToolResult(Text("id"), Text("text") ?? "", frame.TryGetProperty("error", out var error) && error.GetBoolean());
                    break;
                case "plan":
                    yield return new PlanUpdated([new LayerPlanEntry(Text("text")!, PlanEntryStatus.InProgress)]);
                    break;
                case "end" when Text("kind") is { } kind:
                    foreach (var report in Closing(frame)) yield return report;
                    yield return new SessionEnded(ProviderVerdict.Failed, true, Text("subtype"), Text("id"), null, Text("why"))
                    {
                        Failure = new AgentFailure(Enum.Parse<AgentFailureKind>(kind), Text("by") ?? "tests:failed")
                        {
                            Restriction = Text("restricts") switch
                            {
                                // A model the agent refused (AG12), and the same subject under a limit or a capacity, which are not one.
                                "model" => new AgentRestriction(RestrictionCause.Unknown, RestrictionSubject.Model, RestrictionRecovery.OwnerAction),
                                "model-quota" => new AgentRestriction(RestrictionCause.Quota, RestrictionSubject.Model, RestrictionRecovery.Unknown),
                                "model-capacity" => new AgentRestriction(RestrictionCause.Capacity, RestrictionSubject.Model, RestrictionRecovery.Unknown),
                                _ => null,
                            },
                        },
                    };
                    break;
                case "end":
                    foreach (var report in Closing(frame)) yield return report;
                    yield return new SessionEnded(ProviderVerdict.Ok, false, Text("subtype") ?? "success", Text("id"), Text("text"), null);
                    break;
            }

            // An assistant message's usage, which the layer's Claude door ticks after the blocks it read (the context the message held).
            if (Text("t") is "text" or "think" or "call" && frame.TryGetProperty("ctx", out var ctx))
            {
                long Held(string name) => ctx.TryGetProperty(name, out var value) ? value.GetInt64() : 0;
                yield return new UsageLive(Held("in"), Held("out"), Held("read"));
            }
        }

        /// <summary>What a result frame reports before its terminal: the usage, the denials, the tokens by model (AG33), as the layer's Claude door orders them.</summary>
        private static IEnumerable<AgentStreamEvent> Closing(JsonElement frame)
        {
            long Count(JsonElement of, string name) => of.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : 0;
            var model = frame.TryGetProperty("model", out var named) ? named.GetString() : null;
            if (frame.TryGetProperty("usage", out var usage))
            {
                yield return new UsageFinal(Count(usage, "in"), Count(usage, "out"), Count(usage, "read"), Count(usage, "write"), model);
            }

            if (frame.TryGetProperty("denied", out var denied))
            {
                foreach (var each in denied.EnumerateArray())
                {
                    yield return new AgentToolDenied(each.GetProperty("name").GetString()!, each.GetProperty("id").GetString(), null);
                }
            }

            if (frame.TryGetProperty("byModel", out var byModel))
            {
                yield return new AgentModelUsage(
                    [.. byModel.EnumerateObject().Select(each => new AgentModelUsageEntry(
                        each.Name, Count(each.Value, "in"), Count(each.Value, "out"), Count(each.Value, "read"), Count(each.Value, "write"),
                        Count(each.Value, "window"), each.Name == model))],
                    DateTimeOffset.UnixEpoch);
            }
        }
    }
}
