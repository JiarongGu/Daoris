using System.Text.Json;
using System.Text.Json.Serialization;
using Daoris.Knowledge;

namespace Daoris.Knowledge.Http;

// The wire shapes not yet declared in Daoris.Contracts beside their routes, and their source-generated serializer
// context. A domain's shapes move there with its routes.

// An ask (D65 §1a): a sentence at a workspace. Files arrive whole — the ask door is a local host's —
// and `To` is the asker naming the receiver, which publishes at once.
// `Review` and `ReviewWords` (REVIEWENV1b, design §1.5): the person's review choice from the composer, and their words with it.
// `Kind`, `Workflow` and `WorkflowWords` (WORKFLOW1e, the workflow design §4.3): the person's kind and workflow for its work.
// `Short` (HEAD2e): the asker's short title, at most 40 characters, judged as a quest's is; absent for none.
// `Paths` (INTEGRATE1l): the paths the asker declares the work touches, judged as a quest's are; each quest it publishes carries them.
public sealed record AskRequestBody(
    string? Workspace, string? Sentence, IReadOnlyList<string>? Links,
    IReadOnlyList<QuestAttachmentRequest>? Attachments, string? To, string? Review = null, string? ReviewWords = null,
    string? Kind = null, string? Workflow = null, string? WorkflowWords = null, string? Short = null,
    IReadOnlyList<string>? Paths = null);
// A publish is a person's `To` alone; an intake (D65 §1b) adds its own words, carry and chain, and
// names its `Session` — which moves the ask's tier to `intake` only when it is the ask's own. Its
// `Requirements` quote the person (DRIFT1c), judged against the ask's words. `Review` is the chain's choice, an intake's only on
// the person's quoted words; `ReviewProposal` an intake's proposal with its reason, kept on the ask (REVIEWENV1b).
public sealed record AskPublishRequest(
    string? To, string? Title = null, string? Body = null, IReadOnlyList<string>? Links = null,
    IReadOnlyList<QuestAttachmentRequest>? Attachments = null, IReadOnlyList<QuestStepWire?>? Then = null,
    string? Session = null, IReadOnlyList<QuestRequirementWire?>? Requirements = null, string? Short = null,
    QuestReviewWire? Review = null, ReviewProposalWire? ReviewProposal = null, WorkflowProposalWire? WorkflowProposal = null,
    // INTEGRATE1l: the paths an intake declares, added to the ask's own and never in place of them.
    IReadOnlyList<string>? Paths = null);
// An intake's review proposal (REVIEWENV1b, design §1.5): a choice and its reason, at most 300 characters.
public sealed record ReviewProposalWire(string? Choice, string? Reason);
// An intake's kind proposal (WORKFLOW1i, design §4.3): a kind and its reason, the person's own words it quotes, at most 300 characters.
public sealed record WorkflowProposalWire(string? Kind, string? Reason);
// The person's review choice on an ask's page (REVIEWENV1b, design §1.5), and their words with it.
public sealed record AskReviewRequest(string? Choice, string? Words = null);
// The person's kind and workflow on an ask (WORKFLOW1e, the workflow design §4.3): either, both, or neither to clear them.
public sealed record AskWorkflowRequest(string? Kind = null, string? Workflow = null, string? Words = null);
public sealed record AskCloseRequest(string? Reason);
public sealed record DeclarationMatchResponse(string Repository, int Score, IReadOnlyList<string> Matched);
// `Tier` is said on every record (model-decoupling): which tier answered, never implied. `Intake` is
// the session that served it (D65 §1b), once one opened — the record a reader follows to its question.
// `State` is the desk's reading of it, `Done` included (USE1c); `Deletable` whether the desk would delete
// it with every quest asked by it (D95).
// `Words` (DRIFT1a, D133 §1) is every word the person gave on it, oldest first: its sentence, then each
// answer and each message added to a session, with when, the session and its quest. `WordsKeptFrom` is
// said only of an ask made before the words were kept: from when they are, since nothing is back-filled.
// `GoAheads` (KNOWUSE1a, D135 §2) is every go-ahead its sessions asked the person for, oldest first, one per act.
// `ReviewChoices` (REVIEWENV1b, design §1.5) is the person's review choices on it, oldest first, the latest standing;
// `ReviewProposals` its intake's proposals, each with its reason and the quest it came with. Each absent where there is none.
// `WorkflowChoices` (WORKFLOW1e, the workflow design §4.3) is the person's kind and workflow choices on it, oldest first, the
// latest standing, one naming neither a choice cleared; absent where there is none. `WorkflowProposals` (WORKFLOW1i) is its intake's
// kind proposals, each with its reason and the quest it came with, kept beside the choices and applied only by the person's press;
// absent where there is none. `Short` (HEAD2e) is the asker's short title, null for none (every ask from before).
public sealed record AskResponse(
    string Id, string Workspace, string Sentence, string State, string Tier, DateTimeOffset Asked,
    DateTimeOffset Updated, string? Asker, string? Note, IReadOnlyList<string> Links,
    IReadOnlyList<QuestAttachmentResponse> Attachments, IReadOnlyList<DeclarationMatchResponse> Proposal,
    IReadOnlyList<string> Quests, string? Intake = null, bool Deletable = false,
    IReadOnlyList<AskWordResponse>? Words = null, DateTimeOffset? WordsKeptFrom = null,
    IReadOnlyList<GoAheadResponse>? GoAheads = null,
    IReadOnlyList<AskReviewChoiceResponse>? ReviewChoices = null, IReadOnlyList<AskReviewProposalResponse>? ReviewProposals = null,
    IReadOnlyList<AskWorkflowChoiceResponse>? WorkflowChoices = null, IReadOnlyList<AskWorkflowProposalResponse>? WorkflowProposals = null,
    string? Short = null,
    // INTEGRATE1l: the paths its asker declared, empty for none; absent from a host before it.
    IReadOnlyList<string>? Paths = null);
public sealed record AskWorkflowProposalResponse(string Kind, string Reason, DateTimeOffset At, string? Session, string? Quest);
public sealed record AskReviewChoiceResponse(string Choice, DateTimeOffset At, string? Words);
public sealed record AskReviewProposalResponse(string Choice, string Reason, DateTimeOffset At, string? Session, string? Quest);
// One word (DRIFT1a): `kind` is `asked`, `answered`, `added` or `reopened` (MSG1a: said after its session ended, which
// went on with it), or a review's `reviewed`, `not-yet` or `skipped` (REVIEWENV1b); the ask's own sentence names no session.
public sealed record AskWordResponse(string Kind, string Text, DateTimeOffset At, string? Session, string? Quest);
// An ask's channel (SESSONE2a, D167 points 6 to 8). The person's post: `to` is one of the ask's repositories or `everyone`, `re` the id
// of a message it answers.
// COWORK1d: `urgent` false keeps the person's message ordinary; absent, it is their default, urgent (D188 point 2).
public sealed record ChannelPostRequest(string? Text, string? To = null, string? Re = null, bool? Urgent = null);
// Moves an entry's read mark (the person's, `person`, when `entry` is left out) to `through`, else to the newest item.
public sealed record ChannelReadRequest(string? Entry = null, DateTimeOffset? Through = null);
// One item of the channel, in time order: a `message` or one of Daoris's coded `line`s, read from the ask's quests and never stored. A
// message's `to` absent is everyone; `person` true is the person's own words, a session's that agent's claim; `urgent` absent only on
// one from before. Read for an entry, its `ack` and `ackSession`; for the person, `receipts`, one per repository it addresses. A message
// to one part names its `part`, its `channel` and, for the person, its `receipt`.
public sealed record ChannelItemResponse(
    string Kind, DateTimeOffset At, string? Id = null, bool? Person = null, string? Session = null, string? Repository = null,
    string? Agent = null, string? Entry = null, string? From = null, string? To = null, string? Re = null, bool? Question = null,
    bool? Open = null, string? Text = null, string? Origin = null, string? Code = null, string? Quest = null, string? Name = null,
    bool? Urgent = null, string? Ack = null, string? AckSession = null, IReadOnlyList<ChannelReceiptResponse>? Receipts = null,
    string? Part = null, string? Channel = null, string? Receipt = null);
// What a message reached in one repository it addresses (COWORK1d): `waiting`, `held`, `sent`, `taken`, `read`, `partly`, `answered`
// or `not-reached`, never more than was observed.
public sealed record ChannelReceiptResponse(string Repository, string State);
// An entry's acknowledgment of messages by id (COWORK1d): `taking` claims each for `session`'s part, one part's only; `released` lets a
// claim go; `sent`, `taken`, `read` and `partly` raise what the entry holds, never lowering it. Answers the ids it changed.
public sealed record ChannelAckRequest(string? Entry, string? Session, string? State, IReadOnlyList<string>? Messages);
public sealed record ChannelAckResponse(IReadOnlyList<string> Acknowledged);
// The channel as one entry reads it: every item (or those after `since`), `unread` against the entry's `mark` (absent for none). The
// person's `unread` (NOTIFY2g) counts only what an agent or a teammate wrote, never their own posts nor Daoris's lines.
public sealed record ChannelResponse(string Ask, string Entry, IReadOnlyList<ChannelItemResponse> Items, int Unread, DateTimeOffset? Mark)
{
    // For the person, the messages to one part each, never among `items`, which an older page reads as to everyone.
    public IReadOnlyList<ChannelItemResponse>? Addressed { get; init; }
}
public sealed record ChannelPostResponse(ChannelItemResponse Message);
// The asks the mirrored messages name that this machine does not hold (SESSONE2h), newest message first. There is no ask record here,
// so no title: `workspace` is from the messages, `origins` the machines they came from, `unread` is against the person's local mark,
// counted as the person's channel `unread` is (NOTIFY2g).
public sealed record ElsewhereAskResponse(
    string Id, string Workspace, IReadOnlyList<string> Origins, DateTimeOffset Latest, int Messages, int Unread);
public sealed record ElsewhereAsksResponse(IReadOnlyList<ElsewhereAskResponse> Asks);
// One go-ahead (KNOWUSE1a): the act by its `kind` (write, release, push, sign-in, run), where it lands (`on`) and what it
// touches (`act`); `state` is `asked`, `approved` or `refused`; `asked` is each session's request, oldest first; `answer`
// the person's, absent while it waits; `near` the go-ahead whose words it shared where they could not tell.
public sealed record GoAheadResponse(
    int Number, string Kind, string On, string Act, string State, IReadOnlyList<GoAheadRequestResponse> Asked,
    GoAheadAnswerResponse? Answer, int? Near);
public sealed record GoAheadRequestResponse(string Session, string? Quest, DateTimeOffset At, string Why);
public sealed record GoAheadAnswerResponse(bool Approved, string? Words, DateTimeOffset At);
// The person's answer to a go-ahead (KNOWUSE1a): `answer` is `approved` or `refused`; `words` theirs, where they give any.
// `goesOn` false leaves a parked session that waited on it to its caller, which answers that park itself (GOAHEAD2, the
// session page's door); absent or true, the answer that leaves none of a park's go-aheads waiting is its answer.
public sealed record GoAheadAnswerRequest(string? Answer, string? Words, bool? GoesOn = null);
public sealed record AskActionResponse(AskResponse Ask, string Message, QuestResponse? Quest);
// DRIFT1a: what the person added to a running session, as its driver reports it.
public sealed record AddedRequest(string? Text);
// `Kept` false is no error: the session is on no ask, and the message says so. `Ask` is the ask it was kept on.
public sealed record AddedResponse(bool Kept, string Message, AskResponse? Ask);
// `Embedded` is what the semantic half made of the entries (D123): how many, the vectors they became, how
// many were longer than the window and split. Absent when it did not run, and `semanticError` says why.
public sealed record RefreshResponse(
    int Entries, int Repositories, int Withheld, string? SemanticError, IReadOnlyList<string> Absent,
    EmbeddedResponse? Embedded = null, IReadOnlyList<ReadingResponse>? Read = null);
// KNOWCURRENT1: when a repository's checkout was read and the commit it was at (null for a folder with no commit).
public sealed record ReadingResponse(string Repository, DateTimeOffset ReadAt, string? Commit);
public sealed record EmbeddedResponse(int Entries, int Pieces, int Split, int Window);
// `Uses` is what the repository says it depends on (D91); absent is nothing declared.
public sealed record DomainRequest(
    string? Summary, IReadOnlyList<string>? Owns, IReadOnlyList<string>? Accepts, IReadOnlyList<string>? Uses = null);
// `Workspace` is null on the way IN when the client said nothing — which is what preserves the row
// (D48 §2). It is never null on the way out: a reader is told which circle it is looking at.
// `DefaultBranch` is the same shape for the same reason (D48 §6): the checkout that registers knows
// its canonical line and the deployment cannot ask git, but an ordinary registration says nothing
// about it — so null preserves what was declared rather than erasing it. The commit and its base are a
// checkout's, read by the same rules as a feed's (SYNC5b): a shared deployment orders declarations by
// them, and a local host, where the registration is this machine's own, reads none of them.
// `Adopted` is false only from a door that looked and found no manifest — the desktop's *add* of a
// folder that has not adopted (D70). Silence is the connector's `connect`, which runs in an adopter
// and is adoption, so it stays true; a shared deployment takes no false at all (it holds no roots, and
// an unadopted repository is never joined).
// `Lanes` (D115 §2.2) is the words of the lanes the repository declares, never their paths. Null on the
// way in is unstated and preserves the row's, as the workspace does: only `connect` reads the file, and
// it always says, `[]` for none.
public sealed record RegisterRequest(
    string Repository, IReadOnlyList<string>? Packs, DomainRequest? Domain, string? Root,
    bool? Join, bool? ShareKnowledge, string? Workspace, string? DefaultBranch,
    string? Commit = null, DateTimeOffset? CommittedAt = null, string? Branch = null, string? Base = null,
    bool? Adopted = null, IReadOnlyList<LaneWire>? Lanes = null);
// One lane's words (D115 §2.2), the same shape both ways; judged by `Declared.Lanes` on the way in.
public sealed record LaneWire(string? Id, string? Title, string? Summary, bool? Steward);
public sealed record RegisteredResponse(string Repository, DateTimeOffset At, string Workspace);
public sealed record RetiredResponse(string Repository, bool Retired, string Message);
// The repositories this machine's checkouts took out of a circle, not yet told to its deployment (SYNC5b).
public sealed record RetiredPendingResponse(string Workspace, IReadOnlyList<string> Repositories);
public sealed record WireRequest(string? Workspace);
// `Workspace` names the circle every imported row lands in (D77); silence moves nobody.
public sealed record ImportRequest(string? Folder, string? Workspace = null);
public sealed record ImportedResponse(string Folder, int Imported, IReadOnlyList<string> Repositories, string Message)
{
    /// <summary>The circle they were wired to, when the import named one.</summary>
    public string? Workspace { get; init; }
}
// `Addressable` is the exchange's own judgement (D70), answered so no page re-derives it from
// `Adopted`: a repository registered here with a root can be asked without having adopted, and a
// browser that is never told the root must still offer it as a receiver.
public sealed record RegistrationResponse(
    string Repository, bool Adopted, bool Registered, string? Summary,
    IReadOnlyList<string> Owns, IReadOnlyList<string> Accepts, IReadOnlyList<string> Packs, int Entries,
    string? Root, bool Joined, bool SharesKnowledge, string Workspace, string? DefaultBranch, bool Addressable,
    IReadOnlyList<string> Uses, IReadOnlyList<LaneWire>? Lanes = null);
// `Quest` is null for a chat (D49 §3) and `Kind` says which way in it was — the same record either
// way, which is the point: a conversation is a session, not a second kind of thing.
// `HarnessVersion` and `Profile` say which tool and which account produced this (D49 §4), and `Tree`
// which working tree it held (D51). The profile and the tree are machine-local and guarded like the
// transcript beside them: they answer to the machine that ran the session and travel no further — and
// the tree is a filesystem path, which is the sharpest reason of the three.
public sealed record SessionResponse(
    string Id, string? Quest, string Repository, string Adapter, string State,
    string? Note, string? Evidence, string? Transcript, DateTimeOffset Created, DateTimeOffset Updated,
    string Workspace, string Kind, string? HarnessVersion, string? Profile, string? Tree,
    // SURF6: the commit the tree stood at when the spawn began, so the review's range is a fact. It
    // is a repository fact rather than a machine one, but only the machine holding the checkout can
    // do anything with it — so it rides the same loopback gate as the tree beside it.
    string? BaseCommit = null,
    // D65 §1b: the ask an intake answers — null for every other session.
    string? Ask = null,
    // STANDDOWN2: whether it took its own quest, through its own connector. The person's answer to one that parked to ask them is
    // read from `Said` alone (ONEWAY1m).
    bool Took = false,
    // D104: a stop that was not the person's — the sweep's, or a shutdown's — which the driver carries on.
    bool Interrupted = false,
    // TOOL4c (D125 §5.2): a failure an account's limit made. It names no account, so every caller is told.
    bool Limit = false,
    // SESSUX1f (D126 §5.4): whether the ledger would delete its record, D95's way, so the driver offers *Delete…* only
    // where it would be taken. False at a shared deployment, which has no delete door.
    bool Deletable = false,
    // MSG1a (D137 §2.4): the person's words waiting for it to go on with them, in order. Their own words, which may name
    // anything on this machine: answered to it only, like the transcript.
    IReadOnlyList<SaidWordResponse>? Said = null,
    // LANG1a (D142 point 2): the note's lines, each a code with its values or someone's words, beside `note`, which stays as it
    // always read. Null for a record from before parts. To another machine, every string in them cleaned as the note is.
    JsonElement? NoteParts = null,
    // XAGENT1c (D155 point 11): the second opinion a reviewer's record reads another session's work for. This machine's own,
    // like the opinion: answered to it only, and absent for every other session.
    string? Opinion = null,
    // MIXAGENT2a-i (D163 point 2): the session a helper's record answers. This machine's own, like the opinion: answered to it
    // only, and absent for every other session.
    string? Serves = null,
    // MIXAGENT1a-iii (D163 point 4): the session this one continues, why and who asked, by the driver's codes. This machine's own,
    // like the opinion: answered to it only, and absent for a session that continues none.
    string? From = null,
    string? FromWhy = null,
    string? FromBy = null,
    // SESSONE1a (D167 points 2 and 3): the line of work this record belongs to, `<origin>/<repository>` — the key the records that
    // continue one line of work share. It names no path and no account, so every caller is told; a record from before it is answered
    // the key the same rule derives, never one written back.
    string? Entry = null,
    // HELPMODE1a2 (D183 point 3): the access mode an Ask Daoris record's run got, `restricted` or `full`; absent for every other record.
    // This machine's own, like the opinion: answered to it only.
    string? HelpMode = null,
    // AUTONOMY1a (D184 point 1): the access setup a driven session started under, each pause with its value; absent for a record told
    // none. This machine's own, like the opinion: answered to it only.
    IReadOnlyDictionary<string, string>? Access = null,
    // CLOSETAKE1 (FABRIC-CLOSE1o-ii): why the closure graph named for its take did not bind it (`expired`, `replaced` or `remote`), for
    // the driver's end line; additive, absent otherwise. This machine's own, like the opinion: answered to it only.
    string? TookBind = null,
    // KEEP1b (D195 point 6): the downloaded versions its starts were handed, which the downloads' rule keeps while it runs; absent for a
    // record told none. This machine's own, like the opinion: answered to it only.
    IReadOnlyList<string>? Resolved = null,
    // COAGENT1b (D199 point 1): the piece a partner's record writes, beside `serves` naming its lead; absent for every other record.
    // This machine's own, like the opinion: answered to it only.
    string? Piece = null,
    // CHATFLOW1a (the chat-flow design §4.2): the quest a chat took through its own connector (CHATTAKE1), its `quest` staying null;
    // absent for every other record. This machine's own, like the opinion: answered to it only.
    string? TookQuest = null,
    // D205: this machine's actor provenance. Never on the feed or answered off loopback.
    string? Actor = null, string? Choice = null, JsonElement? Asked = null, JsonElement? Override = null, JsonElement? Ran = null,
    // STAFFPOLICY1c2: why the role's policy chose this start's choice; this machine's own, answered to it only, absent under no policy.
    JsonElement? Chosen = null,
    // FOUNDATION1c (D212; the foundation design §2.2): the work the record was opened for, read from its marks and never stored
    // (`ParticipantWork.Of`): `build`, `chat`, `ask-daoris`, `intake`, `help`, `partner` or `review`. This machine's own, like the opinion:
    // answered to it only, absent for a mirror, which is another machine's, and never on the sync wire.
    string? Work = null,
    // LINESTOGETHER1a (the review-flow design §2): the id of the part its line ends at (`Lines.HeadOfLineAsync`), the list's answer only; absent
    // for a mirror, and where the walk ended on a cycle or the bound. This machine's own, like the opinion: answered to it only.
    string? Head = null);
// MSG1a: one word waiting on a record — its id, which the record's events say again where the session took it; the
// words; when; its files' names, never where they are; and whether it was said after the record ended. XAGENT1c: `by`, the
// second opinion whose findings these are, another agent's claims; absent, the person's words, as every word before it was.
// COWORK1b (D188): `from`, the author where it is not the person (`daoris`), and `key`, its producer's; absent on a word kept with none.
// COWORK1c (D188): `level`, when its sender asked it to reach a working session, and `attempt`, when a dispatcher first wrote it to a
// harness; each absent on a word kept with none.
// `Decision`, `Question` and `Choice` (SUPERVISOR1a-i, D208): what a `from: supervisor` word claims, absent on every other.
public sealed record SaidWordResponse(
    string Id, string Text, DateTimeOffset At, IReadOnlyList<string> Files, bool Reopens, string? By = null, string? Helper = null,
    string? From = null, string? Key = null, string? Level = null, DateTimeOffset? Attempt = null,
    string? Decision = null, string? Question = null, string? Choice = null);
// MSG1a (D137 §5.3): what the person says to a parked or ended session. `Files` are the names of what they gave with it;
// a path is cut to its name before it is kept.
// `Channel` (SESSONE2e, D167) is the id of the channel message the words are, which the channel already keeps on the ask:
// absent, the door keeps them as before.
// COWORK1b (D188): `From` names the author where it is not the person (`daoris`, refused `from` for any other), and `Key` the
// producer's key, a word kept once per key on the record; absent, the door keeps the person's words as before.
// COWORK1c (D188): `Level` (`ordinary`, `urgent` or `now`, refused `level` for any other), and `Running`, the driver that runs the
// session asking for its run: a running record keeps the word only so; absent, it refuses as before.
// `Decision`, `Question` and `Choice` (SUPERVISOR1a-i, D208): the supervisor's release names them with `from: supervisor` and no
// text, the service composing the words from the choice; any other author naming one is refused `from`.
public sealed record SayRequest(
    string? Text, IReadOnlyList<string>? Files = null, string? Channel = null, string? From = null, string? Key = null,
    string? Level = null, bool? Running = null, string? Decision = null, string? Question = null, string? Choice = null,
    // FOUNDATION1 row p (the design §3.4 rule 5): that the caller reads a settled answer, which its keyed say may be given where the key's
    // word left a record of its line. A say without it, an older client's, is answered by the door as it was; a host before the row reads none.
    bool? Settled = null,
    // FOUNDATION1o (the design §3.4 rule 4): the shell's word for a record that winds up or moves, kept on it for its next part; a host before
    // the row reads none and refuses it `running`, which the shell then holds as before.
    bool? Ending = null);
// The session as it stands, the ledger's sentence, and the word as kept (to this machine only).
public sealed record SaidResponse(SessionResponse Session, string Message, SaidWordResponse? Said);
// A keyed say or hand answered settled (row p): a success that names the record the key's word left, with no session, no `said` and no word at
// all, so a reader that reads a word finds none. Sent only to a request that asked with `settled`.
public sealed record SettledResponse(string Message, string Settled);
// A say refused: the sentence as `error`, beside its word (`refusal`: `no-words`, `not-found`, `not-ours`, `intake`,
// `stood-down`, `running`, XAGENT1c's `opinion`, a reviewer's record, and COWORK1b's `from`, an author this service does not
// know), which a reader acts on instead of the sentence, and
// the quest, ask, machine or second opinion it names. FOUNDATION1 row d-ii (D212): `continued`, the record's line went on past it, beside
// `head`, the line's head where the words go; `unplaced`, a line that loops or runs past the walk's bound, nothing kept. A reader before the
// row reads either as a refusal it does not know and says the sentence.
public sealed record SessionSayRefusalResponse(
    string Error, string Refusal, string? Quest = null, string? Ask = null, string? Origin = null, string? Opinion = null, string? Head = null);
// XAGENT1c (D155 point 11; the second agent design §6.2): a pass the driver asks, every part nullable so one left out is
// refused by the desk naming which. `minutes` is the rule's bound, said, never defaulted here: the service holds no rule.
public sealed record OpinionAskRequest(
    string? Occasion, string? Pass, string? Working, OpinionCandidateWire? Candidate, OpinionReviewerWire? Reviewer,
    string? Posture, int? Minutes, string? Tree, string? Rechecks = null, IReadOnlyList<string?>? Families = null,
    string? HarnessVersion = null);
// The candidate: exactly what a pass reads, by full commit ids.
public sealed record OpinionCandidateWire(string? Repository, string? Base, string? Tip, IReadOnlyList<string?>? Commits);
// The reviewer as the driver's walk chose it: its adapter, its label (`another-maker`, `same-agent`, `maker-not-declared`), its
// declared product and maker, and the account it runs as.
public sealed record OpinionReviewerWire(
    string? Adapter, string? Label, string? Product = null, string? Maker = null, string? Account = null);
// An opinion as this machine keeps it, with where its pass stands (`state`: `reading`, `given`, `failed`, with `why`:
// `ended` or `out-of-time`), derived when read. Answered only to a caller on this machine.
public sealed record OpinionResponse(
    string Id, string Occasion, string Pass, string Working, string Session, string? Rechecks,
    OpinionCandidateWire Candidate, OpinionReviewerWire Reviewer, IReadOnlyList<string> Families, string? Posture, int Minutes,
    string Tier, DateTimeOffset Asked, DateTimeOffset Due, string State, string? Why,
    OpinionGivenResponse? Given, OpinionHandedResponse? Handed, IReadOnlyList<OpinionAnswerResponse> Answers);
// What the reviewer said, once: its numbered findings, what it read, its limits, and a recheck's word on each first-pass finding.
public sealed record OpinionGivenResponse(
    IReadOnlyList<OpinionFindingResponse> Findings, string Read, string? Limits, IReadOnlyList<OpinionRecheckResponse>? Rechecked,
    DateTimeOffset At);
public sealed record OpinionFindingResponse(
    int Number, string Weight, string Where, string Claim, string Consequence, string Reproduce, string Sure, string? Proposal);
public sealed record OpinionRecheckResponse(int Finding, string Says);
// Where the findings went: the working session, the word they wait in on its record, and when.
public sealed record OpinionHandedResponse(string Session, string Word, DateTimeOffset At);
// The working session's answer to one finding, the latest standing: `fixed` with its commit, `rejected` with its evidence, or
// `unresolved` with why. A finding with none reads as unresolved, not answered.
public sealed record OpinionAnswerResponse(int Finding, string Said, string? Commit, string? Evidence, string? Why, DateTimeOffset At);
// A door's answer: the opinion as it now stands, the desk's sentence, and the reviewer's record a pass opened or the working
// session the findings went to, with the word they wait in.
public sealed record OpinionActionResponse(
    OpinionResponse Opinion, string Message, SessionResponse? Session = null, SaidWordResponse? Said = null, string? Settled = null, bool? Waiting = null);
// MIXAGENT2a-ii (D163 point 2): the driver's helper session opens its record, and hands the helper's answer to the asker. Every
// part nullable so one left out is refused by the desk naming which. `text` is Daoris's words around the helper's claim.
public sealed record HelperOpenRequest(string? Asker, string? Adapter, string? Tree, string? Account = null, string? HarnessVersion = null, string? BaseCommit = null);
// COWORK1d: `running` true is the driver that runs a working driven asker handing an urgent ask's answer for its run's next step;
// `level` the ask's, kept beside the word.
// COAGENT1b (D199 point 2; the co-agent design §2.3 step 5): the driver opens a partner's record for one piece, idempotent by the
// piece's id: its lead, its agent, its own clone, the account and version it runs on, and the ask's base commit.
public sealed record PartnerOpenRequest(
    string? Lead, string? Piece, string? Adapter, string? Tree, string? Account = null, string? HarnessVersion = null, string? BaseCommit = null);
public sealed record HelperHandRequest(string? Text, bool? Running = null, string? Level = null, bool? Settled = null);
// CHATFLOW1e (the chat-flow design §6.1 (a), (e)): a first pass's findings handed to a chat run's record the driver names, the line's newest
// in its tree, live between turns or ended; none for the working session itself, as before.
// COMMS1b: `settled` true asks to be told a key the line settled, as a say or a helper's hand does.
public sealed record OpinionHandRequest(string? To = null, bool? Settled = null);
// A helper door's answer: the desk's sentence, the helper's record a pass opened or the asker the answer went to, and the word. `waiting`
// (COMMS1b): the answer was waiting already, `session` holds it and `said` is it, and nothing was kept now.
public sealed record HelperActionResponse(
    string Message, SessionResponse? Session = null, SaidWordResponse? Said = null, string? Settled = null, bool? Waiting = null);
// BRAINSTORM1a3 (D176 points 1, 2 and 8): a person's question for a brainstorm, every part nullable so one left out is refused by
// the desk naming which. `Repository` is the one whose knowledge the packet is frozen from, none for the question alone.
// ONEWAY1t: `Kind` is `brainstorm` (none) or `meeting`; `Id` the caller's own, which makes the ask idempotent; `Sources` the repository's
// paths the packet takes in.
public sealed record BrainstormAskRequest(
    string? Question, string? Repository = null, IReadOnlyList<string?>? Agents = null, string? Kind = null, string? Id = null,
    IReadOnlyList<string?>? Sources = null);
// One brainstorm as its records stand: `State` is `asked` before the driver's look froze its packet, then the driver's own
// (`frozen`, `running`, `ended`); `Packet` the frozen packet's digest, `Sent` the digest the person sent, each null for none.
// A meeting's carries its `Kind` and the `Round` its state is of; a brainstorm's leaves both out.
public sealed record BrainstormResponse(
    string Id, string Question, string? Repository, IReadOnlyList<string> Agents, DateTimeOffset At, string State, string? Code,
    string? Packet, string? Sent, string? Kind = null, int? Round = null);
// One brainstorm shown: the packet as frozen, whole and as written (null before the look froze one), and the document's Markdown.
public sealed record BrainstormShownResponse(BrainstormResponse Brainstorm, JsonElement? Frozen, string? Document);
// A press's answer: the brainstorm as it now stands, and the desk's sentence.
public sealed record BrainstormActionResponse(BrainstormResponse? Brainstorm, string Message);
// MSG1a (D137 §2.4): the words a session took, by their ids; `By` the session that took them where it is not this one
// (a fallback's new session).
public sealed record TakenRequest(IReadOnlyList<string?>? Said, string? By = null);
// COWORK1c (D188): the words a session's dispatcher is about to write to its harness, by their ids, marked attempted before the wire.
public sealed record AttemptRequest(IReadOnlyList<string?>? Said);
// The session as it stands, the ledger's sentence, and the ids named that no longer wait, which were marked nothing.
public sealed record AttemptResponse(SessionResponse Session, string Message, IReadOnlyList<string> Unmarked);
// SAIDBACK1a (D137 §2.4): one word waiting on a record, by its id, taken back before a session took it; with `text`, reworded:
// the old word out and these words kept last under a new id, its files' names kept.
public sealed record TakeBackRequest(string? Word, string? Text = null);
// A take-back's answer: `done`, the ledger's sentence, and, when done, the session as it stands and a reword's new word (to this
// machine only). Not done, `why` is `taken`: the word no longer waits, or the record runs again and may have read it; an
// answer, never an error, as `SESSION_QUEUE_EDIT`'s is (QUEUEEDIT1a). What never goes on is refused as the say door refuses.
public sealed record TakeBackResponse(bool Done, string Message, string? Why = null, SessionResponse? Session = null, SaidWordResponse? Said = null);
// LINESTOGETHER1b (the review-flow design §2): one word waiting on a part, by its id, moved to the head of the line `To` is on (the head the
// page showed; absent, the part's own line).
public sealed record MoveSaidRequest(string? Word, string? To = null);
// A move's answer: `moved`, the ledger's sentence, and `to`, the head it was kept on, with the session as it stands and the word kept there (to
// this machine only); a retry `again`, `to` naming where the word waits or that it left, nothing kept. Not moved, `why` is `taken`: the word no
// longer waits, or was handed to its agent already. Every other refusal is the say door's shape, a running head named as `head`.
public sealed record MoveSaidResponse(
    bool Moved, string Message, string? Why = null, string? To = null, bool? Again = null, SessionResponse? Session = null, SaidWordResponse? Said = null);
// SESSUX1f (D126 §5.4): a session delete refused, or its judgement alone. `error` is the ledger's sentence, the field every
// reader already knows; `refusal` its word, which a reader acts on instead of the sentence; and the facts the word names.
public sealed record SessionDeletionResponse(
    bool Deletable, string? Error = null, string? Refusal = null,
    string? Quest = null, string? Ask = null, string? Origin = null, string? Workspace = null);
// HIST1b (D153; the history-clearing design §6.3): why a unit stays, or a piece of it is kept, as SESSUX1f's refusals travel:
// `error` the desk's sentence, `refusal` its word (`unknown`, `open`, `asked`, `live`, `needs-you`, `awaited`, `unpushed`,
// `not-ours`), which the driver reads instead of the sentence, and the quest, ask, session, machine or workspace it names.
// HIST1l: `waits`, beside `needs-you` only, names what waits on the person (`parked`, `held`, `conflict`, `ask`, `proposal`), so
// the driver says the sentence meant without reading the records again; absent for every other word, and from a host before it.
// HIST1m, the same way: `stands`, beside `open` only, how the work in progress stands (`open`, `taken`), and `by`, beside
// `awaited` only, the open work that names it (`question`, `asker`, `step`). The words themselves are unchanged.
public sealed record HistoryRefusalResponse(
    string Refusal, string Error, string? Quest = null, string? Ask = null, string? Session = null, string? Origin = null,
    string? Workspace = null, string? Waits = null, string? Stands = null, string? By = null);
// One unit (`kind`: `quest`, `ask` or `failed`): what a clear takes, by id, and whether it may go now. `forgotten` is which of
// its quests a remote numbered, forgotten here rather than simply removed; `teammates` this machine's copies of a teammate's
// records; `kept` the pieces listed and kept while the unit goes (a teammate's failed session).
public sealed record HistoryUnitResponse(
    string Kind, string Id, string? Workspace, bool Clearable, IReadOnlyList<string> Quests, IReadOnlyList<string> Forgotten,
    IReadOnlyList<string> Asks, IReadOnlyList<string> Sessions, IReadOnlyList<string> Teammates,
    HistoryRefusalResponse? Refusal, IReadOnlyList<HistoryRefusalResponse> Kept);
public sealed record HistoryPlanResponse(IReadOnlyList<HistoryUnitResponse> Units);
// The second press names its units as the first listed them: each `kind` and `id`.
public sealed record HistoryUnitRequest(string? Kind, string? Id);
public sealed record HistoryClearRequest(IReadOnlyList<HistoryUnitRequest?>? Units);
// What the press did to one unit: the unit as judged where it was cleared, whether it went, and the desk's sentence. HIST1j:
// `failed`, only where a removal failed, names the quests and asks whose kept files the disk would not let go of, so the driver
// counts them as failed and frees none of their bytes; absent otherwise, so a driver from before reads the answer as it did.
public sealed record HistoryClearedResponse(HistoryUnitResponse Unit, bool Cleared, string Message, HistoryFailedResponse? Failed = null);
public sealed record HistoryFailedResponse(IReadOnlyList<string> Quests, IReadOnlyList<string> Asks);
public sealed record HistoryClearResponse(IReadOnlyList<HistoryClearedResponse> Units);
// The tree comes IN from the driver, which is the half that knows: the service has no checkout to
// look at, exactly as it has no binaries to probe (D46 §7). Unstated resolves to the registered root.
// MIXAGENT1a-iii: `from`, `fromWhy` and `fromBy` name the session the new one continues, as the driver knows it: a conversation
// handed over, a quest's session carried on after a hand-over or onto another account after a limit. Absent for any other open.
// AUTONOMY1a: `access`, the access setup the driver resolved for this start, each pause with its value; absent from a driver before it.
// FOUNDATION1 d-ii part 3 (D212, the design §3.4 rule 3): `moves`, beside `opens` alone, the words the keyed open moves onto the record it opens in
// its own step: `from`, the record they move from (the open's own `from`, or a start's source where it names none); `words`, the ids the course
// named; `read`, the ids it saw waiting there when it read, so the person's words kept since move too. A host before it skips the member and
// moves nothing, which its `opens` mark below 2 tells.
public sealed record OpenMovesRequest(string? From, List<string>? Words, List<string>? Read);
public sealed record OpenSessionRequest(
    string Quest, string Adapter, string? HarnessVersion, string? Profile, string? Tree,
    string? BaseCommit = null, string? From = null, string? FromWhy = null, string? FromBy = null,
    Dictionary<string, string>? Access = null, string? Actor = null, string? Opens = null, OpenMovesRequest? Moves = null);
public sealed record OpenChatRequest(
    string Repository, string? Adapter, string? HarnessVersion, string? Profile, string? Tree,
    string? BaseCommit = null, string? From = null, string? FromWhy = null, string? FromBy = null, string? Actor = null, string? Opens = null,
    OpenMovesRequest? Moves = null);
// An intake (D65 §1b): the ask it answers and the room it runs in — the driver's, which is the half
// that is about to run a process there. The circle comes from the ask, never from the caller.
public sealed record OpenIntakeRequest(
    string? Ask, string? Adapter, string? Room, string? HarnessVersion = null, string? Profile = null, string? Actor = null);
// Ask Daoris (HELP1a, D89): the room its conversation runs in, the driver's to name. `Workspace` (ASKSCOPE1a): the one its
// window was on at the press, which the record keeps; none is `default`, as a driver from before sends it. `Mode` (HELPMODE1a2):
// the access mode its start got, `restricted` or `full`, which the record keeps; none is `restricted`, as a driver from before sends it.
public sealed record OpenHelpRequest(
    string? Adapter, string? Room, string? HarnessVersion = null, string? Profile = null, string? Workspace = null,
    string? Mode = null, string? Actor = null);
public sealed record ActorKeepRequest(string? Choice = null, JsonElement? Asked = null, JsonElement? Override = null, JsonElement? Ran = null, JsonElement? Chosen = null);
// HELPMODE1a2 (D183 point 3): the access mode an Ask Daoris conversation going on runs under, as its driver resolved it.
public sealed record HelpModeRequest(string? Mode);
// AUTONOMY1b2 (D184 point 1): the access setup a resumed driven session now runs under, each pause with its value, as its driver resolved it.
public sealed record AccessKeepRequest(Dictionary<string, string>? Access);
// KEEP1b (D195 point 6): the downloaded versions a start of the record was handed, `agent|tool/<name>/<version>`, as its driver read them.
public sealed record ResolvedKeepRequest(List<string>? Resolved);
// KEEPSIZE1b (the keep-size design §2.2): a finished tree's record, the manifest's facts and the commits read from its tip, oldest first;
// `commits` null with `commitsUnread` where the tip no longer read. Machine-local: no wire carries it.
public sealed record WorkRecordPostRequest(
    string? Workspace, string? Repository, string? Folder, string? Tree, DateTimeOffset? Moved, string? Unit, string? Branch, string? Line,
    WorkRecordHold? Hold, string? Tip, string? GrewFrom, string? Ref, List<WorkRecordCommit>? Commits, string? CommitsUnread,
    WorkRecordLeft? Left, bool Whole = false, WorkRecordLanding? Landing = null, string? LandingWhy = null,
    Dictionary<string, MemoryTurnsRequest>? Turns = null)
{
    /// <summary>The post, where every part the record is keyed or read by is here; null otherwise.</summary>
    public WorkRecordPost? ToPost() =>
        Blank(Workspace) || Blank(Repository) || Blank(Folder) || Blank(Tree) || Moved is not { } moved || Blank(Branch) || Hold is null
        || Blank(Hold.Kind) || Blank(Tip) || Left is null || Left.Names is null
            ? null
            : new(Workspace!, Repository!, Folder!, Tree!, moved, Unit, Branch!, Line, Hold, Tip!, GrewFrom, Ref, Commits, CommitsUnread, Left, Whole)
            {
                Landing = Landing is { Branch.Length: > 0 } ? Landing : null,
                LandingWhy = LandingWhy,
            };

    private static bool Blank(string? text) => string.IsNullOrWhiteSpace(text);
}
// KEEPSIZE1b (Codex's read of the branch, finding 8): the finished trees' records no session names, kept at or before `before`, cleared by
// history's age on their own `at`; `workspace` narrows it to one circle.
public sealed record WorkRecordAgeRequest(DateTimeOffset? Before, string? Workspace = null);
// The records it cleared.
public sealed record WorkRecordAgeResponse(int Cleared);
// The record as kept: whether this post kept it (false for a second post of a kept identity, which wrote nothing), when, and its sessions.
public sealed record WorkRecordResponse(bool Kept, DateTimeOffset At, IReadOnlyList<WorkRecordSession> Sessions);
// `Interrupted` (D104): a move to `stopped` that was not the person's. `Limit` (TOOL4c): a move to `failed`
// that an account's limit made. Silence is the old reading of both. `NoteParts` (LANG1a): the note's lines by code, beside
// it; a note sent without them clears the record's, and an older driver sends none. `Expected` (TERMTRUST1a, D201 point 2): the
// state the record must be in, compared in the ledger's step; none moves from any state the move allows, as before. `UnbindAsk`
// (LIMITRESUME1h, D206): an intake's end lets its ask go in the same step, for the next intake; none keeps the binding, as before.
// `Expect` (SUPERVISOR1a-i, D208): the supervisor's words a move to working admits, exactly; none admits none.
// `Turns` (KNOWCENTRAL2d): the usage history's turn lines of each session a commit point releases memory for, by session; none names
// every model `unknown`, as before.
public sealed record AdvanceSessionRequest(
    string? State, string? Note, string? Evidence, string? Transcript, bool? Interrupted = null, bool? Limit = null,
    JsonElement? NoteParts = null, string? Expected = null, bool? UnbindAsk = null, IReadOnlyList<string>? Expect = null,
    Dictionary<string, MemoryTurnsRequest>? Turns = null);
// One session's turn lines as the driver's usage history keeps them (the memory coordinator design §1), and the instants the person asked.
public sealed record MemoryTurnsRequest(List<MemoryTurnLine>? Turns, List<DateTimeOffset>? Asked)
{
    /// <summary>The lines by session, each line with an end; null for none.</summary>
    public static IReadOnlyDictionary<string, MemoryTurns>? Of(Dictionary<string, MemoryTurnsRequest>? turns) =>
        turns is null
            ? null
            : turns.ToDictionary(
                pair => pair.Key,
                pair => new MemoryTurns(
                    [.. (pair.Value.Turns ?? []).Where(line => line.Ended is not null)
                        .Select(line => new MemoryTurn(line.Seq, line.Started, line.Ended!.Value, line.Model ?? ""))],
                    pair.Value.Asked ?? []),
                StringComparer.Ordinal);
}
// One turn line: its sequence, its start where the line says, its end and its model, or the split's models joined.
public sealed record MemoryTurnLine(long Seq, DateTimeOffset? Started, DateTimeOffset? Ended, string? Model);
// KNOWCENTRAL2d: the person's decision through an awaiting end's row, and their Keep or Drop of an ended session's staged changes.
public sealed record MemoryDecideRequest(bool? Approve);
public sealed record MemoryKeepRequest(bool? Keep);
public sealed record MemoryDecisionResponse(string Message, MemoryOp? Op, int Moved);
// A session's memory changes as its record's page shows them, an end's reason as its proposer wrote it.
public sealed record SessionMemoryResponse(IReadOnlyList<MemoryOp> Ops);
// KNOWCENTRAL2e: every session with a memory change waiting on the person, for the driver's poll.
public sealed record MemoryWaitsResponse(IReadOnlyList<MemoryWait> Waits);
// KNOWCENTRAL2f: what a line read, each entry as it stands now, for the brief a part continuing it is handed.
public sealed record MemoryReadResponse(IReadOnlyList<MemoryShownResponse> Shown);
public sealed record MemoryShownResponse(string Reference, long Read, string Standing, long? Revision, long? Checked, string? Why, string Line);
// SUPERVISOR1a-i (D208): a park the driver took up put back, less the supervisor's words it admitted and never handed.
public sealed record RestoreParkRequest(IReadOnlyList<string>? TakeBack, string? Note, JsonElement? NoteParts = null);
public sealed record SessionActionResponse(SessionResponse Session, string Message);
// A keyed open's answer (row d-i): the key's echo and `transfer`, from d-ii part 3 each moved word in order, its id on the record left (`word`)
// and on the record opened (`id`, which a moved word keeps), its producer `key` (no member for a word with none) and its `transfer` id.
public sealed record SessionOpenResponse(SessionResponse? Session, string Message, string Opens,
    IReadOnlyList<OpenTransfer> Transfer, string? Head = null, string? Error = null, string? Code = null);
// `FirstLine` and `LastLine` (ORIENT2e): the lines of its file an entry is, as `/api/entries` answers them, taken
// only where they can be its body's; a feed that names none is read as before.
public sealed record FeedEntryRecord(
    string? Kind, string? Title, string? Body, string? RelativePath, string? Anchor, int? FirstLine = null, int? LastLine = null);
// The three provenance fields are the feed's claim about WHICH point in the history it speaks for
// (D48 §6). Nullable on the wire and judged at the door: a feed that names no commit cannot be
// compared with what is held, and a wholesale replacement that cannot be compared is the flapping
// this exists to end. `Base` is the held commit the feeding machine asked git about (SYNC5a); absent,
// commit time decides.
public sealed record FeedEntriesRequest(
    string Repository, IReadOnlyList<FeedEntryRecord>? Entries,
    string? Commit, DateTimeOffset? CommittedAt, string? Branch, string? Base = null);
// A repository's code map, fed (MAP3b): the file's text as one string, judged whole again at the door,
// or null when the checkout keeps none at that commit. The provenance is the entries feed's, field for
// field — one judgement, two feeds.
public sealed record FeedCodeMapRequest(
    string Repository, string? File, string? Map,
    string? Commit, DateTimeOffset? CommittedAt, string? Branch, string? Base = null);
// Which commit a repository's knowledge, code map and declaration stand on at this deployment — what a
// feeding machine asks git about before it feeds (SYNC5a, SYNC5b). Null where nothing has fed.
public sealed record FeedHeldResponse(string Repository, string? Knowledge, string? CodeMap, string? Registration);
// A machine's sync pass (D69, SYNC4), as its driver reads it: what the quest half pushed, the moves of
// this machine that became conflicts, what the remote would not keep, what is still behind; how many
// session records went up and came down; and the wall, if there was one. `Machine` is this store's id.
// Neither operations nor records cross this door — the remote's doors speak Core's `QuestWire` and
// `SessionWire`, and this one only says what a pass did.
public sealed record QuestConflictNote(string Quest, string Attempted);
public sealed record QuestPushRefusalWire(string Quest, string Reason);
// `CodeMapsFetched` is how many of the team's code maps came down, new or moved (MAP3e).
public sealed record SyncResponse(
    string Workspace, string Machine, bool Wired, int Pushed, IReadOnlyList<QuestConflictNote> Conflicts,
    IReadOnlyList<QuestPushRefusalWire> Refused, IReadOnlyList<string> Behind,
    int SessionsPushed, int SessionsFetched, string? Problem, int CodeMapsFetched);
// Where a circle stands on this machine (SYNC6a): what it has not pushed, the quests the last pass
// left behind, the quests carrying a conflict, when a pass last reached the remote and last tried, and
// the wall it hit. A circle with no remote here answers `wired: false` and nothing else, because
// "unpushed" means nothing where there is nowhere to push. The machines of the workspace its remote last said had not declared they
// know a bound quest (FABRIC-CLOSE1i2), as the last pass heard them: empty where every one did, absent where no pass heard the handshake.
public sealed record SyncStandingResponse(
    string Workspace, bool Wired, int Ahead, IReadOnlyList<string> Behind, IReadOnlyList<string> Conflicts,
    DateTimeOffset? Synced, DateTimeOffset? Tried, string? Problem, IReadOnlyList<string>? Undeclared = null);
// A person's retirement of a machine at a circle's remote (FABRIC-CLOSE1i2): whether the remote retired one, false where it had heard
// nothing from it, and the machines it still counts undeclared after it.
public sealed record MachineRetiredResponse(
    string Workspace, string Machine, bool Retired, IReadOnlyList<string> Undeclared, string Message);
// Where this machine's claim on one quest stands: none, held, unconfirmed or lost (D68 §4). Why a lost one lost, where its take lost to
// the person's reopen of the work the step grew from (FABRIC-CLOSE1c18): `source-reopened`; null otherwise, as an older host answers.
// `Forward` (SYNCMERGE1b0): the take this machine holds the quest by, where the person's bring-forward made it, the one take a stood-down
// session is carried on over; absent otherwise, and from a host before it. `Taken` (CARRY3): when the take the quest stands taken by
// was made, on its machine's clock; null while it is not taken, and absent from a host before it.
public sealed record QuestClaimResponse(
    string Quest, string Claim, string? Why = null, QuestForwardResponse? Forward = null, DateTimeOffset? Taken = null);
// HANDBACK1 (D219): one quest's handback as `GET /api/handbacks/{id}` answers it, by the quest's id or the id of the session that
// served it, in any state. `Found`: whether the id named a quest or a session here, so a client tells this door's answer from an older
// host's 404. `Session`: the session the id named, absent for a quest's id. `Quest`: the quest, with its own note as it keeps it.
// `Handback`: where a session's close is recorded on it, whole, with the session that wrote it; absent where none is. `Said`: the read's
// words, the same the connector's `handback_read` answers, which the terminal prints.
public sealed record HandbackResponse(
    string Id, bool Found, string? Session, HandbackQuestResponse? Quest, HandbackRecordResponse? Handback, string Said)
{
    public static HandbackResponse Of(HandbackReading read) => new(
        read.Id, read.Found, read.Session?.Id,
        read.Quest is { } quest
            ? new HandbackQuestResponse(quest.Id, quest.Title, quest.Status.ToString(), quest.From, quest.To, quest.Workspace, quest.Note)
            : null,
        read.Handback is { } said
            ? new HandbackRecordResponse(
                said.Spelled, said.Note,
                said.Answers.Count == 0 ? null : [.. said.Answers.Select(answer => new QuestAnswerWire(answer.Requirement, answer.Met, answer.Departed, answer.Quote))],
                said.At, said.Machine, said.Session)
            : null,
        read.Said);
}
// The quest a handback hands back: its name, where it stands, between whom, and its note as the quest keeps it.
public sealed record HandbackQuestResponse(string Id, string Title, string Status, string From, string To, string Workspace, string? Note);
// A handback whole: its act (`done`, `ready` or `declined`), its note, how it answered each requirement (absent for none), when on
// the machine that made it, and the session that wrote it.
public sealed record HandbackRecordResponse(
    string Act, string? Note, IReadOnlyList<QuestAnswerWire>? Answers, DateTimeOffset At, string Machine, string Session);
public sealed record FeedResponse(int Accepted, string Message);
// A repository's code map is answered in Core's shape (CodeMapWire, MAP3e), not a record here: the
// host that brings a teammate's map down reads the same answer the door writes.
/// <summary>
/// A refusal the CLIENT should report rather than fix (D48 §6) — a stale or branch feed is the system
/// working, and the machine that is behind is simply behind. The flag is what lets a sync loop tell
/// "this is news" from "this is broken" without matching on the sentence: `error` stays the field
/// every reader already knows, so the message still travels verbatim.
/// </summary>
public sealed record FeedRefusalResponse(string Error, bool Information);

// PERSONDOOR1b (D156 point 4, design §4.2; ONEWAY1n-i): an ask for the person's confirmation. `Path` is the refused request's
// path and query exactly as sent, `Body` its body exactly as sent (absent for none), and `Session` the session asking, as its
// record names it, or absent for a terminal's ask (PERSONDOOR1j). The host makes that request itself at the Confirm.
public sealed record ConfirmationAskRequest(string? Method, string? Path, string? Body, string? Session);
// An asker's withdrawal of its own ask (ONEWAY1n-i): the token its ask was answered with.
public sealed record ConfirmationWithdrawRequest(string? Token);
// One value the request carries: an address's (a route's parameter, then the query's, by name) or the body's, by its JSON
// pointer (`/setUp/machine`, `/units/0/id`; `~0` and `~1` escape a name's `~` and `/`). `Type` is its JSON type (`string`,
// `number`, `boolean`, `null`, `object`, `array`), so a string "3" and a number 3 read apart; `Value` is a string as it is
// and anything else as its JSON. Only names the door binds are ever shown: an ask carrying another is refused.
public sealed record ConfirmationFieldResponse(string Name, string Type, string Value);
// A confirmation as the window shows it and its asker polls it (ONEWAY1n-i): `State` is `waiting`, `making`, `made`, `refused`,
// `withdrawn`, `stale` or `failed`; `Route` the door's pattern and `Act` its own name in the host's table (the form's, where the
// body makes one); `Values` the address's values and `Fields` every value of the body, so nothing the request made would carry is
// out of sight; `Session` the session the ask named, absent for a terminal's. No clock ends one. Once it ends, `Ended`; once made,
// the door's `Status` and its `Answer` as text (absent past 64 KB), and where the door refused or failed it, its `Reason`, the
// door's own sentence. Never the asker's token.
public sealed record ConfirmationResponse(
    string Id, string State, string Method, string Path, string Route, string Act,
    IReadOnlyList<ConfirmationFieldResponse> Values, IReadOnlyList<ConfirmationFieldResponse> Fields, string? Body,
    DateTimeOffset Asked, string? Session, DateTimeOffset? Ended, int? Status, string? Answer, string? Reason);
// A confirm's, a refusal's or a withdrawal's answer: the confirmation as it now stands, and a sentence the caller says.
public sealed record ConfirmationActionResponse(ConfirmationResponse Confirmation, string Message);
// An ask's answer (ONEWAY1n-i): the confirmation, the sentence its asker says while it waits, and the token that withdraws it,
// answered here alone.
public sealed record ConfirmationAskedResponse(ConfirmationResponse Confirmation, string Message, string Token);
// NOTIFY2j: what waits for the person, as a held wait answers it. `Since` is the cursor to ask from next (`<epoch>.<n>`), and
// `Waiting` every confirmation waiting now, whole, oldest first, as `GET /api/confirmations` lists them.
public sealed record ConfirmationsWaitingResponse(string Since, IReadOnlyList<ConfirmationResponse> Waiting);
// FABRIC-EVENT3b (the fabric events design §A4.2): the change stream's answer. `Since` is the cursor to ask from next
// (`<epoch>.<n>`), and `Moved` the ledger families that moved after the one asked from, by name (`sessions`, `quests`): empty
// at the wait's end with nothing moved. Families, never records, and no path (D169 §3). `Journal` (FOUNDATION1 row k part 2) is the event
// journal's place, answered to a reader that asked for it with `journal=1` and written to none else (null is left out), so an answer to a client
// from before it is what it always was; the family `journal` is likewise named only to a reader that asked.
public sealed record ChangesResponse(string Since, IReadOnlyList<string> Moved, ChangesJournalResponse? Journal = null);
// Where the event journal stands: the store's identity its positions are of, the lowest position kept (the floor) and the newest issued.
public sealed record ChangesJournalResponse(string Store, long Floor, long Newest);

[JsonSerializable(typeof(ChangesResponse))]
[JsonSerializable(typeof(JournalPage))]
[JsonSerializable(typeof(AskReviewRequest))]
[JsonSerializable(typeof(AskWorkflowRequest))]
[JsonSerializable(typeof(AskRequestBody))]
[JsonSerializable(typeof(AskPublishRequest))]
[JsonSerializable(typeof(AskCloseRequest))]
[JsonSerializable(typeof(AskActionResponse))]
[JsonSerializable(typeof(AskResponse))]
[JsonSerializable(typeof(IEnumerable<AskResponse>))]
[JsonSerializable(typeof(GoAheadAnswerRequest))]
[JsonSerializable(typeof(ChannelPostRequest))]
[JsonSerializable(typeof(ChannelReadRequest))]
[JsonSerializable(typeof(ChannelResponse))]
[JsonSerializable(typeof(ChannelPostResponse))]
[JsonSerializable(typeof(ChannelAckRequest))]
[JsonSerializable(typeof(ChannelAckResponse))]
[JsonSerializable(typeof(AddedRequest))]
[JsonSerializable(typeof(AddedResponse))]
[JsonSerializable(typeof(RefreshResponse))]
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(RegisteredResponse))]
[JsonSerializable(typeof(RetiredResponse))]
[JsonSerializable(typeof(RetiredPendingResponse))]
[JsonSerializable(typeof(WireRequest))]
[JsonSerializable(typeof(ImportRequest))]
[JsonSerializable(typeof(ImportedResponse))]
[JsonSerializable(typeof(IEnumerable<RegistrationResponse>))]
[JsonSerializable(typeof(IEnumerable<SessionResponse>))]
[JsonSerializable(typeof(OpenSessionRequest))]
[JsonSerializable(typeof(ActorKeepRequest))]
[JsonSerializable(typeof(OpenChatRequest))]
[JsonSerializable(typeof(OpenIntakeRequest))]
[JsonSerializable(typeof(OpenHelpRequest))]
[JsonSerializable(typeof(HelpModeRequest))]
[JsonSerializable(typeof(AdvanceSessionRequest))]
[JsonSerializable(typeof(SessionActionResponse))]
[JsonSerializable(typeof(SessionOpenResponse))]
[JsonSerializable(typeof(SessionDeletionResponse))]
[JsonSerializable(typeof(SayRequest))]
[JsonSerializable(typeof(SaidResponse))]
[JsonSerializable(typeof(SessionSayRefusalResponse))]
[JsonSerializable(typeof(TakenRequest))]
[JsonSerializable(typeof(AttemptRequest))]
[JsonSerializable(typeof(AttemptResponse))]
[JsonSerializable(typeof(TakeBackRequest))]
[JsonSerializable(typeof(TakeBackResponse))]
[JsonSerializable(typeof(OpinionAskRequest))]
[JsonSerializable(typeof(OpinionResponse))]
[JsonSerializable(typeof(IEnumerable<OpinionResponse>))]
[JsonSerializable(typeof(OpinionActionResponse))]
[JsonSerializable(typeof(HelperOpenRequest))]
[JsonSerializable(typeof(HelperHandRequest))]
[JsonSerializable(typeof(OpinionHandRequest))]
[JsonSerializable(typeof(PartnerOpenRequest))]
[JsonSerializable(typeof(HelperActionResponse))]
[JsonSerializable(typeof(BrainstormAskRequest))]
[JsonSerializable(typeof(IEnumerable<BrainstormResponse>))]
[JsonSerializable(typeof(BrainstormShownResponse))]
[JsonSerializable(typeof(BrainstormActionResponse))]
[JsonSerializable(typeof(HistoryPlanResponse))]
[JsonSerializable(typeof(HistoryClearRequest))]
[JsonSerializable(typeof(HistoryClearResponse))]
[JsonSerializable(typeof(ConfirmationAskRequest))]
[JsonSerializable(typeof(ConfirmationWithdrawRequest))]
[JsonSerializable(typeof(ConfirmationResponse))]
[JsonSerializable(typeof(IEnumerable<ConfirmationResponse>))]
[JsonSerializable(typeof(ConfirmationsWaitingResponse))]
[JsonSerializable(typeof(ConfirmationActionResponse))]
[JsonSerializable(typeof(ConfirmationAskedResponse))]

[JsonSerializable(typeof(FeedEntriesRequest))]
[JsonSerializable(typeof(FeedCodeMapRequest))]
[JsonSerializable(typeof(FeedHeldResponse))]
[JsonSerializable(typeof(SyncResponse))]
[JsonSerializable(typeof(SyncStandingResponse))]
[JsonSerializable(typeof(MachineRetiredResponse))]
[JsonSerializable(typeof(QuestClaimResponse))]
[JsonSerializable(typeof(HandbackResponse))]
[JsonSerializable(typeof(FeedResponse))]
[JsonSerializable(typeof(FeedRefusalResponse))]
internal sealed partial class ApiJson : JsonSerializerContext;
