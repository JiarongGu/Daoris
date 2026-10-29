namespace Daoris.Driver;

/// <summary>
/// The words a keyed open moved onto the part it opened, as that part's delivery reads them (FOUNDATION1d-ii part 4; the foundation design
/// §3.4 rule 3, O6-F13): read from the destination by their ids in the transfer's order, never from the record they left, which holds them no
/// more. Each word's files are copied from that record's folder into the destination's before it is handed (O6-F8); each is marked attempted
/// on the destination as the hand is made (<see cref="AttemptedAsync"/>), so a shell cut off under the hand never sends it again by itself
/// (rule 5), and one the mark does not name stays there for the destination's own go-on; each is shown by its own <c>User</c> event under its id, with its author and files, as a
/// go-on's words are (<see cref="ResumeAsk.Opening"/>, O6-F2), the display's and never delivery's evidence; and once delivered they leave the
/// destination by those ids, judged by what it holds after the take, never the take's answer.
/// </summary>
/// <remarks>
/// <para><b>Only where the open moved them.</b> A host whose <c>opens</c> mark is below 2, or that keeps no marks, moves nothing: its site reads,
/// delivers and takes the old record's words as before (<see cref="Moving"/> false).</para>
/// <para><b>The moved words are the destination's</b> from the open on: the record they left is no longer its line's head, so its go-on refuses
/// them, and the destination's go-on carries any that wait there still, after its first turn.</para>
/// </remarks>
internal sealed class MovedWords
{
    private readonly IReadOnlyList<(MovedWord Moved, SaidWordView Word)> _words;

    private readonly string _home;

    private MovedWords(string from, string to, IReadOnlyList<(MovedWord Moved, SaidWordView Word)> words, string home)
    {
        From = from;
        To = to;
        _words = words;
        _home = home;
        Files = [.. words.SelectMany(each => ChatFiles.Kept(home, to, each.Word.Files))];
    }

    /// <summary>
    /// Whether the start's open moved the words (FOUNDATION1d-ii part 4): its host was asked and echoed the key, or it answered a transfer, which
    /// a host below the mark replaying a newer host's receipt does. The delivery is then the destination's moved words.
    /// </summary>
    public static bool Moving(ChatStart started) => started.Moves || started.Moved.Count > 0;

    /// <summary>
    /// What a keyed open is asked to move (FOUNDATION1d-ii part 3's <see cref="MoveWords"/>): from <paramref name="from"/>, the words the site
    /// named, and every id it saw waiting there when it read, so a word of the person's kept since moves too.
    /// </summary>
    public static MoveWords Asked(string from, IEnumerable<string?> named, IEnumerable<string?> read) =>
        new(from, [.. named.OfType<string>().Distinct(StringComparer.Ordinal)], [.. read.OfType<string>().Distinct(StringComparer.Ordinal)]);

    /// <summary>The record the words moved from.</summary>
    public string From { get; }

    /// <summary>The part the open opened, which holds them.</summary>
    public string To { get; }

    /// <summary>The moved words the destination holds and marked attempted, in the transfer's order: what the delivery hands.</summary>
    public IReadOnlyList<SaidWordView> Words => [.. _words.Select(each => each.Word)];

    /// <summary>Their ids on the destination: what leaves it once they are delivered.</summary>
    public IReadOnlyList<string> Ids => [.. _words.Select(each => each.Moved.Id)];

    /// <summary>Their ids on the record they left, which a moved word keeps: what that record's line says went.</summary>
    public IReadOnlyList<string> Left => [.. _words.Select(each => each.Moved.Word)];

    /// <summary>Their files, as the destination's folder keeps them now.</summary>
    public IReadOnlyList<KeptFile> Files { get; }

    /// <summary>The words joined by a blank line, in order, as a move's message hands the person's.</summary>
    public string Text => string.Join("\n\n", Words.Select(word => word.Text));

    /// <summary>
    /// What the destination's conversation shows of them: one <c>User</c> event per word under its id, the person's or Daoris's or another
    /// agent's by its mark, with its files' names (O6-F2), as a go-on's opening shows its words.
    /// </summary>
    public IReadOnlyList<SessionEvent> Shown => [.. Words.Select(Of)];

    /// <summary>One moved word's event, as <see cref="ResumeAsk.Opening"/> records a go-on's.</summary>
    internal static SessionEvent Of(SaidWordView word) => new()
    {
        Kind = SessionEventKind.User,
        Origin = word.Persons ? "person" : "target",
        From = word.Daoris ? DaorisWords.From : word.Supervisors ? SupervisorHanded.From : null,
        Opinion = word.By,
        Helper = word.Helper,
        Choice = word.Choice,
        Id = word.Id,
        Text = word.Text,
        Files = word.Files.Count > 0 ? word.Files : null,
    };

    /// <summary>
    /// The words the open moved to <paramref name="to"/> as it holds them now, their files copied from <paramref name="from"/>'s folder; null where
    /// the destination's record could not be read, so nothing is handed and the words wait there. A moved word no longer waiting (taken back
    /// since) is not handed.
    /// </summary>
    public static async Task<MovedWords?> OnAsync(
        ServiceClient service, string home, string from, string to, IReadOnlyList<MovedWord> moved, CancellationToken ct)
    {
        PriorSession? record;
        try
        {
            record = await service.RecordAsync(to, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return null;
        }

        if (record is null) return null;
        var waiting = record.Waiting.Where(word => word.Id is not null).ToDictionary(word => word.Id!, StringComparer.Ordinal);
        List<(MovedWord Moved, SaidWordView Word)> words =
            [.. moved.OrderBy(word => word.Order).Where(word => waiting.ContainsKey(word.Id)).Select(word => (word, waiting[word.Id]))];

        // Copied before the hand, so the files are the destination's whoever reads them next.
        foreach (var (_, word) in words) ChatFiles.Moved(home, from, to, word.Files);
        return new MovedWords(from, to, words, home);
    }

    /// <summary>
    /// The words marked attempted on the destination, before they are handed (row p, the design §3.4 rule 5): those the mark named, so a shell
    /// cut off under the hand never sends them again by itself and the come-up settles them uncertain; one it did not name is not handed and
    /// waits there for the destination's own go-on. Asked only where the hand is made: a part nothing here holds is handed nothing.
    /// </summary>
    public async Task<MovedWords> AttemptedAsync(ServiceClient service)
    {
        if (_words.Count == 0) return this;
        var marked = await SaidDoor.AttemptedAsync(service, To, Words).ConfigureAwait(false);
        return new MovedWords(From, To, [.. _words.Where(each => marked.Contains(each.Moved.Id, StringComparer.Ordinal))], _home);
    }

    /// <summary>
    /// The delivered words off the destination by their ids, as its own turn took them; true only where none of them waits there after, read
    /// again, since the service answers a take success when no word it names waits. Never faults.
    /// </summary>
    public async Task<bool> TakeAsync(ServiceClient service, CancellationToken ct = default)
    {
        if (_words.Count == 0) return true;
        try
        {
            await service.TakenAsync(To, Ids, by: null, ct).ConfigureAwait(false);
            return await service.RecordAsync(To, ct).ConfigureAwait(false) is { } after
                   && !after.Waiting.Any(word => word.Id is { } id && Ids.Contains(id, StringComparer.Ordinal));
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or ObjectDisposedException or System.Text.Json.JsonException
                                          || error is OperationCanceledException)
        {
            return false;
        }
    }
}
