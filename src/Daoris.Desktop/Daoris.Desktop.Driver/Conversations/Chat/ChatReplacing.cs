namespace Daoris.Driver;

/// <summary>
/// A new conversation whose agent refused its model at the open (MODELROLE1d2, D205 §3.5(b)): the re-ask within its start, the refused
/// record's replacement and the words moved to it. A class over the runner's context, as <see cref="BriefedMove"/> is; its
/// <see cref="ChatPending"/> carries the start across the records it opens.
/// </summary>
/// <remarks>
/// <para><b>The one rule</b> (§3.3): the start goes on as if the refusal had been known before its spawn. The refusal is kept as the
/// account's reading first; the walk asked again answers a later entry of the same agent on the same account, which is set on the open
/// session, or anything else, where the refused record ends <c>choice-not-offered</c> and the answer opens in its place.</para>
/// <para><b>A pick is never walked afresh</b>: it tries only its agent's other entries on the same account, then waits with its reason.</para>
/// </remarks>
internal sealed class ChatReplacing(ChatContext context, ChatRunner runner)
{
    /// <summary>A replacement opened (MODELROLE1d2): the refused record and the one in its place, which the window follows.</summary>
    public event Action<string, string>? Replaced;

    /// <summary>
    /// The protocol open's re-ask for <paramref name="sessionId"/> (§3.3's first path): the refused model kept as its account's reading, the
    /// walk asked again with that account passed and no count of its own. A later entry of the same agent on the same account is set on
    /// this session, its record's choice and asked moving, and the opening says both; anything else is null, and the open ends.
    /// </summary>
    public Func<ModelRefusal, Task<ActorAsked?>> Refused(
        ChatPending pending, string sessionId, string actor, ISessionAdapter resolved, HarnessSelection selection, ActorEffective chosen,
        DriverConfig config)
    {
        var current = chosen;
        return async refusal =>
        {
            using var log = new MachineLog(context.Home, "driver");
            if (AgentOffers.Refused(context.Home, resolved.Name, selection.Version, selection.Profile, refusal.Value, DateTimeOffset.UtcNow) is { } unkept)
            {
                log.Write("warn", "actor.offers-unkept", [("session", sessionId), ("why", unkept)]);
            }

            log.Write("info", "actor.refused", [("actor", actor), ("session", sessionId), ("agent", resolved.Name), ("model", refusal.Value),
                ("code", refusal.Code)]);
            pending.Refused(current, selection.Profile, selection.Number);

            // A pick tries its agent's other entries on its own account alone; any other start walks its order as it read it.
            ConversationWalk again;
            try
            {
                again = await runner.WalkAsync(
                        actor, pending.Picked ? ActorChoices.PickAlternatives(actor, config, current.Agent) : pending.Order, config, pending.Workspace,
                        pending.Picked ? selection.Profile : pending.Profile, CancellationToken.None, pending.PassedFor, counts: false,
                        except: selection.Number)
                    .ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or ActorUnreadableException)
            {
                return null;
            }

            var later = again.Chosen;
            var same = later is not null && later != current
                       && string.Equals(later.Agent, current.Agent, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(again.Selection?.Profile, selection.Profile, StringComparison.OrdinalIgnoreCase);
            log.Write("info", "actor.reasked", [("actor", actor), ("session", sessionId), ("refused", current.From), ("answer", later?.From), ("same", same)]);
            if (!same) return null;

            // The same-account move (§3.3): the refused entry passed on this account, said beside the one that runs.
            var why = $"`{selection.Profile ?? "its own sign-in"}` did not offer `{refusal.Value}`";
            RotatedOpening.SayActor(context.Events, sessionId, actor, later!, [new BuildPass(current, refusal.Code, why)]);
            await context.Service.TryKeepActorAsync(sessionId, log, later!.From, later.Asked, ct: CancellationToken.None).ConfigureAwait(false);
            current = later;
            return later.Asked;
        };
    }

    /// <summary>
    /// A word said to the refused record, as its replacement <paramref name="to"/> takes it: its files kept again in that record's own folder,
    /// which its agent reads, and the briefings the refused record composed left for the replacement's own. A word kept on the refused
    /// record leaves it by its id once on the wire here, named as taken by the replacement.
    /// </summary>
    public ChatMessage Moved(ChatMessage word, string from, string to)
    {
        var moved = word with { Files = Rekept(word.Files, to), Process = null };
        if (word.Briefing is not null) context.Browsers.TryRemove(to, out _);
        if (word is { Kept: true, Id: { } id })
        {
            var delivered = word.Delivered;
            moved = moved with
            {
                Kept = false, Attempting = null, Marking = null, Went = null,
                Delivered = () =>
                {
                    delivered?.Invoke();
                    _ = TakeOffAsync(from, id, to);
                },
            };
        }

        return runner.WithProcess(to, moved);
    }

    /// <summary>
    /// The refused record's replacement (§3.5(b)), once its run let go: the refused open's count given back, then the walk's answer opened in
    /// its place, linked to it, and said there; a pick, or a start with nothing left, opens nothing, and its words wait on the refused record,
    /// said why.
    /// </summary>
    public async Task ReplaceAsync(ChatPending pending, string refused, string agent, ModelRefusal refusal)
    {
        context.Harnesses.Release(pending.Number, refused);
        using var log = new MachineLog(context.Home, "driver");
        var account = pending.RefusedOn;
        ChatStart started;
        if (pending.Picked)
        {
            started = new ChatStart(null, $"you picked `{agent}`, and `{account ?? "its own sign-in"}` does not offer `{refusal.Value}`.");
        }
        else
        {
            try
            {
                started = context.Closing ? new ChatStart(null, "Daoris is closing.") : await pending.ReplaceAsync(refused).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                started = new ChatStart(null, error.Message);
            }
        }

        if (started.SessionId is { } to && !string.Equals(to, refused, StringComparison.OrdinalIgnoreCase))
        {
            log.Write("info", "chat.replaced", [("actor", pending.Actor), ("from", refused), ("to", to), ("issued", pending.Issued)]);
            context.Note(to, Noted.Of(
                NoteCodes.ChatReplaced,
                $"Opened in place of session {refused}: `{agent}` did not offer `{refusal.Value}` on `{account ?? "its own sign-in"}`, so nothing ran there, and the words said to it came here.",
                ("session", refused), ("agent", agent), ("model", refusal.Value), ("account", account ?? AgentOffers.OwnHome)));
            Replaced?.Invoke(refused, to);
            return;
        }

        // Nothing opened: the words that moved wait on the refused record as the person's, said why, and go on when the person writes again.
        log.Write("info", "chat.not-replaced", [("actor", pending.Actor), ("session", refused), ("why", started.Message)]);
        context.Note(refused, Noted.Of(
            NoteCodes.ChatChoiceWaits, "None of its choices can run now, so nothing started here; the words said to it wait on it.")
            .Then(" ", new Noted(started.Message, [])));
        foreach (var word in pending.Exhausted())
        {
            if (word.Kept || !word.Persons) continue;
            try
            {
                await context.Service.SayAsync(refused, word.Text, [.. word.Files.Select(file => file.Name)]).ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException)
            {
                // Said in its conversation already, where the person reads it; what could not be kept is theirs to say again.
                log.Write("warn", "chat.words-unkept", [("session", refused), ("error", error.Message)]);
            }
        }
    }

    /// <summary>The files a moved word carries, kept again in <paramref name="to"/>'s folder; a file that no longer reads is left as it was.</summary>
    private IReadOnlyList<KeptFile> Rekept(IReadOnlyList<KeptFile> files, string to)
    {
        if (files.Count == 0) return files;
        try
        {
            return ChatFiles.Keep(context.Home, to, [.. files.Select(file => new ChatUpload(file.Name, AtomicFile.ReadBytes(file.Path)))]);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return files;
        }
    }

    /// <summary>A kept word off the refused record by its id, named as taken by its replacement; a refusal leaves it for the next come-up.</summary>
    private async Task TakeOffAsync(string from, string id, string to)
    {
        try
        {
            await context.Service.TakenAsync(from, [id], by: to).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException or ObjectDisposedException)
        {
            // Left on the record, it is settled as uncertain at the next come-up, never sent twice.
        }
    }
}
