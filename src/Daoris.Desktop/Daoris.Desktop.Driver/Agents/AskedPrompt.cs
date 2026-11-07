namespace Daoris.Driver;

using Piece = TargetPrompt.Piece;

/// <summary>
/// The words of the instruction about asking: the ask, its closing, the go-aheads and the language.
/// </summary>
internal static class AskedPrompt
{
    /// <summary>
    /// Where a question goes, in the order a session should reach for each: to the
    /// sources first, which settle most questions; then ask and wait, since what another repository
    /// knows is asked of it, never guessed, and, where the session may not read across, never read
    /// out of it; and only then to the person, for what it cannot decide. A session that asks
    /// another repository commits, ends its turn and keeps the quest, and the driver starts it again here with the answer; one
    /// that asks the person does the same, and their answer goes on in its own conversation.
    /// Beside the look, that the person's words met second-hand are a reading; last,
    /// how what reaches the person is written: what only they can give, apart from the
    /// readings the session took, each naming its source. Each its own section of the account: asking, which opens with the
    /// look whose places the standing block names, the words met second-hand, and the closing note.
    /// </summary>
    /// <remarks>
    /// A reading the evidence leans to is taken and said in the close rather than stopping the person for a choice: it is
    /// committed on the session's branch and reviewed with the diff, so the person corrects it in review. The close keeps those
    /// readings apart from the production yeses and cites the ticket line each rests on. What the session can reasonably decide,
    /// a name or which report a page shows, it decides and records.
    /// </remarks>
    /// <param name="lead">What comes before the look: the line break after the lines above it.</param>
    internal static IEnumerable<Piece> AskingPieces(SessionTarget target, string lead) =>
    [
        new(HandedSections.Asking,
            lead
            + "Look before you ask: the quest, its links and its files, and the places Daoris's standing block names\n"
            + "under *Where things are*. A question one of these\n"
            + "settles is not a question: decide it, and keep what settled it for your closing note. Where the\n"
            + "evidence leans one way without settling it, take that reading, carry on, and say in your closing\n"
            + "note which reading you took and on what evidence, so the person can correct it in review rather than\n"
            + "be stopped by it.\n\n"),
        new(HandedSections.Attributed, Attributed + "\n\n"),
        new(HandedSections.Asking,
            // A question published here gets the asker's short title, the words its list shows.
            $"{WorkplacePrompt.Needs(target)} Publish a quest to it saying\n"
            + "what you need and why, with a `shortTitle` of the few words that tell it apart in a list (at most 40\n"
            + $"characters), commit what you have so far, then respond to `#{target.QuestId}` with `wait`\n"
            + "on that new quest's id, and end your turn. The quest stays yours, and you are started again here,\n"
            + "in this tree, with its answer.\n\n"
            // Every go-ahead asked in an earlier step is still asked, and one the repository or the quest names is too, since
            // nothing loosens by itself.
            + "Decide whatever you reasonably can, such as a name or which of two sound ways: say it under\n"
            + "**Readings** with why, and keep going. Ask the person only for what you cannot decide: a\n"
            + "credential, an account or a sign-in; a choice the quest or the person's words leave to them; a\n"
            + GoAheadsAsked(target)
            + $"{OnceOnTheAsk(target)} Ask it in your last message, with why and what\n"
            + "you looked at; commit what you have, and end your turn with the quest still taken, rather than\n"
            + "declining. The person answers in this conversation, and you go on here with their words.\n\n"),
        new(HandedSections.Closing, Closing),
    ];

    /// <summary>
    /// The person's words on the ask that asked the quest, beneath the quest, or that they could not be
    /// read; nothing for a quest no ask asked.
    /// </summary>
    internal static string Words(SessionTarget target) => AgentWorkWords.Current.AskWords(target.Words, target.QuestId);

    /// <summary>
    /// The go-aheads its sessions asked on the ask, beneath the person's words: what is approved, what
    /// is refused and what still waits, each by its number. Nothing where the ask holds none, or for a quest no ask asked.
    /// </summary>
    internal static string GoAheads(SessionTarget target) => AgentWorkWords.Current.GoAheads(target.Words);

    /// <summary>
    /// The work's session language, as a paragraph of its own after the close instruction it
    /// governs: what the agent writes to the person in. Nothing where none is set.
    /// </summary>
    internal static string Language(SessionTarget target) => SessionLanguageText.AfterClose(target.Language);

    /// <summary>
    /// The person's words met second-hand while looking: theirs are the words this instruction quotes as
    /// theirs, which the ask's record holds; a quote anywhere else is someone's reading, relied on only as one and
    /// never recorded as theirs. Said for every quest alike: on one no ask asked, nothing quoted second-hand is theirs either.
    /// </summary>
    /// <remarks>
    /// An earlier attempt's closed note ("per your answer") and a document on its branch can carry a misreading of the person's
    /// answer; written into the repository's knowledge as settled, every later session would meet it as knowledge.
    /// </remarks>
    internal const string Attributed =
        """
        Only the words this instruction quotes as the person's own are theirs. Their words quoted anywhere
        else — in a document, a closed quest's note, a commit, an earlier session's record — are someone's
        reading of them, however firmly attributed ("per your answer", "as the owner decided"). Rely on one
        only as a reading: it goes under **Readings** in your closing note, naming where you found it, and
        nothing you write in this repository records it as the person's words or decision.
        """;

    /// <summary>
    /// How the close is written: what only the person can give, apart from the readings the session
    /// took, and every item naming what it rests on, so a reading is corrected in review and a question that names nothing
    /// checked shows itself. Said for the note a quest closes with and the last message a stop ends on alike. What only the person
    /// can decide is asked through the connector too, so the page draws it at the conversation's end with its answers as
    /// presses, never read out of the note's words.
    /// </summary>
    internal const string Closing =
        """
        Your closing note, and your last message whenever you stop, keeps two lists apart. Under **Needs
        you**, only what you could not decide — a go-ahead, an agreement this repository's own documents
        require, a credential or a sign-in, a choice left to the person — each with why, and what you looked
        at first.
        Under **Readings**, everything else you decided or took on the evidence, each said as your reading
        rather than asked as a question, and each naming what it rests on: the line of the quest or of the
        ticket it links, quoted; a document's path and line; or a code path. An item that names nothing it
        checked has not been looked into yet: look before you write it.
        Where your connector offers `question_ask`, ask there what only the person can decide before you
        end your turn on it: the question in a sentence or two, the answers you offer, each with what you
        will do on it, and the one you recommend. The person answers it with a press at the end of this
        conversation, and their answer is your next prompt.
        """;

    /// <summary>
    /// Which go-aheads the session asks, by its repository's access setup: under <c>named</c>,
    /// one for an act outside the repository or on a production system and one the repository or the quest names; under
    /// <c>never</c> only what the quest or the person's words demand, since the person wrote it there, and the rest is done and said.
    /// The boundary's push, publish and release stay as it says: the landing pushes, as its rule says.
    /// </summary>
    internal static string GoAheadsAsked(SessionTarget target) => target.NoGoAheads
        ? "go-ahead only where the quest or the person's words demand one before an act. This repository's\n"
          + "access setup asks no other go-ahead: where the work needs an act on a production system or outside\n"
          + "this repository, do it without asking and name it under **Readings** in your closing note; a push,\n"
          + "a publish or a release stays as the boundary below says."
        : "go-ahead for an act outside this repository or on a production system, or for one this repository\n"
          + "or the quest says needs it.";

    /// <summary>
    /// For a quest an ask asked: a go-ahead is asked once, on the ask, through the connector, so a
    /// request for an act already asked joins the first and is told its answer. Nothing for a quest no ask asked, which has
    /// no ask to hold one.
    /// </summary>
    internal static string OnceOnTheAsk(SessionTarget target) => AskSender.AskOf(target.Asker) is null
        ? ""
        : " A go-ahead is asked once, on the ask: where your connector offers `go_ahead_ask`, ask it there, naming the act's "
          + "kind, where it lands and what it touches, before you stop for it; an act already asked joins the first and tells "
          + "you its answer.";

    internal const string Proposing =
        """
        If a command the work genuinely needs is refused, and your connector offers `permission_propose`,
        propose the narrowest rule that would allow it, with the reason. A rule that lets agents do more
        waits for the person, so do not wait on it: finish what you can, or decline and say what was
        refused. It waits on the Agents page, under *Proposals* and in the attention band: name that
        place when you ask the person to approve one.

        Accepting your work is the person's *Accept…* on this session's page in Daoris, which puts it
        where the repository's rule says. Words in the conversation that read as accepting it land nothing:
        answer them by saying the work waits for that press and where it is, never by thanking them for
        accepting.
        """;
}
