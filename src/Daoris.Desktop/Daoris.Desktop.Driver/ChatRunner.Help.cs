using System.Collections.Concurrent;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's conversations, kept (ASKHIST1): an earlier one goes on in its own harness conversation with the person's words,
/// as a repository's chat does (MSG1c, D137 §2.2), and a new one can start from an earlier one's words.
/// </summary>
/// <remarks>
/// <para><b>One conversation runs in the room at a time</b> (HELP1a): the ledger holds the room as a tree, so going back to an
/// earlier conversation, or starting one from it, first sets aside the one running here, which keeps its own id and can go on
/// later. One answering a turn is never set aside: the words wait, said why, and the next word tries again.</para>
///
/// <para><b>The room is written again</b> as every open writes it, from the machine as the shell reads it now
/// (<see cref="DescribeHelp"/>); where nothing hands a reading, the room as last written serves and nothing outside it is read.</para>
/// </remarks>
public sealed partial class ChatRunner
{
    /// <summary>How long a conversation set aside is waited for to end, so the ledger sees the room free.</summary>
    private static readonly TimeSpan SetAsideBound = TimeSpan.FromSeconds(10);

    // Ask Daoris's conversations running here, by session: the room's, which one at a time holds.
    private readonly ConcurrentDictionary<string, byte> _rooms = new(StringComparer.OrdinalIgnoreCase);

    // What a conversation started from an earlier one is handed with the person's first words: that one's transcript as a file.
    private readonly ConcurrentDictionary<string, Handing> _handing = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The earlier conversation's words, kept as this conversation's file, and the line its agent is told beside them.</summary>
    private sealed record Handing(IReadOnlyList<KeptFile> Files, string Preface);

    /// <summary>
    /// The machine as the room says it (ASKHIST1): the shell's reading, which <c>START_HELP</c> writes the room from, so a
    /// conversation going on finds the room as an open would. Null where nothing reads it, or it read nothing.
    /// </summary>
    public Func<CancellationToken, Task<HelpMachine?>>? DescribeHelp { get; set; }

    /// <summary>
    /// Ask Daoris's ended conversation goes on with the person's words (ASKHIST1): judged as a chat's is up to its account, then
    /// the room's one running conversation set aside, the ledger's move, and its own conversation resumed in the room.
    /// </summary>
    private async Task<ChatStart> HelpGoingOnAsync(
        PriorSession record, ISessionAdapter adapter, HarnessConversation kept, DriverConfig config,
        Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        var sessionId = record.Session;

        // Its account by name, as a chat's: Ask Daoris belongs to no workspace, so the machine's accounts are asked (D89).
        var resume = await _harnesses
            .ResumeAsync(adapter.Name, config, workspace: null, record.Profile, StartKind.Conversation, walks: false, ct: ct)
            .ConfigureAwait(false);
        if (resume.Waits)
        {
            Note(sessionId, $"{ResumeWords.NotYet}{resume.Selection.Refusal} {ResumeWords.HelpDoor}");
            return new(null, resume.Selection.Refusal ?? ResumeWords.HelpDoor);
        }

        if (resume.Elsewhere is { } elsewhere) return CannotGoOn(record, elsewhere, adapter.Name);
        var selection = resume.Selection;
        if (!selection.Allowed) return Held(sessionId, selection.Refusal!);
        if (Continuations.Judge(record, adapter.Name, adapter.Resumes, selection.Profile, kept) is { } account)
        {
            return CannotGoOn(record, account, adapter.Name);
        }

        if (await SetAsideHelpAsync(sessionId, ct).ConfigureAwait(false) is { } answering) return Held(sessionId, answering);

        // 🔴 The ledger's one move out of an ended state (MSG1a, D137 §2.3), before anything is spawned: it holds the room again,
        // and refuses where another conversation holds it, one another process here runs among them.
        try
        {
            await _service.AdvanceAsync(sessionId, "working", Continuations.GoingOnNoted, ct: ct).ConfigureAwait(false);
        }
        catch (DriverException refused)
        {
            return Held(sessionId, refused.Message);
        }

        var now = await ReadAgainAsync(sessionId, ct).ConfigureAwait(false);
        var words = now is { Waiting.Count: > 0 } ? now.Waiting : record.Waiting;
        var ask = new ResumeAsk(
            kept.Conversation, words,
            Continuations.Opening(adapter.Name, selection.Version, record.HarnessVersion, answer: false),
            files: word => ChatFiles.Kept(_home, sessionId, word.Files));

        var (room, reads) = await HelpRoomNowAsync(ct).ConfigureAwait(false);
        return await RunAsync(
            sessionId, $"Ask Daoris `{sessionId}` goes on with your words in its own conversation.", adapter, selection, config,
            HelpPlace(room, reads), onEnded, ct, new GoingOn(record, ask)).ConfigureAwait(false);
    }

    /// <summary>
    /// Start a new Ask Daoris conversation from an earlier one's words (ASKHIST1): the room's running conversation set aside, a
    /// new one opened as <c>START_HELP</c> opens one, and the earlier one's transcript (<see cref="HelpTranscript"/>) kept as its
    /// file, handed with the person's first message. Never a summary: Daoris makes no model call (D24).
    /// </summary>
    /// <param name="earlier">The earlier conversation, an Ask Daoris record of this machine's; the caller has judged that.</param>
    public async Task<ChatStart> StartHelpFromAsync(
        string earlier, string adapter, DriverConfig config, HelpMachine machine,
        Func<string, string, Task>? onEnded = null, CancellationToken ct = default)
    {
        if (_closing) return new(null, "the application is closing, so no conversation starts now.");

        var kept = new HelpConversations(_home);
        var title = (SessionEvents.IsId(earlier) ? kept.Read(earlier).Name : null)
                    ?? _events.Openings([earlier]).GetValueOrDefault(earlier);
        if (HelpTranscript.Of(_events, earlier, title) is not { } transcript)
        {
            return new(null, $"nothing was said in conversation `{earlier}`, so there is nothing to start from.");
        }

        if (await SetAsideHelpAsync(null, ct).ConfigureAwait(false) is { } answering) return new(null, $"{answering}.");

        var start = await StartHelpAsync(adapter, config, machine, onEnded, ct).ConfigureAwait(false);
        if (start.SessionId is not { } id) return start;

        try
        {
            var files = ChatFiles.Keep(_home, id, [new ChatUpload(HelpTranscript.FileName(earlier), Encoding.UTF8.GetBytes(transcript))]);
            _handing[id] = new Handing(files, HelpTranscript.Preface(earlier));
            kept.StartedFrom(id, earlier, HelpConversations.Transcript);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return new(id, $"Ask Daoris `{id}` opened, but conversation `{earlier}`'s words could not be kept for it: {error.Message}");
        }

        return new(id, $"Ask Daoris `{id}` opened from conversation `{earlier}`: its words go with your first message.");
    }

    /// <summary>
    /// Set aside the room's running conversation (HELP1a holds one at a time): each other Ask Daoris conversation running here
    /// is stopped, keeping its own id to go on later, and waited for until its record ends. One answering a turn is never
    /// stopped: the sentence why comes back instead.
    /// </summary>
    /// <param name="goingOn">The conversation going on, which is never its own obstacle; null for a new one.</param>
    private async Task<string?> SetAsideHelpAsync(string? goingOn, CancellationToken ct)
    {
        foreach (var other in _rooms.Keys.Where(id => !string.Equals(id, goingOn, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            if (Taking(other))
            {
                return $"Ask Daoris is answering in conversation `{other}`; once its turn ends, a word said here takes these with it";
            }

            _processes.Stop(other);
            if (_watching.TryGetValue(other, out var watch)) await Task.WhenAny(watch, Task.Delay(SetAsideBound, ct)).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// The room as the machine stands now, written again as an open writes it, and what reading across lets it read (D107); the
    /// room as last written, reading nothing outside it, where nothing hands a reading or the reading failed.
    /// </summary>
    private async Task<(string Room, IReadOnlyList<HelpRead> Reads)> HelpRoomNowAsync(CancellationToken ct)
    {
        if (DescribeHelp is { } describe)
        {
            try
            {
                if (await describe(ct).ConfigureAwait(false) is { } machine) return (HelpRoom.Prepare(_home, machine), machine.Reads);
            }
            catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // Any reading's failure, the service's or a file's: the room on disk still says what Daoris is and its doors.
            }
        }

        return (HelpRoom.PathOf(_home), []);
    }
}
