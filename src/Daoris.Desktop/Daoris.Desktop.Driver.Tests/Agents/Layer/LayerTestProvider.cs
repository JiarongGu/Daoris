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

    public static AgentProviderDescriptor Descriptor { get; } = new()
    {
        Id = Id,
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

    /// <summary>The provider's registration under <paramref name="door"/>, its program <paramref name="command"/>.</summary>
    public static LayerRegistration Registration(IReadOnlyList<string> command, string door = Id) =>
        new(door, builder => builder.AddAgentProvider(
            Descriptor, services => new AgentProvider(Descriptor, _ => new Session(services.GetRequiredService<IProcessRunner>(), command))));

    /// <summary>A scenario's program in <paramref name="scratch"/>, saying what <paramref name="spec"/> says, as a command.</summary>
    /// <remarks>
    /// The spec's fields: <c>out</c>, lines said at once (a string as written, an object as its JSON); <c>err</c>, stderr lines first;
    /// <c>wait</c>, milliseconds before <c>after</c>'s lines; <c>hang</c>, never ending by itself; <c>echo</c>, its stdin said back;
    /// <c>exit</c>, its code. A resume's conversation is said first, as <c>resumed id</c>.
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
          if (spec.echo) say(`heard ${heard}`);
          for (const line of spec.err ?? []) console.error(line);
          for (const line of spec.out ?? []) say(line);
          if (spec.wait) await new Promise((done) => setTimeout(done, spec.wait));
          for (const line of spec.after ?? []) say(line);
          if (spec.hang) setInterval(() => {}, 1000);
          else process.exitCode = spec.exit ?? 0;
        });
        """;

    private sealed class Session(IProcessRunner runner, IReadOnlyList<string> command) : IAgentSession
    {
        public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
            AgentSessionOptions options, [EnumeratorCancellation] CancellationToken ct = default)
        {
            List<string> args = [.. command.Skip(1)];
            if (options.ResumeToken is { } token) args.AddRange(["--resume", token]);
            var lines = runner.StreamLinesAsync(
                command[0], args, stdin: options.Prompt, inactivityTimeout: TimeSpan.FromSeconds(options.TimeoutSeconds ?? 120),
                workingDirectory: options.WorkingDirectory, ct: ct).GetAsyncEnumerator(ct);
            SessionEnded? ended = null;
            SessionEnded? faulted = null;
            string? conversation = null;
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
                        if (each is SessionEnded terminal)
                        {
                            if (ended is not null) continue;
                            ended = terminal;
                        }

                        yield return each;
                    }
                }
            }

            if (ended is null)
            {
                yield return faulted ?? new SessionEnded(
                    ProviderVerdict.Failed, true, null, conversation, null, "no output produced (no terminal result)")
                {
                    Failure = new AgentFailure(AgentFailureKind.Fatal, "no-terminal-event"),
                };
            }
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
                    yield return new SessionStarted(Text("id")!);
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
                case "usage":
                    yield return new UsageFinal(Count("in"), Count("out"), Count("read"), Count("write"), Text("model"));
                    break;
                case "end" when Text("kind") is { } kind:
                    yield return new SessionEnded(ProviderVerdict.Failed, true, Text("subtype"), Text("id"), null, Text("why"))
                    {
                        Failure = new AgentFailure(Enum.Parse<AgentFailureKind>(kind), Text("by") ?? "tests:failed"),
                    };
                    break;
                case "end":
                    yield return new SessionEnded(ProviderVerdict.Ok, false, Text("subtype") ?? "success", Text("id"), Text("text"), null);
                    break;
            }
        }
    }
}
