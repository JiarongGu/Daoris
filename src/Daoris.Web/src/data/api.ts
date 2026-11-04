// The service's surface. Same origin in production; the dev server proxies /api, so no code path
// differs between the two. Reads everywhere; the writes are service state only — quests and their
// conflicts, asks, registrations and refresh (D36, D38, D48, D65). Doctrine has no write here, by
// design (D31): where a rule should change, the UI proposes the command to run in the repository that
// owns it.

// The knowledge reads' shapes are the service's declarations (`generated/service.ts`, written from src/Daoris.Contracts),
// under the names the page reads them by.
import type {
  AskActionResponse, AskResponse, DeclarationMatchResponse, GoAheadRequestResponse, GoAheadResponse, HitResponse, ProvenanceResponse,
  QuestAnswerResponse, QuestAttachmentResponse, QuestConflictResponse, QuestRequirementResponse,
  QuestResponse, QuestSetUpWire,
} from './generated/service';
export type {
  StatusResponse as Status, ProvenanceResponse as Provenance, RepositoryResponse as Repository, HitResponse as Hit,
  EntryResponse as Entry, ConvergenceEntryResponse as ConvergenceEntry, ConvergenceResponse as Convergence,
} from './generated/service';
/**
 * A search's hits, and which tier answered them: `lexical`, `semantic`, `lexical+semantic`, or `none` when no half
 * answered, which is not the same as nothing matching. Null from a host that sends no header.
 */
export type SearchAnswer = { hits: HitResponse[]; tier: string | null };
/**
 * A file a quest carries, by name (D65 §2). `path` is where THIS machine keeps it — answered only to a
 * caller on this machine, and only when the bytes are here — so the page reads its PRESENCE as "can be
 * opened" and never shows the path itself: a page does not name a machine path. Where the shell pushes
 * the quests and the asks (PUSH1d3), the path is never pushed and `kept` says the same; `fileKept` reads either.
 */
export type QuestAttachment = QuestAttachmentResponse & { kept?: boolean };
/** Whether this machine keeps a file's bytes, so it can be opened: a path pulled, or `kept` pushed (PUSH1d3). */
export const fileKept = (file: QuestAttachment): boolean => Boolean(file.path) || file.kept === true;
/**
 * A quest as every door answers it: the service's declaration (`generated/service.ts`), whose status and note parts it types as the
 * page reads them. A host older than a field leaves it out, which reads as carrying nothing. `kept` is what the shell pushes in
 * place of a path (PUSH1d3).
 */
export type Quest = Omit<QuestResponse, 'attachments'> & { attachments: QuestAttachment[] };
/** A submission by the machine and sequence that made it (FABRIC-CLOSE1a): what a reopen names it by. */
export type SubmissionRef = { machine: string; sequence: number };
/**
 * One move of a quest (FLOWCHART1a2; D186, the flowchart design §2.1), as `GET /api/quests/{id}/moves` answers it on either host,
 * by allow-list: its kind as the service spells it (`published`, `taken`, `done`, `conflict`, `dismissed`, `waited`, …; a newer
 * host may name another, shown as recorded), when and on which machine it was made, its sequence there, the remote's number (absent
 * while it is pending), whether the replay applied it, and the status it reached the quest at (absent before the publish). Its kind's
 * own field: a conflict's `attempted`, a dismissal's `dismisses` (the conflict's machine and sequence), a wait's `waitsOn`, a
 * submission's `session`, and the codes of an evidence read, a set-up and a verdict. `hold`: the hold it set, or `none` where it let
 * one go. Never the publish's words, a note's body, the answers or a path; a field a move does not keep is absent: not recorded.
 */
export type QuestMove = {
  kind: string; at: string; machine: string; sequence: number; number?: number | null; applied: boolean;
  from?: Quest['status'] | null; attempted?: Quest['status'] | null; dismisses?: SubmissionRef | null;
  waitsOn?: string | null; session?: string | null;
  evidence?: 'found' | 'missing' | 'commit' | (string & {}) | null;
  setUp?: 'local' | 'deployed' | (string & {}) | null;
  verdict?: Verdict | (string & {}) | null;
  hold?: QuestHold | 'none' | null;
};
/**
 * A taker's submission in a closure graph (FABRIC-CLOSE1a): the operation that made it, when, and its note. `session`
 * (FABRIC-CLOSE1c4): the session that said its `ready`, the one a reopen of it is told; absent where none did, and from a host before it.
 */
export type QuestSubmission = SubmissionRef & { at: string; note?: string | null; session?: string | null };
/**
 * A quest's reopen of its own submission (FABRIC-CLOSE1c4): the reopen by its machine and sequence, the submission it reopened
 * (`of`), the person's words as they wrote them, and when; `session` (CLOSE1c8), the session that said the reopened `ready`, the one
 * told of it, absent where none did and from a host before it.
 */
export type QuestReopen = SubmissionRef & { of: SubmissionRef; note?: string | null; at: string; session?: string | null };
/**
 * Why a done, or a submission, is held (EVID1a, REVIEWENV1b, FABRIC-CLOSE1a), as the service spells it: `source-reopened` is a
 * chain's step whose source the person reopened (FABRIC-CLOSE1c). A newer host may name another.
 */
export type QuestHold =
  | 'departed' | 'evidence-unread' | 'evidence-missing' | 'unreviewed' | 'source-reopened' | (string & {});
/**
 * One set-up a set-up step said (REVIEWENV1b, D154 point 9): the commit Daoris read from its tree, where to look (absent on
 * another machine for a local one, whose tab is on the machine that showed it), what it shows, how to show it again, the folder
 * served or a review run's command, the session that said it (absent for the person's own), whether it was local, and when.
 * `machine` and `sequence` are what a verdict names it by (REVIEWENV1b3).
 */
export type QuestSetUp = QuestSetUpWire;
/** Which set-up a verdict answers, whole: the machine that kept it and its sequence there (REVIEWENV1b3). */
export type SetUpRef = { machine: string; sequence: number };
/**
 * The person's verdict (REVIEWENV1b, D154 point 8): `reviewed`, `not-yet` or `skipped`, the set-up it answers and that set-up's
 * commit (none on a skip), their words, and when.
 */
export type QuestVerdict = {
  said: 'reviewed' | 'not-yet' | 'skipped' | (string & {});
  setUp?: { machine?: string | null; sequence?: number | null } | null;
  commit?: string | null; words?: string | null; at?: string | null; machine?: string | null;
};
/** A review's verdict as the person gives it at the quest's door (REVIEWENV1b), in the service's words. */
export type Verdict = 'reviewed' | 'not-yet' | 'skipped';
/** One thing the person requires (DRIFT1c): their words, quoted as they said them, and the check that proves them. */
export type QuestRequirement = QuestRequirementResponse;
/**
 * How a done answered one requirement (DRIFT1d): `met` with how its check was met, or `departed` with the reason and
 * `quote`, the person's words it turns on, each in the done's words. The host leaves the absent half out.
 */
export type QuestAnswer = QuestAnswerResponse;
/** One lane a repository declares (D115 §2.2), as the registry answers it: its words, never its paths. */
export type Lane = { id: string; title: string; summary: string; steward: boolean };
/**
 * A move that lost the race to the remote. `attempted` is the status it tried to reach; `note` is its
 * own words, verbatim. `machine` and `sequence` name it on every machine — what a dismissal names.
 */
export type QuestConflict = QuestConflictResponse;
/**
 * A bring-forward's fresh take (SYNCMERGE1b0, D178 points 3 and 8): its machine and sequence, when it was made, and how it stands as a
 * claim reads one: `held` once a remote numbered it, `unconfirmed` while it is only here, `lost` where it lost again.
 */
export type QuestForward = SubmissionRef & { at: string; standing: 'held' | 'unconfirmed' | 'lost' | (string & {}) };
/**
 * Where a circle stands on this machine (SYNC6a), read from its own host without reaching the
 * remote. A circle with no remote here answers `wired: false` and nothing else of use.
 */
export type SyncStanding = {
  workspace: string; wired: boolean; ahead: number; behind: string[]; conflicts: string[];
  /** When a pass last reached the remote; a wall does not move it. */
  synced?: string | null;
  /** When a pass last ran, reaching the remote or not. */
  tried?: string | null;
  /** The wall the last pass hit, in the host's words; null when it reached the remote. */
  problem?: string | null;
  /**
   * The machines its remote last said had not declared they know a bound quest (FABRIC-CLOSE1i2), as the last pass heard them: empty
   * where every one did, absent where no pass heard the handshake. Each holds a bind through the remote off until it declares or is retired.
   */
  undeclared?: string[];
};
/** One step of a chain. `{parent}` in its words becomes the id of the quest it follows. */
export type QuestStep = { to: string; title: string; body: string };
/** One module of a repository, as its code map names it (MAP3a). `path` is repository-relative. */
export type CodeModule = { id: string; path: string; summary: string };
export type CodeDependency = { from: string; to: string; kind: string };
/**
 * A repository's code map (MAP3a). `file` is which candidate was read, absent when it keeps none;
 * `problem` is why the file was refused WHOLE, verbatim — and then both lists are empty, because a
 * half-drawn map reads as a whole one. Both are ABSENT rather than null on the wire: the host omits
 * a null field. `fed` is where the map came from when no checkout here was read (MAP3b, MAP3e): a
 * teammate's, brought down by the sync, at the commit the circle's deployment holds it.
 */
export type CodeMapAnswer = {
  repository: string; file?: string | null; problem?: string | null;
  modules: CodeModule[]; dependencies: CodeDependency[];
  fed?: ProvenanceResponse;
};
export type QuestAction = { quest: Quest; message: string };
/**
 * The person's retirement of a machine at a circle's remote (FABRIC-CLOSE1i2): whether the remote retired one (false where it had heard
 * nothing from it), the machines it still counts undeclared after it, and the host's sentence.
 */
export type MachineRetired = { workspace: string; machine: string; retired: boolean; undeclared: string[]; message: string };
/**
 * An ask: a sentence entered at a WORKSPACE rather than at a repository, held as a record of what became of it, as the service
 * declares it (`generated/service.ts`). `tier` names what answered it, on every record; a tier this page has no word for is shown
 * as the service wrote it. A part a host older than its field leaves out reads as none. Its files' `kept` is what the shell pushes in
 * place of a path (PUSH1d3).
 */
export type Ask = Omit<AskResponse, 'attachments'> & { attachments: QuestAttachment[] };
/**
 * `Done` is derived by the service: the ask became quests and every one of them has closed. The default list leaves it out, as it
 * leaves out a closed one.
 */
export type AskState = Ask['state'];
/** A repository the declarations tier proposed, with the words its declarations share with the ask. */
export type DeclarationMatch = DeclarationMatchResponse;
/** One session's request for a go-ahead: who asked, on which quest, when, and why, in its words. */
export type GoAheadRequest = GoAheadRequestResponse;
/**
 * A go-ahead: the person's yes for one act outside a repository, asked once on the ask. `kind` is the service's word (write,
 * release, push, sign-in, run); `on` and `act` are the session's words for where it lands and what it touches, shown as written.
 * `answer` is absent while it waits on the person.
 */
export type GoAhead = GoAheadResponse;
export type AskAction = Omit<AskActionResponse, 'ask' | 'quest'> & { ask: Ask; quest?: Quest | null };
export type Registration = {
  repository: string; adopted: boolean; registered: boolean; summary?: string;
  owns: string[]; accepts: string[]; packs: string[]; entries: number; workspace?: string;
  /**
   * The checkout on THIS machine — answered only to a loopback caller of a local host (D46/D47 §4),
   * so it is absent in a browser over a remote and absent for a teammate's mirrored registration. Its
   * presence is exactly the question "is there a working tree here to manage".
   */
  root?: string;
  joined?: boolean;
  sharesKnowledge?: boolean;
  /**
   * The repositories it says it uses (D91), from its manifest's `domain.uses`, read by the host's rule.
   * Absent from a host older than the field, which says the same as empty: nothing declared.
   */
  uses?: string[];
  /**
   * The lanes it declares (D115 §2.2), each addressed as `repository:id`. Absent from a host older than
   * the field, which says the same as empty: a quest asks the whole repository.
   */
  lanes?: Lane[];
  /**
   * Whether a quest can be addressed to it — the exchange's own judgement, answered by the host so no
   * page re-derives it (D70): an adopter, or a repository registered on that machine with a root, which
   * a browser is never told. Absent from a host older than D70, where only an adopter could be asked.
   */
  addressable?: boolean;
};

/** Whether a repository can be asked — the host's answer, or adoption where an older host gives none. */
export const canBeAsked = (registration: Registration): boolean =>
  registration.addressable ?? registration.adopted;
export type SessionState =
  | 'queued' | 'starting' | 'working' | 'awaiting-person'
  | 'completed' | 'declined' | 'stood-down' | 'failed' | 'stopped';
/**
 * Which states still hold their repository — the wire half of `Session.Active`, exhaustive here for
 * the reason the tone map is: the rail, the quest card and the attention band all ask it, and a set
 * spelled out three times acquires a tenth state in two of them. Spelled beside the states, so the
 * sessions' query reads the open ones from a pushed list as the service does (PUSH1d2); `design/ui`
 * says it again for the views.
 */
export const SESSION_ACTIVE: ReadonlySet<SessionState> =
  new Set<SessionState>(['queued', 'starting', 'working', 'awaiting-person']);
/** D205 §9.3: what a start asked for, tagged so an explicit `default` differs from an account default. */
export type ActorValue = { source: 'entry' | 'account-default' | 'command' | 'environment'; value?: string | null; carried: boolean };
export type ActorAsked = { agent: string; model: ActorValue; effort: ActorValue };
/** STAFFPOLICY1c2: why a role's policy chose a start's choice; machine-local, absent under no policy, on a mirror and on an old record. */
export type ActorChosen = { rule: string; by: string; condition?: string | null; from?: string | null };
export type ActorRan = { models: string[]; more: number; entries: { at: string; acknowledged?: string | null; effort?: string | null }[]; entriesMore: number };
export type ActorOverride = { model?: string; effort?: string };
/**
 * A session's RECORD (D46) — the process lives on the driving machine, never here.
 *
 * `kind` is how it was entered (D49 §3): the driver planned a `driven` one from a quest, a person
 * opened a `chat`. Everything else about them is the same, which is the point — and it is why
 * `quest` is optional: a conversation may serve none.
 */
export type Session = {
  /** D205: machine-local provenance; absent on old records, mirrors and remote responses. */
  actor?: string | null;
  choice?: string | null;
  asked?: ActorAsked | null;
  override?: ActorOverride | null;
  ran?: ActorRan | null;
  chosen?: ActorChosen | null;
  id: string; quest?: string | null; repository: string; adapter: string; state: SessionState;
  kind?: 'driven' | 'chat';
  note?: string; evidence?: string; created: string; updated: string; workspace?: string;
  /** The harness version observed at spawn (D49 §4) — which tool produced this, answerable later. */
  harnessVersion?: string | null;
  /**
   * Which named credential profile it ran as. Machine-local and answered only to the machine that
   * ran it, exactly like the transcript — so a browser over a keyed remote sees null here, and that
   * is the guarantee working rather than a field the service forgot to fill.
   */
  profile?: string | null;
  /**
   * The working tree it holds (D51) — the unit of exclusion, and a filesystem path outright, so it
   * is guarded exactly as the profile and the transcript are. Null is the repository's registered
   * root: a session that asked for no tree of its own, or a record from a machine that rightly sent
   * no path. Surfaces show its last segment, which is the branch the tree was cut for.
   */
  tree?: string | null;
  /**
   * The commit its tree stood at when the spawn began (SURF6): what its review is measured from. Guarded as the tree
   * is, so a browser over a keyed remote and a teammate's record have none; the review's frame names it while git
   * reads the range (REVIEW4).
   */
  baseCommit?: string | null;
  /** The ask an intake session answers (D65 §1b) — absent for every other session. */
  ask?: string | null;
  /**
   * The second opinion this session reads another session's work for, as its reviewer (XAGENT1c, D155 point 11): a chat on no
   * quest, one turn and no words. This machine's own, like the opinion: a teammate's record and a browser have none.
   */
  opinion?: string | null;
  /**
   * The session a helper answers one question for (MIXAGENT2a-i, D163): a chat on no quest that reads and changes nothing. This
   * machine's own, as the opinion is: a teammate's record and a browser have none.
   */
  serves?: string | null;
  /**
   * The piece a partner's record writes, the ask's id (COAGENT1b, D199 point 1): kept only beside `serves`, which names its lead.
   * This machine's own, as `serves` is: a teammate's record and a browser have none.
   */
  piece?: string | null;
  /**
   * The session this one continues (MIXAGENT1a-iii, D163's note): a conversation handed over, or a quest's carry-on of a session
   * handed over or cut off by a limit. Why (`press`, `near`, `limit`, `agent` or `curve`) and who asked (`person`, `daoris` or
   * `agent`) come with it, the three kept together or not at all. This machine's own, as the opinion is: null on any other
   * session, on a record from before the link, on a teammate's record and in a browser.
   */
  from?: string | null;
  fromWhy?: string | null;
  fromBy?: string | null;
  /**
   * The words waiting on the record (MSG1a, D137 §2.4), each with the marks that say whose it is. A park holding a word of the
   * person's is answered (ANSWER1b, D131) and goes on at the driver's next look rather than waits (ANSWER1c): `personsSaid` reads
   * it, the one read of a park's answer (ONEWAY1m). Answered to this machine only: absent over a keyed remote and on a teammate's.
   */
  said?: SaidWord[] | null;
  /**
   * An account's limit made its failure (TOOL4c): a `failed` record the page says is *limited*, quietly (LIMITRESUME1a, D206
   * point 1). It names no account, so every caller is told; false or absent on any other record and from a host before it.
   */
  limit?: boolean;
  /**
   * The access setup a driven session started under (AUTONOMY1a, D184 point 1): each pause by name with its value, as the driver
   * resolved it at the start. This machine's own, as the opinion is: null on any other kind, on a record from before the field, on
   * a teammate's record and in a browser.
   */
  access?: Record<string, string> | null;
  /**
   * The note's lines, one part each, beside the note's English (LANG1a, D142 point 2): the page words a coded part in the
   * reader's language and shows a words part as written (LANG1b, `work/note.ts`). Absent or null for a record from before
   * parts, and from a host older than the field, whose note is shown as kept.
   */
  noteParts?: NotePart[] | null;
  /**
   * The work the record was opened for (FOUNDATION1c, D212; the foundation design §2.2): `build`, `chat`, `ask-daoris`, `intake`,
   * `help`, `partner` or `review`, read from its marks by the host and judged in .NET, pushed beside `kind`. This machine's own, as the
   * opinion is: absent on a teammate's record, which has none here, in a browser, and from a host before the field, which `identity.ts`
   * then draws by the marks themselves.
   */
  work?: string | null;
  /**
   * The quest a chat took through its own connector (CHATFLOW1a), its `quest` staying null; pushed for the at-work view, which words a
   * chat's wait by it (COWORKVIEW1b-i). Absent for every other record.
   */
  tookQuest?: string | null;
};

/**
 * One word waiting on a record, as the service answers it to this machine (MSG1a): its id, words, when and its files' names, and the
 * marks whose it is is read from (`from`, `by`, `helper`, `key`; COMMS1a's one rule, `wordSenderOf`), each absent on the person's.
 */
export type SaidWord = {
  id: string; text: string; at: string; files: string[]; reopens?: boolean;
  from?: string | null; by?: string | null; helper?: string | null; key?: string | null; level?: string | null; question?: string | null;
};

/**
 * Whose words a words part is (LANG1a, D142 point 1): the agent's, the person's, a program's passed through, or `before`,
 * an English note from before parts carried whole. A newer writer may name another, which is shown as written too.
 */
export type NoteBy = 'agent' | 'person' | 'program' | 'before';

/**
 * One line of a session's note (LANG1a, D142 point 2; the language design §3). A **coded part** is Daoris's: its code, the
 * facts its line carries (values, never sentences: ids, a list of quest ids, an exit code, a count of minutes, an ISO 8601
 * UTC moment, a reason's code with its own values), and `text`, its English as its writer wrote it, which a page that does
 * not know the code shows. A **words part** is someone's words and whose they are.
 */
export type NotePart =
  | { code: string; values?: Record<string, unknown> | null; text?: string | null; words?: undefined }
  | { words: string; by?: NoteBy | (string & {}) | null; code?: undefined };

/**
 * The "repository" Ask Daoris's sessions are recorded in (HELP1a, D89): a colon is in no folder name,
 * so no registered repository is ever called this.
 *
 * @remarks A twin (`.claude/knowledge/twins.md`): the service's `SessionLedger.HelpRepository` and the
 * driver's `HelpRoom.Repository` spell it too, and each side's test holds the spelling.
 */
export const HELP_REPOSITORY = 'daoris:help';

/**
 * What one re-scan changed — and, when the semantic half failed, the service's own sentence.
 *
 * `absent` names registered repositories whose checkout is no longer where the registry says it is
 * (D48 §3). Named rather than skipped: a repository that quietly stops contributing looks exactly like
 * one with nothing to say, and the count still looks healthy.
 *
 * `embedded` is what the semantic half made of the entries (D123): how many it embedded, the vectors
 * they became, how many were longer than the window and split into pieces, and the window, which is the
 * deployment's. Absent when that half did not run, which is never a zero.
 */
export type RefreshReport = {
  entries: number; repositories: number; withheld: number; semanticError?: string; absent?: string[];
  embedded?: Embedded;
};

/** One pass of the semantic half (D123), as the refresh answer carries it. */
export type Embedded = { entries: number; pieces: number; split: number; window: number };

/** What a retire actually did — and its sentence, which is mostly about what it did NOT do. */
export type Retired = { repository: string; retired: boolean; message: string };

/**
 * A query string from the parameters that are actually set — null and undefined are omitted, so a
 * door asked with no workspace is asked for every circle it holds (D48 §4: the door never invents a
 * default, and neither does this).
 */
import { PAGE_SHOWN } from './results';
import { onHost } from './host';
import { ROUTES, type RouteName, type Routes } from './generated/service';

function qs(params: Record<string, string | number | boolean | null | undefined>): string {
  const pairs = Object.entries(params)
    .filter(([, value]) => value !== null && value !== undefined)
    .map(([key, value]) => `${key}=${encodeURIComponent(String(value))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

/**
 * A request, and a refusal of Daoris's own when it reaches nobody (UX5 U29).
 *
 * @remarks
 * With the machine's service stopped, `fetch` rejects with the browser's words, and the toast said
 * *Failed to fetch*: nothing a person can act on. `SERVICE_UNREACHABLE` says the service is not
 * answering and that the screen shows the last it said, which is what the query layer keeps showing.
 * A cancellation stays a cancellation: the query layer cancels on purpose, and nobody is told.
 */
async function reach(path: string, init?: RequestInit): Promise<Response> {
  try {
    // At the host's loopback address when the page is on the desktop engine's app origin (D92).
    return await fetch(onHost(path), init);
  } catch (error) {
    if ((error as { name?: unknown } | null)?.name === 'AbortError') throw error;
    throw Object.assign(new Error('the service is not answering'), { code: 'SERVICE_UNREACHABLE' });
  }
}

async function answered(response: Response): Promise<Response> {
  if (response.ok) return response;
  // The service reports its own errors as { error }; anything else means the host itself failed, and the status line is the
  // only thing that says anything useful. The status rides along, so a reader can tell "the service has none by that name"
  // (an entry gone since it was found) from a service that failed; the sentence is still the service's own.
  const body = await response.json().catch(() => null);
  throw Object.assign(new Error(body?.error ?? `${response.status} ${response.statusText}`), { status: response.status });
}

async function get<T>(path: string, signal?: AbortSignal): Promise<T> {
  return (await answered(await reach(path, { signal }))).json() as Promise<T>;
}

type Scalar = string | number | boolean | null | undefined;

/**
 * A route the service declares (`generated/service.ts`), by its record's name: its path filled from the request, the rest in a
 * read's query or a write's body (but for what it names for its query), a write sent as every write is. Typed by the declaration.
 */
async function send<N extends RouteName>(name: N, request: Routes[N]['request'], signal?: AbortSignal) {
  const route = ROUTES[name];
  const rest = { ...request } as Record<string, unknown>;
  const path = route.path.replace(/\{(\w+)[^}]*\}/g, (_, key: string) => {
    const value = rest[key];
    delete rest[key];
    return encodeURIComponent(String(value));
  });
  if (route.method === 'GET') {
    const response = await answered(await reach(`${path}${qs(rest as Record<string, Scalar>)}`, { signal }));
    return { answer: await response.json() as Routes[N]['answer'], response };
  }
  const query: Record<string, Scalar> = {};
  for (const key of route.query as readonly string[]) {
    query[key] = rest[key] as Scalar;
    delete rest[key];
  }
  const response = await wrote(`${path}${qs(query)}`, rest, route.method as 'POST' | 'DELETE');
  return { answer: await response.json() as Routes[N]['answer'], response };
}

/**
 * One second opinion as `GET /api/opinions` lists it (XAGENT1g4c), the fields the join reads: `working` is the session whose
 * work was read, `session` the reviewer's own record. The wire carries more (findings, tier, state); the page asks no more here.
 */
export type OpinionRow = { id: string; working: string; session: string };

/** Whether a read failed because the service holds nothing by that name: an answer, where any other failure is not. */
export const notFound = (error: unknown): boolean => (error as { status?: unknown } | null)?.status === 404;

/**
 * The person key (PERSONDOOR1f; D156 point 2, the person-door design §2.1 and §2.2): the key the shell minted for the host it
 * started, asked of it over the bridge as the page loads (`usePersonKey`) and held here, in this module's memory alone, never in
 * the page's storage, a cookie or a URL, and sent as `Daoris-Person` on every write. A reload asks again. None where the shell
 * holds none (a host it adopted, a plain browser), and every write is sent as it was.
 *
 * @remarks
 * The key goes only where `reach` sends a write: the host's loopback address inside the shell, or the page's own origin, which
 * inside the shell is its host's (`onHost`, D92). Only the shell answers a key, so a page outside it never holds one.
 */
let personKey: string | null = null;
let askKey: (() => Promise<string | null>) | null = null;

/** The key `ask` answers, or none where it answers none or cannot be asked (an older shell). */
async function askedKey(): Promise<string | null> {
  if (!askKey) return null;
  try {
    const key = await askKey();
    return typeof key === 'string' && key.length > 0 ? key : null;
  } catch {
    return null;
  }
}

/**
 * Hold the key `ask` answers, and ask it again when a write is refused `stale` (a host the shell started again, design §2.3);
 * null lets go of it. Answers the key now held.
 */
export async function holdPersonKey(ask: (() => Promise<string | null>) | null): Promise<string | null> {
  askKey = ask;
  personKey = await askedKey();
  return personKey;
}

/** Whether the page holds the person key: the service's confirmations are the page's to answer only then (their door takes it). */
export const holdsPersonKey = (): boolean => personKey !== null;

const write = (path: string, body: unknown, method: 'POST' | 'DELETE') => {
  const headers = {
    ...(body === undefined ? {} : { 'content-type': 'application/json' }),
    ...(personKey ? { 'Daoris-Person': personKey } : {}),
  };
  return reach(path, {
    method,
    ...(Object.keys(headers).length > 0 ? { headers } : {}),
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
};

async function post<T>(path: string, body: unknown, method: 'POST' | 'DELETE' = 'POST'): Promise<T> {
  return (await wrote(path, body, method)).json() as Promise<T>;
}

/** A write's response once the host took it: the person key sent, asked again once where it was stale, a refusal thrown. */
async function wrote(path: string, body: unknown, method: 'POST' | 'DELETE'): Promise<Response> {
  let response = await write(path, body, method);
  // PERSONDOOR1f (design §2.3, §5.2): a key from an earlier start of the host is refused `stale`. The shell is asked again and
  // the write sent once more with the key it holds now; a second refusal is the host's sentence, said as every refusal is.
  if (response.status === 403 && personKey !== null) {
    const refused = await response.clone().json().catch(() => null);
    if (refused?.code === 'stale') {
      personKey = await askedKey();
      response = await write(path, body, method);
    }
  }
  if (!response.ok) {
    // The service's refusal sentence IS the contract — a publish to a non-adopter, a decline with no
    // reason, a respond racing a take that already won. It reaches the person verbatim (D38). Its code rides along where the
    // host gives one (D156 §5.2's `person-only`, `stale`; a confirmation's `not-made`), so a reader can tell the person's door from the rest.
    const parsed = await response.json().catch(() => null);
    throw Object.assign(new Error(parsed?.error ?? `${response.status} ${response.statusText}`), {
      status: response.status, ...(typeof parsed?.code === 'string' ? { code: parsed.code } : {}),
    });
  }
  return response;
}

/** A nonce for the host's proof of possession (design §2.3): random, in base64url, never kept. */
function nonce(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(24));
  return btoa(String.fromCharCode(...bytes)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/**
 * An ask the window confirms at the service's own door (PERSONDOOR1b, D156 §4.2; ONEWAY1n-i): the door's act in the host's table and
 * every value the request carries, as `GET /api/confirmations` lists them. No clock ends one: it waits until the person answers or its
 * asker withdraws it, and the host makes the request at the Confirm. `session` is the session that asked, absent for a terminal's;
 * once it ends, `ended`, and for one made, the door's `status`, its `answer` and, where it refused, its `reason`. Never its token.
 */
export type ServiceConfirmation = {
  id: string; state: string; method: string; path: string; route: string; act: string;
  values: { name: string; type: string; value: string }[]; fields: { name: string; type: string; value: string }[];
  asked: string; session?: string | null; ended?: string | null; status?: number | null; answer?: string | null; reason?: string | null;
};

/**
 * A write only the person may make (D156 §3.2's person-only doors, PERSONDOOR1), as REVIEWENV1g presses them: a review's
 * verdict, their own set-up step, an ask, and what a review is set to.
 *
 * @remarks
 * **The seam where the person's key goes.** `post` sends the key the bridge answered (PERSONDOOR1f, `holdPersonKey`), held in
 * memory, as `Daoris-Person` on every write; these doors refuse without it, so they come through here, and a refusal for want of
 * it is the host's sentence, said as every refusal is. The other person-only doors (the yes, the done, a go-ahead's answer, a
 * delete) call `post` and take the key there with every write. A local host enforces its doors only once its starter hands it a
 * key (PERSONDOOR1a), and the page holds one only where the shell answers one.
 */
const asThePerson = <T>(path: string, body: unknown): Promise<T> => post<T>(path, body);

export const api = {
  status: async (signal?: AbortSignal) => (await send('ReadStatus', {}, signal)).answer,
  /**
   * Whether the host holds a person key of its own (D156 §2.3): asked with a fresh nonce, a host holding one answers its `proof`,
   * and one holding none answers as it always did. The page holding no key beside a host that holds one is the adoption notice's
   * case (PERSONDOOR1f): the person's own acts are refused there.
   */
  hostHoldsAKey: async (signal?: AbortSignal): Promise<boolean> =>
    typeof (await send('ReadStatus', { prove: nonce() }, signal)).answer.proof === 'string',
  /** What waits at the service for the person's confirmation (PERSONDOOR1b): a read, whatever key the page holds. */
  confirmations: (signal?: AbortSignal) => get<ServiceConfirmation[]>('/api/confirmations', signal),
  /**
   * The person's Confirm or Refuse of one, a door of theirs alone, which takes the key `post` sends. At a Confirm the host makes the
   * request (ONEWAY1n-i): one its door refused is a 409 with that door's sentence, said as every refusal is.
   */
  answerConfirmation: (id: string, confirm: boolean) =>
    post<{ confirmation: ServiceConfirmation; message: string }>(
      `/api/confirmations/${encodeURIComponent(id)}/${confirm ? 'confirm' : 'refuse'}`, undefined),
  // Every cross-repository read below takes the scope (WSP5; D48 §4): null asks for every circle the
  // deployment holds, and the switcher says so; a name asks for that one, and nothing else is mixed in.
  repositories: async (workspace: string | null, signal?: AbortSignal) => (await send('ListRepositories', { workspace }, signal)).answer,
  entry: async (id: string, signal?: AbortSignal) => (await send('ReadEntry', { id }, signal)).answer,
  // A search and a convergence ask one MORE than the view shows, so it can say whether there are more without guessing
  // (`results.ts`); the extra row is never rendered.
  search: async (q: string, localOnly: boolean, workspace: string | null, signal?: AbortSignal): Promise<SearchAnswer> => {
    const { answer, response } = await send('Search', { q, localOnly, limit: PAGE_SHOWN + 1, workspace }, signal);
    // Which tier answered rides a header, so the body stays the array every other client reads.
    return { hits: answer, tier: response.headers.get('x-daoris-tier') };
  },
  convergence: async (minimumSimilarity: number, workspace: string | null, signal?: AbortSignal) =>
    (await send('FindConvergence', { minimumSimilarity, limit: PAGE_SHOWN + 1, workspace }, signal)).answer,
  quests: async (repository: string | null, includeClosed: boolean, workspace: string | null, signal?: AbortSignal) =>
    (await send('ListQuests', { includeClosed, repository, workspace }, signal)).answer,
  /** One quest's moves, in the order its history replays, on both hosts; null from a host without the door, which answers 404. */
  questMoves: async (id: string, signal?: AbortSignal): Promise<QuestMove[] | null> => {
    try {
      const { answer } = await send('ReadQuestMoves', { id }, signal);
      if (!Array.isArray(answer?.moves)) throw new Error('the service answered something other than the quest\'s moves');
      return answer.moves as QuestMove[];
    } catch (error) {
      if (notFound(error)) return null;
      throw error;
    }
  },
  // Asks (D65 §1a), a local host's doors: the screen twin of `daoris ask` (INT4c, D50). Files
  // travel whole, as a quest's do, because the host keeps them until the ask becomes quests.
  asks: async (includeClosed: boolean, workspace: string | null, signal?: AbortSignal) =>
    (await send('ListAsks', { includeClosed, workspace }, signal)).answer,
  ask: (body: {
    workspace: string; sentence: string; links?: string[]; attachments?: { name: string; content: string }[];
    to?: string;
    /** The person's review choice for its work (REVIEWENV1b, design §1.5): `off`, `on` or an environment's name, and their words. */
    review?: string; reviewWords?: string;
    /** The person's short title for it (HEAD2e), at most 40 characters; the service refuses a longer one in its words. */
    short?: string;
  }) => send('PostAsk', body).then(({ answer }) => answer),
  // The person's review choice on an ask's page, or its intake's proposal applied (REVIEWENV1b, design §1.5): the latest stands.
  chooseAskReview: (id: string, choice: string, words?: string) =>
    send('ChooseAskReview', { id, choice, ...(words ? { words } : {}) }).then(({ answer }) => answer),
  // An intake's kind proposal applied (WORKFLOW1i-c, design §4.3): a post replaces the latest choice whole, so the workflow it
  // has is sent again. No words: the reason is the agent's quoting, not the person's act.
  chooseAskWorkflow: (id: string, kind: string, workflow?: string | null) =>
    send('ChooseAskWorkflow', { id, kind, ...(workflow ? { workflow } : {}) }).then(({ answer }) => answer),
  publishAsk: (id: string, to: string) => send('PublishAsk', { id, to }).then(({ answer }) => answer),
  closeAsk: (id: string, reason: string) => send('CloseAsk', { id, reason }).then(({ answer }) => answer),
  // The person's yes or no to a go-ahead a session asked on their ask (KNOWUSE1a), with their words where they give any;
  // `daoris ask go-ahead` is the terminal's twin. `goesOn` false says no park goes on with the answer (GOAHEAD2's door):
  // a stale go-ahead's refusal runs nothing (GOAHEADSTALE1). Absent, the host reads it as true, as it always has.
  answerGoAhead: (id: string, number: number, approved: boolean, words?: string, goesOn = true) =>
    send('AnswerGoAhead', {
      id, number: String(number), answer: approved ? 'approved' : 'refused', ...(words ? { words } : {}), ...(goesOn ? {} : { goesOn: false }),
    }).then(({ answer }) => answer),
  // Deleting a record made by mistake (D95) — `daoris ask delete` and `quest delete` are the
  // terminal's twins. A refusal is the service's sentence, verbatim, like every other write's.
  deleteAsk: (id: string) => send('DeleteAsk', { id }).then(({ answer }) => answer),
  deleteQuest: (id: string) => send('DeleteQuest', { id }).then(({ answer }) => answer),
  registry: (workspace: string | null, signal?: AbortSignal) =>
    get<Registration[]>(`/api/registry${qs({ workspace })}`, signal),
  // A repository's own code map (MAP3a), read from its committed file — never written to (D32).
  codeMap: (repository: string, signal?: AbortSignal) =>
    get<CodeMapAnswer>(`/api/code-map/${encodeURIComponent(repository)}`, signal),
  // Where a circle stands on this machine (SYNC6a) — the host's store, never the remote itself.
  syncStanding: (workspace: string, signal?: AbortSignal) =>
    get<SyncStanding>(`/api/sync${qs({ workspace })}`, signal),
  sessions: (repository: string | null, includeClosed: boolean, workspace: string | null, signal?: AbortSignal) =>
    get<Session[]>(`/api/sessions${qs({ includeClosed, repository, workspace })}`, signal),
  // The second opinions this machine holds (XAGENT1g4c): which session read which, to say whose work a reviewer's row reads.
  // This machine's own (D47 §4): another host answers 404.
  opinions: (signal?: AbortSignal) => get<OpinionRow[]>('/api/opinions', signal),
  // The registration lifecycle (D48 §3/§7). Registration state only: no file is written, no doctrine
  // is touched, and adding a repository still needs the shell — a page may not name a machine path.
  registerRepository: (body: {
    // Whether the shell found a manifest there (D70): registered is addressable, adopted is disciplined.
    repository: string; root?: string; workspace?: string; adopted?: boolean;
    // `uses` (D91) replaces the row's, and absent is none: a door that read the file states it (MANAGE1).
    domain?: { summary?: string; owns: string[]; accepts: string[]; uses?: string[] };
    packs?: string[]; join?: boolean; shareKnowledge?: boolean;
  }) => post<{ repository: string; workspace: string }>('/api/registry', body),
  wireRepository: (repository: string, workspace: string) =>
    post<{ repository: string; workspace: string }>(
      `/api/registry/${encodeURIComponent(repository)}/workspace`, { workspace }),
  retireRepository: (repository: string) =>
    post<Retired>(`/api/registry/${encodeURIComponent(repository)}`, undefined, 'DELETE'),
  // `daoris repository import <folder> [--workspace W]`'s screen door (D50, D75, D77): a folder's subdirectories
  // registered at once, into the named workspace when there is one. The answer's `message` is the
  // service's sentence, and it is shown as said.
  importFolder: (folder: string, workspace?: string) =>
    post<{ folder: string; imported: number; repositories: string[]; message: string; workspace?: string }>(
      '/api/registry/import', { folder, ...(workspace ? { workspace } : {}) }),
  // Files travel whole to the local host, the only one that serves the platform; a shared one refuses content.
  publishQuest: (quest: {
    from: string; to: string; title: string; body: string;
    links?: string[]; attachments?: { name: string; content: string }[]; then?: QuestStep[];
    /** The person's short title (SESSUX1j), at most 40 characters; the service refuses a longer one in its words. */
    short?: string;
  }) => send('PublishQuest', quest).then(({ answer }) => answer),
  /** Where a kept file is opened — the local host's own route, which answers this machine only. */
  attachmentUrl: (quest: string, sha256: string) =>
    onHost(`/api/quests/${encodeURIComponent(quest)}/attachments/${encodeURIComponent(sha256)}`),
  respondQuest: (id: string, action: 'take' | 'done' | 'decline', reason: string | null) =>
    send('RespondQuest', { id, action, reason }).then(({ answer }) => answer),
  // The person's yes to a done's departure from what they required: what it held goes on.
  acceptQuest: (id: string) => send('AcceptQuest', { id }).then(({ answer }) => answer),
  // The person's own done, with their words where they wrote any; never respond's door, which is an agent's done.
  personDone: (id: string, note: string | null) =>
    send('MarkQuestDone', { id, ...(note ? { note } : {}) }).then(({ answer }) => answer),
  // The person's reopen of the submission the page showed, named whole, which the host refuses once another is current.
  reopenQuest: (id: string, submission: SubmissionRef, note: string | null) =>
    send('ReopenQuest', {
      id, submission: { machine: submission.machine, sequence: submission.sequence }, ...(note ? { note } : {}),
    }).then(({ answer }) => answer),
  // The person's word to go on with a step its source's reopen held, refused until the source's submission is unheld.
  continueQuest: (id: string) => send('ContinueQuest', { id }).then(({ answer }) => answer),
  // The person's verdict on a review (REVIEWENV1g, D154 point 8; design §3.3, §3.6): `reviewed` or `not-yet` on the set-up their
  // view drew, named whole, which the host refuses as stale where a newer one was shown since (REVIEWENV1b3); a skip names
  // none. `daoris quest review` is the terminal's twin.
  reviewQuest: (id: string, verdict: Verdict, words?: string | null, setUp?: SetUpRef | null) =>
    send('ReviewQuest', {
      id, verdict, ...(words ? { words } : {}), ...(setUp && verdict !== 'skipped' ? { setUp } : {}),
    }).then(({ answer }) => answer),
  // The person's *Set it up in `<environment>`* (REVIEWENV1b, design §2.1, §3.6): a set-up step published following a done quest.
  setUpStep: (id: string, environment: string) =>
    send('PublishSetUpStep', { id, environment }).then(({ answer }) => answer),
  // The person retiring a machine the circle's remote still counts (FABRIC-CLOSE1i2): one whose store was deleted, or that left the
  // workspace, no longer holds its binds off; a push from it counts it again.
  retireMachine: (workspace: string, machine: string) =>
    asThePerson<MachineRetired>(`/api/sync/machines/${encodeURIComponent(machine)}/retire${qs({ workspace })}`, {}),
  // A person dismissing one conflict (SYNC6c), by the name every machine knows it by.
  dismissConflict: (id: string, machine: string, sequence: number) =>
    send('DismissConflict', { id, machine, sequence }).then(({ answer }) => answer),
  // The person bringing this machine's lost take forward by the conflict's name; a refusal, or a loss again, is a 409.
  forwardConflict: (id: string, machine: string, sequence: number) =>
    send('ForwardConflict', { id, machine, sequence }).then(({ answer }) => answer),
  // Through the same helper as every write, so the service's refusal — a shared deployment is fed,
  // not scanned — reaches the person as the sentence, never as a bare status code.
  refresh: () => post<RefreshReport>('/api/refresh', {}),
};
