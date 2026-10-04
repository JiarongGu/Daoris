// The service's surface. Same origin in production; the dev server proxies /api, so no code path
// differs between the two. Reads everywhere; the writes are service state only — quests and their
// conflicts, asks, registrations and refresh (D36, D38, D48, D65). Doctrine has no write here, by
// design (D31): where a rule should change, the UI proposes the command to run in the repository that
// owns it.

// `workspace` rides every cross-repository shape (D48): it is the unit of sharing, so a view that
// shows material from more than one must be able to say which is which. Optional on the way in —
// a host older than workspaces simply does not send it, and nothing here should crash over that.
export type Status = { semantic: boolean; tier: string; note?: string };
/**
 * Which point in a repository's history a deployment's copy of its knowledge came from (D48 §6).
 *
 * Absent on a deployment that reads its own checkouts — it has no feed, and what it shows is the
 * machine's own state. Present on a shared one, where the index is a *claim about a commit*: naming
 * the commit is what makes staleness something a person can see rather than must assume.
 */
export type Provenance = {
  commit: string; shortCommit: string; committedAt: string; branch: string; origin?: string;
};
export type Repository = {
  name: string; total: number; local: number; canonical: number; workspace?: string;
  fed?: Provenance;
};
export type Hit = {
  id: string; repository: string; kind: string; title: string;
  path: string; excerpt?: string; score: number; workspace?: string;
};
/**
 * A search's hits, and which tier answered them (TIER1): `lexical`, `semantic`, `lexical+semantic`,
 * or `none` when no half answered — which is not the same as nothing matching. Null from a host older
 * than the header.
 */
export type SearchAnswer = { hits: Hit[]; tier: string | null };
export type Entry = {
  id: string; repository: string; kind: string; provenance: string;
  title: string; path: string; body: string; workspace?: string;
};
export type ConvergenceEntry = {
  id: string; repository: string; kind: string; title: string; path: string;
};
export type Convergence = {
  method: 'Identical' | 'Restatement' | 'Convergent';
  similarity: number;
  repositories: string[];
  entries: ConvergenceEntry[];
  suggestion: string;
};
/**
 * A file a quest carries, by name (D65 §2). `path` is where THIS machine keeps it — answered only to a
 * caller on this machine, and only when the bytes are here — so the page reads its PRESENCE as "can be
 * opened" and never shows the path itself: a page does not name a machine path.
 */
export type QuestAttachment = { name: string; sha256: string; bytes: number; path?: string };
export type Quest = {
  id: string; from: string; to: string; title: string; body: string;
  status: 'Open' | 'Taken' | 'Done' | 'Declined';
  note?: string; filed: string; updated: string; workspace?: string;
  /** Absent from a host older than D65 — the same as carrying nothing. */
  links?: string[];
  attachments?: QuestAttachment[];
  /** What this quest's close publishes next (D65 §4), in order — the service does it, not the page. */
  then?: QuestStep[];
  /** The quest whose close published this one, when it is a step of a chain. */
  parent?: string;
  /**
   * Moves another machine made that reached the remote second (D68 §5): kept on the quest for a
   * person, never merged. Absent from a host older than the sync, which is the same as none.
   */
  conflicts?: QuestConflict[];
  /**
   * The question its taker asked another repository and waits on (D79) — a quest id — or absent. A
   * taken quest carrying one is waiting, not stuck: the driver resumes it once that quest closes.
   */
  awaits?: string | null;
  /**
   * The session whose connector published it (SESS1) — absent for a person's publish, a chain step,
   * and a host older than the field, which all say the same: no session asked.
   */
  publishedBy?: string | null;
  /**
   * Whether the host would delete it (D95): open, and nobody has started on it. The service's own
   * judgement, so the page offers *Delete* only where the door would take it. Absent from a host older
   * than the field, and false at a shared deployment, which has no delete door.
   */
  deletable?: boolean;
  /**
   * The lanes of `to` it addresses (D115 §2.2), sorted — asked as `repository:lane+lane`. `to` stays the
   * repository. Absent or empty is a quest to the whole repository, which every older quest is.
   */
  lanes?: string[];
  /**
   * What the person requires of it (DRIFT1c, D133 §3), in the service's order, the numbers its done answers by: each their
   * own words and the check that proves them. Absent or empty is a quest that names none, and a host older than the field.
   */
  requirements?: QuestRequirement[];
  /** How its done answered each requirement (DRIFT1d, §4), by number; absent or empty before a done answered. */
  answers?: QuestAnswer[];
  /**
   * Whether a departure holds it for the person's yes (DRIFT1d): closed done, departing from what they required, so the
   * chain's next step and a quest waiting on it wait. Absent is false. `accepted` is when the person gave the yes.
   */
  held?: boolean;
  accepted?: string | null;
  /**
   * What a list calls it (SESSUX1j, D126 §9): its publisher's short title, or the name the service read from its own
   * words. Absent from a host older than the field, and then its title names it (`questName`).
   */
  short?: string | null;
};
/** One thing the person requires (DRIFT1c): their words, quoted as they said them, and the check that proves them. */
export type QuestRequirement = { quote: string; check: string };
/**
 * How a done answered one requirement (DRIFT1d): `met` with how its check was met, or `departed` with the reason and
 * `quote`, the person's words it turns on, each in the done's words. The host leaves the absent half out.
 */
export type QuestAnswer = { requirement: number; met?: string | null; departed?: string | null; quote?: string | null };
/** One lane a repository declares (D115 §2.2), as the registry answers it: its words, never its paths. */
export type Lane = { id: string; title: string; summary: string; steward: boolean };
/**
 * A move that lost the race to the remote. `attempted` is the status it tried to reach; `note` is its
 * own words, verbatim. `machine` and `sequence` name it on every machine — what a dismissal names.
 */
export type QuestConflict = {
  machine: string; sequence: number; attempted: Quest['status']; note?: string | null; at: string;
};
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
  fed?: Provenance;
};
export type QuestAction = { quest: Quest; message: string };
/**
 * An ask (D65 §1a): a sentence entered at a WORKSPACE rather than at a repository, held as a record of
 * what became of it. `tier` names what answered it, on every record (`model-decoupling`); a tier this
 * page has no word for is shown as the service wrote it. Optional fields are absent rather than null
 * on the wire — the host leaves nulls out.
 */
/**
 * `Done` is derived by the service (USE1c): the ask became quests and every one of them has closed.
 * The default list leaves it out, as it leaves out a closed one.
 */
export type AskState = 'Open' | 'Proposed' | 'Published' | 'Done' | 'Closed';
/** A repository the declarations tier proposed, with the words its declarations share with the ask. */
export type DeclarationMatch = { repository: string; score: number; matched: string[] };
export type Ask = {
  id: string; workspace: string; sentence: string; state: AskState; tier: string;
  asked: string; updated: string;
  /** Who asked, when the door knows; absent is this machine's person. */
  asker?: string | null;
  /** Why it was closed, or the service's sentence about the receiver it named — verbatim. */
  note?: string | null;
  links: string[];
  /** Its files, by name — kept on this machine until the ask becomes quests. `path` is never shown. */
  attachments: QuestAttachment[];
  proposal: DeclarationMatch[];
  /** The quests it became, in the order they were published. */
  quests: string[];
  /** The intake session that served it (D65 §1b), once one opened — absent for every other ask. */
  intake?: string | null;
  /**
   * Whether the host would delete it, with every quest asked by it (D95): nothing stands on any of
   * them. The service's judgement, like a quest's; absent from a host older than the field.
   */
  deletable?: boolean;
  /**
   * The go-aheads its sessions asked the person for (KNOWUSE1a, D135 §2), oldest first, one per act; absent from a host
   * older than them.
   */
  goAheads?: GoAhead[];
};
/** One session's request for a go-ahead: who asked, on which quest, when, and why, in its words. */
export type GoAheadRequest = { session: string; quest?: string | null; at: string; why: string };
/**
 * A go-ahead (KNOWUSE1a): the person's yes for one act outside a repository, asked once on the ask. `kind` is the
 * service's word (write, release, push, sign-in, run); `on` and `act` are the session's words for where it lands and
 * what it touches, shown as written. `answer` is absent while it waits on the person.
 */
export type GoAhead = {
  number: number; kind: string; on: string; act: string; state: 'asked' | 'approved' | 'refused';
  asked: GoAheadRequest[];
  answer?: { approved: boolean; words?: string | null; at: string } | null;
  /** The go-ahead whose words it shared where they could not tell, when that is why it was asked again. */
  near?: number | null;
};
export type AskAction = { ask: Ask; message: string; quest?: Quest | null };
/** What a delete did (D95): the record it removed, and the service's sentence, shown as said. */
export type Deleted = { id: string; message: string };
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
 * A session's RECORD (D46) — the process lives on the driving machine, never here.
 *
 * `kind` is how it was entered (D49 §3): the driver planned a `driven` one from a quest, a person
 * opened a `chat`. Everything else about them is the same, which is the point — and it is why
 * `quest` is optional: a conversation may serve none.
 */
export type Session = {
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
   * The person's answer to a driven session that parked to ask them (STANDDOWN2). The service keeps the record parked
   * with it until the driver's next look (ANSWER1b, D131), so a parked record that holds one goes on rather than waits
   * (ANSWER1c). The person's own words, answered to this machine only: null over a keyed remote and on a teammate's.
   */
  answer?: string | null;
  /**
   * The note's lines, one part each, beside the note's English (LANG1a, D142 point 2): the page words a coded part in the
   * reader's language and shows a words part as written (LANG1b, `work/note.ts`). Absent or null for a record from before
   * parts, and from a host older than the field, whose note is shown as kept.
   */
  noteParts?: NotePart[] | null;
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

async function get<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await reach(path, { signal });
  if (!response.ok) {
    // The service reports its own errors as { error }; anything else means the host itself failed,
    // and the status line is the only thing that will say anything useful.
    const body = await response.json().catch(() => null);
    // The status rides along, so a reader can tell "the service has none by that name" (an entry gone since it
    // was found, FRAME1f) from a service that failed; the sentence is still the service's own.
    throw Object.assign(new Error(body?.error ?? `${response.status} ${response.statusText}`), { status: response.status });
  }
  return response.json() as Promise<T>;
}

/** Whether a read failed because the service holds nothing by that name: an answer, where any other failure is not. */
export const notFound = (error: unknown): boolean => (error as { status?: unknown } | null)?.status === 404;

async function post<T>(path: string, body: unknown, method: 'POST' | 'DELETE' = 'POST'): Promise<T> {
  const response = await reach(path, {
    method,
    ...(body === undefined
      ? {}
      : { headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) }),
  });
  if (!response.ok) {
    // The service's refusal sentence IS the contract — a publish to a non-adopter, a decline with no
    // reason, a respond racing a take that already won. It reaches the person verbatim (D38).
    const parsed = await response.json().catch(() => null);
    throw new Error(parsed?.error ?? `${response.status} ${response.statusText}`);
  }
  return response.json() as Promise<T>;
}

export const api = {
  status: (signal?: AbortSignal) => get<Status>('/api/status', signal),
  // Every cross-repository read below takes the scope (WSP5; D48 §4): null asks for every circle the
  // deployment holds, and the switcher says so; a name asks for that one, and nothing else is mixed in.
  repositories: (workspace: string | null, signal?: AbortSignal) =>
    get<Repository[]>(`/api/repositories${qs({ workspace })}`, signal),
  entry: (id: string, signal?: AbortSignal) =>
    get<Entry>(`/api/entry?id=${encodeURIComponent(id)}`, signal),
  search: async (q: string, localOnly: boolean, workspace: string | null, signal?: AbortSignal): Promise<SearchAnswer> => {
    // One MORE than the view shows, so the view can say whether there are more without guessing
    // (`results.ts`). The extra row is never rendered.
    const response = await reach(`/api/search${qs({ q, localOnly, limit: PAGE_SHOWN + 1, workspace })}`, { signal });
    if (!response.ok) {
      const body = await response.json().catch(() => null);
      throw new Error(body?.error ?? `${response.status} ${response.statusText}`);
    }
    // Which tier ANSWERED this search (TIER1, D24) rides a header, so the body stays the array every
    // other client reads. A host older than it sends none, and the view falls back to the configured.
    return { hits: await response.json() as Hit[], tier: response.headers.get('x-daoris-tier') };
  },
  convergence: (minimumSimilarity: number, workspace: string | null, signal?: AbortSignal) =>
    // One more than the view shows, for the same reason search asks for one more (`results.ts`).
    get<Convergence[]>(`/api/convergence${qs({ minimumSimilarity, limit: PAGE_SHOWN + 1, workspace })}`, signal),
  quests: (repository: string | null, includeClosed: boolean, workspace: string | null, signal?: AbortSignal) =>
    get<Quest[]>(`/api/quests${qs({ includeClosed, repository, workspace })}`, signal),
  // Asks (D65 §1a), a local host's doors: the screen twin of `daoris-driver ask` (INT4c, D50). Files
  // travel whole, as a quest's do, because the host keeps them until the ask becomes quests.
  asks: (includeClosed: boolean, workspace: string | null, signal?: AbortSignal) =>
    get<Ask[]>(`/api/asks${qs({ includeClosed, workspace })}`, signal),
  ask: (body: {
    workspace: string; sentence: string; links?: string[]; attachments?: { name: string; content: string }[];
    to?: string;
  }) => post<AskAction>('/api/asks', body),
  publishAsk: (id: string, to: string) =>
    post<AskAction>(`/api/asks/${encodeURIComponent(id)}/publish`, { to }),
  closeAsk: (id: string, reason: string) =>
    post<AskAction>(`/api/asks/${encodeURIComponent(id)}/close`, { reason }),
  // The person's yes or no to a go-ahead a session asked on their ask (KNOWUSE1a), with their words where they give any;
  // `daoris-driver ask --go-ahead` is the terminal's twin.
  answerGoAhead: (id: string, number: number, approved: boolean, words?: string) =>
    post<AskAction>(`/api/asks/${encodeURIComponent(id)}/go-aheads/${number}`, {
      answer: approved ? 'approved' : 'refused', ...(words ? { words } : {}),
    }),
  // Deleting a record made by mistake (D95) — `daoris-driver ask --delete` and `quest delete` are the
  // terminal's twins. A refusal is the service's sentence, verbatim, like every other write's.
  deleteAsk: (id: string) => post<Deleted>(`/api/asks/${encodeURIComponent(id)}`, undefined, 'DELETE'),
  deleteQuest: (id: string) => post<Deleted>(`/api/quests/${encodeURIComponent(id)}`, undefined, 'DELETE'),
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
  // `daoris import <folder> [--workspace W]`'s screen door (D50, D75, D77): a folder's subdirectories
  // registered at once, into the named workspace when there is one. The answer's `message` is the
  // service's sentence, and it is shown as said.
  importFolder: (folder: string, workspace?: string) =>
    post<{ folder: string; imported: number; repositories: string[]; message: string; workspace?: string }>(
      '/api/registry/import', { folder, ...(workspace ? { workspace } : {}) }),
  // Files travel WHOLE to the local host, which keeps them under this machine's home (D65 §2); a
  // shared deployment refuses content outright, and the platform is only ever served by a local one.
  publishQuest: (quest: {
    from: string; to: string; title: string; body: string;
    links?: string[]; attachments?: { name: string; content: string }[]; then?: QuestStep[];
    /** The person's short title (SESSUX1j), at most 40 characters; the service refuses a longer one in its words. */
    short?: string;
  }) => post<QuestAction>('/api/quests', quest),
  /** Where a kept file is opened — the local host's own route, which answers this machine only. */
  attachmentUrl: (quest: string, sha256: string) =>
    onHost(`/api/quests/${encodeURIComponent(quest)}/attachments/${encodeURIComponent(sha256)}`),
  respondQuest: (id: string, action: 'take' | 'done' | 'decline', reason: string | null) =>
    post<QuestAction>(`/api/quests/${encodeURIComponent(id)}/respond`, { action, reason }),
  // The person's yes to a done's departure from what they required (DRIFT1d2, D133 §4): what it held goes on. A local
  // host's door, as a delete is; `daoris-driver quest accept` is the terminal's twin.
  acceptQuest: (id: string) => post<QuestAction>(`/api/quests/${encodeURIComponent(id)}/accept`, {}),
  // A person dismissing one conflict (SYNC6c), by the name every machine knows it by.
  dismissConflict: (id: string, machine: string, sequence: number) =>
    post<QuestAction>(`/api/quests/${encodeURIComponent(id)}/conflicts/dismiss`, { machine, sequence }),
  // Through the same helper as every write, so the service's refusal — a shared deployment is fed,
  // not scanned — reaches the person as the sentence, never as a bare status code.
  refresh: () => post<RefreshReport>('/api/refresh', {}),
  // The person's answer to a driven session that parked to ask them (STANDDOWN2): the record stays parked
  // with their words, and the same session goes on with them at the driver's next look (D131); where it
  // cannot, a new session carries the quest on in the same tree — `daoris-driver answer` is the twin.
  answerSession: (id: string, answer: string | null) =>
    post<{ message: string }>(`/api/sessions/${encodeURIComponent(id)}/answer`, { answer }),
};
