using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The agent's <c>session/request_permission</c>, refused always, and by the option's KIND rather than its position; or, where the
/// person answers the session's requests, held for them.
/// </summary>
/// <remarks>
/// <para><b>The driver widens nothing.</b> A request reaching the driver at all means the repository's own checked-in permission
/// configuration did not already cover the action; the driver is a component, not a party to the work, and widening the posture at
/// runtime is precisely what "a better approval surface must not widen autonomy" forbids. The harness's standing posture is set where
/// it belongs, at session creation, so ordinary reversible work never reaches this path.</para>
///
/// <para><b>With no refusal offered, the answer is <c>cancelled</c></b>, never the first option that happens to be present. Fail closed
/// means closed even when the menu is unhelpful.</para>
///
/// <para><b>Under a repository's asking permission prompts it is held for the person instead</b> (<see cref="HoldPermission"/>): the
/// person's press is the say kept for them, one call at a time; the driver itself still allows nothing.</para>
/// </remarks>
[TurnStage("permission", TurnPhase.Request)]
internal sealed class AcpPermission(ITurnWiring wiring) : ITurnStage
{
    /// <summary>The answers to held requests still on their way, which the stream's end waits for before it says it ended.</summary>
    private readonly List<Task> _answering = [];

    public async ValueTask<bool> RunAsync(TurnFrame frame)
    {
        if (frame.Method != "session/request_permission") return false;

        // A request is the agent's turn doing something, never its silence; with no prompt open, the harness's turn beginning.
        wiring.Wake.Asked(wiring.Prompted());
        await AnswerPermissionAsync(frame.Id, frame.Frame).ConfigureAwait(false);
        return true;
    }

    private async Task AnswerPermissionAsync(JsonElement id, JsonElement frame)
    {
        if (HoldPermission(id, frame)) return;

        var rejectId = RejectOption(frame);
        var outcome = rejectId is null
            ? new JsonObject { ["outcome"] = "cancelled" }
            : new JsonObject { ["outcome"] = "selected", ["optionId"] = rejectId };

        var call = frame.TryGetProperty("params", out var q) && q.ValueKind == JsonValueKind.Object
                   && q.TryGetProperty("toolCall", out var named) && named.ValueKind == JsonValueKind.Object
            ? named : (JsonElement?)null;
        var callId = call is { } c && c.TryGetProperty("toolCallId", out var asked) && asked.ValueKind == JsonValueKind.String
            ? asked.GetString() : null;
        // Said of every place a session runs, a repository, an intake's room, Ask Daoris's, since the rules it is handed and the
        // place's own settings are what refused it, wherever that is.
        const string governs = "— the rules it runs under govern, and the driver may not widen them";
        wiring.Refused.Add(callId, Named(call).Trim('`'));
        // The console is the raw view, and keeps the request as the wire said it.
        wiring.Say($"  permission refused: {(call is { } raw ? AcpRender.Compact(raw) : "a tool call")} {governs}");
        // 🔴 The record names the call as a reader knows it, its title, else its kind: the request's JSON made every refusal four
        // lines of wire. Its id keeps the note with its call; the title is the agent's words, as it may hold a path.
        var refused = Noted.Of(EndNoteCodes.RefusedLead, "permission refused:")
            .Then(" ", Noted.Said(Named(call), NoteBy.Agent))
            .Then(" ", Noted.Of(EndNoteCodes.RefusedGoverns, governs));
        wiring.Emit(new SessionEvent { Kind = SessionEventKind.Note, Id = callId, Text = refused.Note, Parts = refused.Parts });

        await wiring.SendAsync(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = JsonNode.Parse(id.GetRawText()),
            ["result"] = new JsonObject { ["outcome"] = outcome },
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// The option that refuses, read without trusting the frame's shape: null when none can be read, which answers <c>cancelled</c>,
    /// also a refusal. A read that threw on an unexpected shape left the request unanswered, and an unanswered request is a turn hung
    /// for ever.
    /// </summary>
    private static string? RejectOption(JsonElement frame)
    {
        if (!frame.TryGetProperty("params", out var p) || p.ValueKind != JsonValueKind.Object
            || !p.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var option in options.EnumerateArray())
        {
            if (option.ValueKind != JsonValueKind.Object) continue;
            if (!option.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String) continue;
            if (kind.GetString() is not ("reject_once" or "reject_always")) continue;
            if (option.TryGetProperty("optionId", out var o) && o.ValueKind == JsonValueKind.String) return o.GetString();
        }

        return null;
    }

    /// <summary>
    /// Hold a permission request for the person where the session's posture says the person answers them and it has a door
    /// (<see cref="ITurnWiring.Permissions"/>): listed with what the person may choose, said on the console and in the record, and
    /// answered off the read loop once the person answers or it is released. False leaves it to the refusal.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Off the loop.</b> The reader dispatches each frame before it reads the next, so a hold awaited here would stall every
    /// frame after it: the agent's updates, a cancel's answer, the stream's end. The answer goes later, through the same serialized
    /// writer as every other frame.</para>
    ///
    /// <para><b>A request this client cannot read is still refused at once</b>: params or options of an unexpected shape hold nothing,
    /// and the refusal answers it.</para>
    /// </remarks>
    private bool HoldPermission(JsonElement id, JsonElement frame)
    {
        if (!wiring.PersonAnswers || wiring.Permissions is not { } holds || Offered(frame) is not { } offered) return false;

        var call = frame.GetProperty("params").TryGetProperty("toolCall", out var named) && named.ValueKind == JsonValueKind.Object
            ? named : (JsonElement?)null;
        var callId = call is { } c && c.TryGetProperty("toolCallId", out var asked) && asked.ValueKind == JsonValueKind.String
            ? asked.GetString() : null;
        var title = Named(call);
        var request = id.ValueKind == JsonValueKind.String ? id.GetString() ?? "" : id.GetRawText();
        // The registry keeps the agent's own title and kind, plain, for the page to word; the notes say it as a reader knows it.
        var plain = call is { } p && Json.Text(p, "title")?.Trim() is { Length: > 0 } written ? TextBounds.Clip(written, 160) : null;
        // A turn waiting on the person is never silent, and its quiet counts from the answer (AnsweredAsync). The wake knows of the
        // hold before the hold can be seen, or answered, anywhere: a quiet judged between the two read a held turn as silent.
        wiring.Wake.Held();
        if (holds.Hold(request, callId, plain, offered, call is { } k ? Json.Text(k, "kind") : null) is not { } held)
        {
            wiring.Wake.Released();
            return false;
        }

        var raw = call is { } shown ? AcpRender.Compact(shown) : "a tool call";
        try
        {
            Said($"  permission held for the person's answer: {raw}",
                Noted.Of(HeldNoteCodes.AskingHeld, "permission held for the person's answer:").Then(" ", Noted.Said(title, NoteBy.Agent)), callId);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The console that would say it is gone; the hold stands and its answer still goes, as AnswerAsync's does.
        }

        var answering = AnsweredAsync(id, raw, callId, title, held, holds.Bound);
        lock (_answering)
        {
            _answering.RemoveAll(each => each.IsCompleted);
            _answering.Add(answering);
        }

        return true;
    }

    /// <summary>
    /// The options a permission request offered, read without trusting the frame's shape: null where its params or options are not
    /// what the protocol says, and then nothing is held. An option with no id or no kind is not one the person can choose.
    /// </summary>
    private static IReadOnlyList<PermissionChoice>? Offered(JsonElement frame)
    {
        if (!frame.TryGetProperty("params", out var p) || p.ValueKind != JsonValueKind.Object
            || !p.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var offered = new List<PermissionChoice>();
        foreach (var option in options.EnumerateArray())
        {
            if (option.ValueKind != JsonValueKind.Object
                || Json.Text(option, "optionId") is not { Length: > 0 } optionId || Json.Text(option, "kind") is not { Length: > 0 } kind)
            {
                continue;
            }

            offered.Add(new PermissionChoice(optionId, Json.Text(option, "name") ?? optionId, kind));
        }

        return offered;
    }

    /// <summary>
    /// The answer to a held request, once the person answers it or it is released: the option the person chose where it allows the
    /// call, else the offered rejection they chose, else <c>cancelled</c>; said on the console and in the record, and sent unless the
    /// stream is over. A call it did not allow is marked refused, so its later <c>failed</c> reads as what it is.
    /// </summary>
    private async Task AnsweredAsync(JsonElement id, string raw, string? callId, string title, Task<PermissionEnd> held, TimeSpan bound)
    {
        try
        {
            await AnswerAsync(id, raw, callId, title, await held.ConfigureAwait(false), bound).ConfigureAwait(false);
        }
        finally
        {
            // The hold is over once its answer is said and sent, and the turn's quiet counts from then, after the record says it.
            wiring.Wake.Released();
        }
    }

    /// <summary><see cref="AnsweredAsync"/>'s answer, said and sent once the hold has ended as <paramref name="end"/>.</summary>
    private async Task AnswerAsync(JsonElement id, string raw, string? callId, string title, PermissionEnd end, TimeSpan bound)
    {
        JsonObject outcome;
        try
        {
            if (end.Chosen is { Allows: true } allowed)
            {
                Said($"  permission allowed by the person: {raw}",
                    Noted.Of(HeldNoteCodes.AskingAllowed, "permission allowed by the person:").Then(" ", Noted.Said(title, NoteBy.Agent)), callId);
                outcome = new JsonObject { ["outcome"] = "selected", ["optionId"] = allowed.Id };
            }
            else
            {
                wiring.Refused.Add(callId, title.Trim('`'));
                var minutes = Math.Max(1, (int)Math.Ceiling(bound.TotalMinutes));
                var close = end.Why switch
                {
                    PermissionWhy.Answered => Noted.Of(HeldNoteCodes.AskingDenied, "— the person refused it"),
                    PermissionWhy.Cancelled => Noted.Of(HeldNoteCodes.AskingCancelled, "— the turn was stopped before the person answered"),
                    PermissionWhy.Bound => Noted.Of(
                        HeldNoteCodes.AskingBound,
                        $"— nobody answered within its {minutes}-minute bound, and a turn is never left waiting for ever",
                        ("minutes", minutes)),
                    _ => Noted.Of(HeldNoteCodes.AskingEnded, "— the session ended before the person answered"),
                };
                Said($"  permission refused: {raw} {close.Note}",
                    Noted.Of(EndNoteCodes.RefusedLead, "permission refused:").Then(" ", Noted.Said(title, NoteBy.Agent)).Then(" ", close), callId);
                outcome = end.Chosen is { } rejection
                    ? new JsonObject { ["outcome"] = "selected", ["optionId"] = rejection.Id }
                    : new JsonObject { ["outcome"] = "cancelled" };
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The console that would say it is gone; the answer still goes, failing closed.
            outcome = new JsonObject { ["outcome"] = "cancelled" };
        }

        if (wiring.StreamOver) return;
        try
        {
            await wiring.SendAsync(new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = JsonNode.Parse(id.GetRawText()),
                ["result"] = new JsonObject { ["outcome"] = outcome },
            }).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The agent's input closed as it is answered: its stream's end says the rest.
        }
    }

    /// <summary>The driver's line about one call: the console's raw view, and the record's coded note, kept with its call.</summary>
    private void Said(string line, Noted noted, string? callId)
    {
        wiring.Console.End();
        wiring.Say(line);
        wiring.Emit(new SessionEvent { Kind = SessionEventKind.Note, Id = callId, Text = noted.Note, Parts = noted.Parts });
    }

    /// <summary>End what the session holds for the person, saying why, and wait for each answer to be said.</summary>
    public async Task ReleaseHeldAsync(string why)
    {
        wiring.Permissions?.ReleaseAll(why);
        Task[] answering;
        lock (_answering) answering = [.. _answering];
        try
        {
            await Task.WhenAll(answering).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Each answer keeps its own failure; the stream's end is said whatever became of them.
        }
    }

    /// <summary>A tool call as a reader knows it: its title, bounded; else its kind; else "a tool call".</summary>
    private static string Named(JsonElement? call)
    {
        if (call is not { } c) return "a tool call";
        if (c.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String
            && title.GetString()?.Trim() is { Length: > 0 } text)
        {
            return $"`{TextBounds.Clip(text, 160)}`";
        }

        return c.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String
               && kind.GetString() is { Length: > 0 } k
            ? $"an unnamed `{k}` call"
            : "a tool call";
    }
}
