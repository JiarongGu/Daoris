using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The claiming instruction (D46 §3), composed once and delivered per harness. Project-agnostic on
/// purpose: it travels to repositories that know nothing of this one's decision numbering, so it
/// speaks in the canon's names, never in D-numbers.
/// </summary>
public static class TargetPrompt
{
    /// <summary>
    /// Bind the maker of this start, including a start continuing another maker's work: told that maker's trailer too where the part
    /// before it left work uncommitted in the tree (MAKERTRAIL2). Its standing block is bound to the harness too: the tree's skills where
    /// it loads none itself, and the rules where it cuts the native instruction file (CONTEXTONE1c).
    /// </summary>
    internal static SessionTarget ForStart(SessionTarget target, ProviderRegistry adapters, AgentDoor adapter) =>
        target with
        {
            CommitTrailer = CommitTrailers.For(adapters, adapter, target.InFlight.Count > 0 ? target.LeftBy : null),
            Block = target.Block is { } block ? AgentContext.Current.OnHarness(block, adapter, target.Root) : null,
        };

    /// <summary>
    /// The instruction a session is handed — an intake's own where the target carries one, so every
    /// door that delivers a target (the pipe's argument, the protocol's prompt, <c>DAORIS_TARGET</c>)
    /// delivers the same words without knowing which kind of session it is.
    /// </summary>
    public static string Compose(SessionTarget target) => Composed(target).Text;

    /// <summary>
    /// The instruction a session is handed, and the composer's account of it (CONTEXT1, D143 point 1): the same words as
    /// <see cref="Compose"/>, joined from named pieces, each counted as it joins, so the account's sizes add up to the text.
    /// </summary>
    public static ComposedInstruction Composed(SessionTarget target)
    {
        if (target.Prompt is { } prompt) return AgentContext.Current.Intake(target, prompt, Block(target, prompt.Length));

        var pieces = target.Answered is { } answered ? ResumedPrompt.ResumingPieces(target, answered)
            : target.CutOff is { } cutOff ? ResumedPrompt.CarryingOnPieces(target, cutOff)
            : ClaimedPrompt.ClaimingPieces(target);
        return AgentContext.Current.Account(target, pieces, Block(target, pieces.Sum(piece => piece.Text.Length)));
    }

    /// <summary>
    /// The standing block the instruction ends with (CONTEXTONE1c), fitted to the <paramref name="before"/> characters composed ahead of
    /// it: inline, or the line naming its file; null where the target carries none.
    /// </summary>
    internal static StandingBlockTold? Block(SessionTarget target, int before) =>
        target.Block is { } facts ? AgentContext.Current.Block(facts, before) : null;

    /// <summary>Whether the target's standing block goes through its file (CONTEXTONE1c): what the start writes, and lets the session read.</summary>
    internal static bool Filed(SessionTarget target) => Block(target, Before(target)) is { Filed: true };

    private static int Before(SessionTarget target) => target.Prompt is { } prompt
        ? prompt.Length
        : (target.Answered is { } answered ? ResumedPrompt.ResumingPieces(target, answered)
            : target.CutOff is { } cutOff ? ResumedPrompt.CarryingOnPieces(target, cutOff)
            : ClaimedPrompt.ClaimingPieces(target)).Sum(piece => piece.Text.Length);

    /// <summary>A piece of an instruction: the section it belongs to, and its words.</summary>
    internal readonly record struct Piece(string Section, string Text);

    /// <summary>
    /// Who the agent is and the quest: the opening line, the line naming the quest and how this start stands to it, then the
    /// quest's title and body, as every instruction begins.
    /// </summary>
    internal static Piece Head(SessionTarget target, string line) => new(
        HandedSections.Quest,
        $"You are the agent for `{target.Repository}`, working inside its own repository and nowhere else.\n\n{line}\n\n"
        + $"# {target.Title}\n\n{target.Body}\n");

    /// <summary>What the quest carries and what the person said about it, beneath it, as every instruction carries them.</summary>
    internal static IEnumerable<Piece> Beneath(SessionTarget target) =>
    [
        new(HandedSections.Carried, ResumedPrompt.Carried(target)),
        new(HandedSections.Requirements, RequirementsPrompt.Required(target)),
        new(HandedSections.Words, AskedPrompt.Words(target)),
        new(HandedSections.GoAheads, AskedPrompt.GoAheads(target)),
        // What waited on the ask's channel for this line of work (SESSONE2b), counted with what the sessions before it left, as the
        // parts before it are (SESSONE1d), so no page catalogue changed.
        new(HandedSections.CarryOn, AgentWorkWords.Current.Channel(target.Channel)),
        // CONTEXTDRIVE1b: a start's line's course, counted in the brief's own section; a carry-on's brief carries it among its own.
        .. (target.Course?.Pieces ?? []).Select(piece => new Piece(piece.Section, piece.Text)),
    ];

    /// <summary>
    /// The close paragraph and everything after it, as every instruction ends before its standing block: the language line inside the
    /// close's line, the landing and the checkouts to read, asking, the closing note, proposing and the boundary.
    /// </summary>
    internal static IEnumerable<Piece> Tail(SessionTarget target, string close) =>
    [
        // What its start did against the line, in a tree that stands (LANDSYNC2): the driver's own words, so counted with the close
        // they come before, and no page catalogue changed.
        new(HandedSections.Close, target.InSync is { Length: > 0 } told ? $"\n{told}\n" : ""),
        // What its tree's open copied of the checkout's dependencies (TREEDEPS1): the driver's own words, counted with the close.
        new(HandedSections.Close, target.Dependencies is { Length: > 0 } copied ? $"\n{copied}\n" : ""),
        // Its repository's heavy gates, once at its end through the slot (HEAVYRUN1b1): the driver's own words, counted with the close.
        new(HandedSections.Close, target.Heavy is { Length: > 0 } heavy ? $"\n{heavy}\n" : ""),
        // A set-up step's own section (REVIEWENV1c, design §2.2), before the close it changes, and accounted as its own since the
        // page words it (REVIEWENV1c2).
        new(HandedSections.SetUp, WorkplacePrompt.SetUp(target)),
        new(HandedSections.Close, close),
        new(HandedSections.Close, target.CommitTrailer is { } trailer ? $"\n\n{trailer}\n" : ""),
        new(HandedSections.Language, AskedPrompt.Language(target)),
        new(HandedSections.Close, "\n"),
        new(HandedSections.Landing, WorkplacePrompt.Landing(target)),
        // The browser handed, a block of its own (BRWHINT1b); the driver's own words, so counted with the close.
        new(HandedSections.Close, target.Browser is { } browser ? $"\n{browser}\n" : ""),
        new(HandedSections.Reading, WorkplacePrompt.Reading(target)),
        .. AskedPrompt.AskingPieces(target, lead: "\n"),
        new(HandedSections.Proposing, "\n\n" + AskedPrompt.Proposing),
        new(HandedSections.Boundary, "\n\n" + WorkplacePrompt.Boundary(target)),
        // A held remote's Git section (GITAUTH1a, the git-auth design §5.3): the driver's own words, beside the boundary and counted
        // with it.
        new(HandedSections.Boundary, target.Git is { Length: > 0 } git ? $"\n\n{git}" : ""),
    ];

    /// <summary>
    /// How a session ends a quest its closure graph closes (FABRIC-CLOSE1l, folding CLOSE1g; the fabric events design §E5): `ready`, never
    /// `done`, which the service refuses on a bound quest, naming `ready`.
    /// </summary>
    internal const string ReadyWords =
        "Never `done`: its graph refuses it, and closes\nthe quest itself once its holds and gates are through.";

    /// <summary>
    /// Why the session before did not finish: cut off (D80), or stopped to ask the person, who has
    /// answered (STANDDOWN2) — in their words, which are the reason this session exists — or stopped by the
    /// person, who has since released the quest (SESSUX1b).
    /// </summary>
    /// <remarks>
    /// The ask's words are the source of what the person said (DRIFT1b, D133 §2): an answer quoted among them above is
    /// pointed to rather than quoted twice. One that is not there, on a quest no ask asked, an answer given before the
    /// words were kept, or words that could not be read, is quoted here as it always was.
    /// </remarks>
    internal static string Before(SessionTarget target, string record) => target.PersonSaid is { } said
        ? (AgentWorkWords.Current.Quotes(target.Words, said)
            ? "An earlier session on this quest stopped to ask the person, and they answered: their answer is quoted among "
              + $"their words above.\n\nIts record reads: {record}"
            : $"An earlier session on this quest stopped to ask the person, and they answered:\n\n"
              + TextBounds.Quote(said, bareBlank: false) + $"\n\nIts record reads: {record}")
        : target.HandedOver is { } agent
            ? $"An earlier session on this quest, run by {agent}, was stopped so that the quest could go on with you: it was handed "
              + $"over, and you carry on from {(target.Brief is null ? "what it left" : "Daoris's brief of its line")} below. Its record reads: {record}"
        : target.Released
            ? "An earlier session on this quest was stopped by the person before it closed it, and they have since released "
              + $"the quest for you to carry on. Its record reads: {record}"
            : target.BroughtForward
                ? $"The person brought forward the work of an earlier session on this quest, whose take of it had lost. Its record reads: {record}"
            : $"An earlier session on this quest was cut off before it closed it: {record}";

    /// <summary>
    /// The most characters of the requirements' words and checks an instruction carries, for the person's words' reason
    /// (<see cref="AskWordsText"/>): the instruction is one value of the first prompt, which <c>DAORIS_TARGET</c> carries and
    /// Windows caps (<see cref="StandingBlock.ArgumentLimit"/>). The service bounds each half at 2,000 characters and a quest at 20,
    /// which together would not fit.
    /// </summary>
    internal const int RequirementsLimit = 8_000;

    /// <summary>The most earlier parts a further part of a session is told of by name (SESSONE1d): the newest, the rest counted.</summary>
    internal const int PartsShown = 3;

    internal static string DaorisWaited(IReadOnlyList<SaidWordView> words) => ResumedPrompt.DaorisWaited(words);

    internal static int RequirementsShown(IReadOnlyList<QuestRequirementView> requirements) =>
        RequirementsPrompt.RequirementsShown(requirements);
}
