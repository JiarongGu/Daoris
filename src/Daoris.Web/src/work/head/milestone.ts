// The milestone of a unit of work (FABRIC-MILESTONE1b; the fabric events design §D0): whether its end was reached, the result kept,
// its cleanup (pending, with what waits and why, or completed, with what went) and the recovery retained and until when. The
// driver derives it at each read from the records it keeps (`WORK_STATE`, `daoris-driver work state`); the page stores nothing.
import type { TFunction } from 'i18next';
import { treeName } from '../identity';

/** One thing a unit owns, with the role it has and what became of it (the inventory's entry). */
export type MilestoneEntry = {
  unit: string;
  /** `tree`, `session-branch`, `landing-branch`, `line`, `moved-aside`, `recovery-ref`, `auto-landing`, `history` or `end`. */
  kind: string;
  resource: string;
  /** `temporary`, `result`, `evidence` or `recovery`. */
  role: string;
  /** `kept`, `retained`, `removed`, `blocked` or `pending`. */
  disposition: string;
  /** Why, in the driver's clause: what waits, what blocks, what the proof was. Shown as recorded. */
  detail: string;
  /** A recovery's purge date, where one stands. */
  until?: string | null;
  proof?: string | null;
  /** The repository a recovery belongs to (RECOVERYVIEW1); a shell older than it answers none. */
  repository?: string | null;
  /** The commit a recovery ref names (RECOVERYDROP1), which a delete is verified against; a shell older than it answers none. */
  commit?: string | null;
  /** Why a blocked entry is blocked (OVERVIEWNOISE1b), which the page words (`entryDetail`); a shell older than it answers none. */
  code?: string | null;
  /** The values its code is worded with, by name. */
  facts?: Record<string, string> | null;
};

/** The recovery refs of one repository a press of the delete names, each with the commit the page showed (RECOVERYDROP1). */
export type RecoveryDropGroup = { repository: string; refs: { ref: string; commit: string }[] };

/** What a press of the delete answered across its repositories: what went, what was kept and why, and what refused the whole. */
export type RecoveryDropResult = {
  dropped: { repository: string; ref: string; commit: string }[];
  skipped: { repository: string; ref: string; why: string }[];
  /** A repository's whole press refused: not the lease's holder, or no checkout here. The driver's own sentence. */
  refused: string[];
};

/** Whether the end was reached, and on what evidence. */
export type MilestoneEnd = {
  /** `ended`, `handed`, `not-ended`, `dropped` (over without its result, CLEANUP1) or `unknown`. */
  state: string;
  /** The proof's code (`merged`, `empty`, `on-line`, `pull-request`, `landed`, `carried`) where ended. */
  by?: string | null;
  on?: string | null;
  revision?: string | null;
  at?: string | null;
  /** The state in the driver's sentence: what a code the page has no words for is said by. */
  said: string;
};

/**
 * The person's words on a held attempt (FABRIC-PERFORM1g): it happened, it did not (a new attempt), or stop the step. On Daoris's own
 * close (FABRIC-CLOSE1q), whose outcome is its door's answer and never the person's word: record that answer again, ask for it again
 * with the same close, or stop the rest of the work. On a failure kept for the person (FABRIC-PERFORM1k): their new attempt.
 */
export type HeldResolution = 'happened' | 'did-not' | 'stop' | 'record' | 'resend' | 'again';

/** The sender Daoris's own close is held under (FABRIC-CLOSE1q): never a plugin's. */
export const CLOSE_SENDER = 'quest.close';

/**
 * A plugin step's attempt held for the person (FABRIC-PERFORM1g, the fabric events design §D6): nothing else can say what became of it,
 * so its gate holds until their word. `why` is a code the page words (`unknown`, `no-reconcile`, `no-outcome`, `not-current`,
 * `not-on-word`, or the one table's refusal of its outcome); `said` is the driver's sentence for a code the page has no words for.
 */
export type HeldAttempt = {
  unit: string;
  node: string;
  attempt: number;
  plugin: string;
  step: string;
  nodeState: string;
  why: string;
  outcome?: string | null;
  revision?: string | null;
  at: string;
  /** The person's word, recorded and not yet applied by the driver's next look. */
  resolution?: HeldResolution | null;
  resolvedAt?: string | null;
  said: string;
  /** The words the driver offers on it (FABRIC-CLOSE1q): a plugin's three, or a held close's own; absent from a driver before the field. */
  words?: HeldResolution[] | null;
  /** A failure kept for the person's code, as its plugin gave it (FABRIC-PERFORM1k); absent on an attempt held. */
  code?: string | null;
};

/**
 * A unit's node record whose file does not read (FABRIC-PERFORM1j, the fabric events design §D3): nothing on its unit is judged or started
 * until the person mends it. `file` is its name in the records' folder; `unit` the unit it still names, or null; `at` when it was last
 * written; `said` the driver's sentence.
 */
export type UnreadableRecord = {
  file: string;
  unit?: string | null;
  at?: string | null;
  said: string;
};

/**
 * A closure graph's end the milestone tells of (FABRIC-CLOSE1j, the fabric events design §E5), read from its node record's history:
 * ended by the person's own done before its end (`overridden`), naming the nodes it stopped; or closed only in part
 * (`partially-closed`), naming the quest lost with its take and the close node, whose stop is offered (`words`, said through
 * `WORK_HELD` on `attempt`) while that node waits again; or the person's `word` on its close taken back (`word-taken-back`,
 * FABRIC-CLOSE1q4), since the submission of `quest` it names was reopened or replaced, said here where no held row says it any more.
 */
export type GraphEnd = {
  unit: string;
  kind: string;
  at: string;
  nodes?: string[] | null;
  quest?: string | null;
  node?: string | null;
  attempt?: number | null;
  nodeState?: string | null;
  /** The person's stop, recorded and not yet applied by the driver's next look. */
  resolution?: HeldResolution | null;
  words?: HeldResolution[] | null;
  /** FABRIC-CLOSE1q4: the person's word the driver did not take on a `word-taken-back` end, `record` or `stop`. */
  word?: HeldResolution | null;
};

export type MilestoneUnit = {
  unit: string;
  repository: string;
  branch?: string | null;
  session?: string | null;
  end: MilestoneEnd;
  result: MilestoneEntry[];
  cleanup: { state: string; waits: MilestoneEntry[]; gone: MilestoneEntry[] };
  recovery: MilestoneEntry[];
  entries: MilestoneEntry[];
  /** Its plugin steps' attempts held for the person (FABRIC-PERFORM1g), joined from the answer's `held` by the page. */
  held?: HeldAttempt[];
  /** Its closure graph's ends (FABRIC-CLOSE1j), joined from the answer's `ends` by the page. */
  ends?: GraphEnd[];
};

/**
 * What `WORK_STATE` answers: the units, Daoris's own recoveries no unit names (RECOVERYVIEW1, CLEANUP1: a folder moved aside, a
 * recovery ref), what could not be read (a repository with no checkout here), every attempt held for the person, of any unit
 * (FABRIC-PERFORM1g: a quest's too, which no milestone names), every node record that does not read (FABRIC-PERFORM1j), and every
 * closure graph's end (FABRIC-CLOSE1j), a quest's too.
 */
export type WorkState = {
  units: MilestoneUnit[]; recovery: MilestoneEntry[]; unread: string[]; held: HeldAttempt[]; unreadable: UnreadableRecord[];
  /** Absent where a state was composed before the field; `readWorkState` always fills it. */
  ends?: GraphEnd[];
};

/** A quest's unit, as the driver names it (`QuestUnits.IdOf`): what its closure graph's ends name (FABRIC-CLOSE1j2). */
export const questUnit = (quest: string): string => `quest:${quest}`;

/** Whether a closure graph's end offers the person the stop of its close node now (FABRIC-CLOSE1j): offered, and no word of theirs waiting. */
export function endOffersStop(end: GraphEnd): boolean {
  return end.kind === 'partially-closed' && !end.resolution && (end.words ?? []).includes('stop') && typeof end.attempt === 'number';
}

/**
 * A closure graph's end in the reader's language (FABRIC-CLOSE1j, §E5): *closed by you before its end*, naming the nodes it stopped; or
 * *partially closed*, naming the quest lost and why, then where its close node stands: its stop offered, the person's stop recorded, or
 * stopped; or the person's word on its close *not taken*, naming the quest whose submission moved (FABRIC-CLOSE1q4). A kind the page
 * has no words for says nothing.
 */
export function endSentences(t: TFunction, end: GraphEnd): string[] {
  if (end.kind === 'overridden') {
    const nodes = end.nodes ?? [];
    return [nodes.length > 0 ? t('work.milestone.closed.overridden', { nodes: nodes.join(', ') }) : t('work.milestone.closed.overriddenNone')];
  }
  if (end.kind === 'word-taken-back') {
    return [t(end.word === 'record' ? 'work.milestone.closed.takenBack.record' : 'work.milestone.closed.takenBack.stop', { node: end.node ?? '', quest: end.quest ?? '' })];
  }
  if (end.kind !== 'partially-closed') return [];
  const node = end.node ?? '';
  const state = end.resolution ? t('work.milestone.closed.worded', { node })
    : endOffersStop(end) ? t('work.milestone.closed.offer', { node, quest: end.quest ?? '' })
      : end.nodeState === 'stopped' ? t('work.milestone.closed.stopped', { node }) : null;
  return [t('work.milestone.closed.partial', { quest: end.quest ?? '' }), ...(state ? [state] : [])];
}

/** The why codes the page has words for; any other is the one table's refusal of an outcome, said with its code. */
const HELD_WHY = ['unknown', 'no-reconcile', 'no-outcome', 'not-current', 'not-on-word', 'failure-kept', 'tries-spent'] as const;

/**
 * Whether it is a failure kept for the person (FABRIC-PERFORM1k, D173's RUN1e note): its plugin said it failed and Daoris tries it
 * no more by itself, so it waits for the person's new attempt rather than their word on what became of it.
 */
export function heldFailed(held: HeldAttempt): boolean {
  return held.why === 'failure-kept' || held.why === 'tries-spent';
}

/** Why an attempt is held, in the reader's language (FABRIC-PERFORM1g); a held close by what its door answered (FABRIC-CLOSE1q). */
export function heldWhy(t: TFunction, held: HeldAttempt): string {
  if (held.plugin === CLOSE_SENDER) {
    if (held.why === 'not-close-word') return t('work.milestone.held.close.why.notCloseWord');
    // FABRIC-CLOSE1q2: record or stop taken back, since the submission the close names was reopened or replaced.
    if (held.why === 'submission-moved') return t('work.milestone.held.close.why.submissionMoved');
    // FABRIC-CLOSE1y: an unknown close (FABRIC-CLOSE1u) is its door's silence, asked again at each look, never a missing record.
    const answer = held.outcome === 'reached' ? 'closed' : held.outcome === 'failed' ? 'refused' : held.why === 'unknown' ? 'unknown' : 'unanswered';
    return t(`work.milestone.held.close.why.${answer}`, { code: held.why });
  }
  const code = HELD_WHY.find((one) => one === held.why);
  return code ? t(`work.milestone.held.why.${code}`, { code: held.code ?? '' }) : t('work.milestone.held.why.other', { code: held.why });
}

/**
 * The words offered on a held attempt (FABRIC-CLOSE1q): the driver's, where it names them; a plugin's three from a driver before the
 * field, and none on a close there, since such a driver would take a plugin's word for its door's answer.
 */
export function heldWords(held: HeldAttempt): HeldResolution[] {
  if (held.words) return held.words;
  return held.plugin === CLOSE_SENDER ? [] : ['happened', 'did-not', 'stop'];
}

/** A held attempt in one line: its step, its attempt, and why, or the person's word waiting for the next look. */
export function heldSentence(t: TFunction, held: HeldAttempt): string {
  return held.resolution
    ? t('work.milestone.held.worded', { node: held.node, attempt: held.attempt, word: t(`work.milestone.held.word.${held.resolution}`) })
    : t(heldFailed(held) ? 'work.milestone.held.failed' : 'work.milestone.held.step', { node: held.node, attempt: held.attempt, why: heldWhy(t, held) });
}

/** The kinds of thing a unit owns that have a word of the page's; a `line` or an `end` is its resource's own name. */
const KINDS = ['tree', 'session-branch', 'landing-branch', 'moved-aside', 'recovery-ref', 'auto-landing', 'history'] as const;

/**
 * An entry by what it is: its kind in the reader's language, where the page has a word, and its name. A tree is named by its folder,
 * as the rail names it (OVERVIEWNOISE1b): its path is this machine's, and the rail and the band have no room for it (D51 §9).
 */
export function entryName(t: TFunction, entry: Pick<MilestoneEntry, 'kind' | 'resource'>): string {
  const kind = KINDS.find((one) => one === entry.kind);
  const name = entry.kind === 'tree' ? treeName(entry.resource) ?? entry.resource : entry.resource;
  return [kind ? t(`work.milestone.kind.${kind}`) : null, name].filter(Boolean).join(' ');
}

/** The codes of a blocked entry the page has words for (OVERVIEWNOISE1b); any other is said by its detail, as recorded. */
const CODES = ['unfollowed', 'unread', 'unknown-folder', 'unknown-unread', 'unknown-branch', 'unrecorded'] as const;

/** The guards the look's tidy keeps a branch by that the page has words for (FABRIC-END4a's `kept`, by its `why`). */
const KEPT = ['in-use', 'dirty', 'busy', 'hidden', 'nested', 'linked', 'refused', 'unread', 'starting', 'sessions'] as const;

/**
 * Why an entry waits or is blocked, in the reader's language (OVERVIEWNOISE1b): a code the page has words for, worded with its facts
 * and its reason said once; any other entry, and a code or a guard the page has no words for, by the driver's sentence as recorded.
 */
export function entryDetail(t: TFunction, entry: Pick<MilestoneEntry, 'detail' | 'code' | 'facts'>): string {
  const facts = entry.facts ?? {};
  if (entry.code === 'kept') {
    const why = KEPT.find((one) => one === facts.why);
    return why ? t(`work.milestone.code.kept.${why}`) : entry.detail;
  }
  const code = CODES.find((one) => one === entry.code);
  return code ? t(`work.milestone.code.${code}`, { branch: facts.branch ?? '' }) : entry.detail;
}

/**
 * What of a unit waits on the person (design §D0; OVERVIEWNOISE1b, *What needs you* only where they have an act), as .NET's
 * `NeedsAPerson` judges it: where no record names its owner, the entries its cleanup is blocked on, else the unit itself; where its
 * owner is known, the trees its session recorded and no branch record follows, which only the clean-up's press takes. A guard the
 * look keeps a branch by is said on the work's foot, and a wait that is pending ends by itself: neither is the person's.
 */
export function waitsOnPerson(unit: MilestoneUnit): MilestoneEntry[] {
  const blocked = unit.cleanup.waits.filter((entry) => entry.disposition === 'blocked');
  if (unit.end.state !== 'unknown') return blocked.filter((entry) => entry.code === 'unfollowed');
  return blocked.length > 0 ? blocked : unit.entries.slice(0, 1);
}

/** The units whose branch, session or id is `name`: the milestone a branch row says (a session branch or a landing branch). */
export function unitOfBranch(units: readonly MilestoneUnit[], repository: string, branch: string): MilestoneUnit | undefined {
  return units.find((unit) => unit.repository.toLowerCase() === repository.toLowerCase()
    && (unit.branch === branch || unit.entries.some((entry) => (entry.kind === 'session-branch' || entry.kind === 'landing-branch') && entry.resource === branch)));
}

/**
 * The units a name picks, as the driver's `WorkInventories.Named` does (PUSH1c2b): a unit's id or session in any case, a branch exact,
 * a branch equal to `daoris/` and the name, or any session or landing branch entry whose resource is the name. With none named, the
 * whole; with one, no recovery (the route answers none for a unit), only the attempts held of the units picked, and the closure
 * graphs' ends of those units or of the unit the name is (FABRIC-CLOSE1j2).
 */
export function workOf(all: WorkState, unit: string | null): WorkState {
  if (!unit) return all;
  const same = (value: string | null | undefined) => typeof value === 'string' && value.toLowerCase() === unit.toLowerCase();
  const units = all.units.filter((each) => same(each.unit) || same(each.session) || each.branch === unit || each.branch === `daoris/${unit}`
    || each.entries.some((entry) => (entry.kind === 'landing-branch' || entry.kind === 'session-branch') && entry.resource === unit));
  const ids = new Set(units.map((each) => each.unit));
  // FABRIC-PERFORM1j: a record that does not read, by the unit it names; one naming none the route picks by its file's key alone.
  // FABRIC-CLOSE1j2: a closure graph's end by the unit it names too, as the route's `end.Unit == picked`: a quest's (`quest:<id>`),
  // which no unit here carries, is what its page asks for.
  return {
    units, recovery: [], unread: all.unread, held: all.held.filter((each) => ids.has(each.unit)),
    unreadable: all.unreadable.filter((each) => !!each.unit && (ids.has(each.unit) || same(each.unit))),
    ends: (all.ends ?? []).filter((each) => ids.has(each.unit) || each.unit === unit),
  };
}

/** Older shells answer nothing of the kind, and a malformed answer reads as none. */
export function readWorkState(answer: unknown): WorkState {
  const read = answer as
    { units?: unknown; recovery?: unknown; unread?: unknown; held?: unknown; unreadable?: unknown; ends?: unknown } | null | undefined;
  // FABRIC-PERFORM1g: a shell older than the field answers none held; FABRIC-CLOSE1j, none ended.
  const held = Array.isArray(read?.held) ? read.held as HeldAttempt[] : [];
  const ends = Array.isArray(read?.ends) ? read.ends as GraphEnd[] : [];
  const units = Array.isArray(read?.units) ? read.units as MilestoneUnit[] : [];
  return {
    units: units.map((unit) => ({ ...unit, held: held.filter((each) => each.unit === unit.unit), ends: ends.filter((each) => each.unit === unit.unit) })),
    recovery: Array.isArray(read?.recovery) ? read.recovery as MilestoneEntry[] : [],
    unread: Array.isArray(read?.unread) ? read.unread.filter((each): each is string => typeof each === 'string') : [],
    held,
    // FABRIC-PERFORM1j: a shell older than the field answers none.
    unreadable: Array.isArray(read?.unreadable) ? read.unreadable as UnreadableRecord[] : [],
    ends,
  };
}
