using Daoris.Contracts.Quests;

namespace Daoris.Contracts.Asks;

// An ask as every door answers it. Its files' paths answer only a caller on a local host's own machine, the quest files' rule.

/// <summary>What the page reads an ask's and a go-ahead's states as, which the wire carries as a word.</summary>
public static class AskPageTypes
{
    public const string State = "'Open' | 'Proposed' | 'Published' | 'Done' | 'Closed'";

    public const string GoAheadState = "'asked' | 'approved' | 'refused'";
}

/// <summary>A repository the declarations tier proposed, with the words its declarations share with the ask.</summary>
public sealed record DeclarationMatchResponse(string Repository, int Score, IReadOnlyList<string> Matched);

/// <summary>
/// An ask: a sentence at a workspace, held as a record of what became of it. <c>Tier</c> is said on every record, which tier answered,
/// never implied; <c>Intake</c> is the session that served it, once one opened. <c>State</c> is the desk's reading, <c>Done</c> included
/// once every quest it became has closed; <c>Deletable</c> whether the desk would delete it with every quest asked by it.
/// </summary>
/// <param name="Words">Every word the person gave on it, oldest first: its sentence, then each answer and each message added to a session.</param>
/// <param name="WordsKeptFrom">Said only of an ask made before the words were kept: from when they are, since nothing is back-filled.</param>
/// <param name="GoAheads">Every go-ahead its sessions asked the person for, oldest first, one per act.</param>
/// <param name="ReviewChoices">The person's review choices, oldest first, the latest standing; absent where there is none.</param>
/// <param name="ReviewProposals">Its intake's proposals, each with its reason and the quest it came with; absent where there is none.</param>
/// <param name="WorkflowChoices">The person's kind and workflow choices, oldest first, the latest standing; absent where there is none.</param>
/// <param name="WorkflowProposals">Its intake's kind proposals, kept beside the choices and applied only by the person's press; absent where there is none.</param>
/// <param name="Short">The asker's short title; null for none.</param>
/// <param name="Paths">The paths its asker declared, empty for none.</param>
public sealed record AskResponse(
    string Id, string Workspace, string Sentence, [property: PageType(AskPageTypes.State)] string State, string Tier, DateTimeOffset Asked,
    DateTimeOffset Updated, string? Asker, string? Note, IReadOnlyList<string> Links,
    IReadOnlyList<QuestAttachmentResponse> Attachments, IReadOnlyList<DeclarationMatchResponse> Proposal,
    IReadOnlyList<string> Quests, string? Intake = null, bool Deletable = false,
    IReadOnlyList<AskWordResponse>? Words = null, DateTimeOffset? WordsKeptFrom = null,
    IReadOnlyList<GoAheadResponse>? GoAheads = null,
    IReadOnlyList<AskReviewChoiceResponse>? ReviewChoices = null, IReadOnlyList<AskReviewProposalResponse>? ReviewProposals = null,
    IReadOnlyList<AskWorkflowChoiceResponse>? WorkflowChoices = null, IReadOnlyList<AskWorkflowProposalResponse>? WorkflowProposals = null,
    string? Short = null,
    IReadOnlyList<string>? Paths = null);

/// <summary>An intake's kind proposal, with its reason: the person's own words it quotes.</summary>
public sealed record AskWorkflowProposalResponse(string Kind, string Reason, DateTimeOffset At, string? Session, string? Quest);

/// <summary>The person's review choice: <c>off</c>, <c>on</c> or an environment's name, with their words.</summary>
public sealed record AskReviewChoiceResponse(string Choice, DateTimeOffset At, string? Words);

/// <summary>An intake's review proposal, with its reason and the quest it came with.</summary>
public sealed record AskReviewProposalResponse(string Choice, string Reason, DateTimeOffset At, string? Session, string? Quest);

/// <summary>
/// One word: <c>asked</c>, <c>answered</c>, <c>added</c> or <c>reopened</c> (said after its session ended, which went on with it), a
/// review's <c>reviewed</c>, <c>not-yet</c> or <c>skipped</c>, or a <c>channel</c> message; the ask's own sentence names no session.
/// </summary>
public sealed record AskWordResponse(string Kind, string Text, DateTimeOffset At, string? Session, string? Quest);

/// <summary>
/// One go-ahead: the act by its <c>Kind</c> (write, release, push, sign-in, run), where it lands (<c>On</c>) and what it touches
/// (<c>Act</c>). <c>Asked</c> is each session's request, oldest first; <c>Answer</c> the person's, absent while it waits; <c>Near</c> the
/// go-ahead whose words it shared where they could not tell.
/// </summary>
public sealed record GoAheadResponse(
    int Number, string Kind, string On, string Act, [property: PageType(AskPageTypes.GoAheadState)] string State,
    IReadOnlyList<GoAheadRequestResponse> Asked, GoAheadAnswerResponse? Answer, int? Near);

/// <summary>One session's request for a go-ahead, in its words.</summary>
public sealed record GoAheadRequestResponse(string Session, string? Quest, DateTimeOffset At, string Why);

/// <summary>The person's answer to a go-ahead.</summary>
public sealed record GoAheadAnswerResponse(bool Approved, string? Words, DateTimeOffset At);

/// <summary>What a door did to an ask: the ask as it now stands, the desk's sentence, and the quest it published, where it did.</summary>
public sealed record AskActionResponse(AskResponse Ask, string Message, QuestResponse? Quest);

/// <summary>What a message reached in one repository it addresses; never more than is observed.</summary>
public sealed record ChannelReceiptResponse(string Repository, string State);

/// <summary>
/// One item of the channel, in time order: a <c>message</c> or one of Daoris's coded <c>line</c>s, read from the ask's quests and never
/// stored. A message's <c>To</c> absent is everyone; <c>Person</c> true is the person's own words, a session's that agent's claim;
/// <c>Urgent</c> absent only on one from before. Read for an entry, its <c>Ack</c> and <c>AckSession</c>; for the person,
/// <c>Receipts</c>, one per repository it addresses. A message to one part names its <c>Part</c>, its <c>Channel</c> and, for the
/// person, its <c>Receipt</c>.
/// </summary>
public sealed record ChannelItemResponse(
    string Kind, DateTimeOffset At, string? Id = null, bool? Person = null, string? Session = null, string? Repository = null,
    string? Agent = null, string? Entry = null, string? From = null, string? To = null, string? Re = null, bool? Question = null,
    bool? Open = null, string? Text = null, string? Origin = null, string? Code = null, string? Quest = null, string? Name = null,
    bool? Urgent = null, string? Ack = null, string? AckSession = null, IReadOnlyList<ChannelReceiptResponse>? Receipts = null,
    string? Part = null, string? Channel = null, string? Receipt = null, bool? Outside = null);

/// <summary>
/// The channel as one entry reads it: every item (or those after <c>since</c>), <c>Unread</c> against the entry's <c>Mark</c> (absent for
/// none). The person's <c>Unread</c> counts only what an agent or a teammate wrote, never their own posts nor Daoris's lines.
/// </summary>
public sealed record ChannelResponse(string Ask, string Entry, IReadOnlyList<ChannelItemResponse> Items, int Unread, DateTimeOffset? Mark)
{
    /// <summary>For the person, the messages to one part each, never among <c>items</c>, which an older page reads as to everyone.</summary>
    public IReadOnlyList<ChannelItemResponse>? Addressed { get; init; }
}

/// <summary>The message the person's post kept.</summary>
public sealed record ChannelPostResponse(ChannelItemResponse Message);

/// <summary>The ids an acknowledgment changed.</summary>
public sealed record ChannelAckResponse(IReadOnlyList<string> Acknowledged);

/// <summary>
/// An ask the mirrored messages name that this machine does not hold, with no title since no ask record is here: the workspace is from
/// the messages, <c>Origins</c> the machines they came from, and <c>Unread</c> is against the person's local mark.
/// </summary>
public sealed record ElsewhereAskResponse(
    string Id, string Workspace, IReadOnlyList<string> Origins, DateTimeOffset Latest, int Messages, int Unread);

/// <summary>The asks held elsewhere, newest message first.</summary>
public sealed record ElsewhereAsksResponse(IReadOnlyList<ElsewhereAskResponse> Asks);
