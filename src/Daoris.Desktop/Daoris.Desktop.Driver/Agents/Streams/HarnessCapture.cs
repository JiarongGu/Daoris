using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a harness says on the pipe door, captured into its transcript, the console and the record: one pump, one tee, one set
/// of rules about which destination is the durable one, shared by a driven session and a conversation. The door's, not the
/// loop's (LOOPOUT1): no module names the loop.
/// </summary>
internal static class HarnessCapture
{
    /// <summary>
    /// A session's opening in its record: the target the driver composed, with the composer's account of it beside it
    /// (CONTEXT1), then — for a carry-on the person answered (STANDDOWN2) — their answer, as theirs.
    /// </summary>
    /// <remarks>
    /// 🔴 An answer to a parked session was nowhere on the page after it was sent. The words travelled only inside the target, which the conversation folds, so the one thing the person
    /// said was nowhere they would look.
    /// </remarks>
    /// <param name="account">What the target was composed of, kept on its event; null where none was composed here.</param>
    internal static IReadOnlyList<SessionEvent> Opening(string prompt, string? personSaid, InstructionAccount? account = null) =>
        personSaid is { Length: > 0 } said
            ?
            [
                new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = prompt, Account = account },
                new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = said },
            ]
            : [new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = prompt, Account = account }];

    /// <summary>
    /// The driven session's capture, and the same for a session the loop did not spawn — a chat (D49 §3), whose process
    /// belongs to <see cref="ChatRunner"/>. Shared rather than copied: one pump, one tee, one set of
    /// rules about which destination is the durable one.
    /// </summary>
    /// <param name="preamble">
    /// A fact about how this run was set up, written ahead of anything the process says — the pipe
    /// door's form of the protocol door's first transcript lines.
    /// </param>
    internal static Task CaptureAsync(
        Process process, string transcript, string sessionId, SessionOutput? output, CancellationToken ct,
        string? preamble = null) =>
        CaptureAsync(process.StandardOutput, process.StandardError, transcript, sessionId, output, ct, preamble);

    /// <summary>
    /// The pipe door's capture over the two streams themselves, so it is testable without a process: what a harness says
    /// on either reaches the transcript and the console.
    /// </summary>
    internal static async Task CaptureAsync(
        TextReader stdout, TextReader stderr, string transcript, string sessionId, SessionOutput? output,
        CancellationToken ct, string? preamble = null)
    {
        await using var file = TranscriptFile.Open(transcript, append: false);
        if (preamble is { Length: > 0 })
        {
            lock (file) file.WriteLine(preamble);
            output?.Append(sessionId, preamble);
        }

        var said = PumpAsync(stdout, file, sessionId, output, ct);
        var errors = PumpAsync(stderr, file, sessionId, output, ct);
        await Task.WhenAll(said, errors).ConfigureAwait(false);
    }

    /// <summary>
    /// The native door's capture when its harness speaks a structured stdout (D76, CONV3): each line
    /// read by the adapter's own mapper, what it renders reaching the transcript and the console, what
    /// it means reaching the record — the protocol door's split, on the pipe.
    /// </summary>
    /// <remarks>
    /// <para><b>The transcript stays text a person reads</b>, exactly as the protocol door's does: the
    /// rendered lines, never the JSON — and a failure's words among them, for the person.</para>
    ///
    /// <para><b>What the harness says in its own words is kept apart</b> (AGT3c, <paramref name="own"/>): its stderr, the lines it
    /// prints outside its protocol and its failed turn's words, which no agent writes. A refused credential is read from those,
    /// never from the transcript, whose last lines may be the agent quoting one.</para>
    ///
    /// <para><b>A record that cannot be written costs a console line, never the session</b>: the
    /// conversation enriches the run, it does not run it.</para>
    /// </remarks>
    /// <param name="prompt">What was asked, for the record's opening line — the composed target of a
    /// driven session. Null for a conversation, whose messages are recorded as the person sends them.</param>
    /// <param name="observed">
    /// Told each event once the record holds it — how a conversation learns its turn ended (CONV4a).
    /// After the record, never before: its next message must land behind the ending it waited for.
    /// </param>
    /// <returns>The context high-water mark the harness reported, or null when it reported none.</returns>
    /// <param name="said">Told what a line says about the account's windows (TOOL6c), apart from the agent's words.</param>
    /// <param name="opened">
    /// The record's opening where it is not a prompt's (ANSWER1a): a resumed run's first line and the person's answer.
    /// </param>
    /// <param name="append">The transcript goes on rather than starting again: a resumed run's record is the one that parked.</param>
    /// <param name="account">What the prompt was composed of (CONTEXT1), kept beside it on its event; null where none was.</param>
    /// <param name="own">Where the harness's own words are kept for its run's ending (AGT3c), begun again here; null keeps none.</param>
    /// <param name="lineageHome">
    /// The home whose session lineage file is told each subagent's start and end the stream says (FABRIC-C1a,
    /// <see cref="ChildLineage.Watch"/>); null tells none.
    /// </param>
    internal static async Task<AcpUsage?> CaptureStructuredAsync(
        TextReader stdout, TextReader stderr, string transcript, string sessionId, SessionOutput? output,
        SessionEvents? events, IStreamMapper mapper, string? prompt, CancellationToken ct, string? preamble = null,
        Action<SessionEvent>? observed = null, string? personSaid = null,
        IReadOnlyList<SessionEvent>? opened = null, bool append = false, Action<JsonElement>? said = null,
        Action<string>? named = null, InstructionAccount? account = null, HarnessWords? own = null, string? lineageHome = null)
    {
        own?.Begin();
        var lineage = lineageHome is null ? null : ChildLineage.Watch(lineageHome, sessionId);
        await using var file = TranscriptFile.Open(transcript, append);
        // Told once, the moment the harness names its conversation (MSG1c): a conversation that lives for hours keeps it
        // before it ends, as the protocol door keeps `session/new`'s id.
        var told = false;

        void Line(string text)
        {
            lock (file) file.WriteLine(text);
            output?.Append(sessionId, text);
        }

        void Event(SessionEvent e) => events?.Keep(sessionId, e, Line);

        if (preamble is { Length: > 0 }) Line(preamble);
        foreach (var opening in opened ?? (prompt is null ? [] : Opening(prompt, personSaid, account)))
        {
            // The driver's own first line is the transcript's too; the person's answer is the conversation's alone.
            if (opening.Kind == SessionEventKind.Note && opening.Text is { } note) Line(note);
            Event(opening);
        }

        var errors = PumpAsync(stderr, file, sessionId, output, ct, also: own is null ? null : own.Said);
        // What it runs beside itself, each its own console stream (CONSOLE3c), kept wherever the
        // session's own console is; a line that is a stream's never reaches the session's reader.
        var beside = output is null ? null : mapper.Beside(new SessionStreams(output, sessionId), Line);
        while (await stdout.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            // Before the streams beside take it: a subagent's start is theirs too, and its lineage is the part's (FABRIC-C1a). And the
            // background work the harness ends with its turn, the run's ending's however its streams are kept (BGWAIT1c).
            lineage?.Invoke(line);
            own?.Heard(line);
            if (beside?.Take(line) == true) continue;
            var mapped = mapper.Read(line);
            if (own is not null)
            {
                if (mapped.Outside) own.Said(line);
                if (mapped.Failed is { } failure) own.Failed(failure);
            }

            if (named is not null && !told && mapper.Conversation is { } conversation)
            {
                told = true;
                try
                {
                    named(conversation);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    // Keeping the id enriches the session; losing it costs a resume, never the run.
                    Line($"[the session's conversation id could not be kept: {error.Message}]");
                }
            }

            foreach (var text in mapped.Lines) Line(text);
            foreach (var e in mapped.Events)
            {
                Event(e);
                observed?.Invoke(e);
            }

            if (mapped.Limits is { } limits && said is not null)
            {
                try
                {
                    said(limits);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    // What a session says of its account's windows enriches the walk; losing it costs a line, never the session.
                    Line($"[what the session said about its account's windows could not be kept: {error.Message}]");
                }
            }
        }

        // Said while the transcript is still open: what it left running ended with it.
        if (beside is not null) await beside.EndAllAsync().ConfigureAwait(false);
        await errors.ConfigureAwait(false);
        return mapper.Usage;
    }

    /// <summary>
    /// One stream into the transcript and the console. Internal rather than local so the tee itself is
    /// testable without a process: the property worth holding is that a line reaches BOTH.
    /// </summary>
    /// <param name="also">Told each line too, after both: the native door's stderr is its harness's own words (AGT3c).</param>
    internal static async Task PumpAsync(
        TextReader reader, TextWriter file, string sessionId, SessionOutput? output, CancellationToken ct,
        Action<string>? also = null)
    {
        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            lock (file) file.WriteLine(line);
            output?.Append(sessionId, line);
            also?.Invoke(line);
        }
    }
}
