using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A session's reader, which lives as long as the conversation does: each frame is dispatched before the next is read, a response to
/// its request, an update through the turn's Update stages, a request through its Request stages or answered as not implemented. Its
/// end faults whatever is still awaited, so an agent that died mid-turn is a sentence rather than a hang. It reaches the session
/// through <see cref="ITurnReadWiring"/>.
/// </summary>
internal sealed class AcpReader(ITurnReadWiring wiring, AcpWire wire)
{
    /// <summary>Read frames until the stream ends, or <paramref name="ct"/> lets the reader go.</summary>
    public async Task PumpAsync(TextReader incoming, CancellationToken ct)
    {
        try
        {
            await wire.ReadAsync(
                incoming,
                DispatchAsync,
                raw: line => Kept(() =>
                {
                    wiring.Console.End();
                    wiring.Say(line);
                    wiring.Emit(new SessionEvent { Kind = SessionEventKind.Raw, Raw = line });
                }),
                unreadable: (error, frame) => Kept(() =>
                {
                    wiring.Console.End();
                    wiring.Say($"[unreadable frame: {error.Message}] {AcpRender.Compact(frame)}");
                    wiring.Emit(new SessionEvent
                    {
                        Kind = SessionEventKind.Raw, Title = "unreadable frame", Text = error.Message, Raw = AcpRender.Compact(frame),
                    });
                }),
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The run ended; the reader is nobody's business now.
        }
        finally
        {
            await wiring.EndStreamsAsync().ConfigureAwait(false);

            // An agent's last words before its stream ended are the ones a person reads the console for.
            wiring.Console.Dispose();

            // Whatever is still awaited will never be answered: faulting it here turns an agent that died mid-handshake into a sentence
            // rather than a hang. 🔴 Where the timeout said it is ending the process before the stream ended, the sentence is the
            // timeout's; whatever is said after is not.
            wiring.StreamIsOver();

            // What it held for the person ends with the stream, said before the stream's end is: there is no wire to answer on now.
            await wiring.Stages.Of<AcpPermission>().ReleaseHeldAsync(PermissionWhy.Ended).ConfigureAwait(false);
            wire.Fail(wiring.StreamEnded(
                "the ACP agent's stream ended before it answered — the agent exited, or it is not "
                + "speaking ACP on stdout."));
        }
    }

    /// <summary>
    /// A line the transcript or the record cannot keep is lost, not the reader: its end would say the agent's stream had ended while the
    /// agent lives.
    /// </summary>
    private static void Kept(Action keep)
    {
        try
        {
            keep();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
        }
    }

    private async Task DispatchAsync(JsonElement frame)
    {
        var hasMethod = frame.TryGetProperty("method", out var method);
        var hasId = frame.TryGetProperty("id", out var id);
        var name = hasMethod && method.ValueKind == JsonValueKind.String ? method.GetString() : null;

        // Anything but an update ends the line the agent's words left open: a turn's answer, a refusal, a request. An update decides for
        // itself.
        if (name != "session/update") wiring.Console.End();

        if (!hasMethod && hasId)
        {
            wire.Complete(id, frame);
            return;
        }

        if (!hasMethod) return; // neither a call nor an answer; nothing to do with it

        if (hasId)
        {
            try
            {
                await AnswerRequestAsync(name, id, frame).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // 🔴 A request is answered, whatever went wrong reading it: an unanswered one is a turn hung for ever, which is worse than
                // any error the agent can be told.
                await wiring.SendAsync(new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = JsonNode.Parse(id.GetRawText()),
                    ["error"] = new JsonObject { ["code"] = -32603, ["message"] = $"daoris could not read {name}: {error.Message}" },
                }).ConfigureAwait(false);
                throw;
            }

            return;
        }

        if (name == "session/update" && frame.TryGetProperty("params", out var p)
            && p.TryGetProperty("update", out var update))
        {
            await StagesAsync(TurnPhase.Update, new TurnFrame(name, frame, default, update)).ConfigureAwait(false);
        }
    }

    /// <summary>The stages of <paramref name="phase"/>, in order, until one takes the frame: whether one did.</summary>
    private async Task<bool> StagesAsync(TurnPhase phase, TurnFrame frame)
    {
        foreach (var stage in wiring.Stages.In(phase))
        {
            if (await stage.RunAsync(frame).ConfigureAwait(false)) return true;
        }

        return false;
    }

    private async Task AnswerRequestAsync(string? method, JsonElement id, JsonElement frame)
    {
        if (await StagesAsync(TurnPhase.Request, new TurnFrame(method, frame, id, default)).ConfigureAwait(false)) return;

        // Anything else the agent asks, this client does not implement, and says so in the protocol's own vocabulary: an unanswered
        // request is a hung turn.
        await wiring.SendAsync(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = JsonNode.Parse(id.GetRawText()),
            ["error"] = new JsonObject
            {
                ["code"] = -32601,
                ["message"] = $"daoris does not implement {method}",
            },
        }).ConfigureAwait(false);
    }
}
