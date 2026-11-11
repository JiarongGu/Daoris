using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a harness says on the pipe door, captured into its transcript, the console and the record: one pump, one tee, one set
/// of rules about which destination is the durable one, shared by a driven session and a conversation. The door's, not the
/// loop's: no module names the loop.
/// </summary>
internal static class HarnessCapture
{
    /// <summary>
    /// A session's opening in its record: the target the driver composed, with the composer's account of it beside it,
    /// then — for a carry-on the person answered — their answer, as theirs.
    /// </summary>
    /// <remarks>
    /// 🔴 The words of an answer to a parked session travel inside the target, which the conversation folds, so they are shown here:
    /// otherwise the one thing the person said would be nowhere they would look.
    /// </remarks>
    /// <param name="account">What the target is composed of, kept on its event; null where none is composed here.</param>
    /// <param name="handed">
    /// For a resume, the words it is handed from the session that asked, each after the target under the id that record gave
    /// it, so the conversation shows them where the session took them: the person's as theirs, Daoris's as Daoris's. Null or
    /// empty for none.
    /// </param>
    internal static IReadOnlyList<SessionEvent> Opening(
        string prompt, string? personSaid, InstructionAccount? account = null, IReadOnlyList<SaidWordView>? handed = null) =>
    [
        new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = prompt, Account = account },
        .. personSaid is { Length: > 0 } said
            ? new[] { new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = said } }
            : [],
        .. (handed ?? []).Select(word => new SessionEvent
        {
            Kind = SessionEventKind.User,
            Origin = word.Persons ? "person" : "target",
            // The supervisor's answer keeps its author where the session took it.
            From = word.Daoris ? SaidFrom.Daoris : word.Supervisors ? SaidFrom.Supervisor : null,
            Watcher = WatchKeys.Is(word.Key) ? true : null,
            Id = word.Id,
            Text = word.Text,
            Files = word.Files.Count > 0 ? word.Files : null,
        }),
    ];

    /// <summary>
    /// The pipe door's capture over the two streams, the one every run's plain text is read by, a conversation's among them
    /// (<see cref="TransportRun"/>): what a harness says on either reaches the transcript and the console. Shared rather than copied:
    /// one pump, one tee, one set of rules about which destination is the durable one.
    /// </summary>
    /// <param name="preamble">
    /// A fact about how this run is set up, written ahead of anything the process says — the pipe
    /// door's form of the protocol door's first transcript lines.
    /// </param>
    internal static async Task CaptureAsync(
        TextReader stdout, TextReader stderr, string transcript, IReadingSink sink, CancellationToken ct, string? preamble = null)
    {
        await using var file = AgentContext.Current.OpenTranscript(transcript, append: false);
        ExceptionDispatchInfo? failure = null;
        if (preamble is { Length: > 0 })
        {
            try
            {
                lock (file) file.WriteLine(preamble);
                sink.Read(new WireReading.Shown(preamble));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                failure = ExceptionDispatchInfo.Capture(error);
            }
        }

        var said = PumpAsync(stdout, file, sink, ct);
        var errors = PumpAsync(stderr, file, sink, ct);
        await Task.WhenAll(said, errors).ConfigureAwait(false);
        failure?.Throw();
    }

    /// <summary>
    /// The native door's capture when its harness speaks a structured stdout: each line
    /// read by the adapter's own mapper, what it renders reaching the transcript and the console, what
    /// it means reaching the record — the protocol door's split, on the pipe.
    /// </summary>
    /// <remarks>
    /// <para><b>The transcript stays text a person reads</b>, exactly as the protocol door's does: the
    /// rendered lines, never the JSON — and a failure's words among them, for the person.</para>
    ///
    /// <para><b>What the harness says in its own words is kept apart</b> (<paramref name="own"/>): its stderr, the lines it
    /// prints outside its protocol and its failed turn's words, which no agent writes. A refused credential is read from those,
    /// never from the transcript, whose last lines may be the agent quoting one.</para>
    ///
    /// <para><b>A record that cannot be written costs a console line, never the session</b>: the
    /// conversation enriches the run, it does not run it.</para>
    /// </remarks>
    /// <param name="prompt">What is asked, for the record's opening line — the composed target of a
    /// driven session. Null for a conversation, whose messages are recorded as the person sends them.</param>
    /// <param name="observed">
    /// Told each event once the record holds it — how a conversation learns its turn ended.
    /// After the record, never before: its next message must land behind the ending it waited for.
    /// </param>
    /// <returns>The context high-water mark the harness reported, or null when it reported none.</returns>
    /// <param name="sink">
    /// Where the run's readings go: each event the record keeps, each line the console shows, the streams beside, the conversation
    /// the stream names, what a line says of the account's windows and a failed turn's one <see cref="WireReading.Failed"/>.
    /// </param>
    /// <param name="named">Told once the stream named its conversation and the sink is told it: where the run learns its words went.</param>
    /// <param name="opened">
    /// The record's opening where it is not a prompt's: a resumed run's first line and the person's answer.
    /// </param>
    /// <param name="append">The transcript goes on rather than starting again: a resumed run's record is the one that parked.</param>
    /// <param name="account">What the prompt is composed of, kept beside it on its event; null where none is.</param>
    /// <param name="own">Where the harness's own words are kept for its run's ending, begun again here; null keeps none.</param>
    /// <param name="echoed">Told the uuid of each line the harness echoed where it took it, in the wire's order with the events.</param>
    internal static async Task<AcpUsage?> CaptureStructuredAsync(
        TextReader stdout, TextReader stderr, string transcript, IReadingSink sink, IStreamMapper mapper, string? prompt,
        CancellationToken ct, string? preamble = null, Action<SessionEvent>? observed = null, string? personSaid = null,
        IReadOnlyList<SessionEvent>? opened = null, bool append = false, Action? named = null, InstructionAccount? account = null,
        HarnessWords? own = null, IReadOnlyList<SaidWordView>? handed = null, Action<string>? echoed = null)
    {
        own?.Begin();
        await using var file = AgentContext.Current.OpenTranscript(transcript, append);
        // Told once, the moment the harness names its conversation: a conversation that lives for hours keeps it
        // before it ends, as the protocol door keeps `session/new`'s id.
        var told = false;

        void Line(string text)
        {
            lock (file) file.WriteLine(text);
            sink.Read(new WireReading.Shown(text));
        }

        void Event(SessionEvent e) => sink.Read(new WireReading.Said(e));

        // A line that cannot be kept is the first failure, thrown once both pipes are read to their ends: a child that is not drained
        // blocks on a full pipe, and the stderr pump writes to the transcript only while it is open.
        ExceptionDispatchInfo? failure = null;
        try
        {
            if (preamble is { Length: > 0 }) Line(preamble);
            foreach (var opening in opened ?? (prompt is null ? [] : Opening(prompt, personSaid, account, handed)))
            {
                // The driver's own first line is the transcript's too; the person's answer is the conversation's alone.
                if (opening.Kind == SessionEventKind.Note && opening.Text is { } note) Line(note);
                Event(opening);
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            failure = ExceptionDispatchInfo.Capture(error);
        }

        var errors = PumpAsync(stderr, file, sink, ct, also: own is null ? null : own.Said);
        // What it runs beside itself, each its own console stream, kept wherever someone watches the session's own console;
        // a line that is a stream's never reaches the session's reader.
        var beside = ReadingStreams.Of(sink) is { } streams ? mapper.Beside(streams, Line) : null;
        var children = new NativeChildren();
        var tasks = new NativeTasks();

        // A child's or a background task's start or end, said once as its reading: the part's lineage keeps a child's, the
        // run's ending the work its harness ended with its turn however its streams are kept, and the streams beside open and
        // end theirs by it.
        void Work(WireReading reading)
        {
            sink.Read(reading);
            own?.Read(reading);
            beside?.Read(reading);
        }

        void Take(string line)
        {
            // Each line is read once into its frame, which every reader of it takes.
            var frame = NativeFrame.Of(line);
            if (children.Read(frame) is { } child) Work(child);
            if (tasks.Read(frame) is { } task) Work(task);
            // What the harness says it loaded, said once as its init frame goes by.
            if (NativeInit.Read(frame) is { } loaded)
            {
                try
                {
                    sink.Read(new WireReading.Loaded(loaded));
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    Line($"[what the session said it loaded could not be kept: {error.Message}]");
                }
            }

            if (beside?.Take(frame) == true) return;
            var mapped = mapper.Read(frame);
            if (own is not null)
            {
                if (mapped.Outside) own.Said(line);
                if (mapped.Failed is { } failure) own.Failed(failure);
                if (mapped.ModelRefused) own.ModelRefused();
            }

            // A failed turn is one reading on either door: the result's own words, which nothing reads for the account until
            // the pipe door's failure reader does.
            if (mapped.Failed is { } failed) sink.Read(new WireReading.Failed(DoorFailures.OfResult(failed)));

            if (!told && mapper.Conversation is { } conversation)
            {
                told = true;
                try
                {
                    sink.Read(new WireReading.ConversationNamed(conversation));
                    named?.Invoke();
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

            if (mapped.Echoed is { } uuid) echoed?.Invoke(uuid);

            if (mapped.Limits is { } limits)
            {
                try
                {
                    sink.Read(new WireReading.WindowsSaid(limits));
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    // What a session says of its account's windows enriches the walk; losing it costs a line, never the session.
                    Line($"[what the session said about its account's windows could not be kept: {error.Message}]");
                }
            }
        }

        try
        {
            while (await stdout.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
            {
                try
                {
                    Take(line);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    failure ??= ExceptionDispatchInfo.Capture(error);
                }
            }

            // Said while the transcript is still open: what it left running ended with it. Then the ends only an update said, its notification
            // never written: the run's ending reads them, and a stream the close already ended keeps its words.
            if (beside is not null) await beside.EndAllAsync().ConfigureAwait(false);
            foreach (var ended in tasks.Close()) Work(ended);
        }
        finally
        {
            await Task.WhenAny(errors).ConfigureAwait(false);
        }

        failure?.Throw();
        await errors.ConfigureAwait(false);
        return mapper.Usage;
    }

    /// <summary>
    /// One stream into the transcript and the console. Internal rather than local so the tee itself is
    /// testable without a process: the property worth holding is that a line reaches BOTH.
    /// </summary>
    /// <param name="also">Told each line too, after both: the native door's stderr is its harness's own words.</param>
    internal static async Task PumpAsync(
        TextReader reader, TextWriter file, IReadingSink sink, CancellationToken ct, Action<string>? also = null)
    {
        // A line that cannot be kept ends nothing: a pipe nobody reads fills, and its writer blocks on the next line. The first
        // failure is thrown once the pipe has ended.
        Exception? failure = null;
        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            try
            {
                lock (file) file.WriteLine(line);
                sink.Read(new WireReading.Shown(line));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                failure ??= error;
            }

            try
            {
                also?.Invoke(line);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                failure ??= error;
            }
        }

        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
