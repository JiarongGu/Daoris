namespace Daoris.Driver;

/// <summary>
/// What a run that resumes a harness conversation carries, and what it learns on the way (ANSWER1a, D131 §1; MSG1b, D137
/// §2.2): the conversation, the person's words that are its next prompt, and the run's first line; whether the agent would
/// not resume it, whether the native door's harness named its conversation at all, and whether the words went.
/// </summary>
/// <param name="words">The person's words waiting on the record, in the order said: a park's answer, or words to an ended record.</param>
/// <param name="appendix">
/// What the prompt carries after the person's words: the answers to the go-aheads it asked, which it was not handed at its
/// start (KNOWUSE1a). The record keeps only the person's words as theirs. Null or empty for none.
/// </param>
/// <param name="files">
/// Where each word's files are kept (MSG1d3, D137 §2.4): the record names them, and the run is handed where they lie, as a
/// conversation's message is (CONV4c). Read once, as the ask is made. Null hands the words alone.
/// </param>
internal sealed class ResumeAsk(
    string conversation, IReadOnlyList<SaidWordView> words, string firstLine, string? appendix = null,
    Func<SaidWordView, IReadOnlyList<KeptFile>>? files = null)
{
    /// <summary>The first line with its lines by code (CONVNOTE2a): its English is the line the ask keeps.</summary>
    public ResumeAsk(
        string conversation, IReadOnlyList<SaidWordView> words, Noted firstLine, string? appendix = null,
        Func<SaidWordView, IReadOnlyList<KeptFile>>? files = null)
        : this(conversation, words, firstLine.Note, appendix, files) => FirstParts = firstLine.Parts;

    /// <summary>The first line's parts, where it was handed with them; null for English alone.</summary>
    private IReadOnlyList<NotePart>? FirstParts { get; }

    // Each word's files, by its place in the words; read once, so both doors are handed the same.
    private readonly IReadOnlyList<IReadOnlyList<KeptFile>> _files =
        [.. words.Select(word => files is null || word.Files.Count == 0 ? Array.Empty<KeptFile>() : files(word))];

    public string Conversation => conversation;

    /// <summary>
    /// The person's words of the turns its account's limit refused (LIMITRESUME1c, D206 §3.5; <see cref="LimitHeld"/>), handed again
    /// first, each with its files: the record already shows them where they were said, so the opening never says them again, and no
    /// id takes them off anything. Empty for every other resume.
    /// </summary>
    public IReadOnlyList<ChatMessage> Resent { get; init; } = [];

    /// <summary>The person's words, in the order said, each with the id its record's <c>said</c> gave it.</summary>
    public IReadOnlyList<SaidWordView> Words => words;

    /// <summary>
    /// Every word's files in the order said (MSG1d3): the protocol door's links after the words' blocks, and what a run is
    /// handed a read of. Empty where none was said, or none is kept any more.
    /// </summary>
    public IReadOnlyList<KeptFile> Files => [.. Resent.SelectMany(word => word.Files), .. _files.SelectMany(each => each)];

    /// <summary>
    /// The person's words joined by a blank line, as the native door's one argument carries them: each with where its files
    /// are kept beneath it, as a conversation's message names them on that door (MSG1d3, <see cref="ChatFiles.PathLines"/>).
    /// </summary>
    /// <remarks>A word pressed as an answer of its park's question is handed as that answer chosen (<see cref="SaidWordView.Handed"/>, PARKVIEW1).</remarks>
    public string Answer => string.Join(
        "\n\n",
        [.. Resent.Select(word => word.Text + ChatFiles.PathLines(word.Files)), .. words.Select((word, at) => word.Handed + ChatFiles.PathLines(_files[at]))]);

    /// <summary>What the conversation is resumed with on the native door: the words, then the appendix after a blank line.</summary>
    public string Prompt => appendix is { Length: > 0 } more ? $"{Answer}\n\n{more}" : Answer;

    /// <summary>The same on the protocol door (D137 §2.2): each word its own text block in order, then the appendix as one more.</summary>
    public IReadOnlyList<string> Blocks =>
        [.. Resent.Select(word => word.Text), .. words.Select(word => word.Handed), .. appendix is { Length: > 0 } more ? new[] { more } : []];

    /// <summary>The ids of the words its record keeps, for the service to take off once the session took them; none from before <c>said</c>.</summary>
    public IReadOnlyList<string> Ids => [.. words.Select(word => word.Id).OfType<string>()];

    /// <summary>Why the agent would not resume it, said by the door; null while nothing refused.</summary>
    public ContinueReason? Refused { get; set; }

    /// <summary>The native door's harness named its conversation on its <c>init</c> line, so it opened it.</summary>
    public bool Named { get; set; }

    /// <summary>
    /// Whether the native door's one retry of this resume was made (SESSCTX1c, D189's amendment): a harness that ended on a failed exit
    /// before it named its conversation is tried once more, and only a second such end reads as <see cref="ContinueWhy.Exited"/>.
    /// </summary>
    public bool Retried { get; private set; }

    /// <summary>The same resume, tried once more (SESSCTX1c): nothing the first try learned of the wire stands, and its opening stays said.</summary>
    public void Again()
    {
        Retried = true;
        Named = false;
        Refused = null;
    }

    /// <summary>
    /// The words went to the session (MSG1b): the protocol door's first prompt is on the wire, or the native door's harness
    /// opened the conversation its argument carried them to. Only then are they taken off the record.
    /// </summary>
    public bool Prompted { get; set; }

    /// <summary>The run's opening was written to the record: the person's words are there, beneath what they answer.</summary>
    public bool Spoken { get; private set; }

    /// <summary>
    /// The second opinions whose findings are among the words (XAGENT1e, D155 point 7), each once, in the order handed: what
    /// the run's end reads the answers of.
    /// </summary>
    public IReadOnlyList<string> Opinions => [.. words.Select(word => word.By).OfType<string>().Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// The record's opening for the resumed run: the driver's first line, then each of the person's words as theirs, under
    /// the id their record gave it, so the words shown while they waited pair with where the session took them (D137 §3.1).
    /// Another agent's findings (XAGENT1e, D155 point 7) are never the person's: they are the turn Daoris composed around that
    /// agent's claims, naming the opinion they are from. Daoris's own words (COWORK1b) are Daoris's, by their mark.
    /// </summary>
    public IReadOnlyList<SessionEvent> Opening()
    {
        Spoken = true;
        return
        [
            new SessionEvent { Kind = SessionEventKind.Note, Text = firstLine, Parts = FirstParts is { Count: > 0 } ? FirstParts : null },
            .. words.Select(word => new SessionEvent
            {
                Kind = SessionEventKind.User,
                Origin = word.Persons ? "person" : "target",
                From = word.Daoris ? DaorisWords.From : null,
                Opinion = word.By,
                // COWORK1e: a helper's answer by its mark, which the answers-alone bound counts (AnswersAlone).
                Helper = word.Helper,
                // PARKVIEW1: a word pressed as an answer of its park's question keeps that mark where the session took it.
                Choice = word.Choice,
                Id = word.Id,
                Text = word.Text,
                Files = word.Files.Count > 0 ? word.Files : null,
            }),
        ];
    }
}
