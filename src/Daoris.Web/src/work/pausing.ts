import type { Quest } from '../api';

// Pausing and abandoning an ask's work or a quest's on the screen (PAUSE1e, D132, `docs/2026-10-02-pause-and-clean-up-design.md`
// §2.6, §3.1, §3.2, §4.2, §7.1): the shapes the driver answers (`bridge/work.ts` asks them) and what each says, as catalogue
// lines. Pure, so the ask's page, the quest's page and a session's header read one rule, a molecule may read it, and every
// plan the driver could answer is a test's argument.

/** A work's scope, as the driver spells it: an ask's, or one quest's. */
export type WorkScopeName = 'ask' | 'quest';

/** What a pause, a resume or an abandon names: exactly one ask or one quest. */
export type WorkTarget = { scope: WorkScopeName; id: string };

/** Whose pause holds a quest (D132 point 3): the tick's `pausedBy`, `SESSION_GROUPS`' and `WORK_PLAN`'s. */
export type PausedByFact = { scope: WorkScopeName; id: string };

/** What a pause does with one piece (design §2.1), the driver's word for it (`PauseAct`). */
export type PauseAct = 'paused' | 'closed' | 'elsewhere' | 'stopped' | 'parked' | 'intake' | 'teammate' | 'ended';

/** What an abandon does with one piece (design §3.2), the driver's word for it (`AbandonAct`). */
export type AbandonAct = 'decline' | 'stop' | 'end' | 'archive' | 'discard' | 'delete' | 'close' | 'keep' | 'none';

/** Why an abandon keeps a piece (design §3.2), the driver's word for it (`AbandonWhy`). */
export type AbandonWhy =
  | 'taken-elsewhere' | 'taken-outside' | 'done' | 'teammate' | 'review' | 'landed' | 'elsewhere' | 'checked-out'
  | 'unknown' | 'in-use' | 'unreached' | 'refused' | 'gone';

/** One quest of the work, and what a pause and an abandon would do with it. */
export type WorkPlanQuest = {
  quest: string; title: string; to: string; status: Quest['status'];
  /** How it joined the work (design §1): asked by the ask, the quest named, or published by a session of the work. */
  joined: 'asked' | 'named' | 'published';
  /** For a quest a session published, that session. */
  by?: string | null;
  pause: PauseAct;
  pausedBy?: PausedByFact | null;
  key: string;
  abandon: AbandonAct;
  kept?: AbandonWhy | null;
  machine?: string | null;
  /** Whether its decline applies only while it is open (PAUSE1c). */
  whileOpen?: boolean;
};

/** One session of the work, and what a pause and an abandon would do with it. */
export type WorkPlanSession = {
  session: string; repository: string; state: string; quest?: string | null;
  intake?: boolean; teammate?: boolean; branch?: string | null;
  pause: PauseAct;
  key: string;
  abandon: AbandonAct;
  /** Whether it is archived last, once stopped or ended. */
  archive?: boolean;
  kept?: AbandonWhy | null;
  machine?: string | null;
};

/** One tree of the work, or its branch alone: its repository and branch, never its path (platform language §4). */
export type WorkPlanTree = {
  repository: string; branch: string; sessions: string[]; key: string;
  abandon: AbandonAct;
  kept?: AbandonWhy | null;
  /** Its tree went without git being told: only the branch is left. */
  gone?: boolean;
  tip?: string | null;
  commits?: number | null;
  uncommitted?: number | null;
  /** Up to five uncommitted files, relative to the repository. */
  files?: string[];
  /** For a tree some of whose commits are elsewhere: a ref that holds one. */
  where?: string | null;
};

/** A tree an abandon discarded, or a branch it deleted alone, with the tip `git branch` brings it back at. */
export type AbandonedTree = {
  repository: string; branch: string; tip?: string | null; commits?: number | null; uncommitted?: number | null;
  sessions?: string[]; alone?: boolean;
};

/** A piece an abandon kept, with why; `detail` is the service's words for a refusal, and never in the record. */
export type AbandonKeep = {
  piece: string; why: AbandonWhy; machine?: string | null; where?: string | null; changed?: boolean | null; detail?: string | null;
};

/** What the remote made of a shared decline (design §5.2). */
export type DeclineAnswer = { quest: string; answer: 'confirmed' | 'lost' | 'unconfirmed' };

/** This machine's record of one abandon (`abandoned.json`, design §4.2): fixed words, never a path. */
export type AbandonEntry = {
  at: string; door: string; reason: string; declined: string[]; closed: boolean; trees: AbandonedTree[];
  stopped: string[]; archived: string[]; stayed: AbandonKeep[]; declines: DeclineAnswer[];
};

/** `WORK_PLAN`'s answer: the work, what a pause and an abandon would do with each piece, its pause and its last abandon. */
export type WorkPlan = {
  scope: WorkScopeName;
  id: string;
  /** Whether a pause would hold anything: a quest open or taken here, or a proposed ask's intake. */
  pausable: boolean;
  /** This scope's own pause, as `driver.json` keeps it, or null. */
  paused: { at?: string | null; stopped: { quest: string; session: string }[] } | null;
  quests: WorkPlanQuest[];
  sessions: WorkPlanSession[];
  trees: WorkPlanTree[];
  landings: { session: string; repository: string; branch: string }[];
  abandon: {
    abandonable: boolean;
    /** What the second press sends back: exactly what this list held. */
    pieces: string[];
    closes?: string | null;
    abandoned?: AbandonEntry | null;
  };
};

/** `WORK_PAUSE`'s answer. */
export type PauseAnswer = {
  scope: WorkScopeName; id: string; did: 'paused' | 'nothing'; already: boolean;
  stopped: { session: string; quest?: string | null }[];
  kept: { session: string; quest?: string | null; why: string; machine?: string | null }[];
};

/** `WORK_RESUME`'s answer: what it released, and what still holds each quest by the planner's verdict (null: unread). */
export type ResumeAnswer = {
  scope: WorkScopeName; id: string; did: 'resumed' | 'not-paused';
  released: { quest?: string | null; session: string }[];
  holds: { quest: string; verdict: string; reason: string }[] | null;
};

/** `WORK_ABANDON`'s answer: what went, what stayed, what changed since the list, and each shared decline's answer. */
export type AbandonAnswer = {
  scope: WorkScopeName; id: string; did: 'abandoned' | 'nothing';
  listed: number; went: number;
  changed: AbandonKeep[]; failed: AbandonKeep[]; joined: string[];
  declined: string[]; closed: boolean;
  stopped: { session: string; quest?: string | null }[];
  discarded: AbandonedTree[];
  archived: string[];
  stayed: AbandonKeep[];
  declines: DeclineAnswer[];
  stillPaused: boolean;
};

/**
 * What a page is handed of this machine's driver for its ask or its quest (PAUSE1e, design §7.1): the plan, and the three
 * presses. Absent in a browser, which has no driver: the page offers none of the three and names the terminal's commands.
 */
export type WorkDoor = {
  /** The plan as the driver answered it; null while it is asked, when the acts wait rather than guess. */
  plan: WorkPlan | null;
  /** Whether its workspace syncs with a remote, so that another machine may take an open quest of it (design §5.1). */
  wired: boolean;
  /** A press on its way: the acts wait for it. */
  busy: boolean;
  /** The last abandon's answer here, and when it came, said at once before the record catches up. */
  outcome?: { answer: AbandonAnswer; at: string } | null;
  onPause: (done: () => void) => void;
  onResume: () => void;
  /** The second press: the person's reason and exactly the pieces the first press listed. */
  onAbandon: (reason: string, pieces: readonly string[], done: () => void) => void;
};

/** A sentence the page says, as a catalogue key and its values; the caller adds `what`, the scope's own name. */
export type Line = { key: string; values?: Record<string, unknown> };

/** A notice: a line, its tone, and for a quest a resume left held, the hold whose sentence it names. */
export type WorkNotice = Line & { tone: 'ok' | 'error'; hold?: { quest: string; verdict: string; reason: string } };

/** The catalogue's `t`, as far as these lines need it. */
export type Translate = (key: string, options?: Record<string, unknown>) => string;

/** The scope's own name in the reader's language: *ask #a1*, *quest #q1*. */
export const scopeName = (t: Translate, target: WorkTarget) => t(`work.scope.${target.scope}`, { id: target.id });

/** A line said in the reader's language, with the scope's own name as `what`. */
export const say = (t: Translate, line: Line, target: WorkTarget) => t(line.key, { ...line.values, what: scopeName(t, target) });

/**
 * Whether a workspace syncs with a remote on this machine, so another machine may take an open quest of it (design §5.1): the
 * machine's wiring names it, or the environment names the machine's remote whole. A workspace unstated is the default, as
 * the driver compares it (`RemoteTarget.Workspace`). No wiring read is none.
 */
export function wiredFor(
  wiring: { fromEnvironment?: boolean; remotes?: readonly { workspace: string }[] } | null | undefined, workspace: string | null | undefined,
): boolean {
  if (!wiring) return false;
  if (wiring.fromEnvironment) return true;
  const named = workspace?.trim() || 'default';
  return (wiring.remotes ?? []).some((remote) => (remote.workspace.trim() || 'default') === named);
}

/** The sender the service writes for a quest an ask asked (`AskDesk.SenderOf`, D65 §4). */
const ASKED_BY = /^ask #(\S+)$/;

/** The ask a quest was asked by, from its sender; null for a quest a repository asked. */
export function askOf(quest: Pick<Quest, 'from'> | null | undefined): string | null {
  return quest ? ASKED_BY.exec(quest.from)?.[1] ?? null : null;
}

/**
 * What an ask's or a quest's header offers (design §7.1), each where it applies and absent where it does not, never
 * disabled (D119 §3.2): *Pause…* while a pause would hold something and it is not paused here, *Resume* while it is,
 * *Abandon…* while the reader would take anything. No answer offers nothing: an act is never guessed.
 */
export function workOffers(plan: WorkPlan | null | undefined) {
  if (!plan) return { pause: false, resume: false, abandon: false, paused: false };
  const paused = plan.paused !== null && plan.paused !== undefined;
  return { pause: plan.pausable && !paused, resume: paused, abandon: plan.abandon.abandonable === true, paused };
}

/**
 * What a pause asks before it ends work in flight (design §2.6): how many running sessions it stops, then, where they
 * apply, that another machine may still take an open quest (a wired workspace's) and that a running intake keeps reading.
 * Null where it stops nothing: such a pause applies at once, with a notice, since nothing is lost.
 */
export function pauseAsk(plan: WorkPlan, { wired }: { wired: boolean }): Line[] | null {
  const stops = plan.sessions.filter((session) => session.pause === 'stopped').length;
  if (stops === 0) return null;
  const lines: Line[] = [{ key: 'work.pause.ask.stops', values: { count: stops } }];
  const open = plan.quests.filter((quest) => quest.status === 'Open' && quest.pause === 'paused');
  if (wired && open.length > 0) {
    lines.push({ key: 'work.pause.ask.elsewhere', values: { quests: open.map((quest) => `#${quest.quest}`).join(', ') } });
  }
  if (plan.sessions.some((session) => session.pause === 'intake')) lines.push({ key: 'work.pause.ask.intake' });
  return lines;
}

/** A piece of the abandon's list: its key, its name, what the abandon does with it, and what a tree holds. */
export type GoingRow = { piece: string; name: Line; act: Line; holds?: Line[] };

/** A piece the abandon keeps: its key, its name, and why. */
export type StayingRow = { piece: string; name: Line; why: Line };

/** A piece named by its key alone, as the answer and the record carry it. A kind this page does not know is said raw. */
export function pieceName(piece: string): Line {
  const at = piece.indexOf(':');
  const kind = at > 0 ? piece.slice(0, at) : '';
  const rest = at > 0 ? piece.slice(at + 1) : '';
  switch (kind) {
    case 'quest': return { key: 'work.abandon.piece.questId', values: { quest: rest } };
    case 'session': return { key: 'work.abandon.piece.session', values: { session: rest } };
    case 'ask': return { key: 'work.abandon.piece.ask', values: { ask: rest } };
    case 'tree': {
      // `tree:<repository>:<branch>`: a repository's name holds no colon, and a branch may hold slashes.
      const split = rest.indexOf(':');
      if (split > 0) return { key: 'work.abandon.piece.tree', values: { repository: rest.slice(0, split), branch: rest.slice(split + 1) } };
      break;
    }
  }
  return { key: 'work.abandon.piece.raw', values: { piece } };
}

/** Why a piece stays, in the words of design §3.2; a piece kept because it changed since the list says so. */
function whyLine(keep: Pick<AbandonKeep, 'why' | 'machine' | 'where' | 'detail' | 'changed'>): Line {
  const changed = keep.changed === true;
  switch (keep.why) {
    case 'taken-elsewhere':
      return keep.machine
        ? { key: 'work.abandon.why.takenElsewhere', values: { machine: keep.machine } }
        : { key: 'work.abandon.why.takenElsewhereUnnamed' };
    case 'taken-outside': return { key: 'work.abandon.why.takenOutside' };
    case 'done': return { key: 'work.abandon.why.done' };
    case 'teammate': return { key: 'work.abandon.why.teammate', values: { machine: keep.machine ?? '' } };
    case 'review': return { key: changed ? 'work.abandon.why.reviewChanged' : 'work.abandon.why.review' };
    case 'landed': return { key: changed ? 'work.abandon.why.landedChanged' : 'work.abandon.why.landed' };
    case 'elsewhere':
      return keep.where
        ? { key: 'work.abandon.why.elsewhere', values: { where: keep.where } }
        : { key: 'work.abandon.why.elsewhereUnnamed' };
    case 'checked-out': return { key: 'work.abandon.why.checkedOut' };
    case 'in-use': return { key: 'work.abandon.why.inUse' };
    case 'unreached': return { key: 'work.abandon.why.unreached' };
    case 'refused':
      return keep.detail ? { key: 'work.abandon.why.refused', values: { detail: keep.detail } } : { key: 'work.abandon.why.refusedUnsaid' };
    case 'gone': return { key: 'work.abandon.why.gone' };
    default: return { key: 'work.abandon.why.unknown' };
  }
}

/** Up to five files, and an ellipsis where the tree holds more (design §3.2). */
const filesOf = (files: readonly string[] | undefined, count: number) =>
  (files ?? []).join(', ') + (count > (files ?? []).length ? ', …' : '');

/**
 * What the abandon's first press lists (design §3.1, §3.2): every piece it takes, in the order its second press takes
 * them, with what it does, and every piece it keeps, with why. A discarded tree says how many commits it holds that are
 * nowhere else and how many files carry uncommitted changes, naming up to five, never a path on this machine.
 */
export function abandonList(plan: WorkPlan): { goes: GoingRow[]; stays: StayingRow[] } {
  const goes: GoingRow[] = [];
  const stays: StayingRow[] = [];

  const questName = (quest: WorkPlanQuest): Line => ({ key: 'work.abandon.piece.quest', values: { quest: quest.quest, title: quest.title } });
  const sessionName = (session: WorkPlanSession): Line => session.intake
    ? { key: 'work.abandon.piece.intake', values: { session: session.session } }
    : session.quest
      ? { key: 'work.abandon.piece.sessionOn', values: { session: session.session, quest: session.quest } }
      : { key: 'work.abandon.piece.session', values: { session: session.session } };
  const treeName = (tree: WorkPlanTree): Line => ({ key: 'work.abandon.piece.tree', values: { repository: tree.repository, branch: tree.branch } });

  for (const quest of plan.quests) {
    if (quest.abandon === 'decline') {
      goes.push({ piece: quest.key, name: questName(quest), act: { key: quest.whileOpen ? 'work.abandon.act.declineOpen' : 'work.abandon.act.declineTaken' } });
    } else if (quest.abandon === 'keep' && quest.kept) {
      stays.push({ piece: quest.key, name: questName(quest), why: whyLine({ why: quest.kept, machine: quest.machine }) });
    }
  }

  for (const session of plan.sessions) {
    const act = session.abandon;
    if (act === 'stop' || act === 'end') {
      const review = !session.archive;
      goes.push({
        piece: session.key,
        name: sessionName(session),
        act: { key: `work.abandon.act.${act}${review ? 'Review' : ''}` },
      });
    } else if (act === 'archive') {
      goes.push({ piece: session.key, name: sessionName(session), act: { key: 'work.abandon.act.archive' } });
    } else if (act === 'keep' && session.kept) {
      stays.push({ piece: session.key, name: sessionName(session), why: whyLine({ why: session.kept, machine: session.machine }) });
    }
  }

  for (const tree of plan.trees) {
    if (tree.abandon === 'discard' || tree.abandon === 'delete') {
      const holds: Line[] = [{ key: 'work.abandon.holds.commits', values: { count: tree.commits ?? 0 } }];
      const uncommitted = tree.uncommitted ?? 0;
      if (uncommitted > 0) holds.push({ key: 'work.abandon.holds.uncommitted', values: { count: uncommitted, files: filesOf(tree.files, uncommitted) } });
      goes.push({ piece: tree.key, name: treeName(tree), act: { key: `work.abandon.act.${tree.abandon}` }, holds });
    } else if (tree.abandon === 'keep' && tree.kept) {
      stays.push({ piece: tree.key, name: treeName(tree), why: whyLine({ why: tree.kept, where: tree.where }) });
    }
  }

  if (plan.abandon.closes) {
    goes.push({ piece: `ask:${plan.abandon.closes}`, name: { key: 'work.abandon.piece.ask', values: { ask: plan.abandon.closes } }, act: { key: 'work.abandon.act.close' } });
  }
  return { goes, stays };
}

/** What went and what stayed, as the page shows them after an abandon (design §4.2). */
export type AbandonOutcome = { went: Line[]; stayed: StayingRow[]; stillPaused: boolean };

const ids = (quests: readonly string[]) => quests.map((quest) => `#${quest}`).join(', ');

/**
 * What went and what stayed, from the second press's answer or from `abandoned.json`'s record of it (design §4.2): the
 * quests declined, the ask closed, the sessions stopped, each tree discarded or branch deleted with the tip `git branch`
 * brings it back at while git keeps its commits, the sessions archived, each shared decline's answer; then each piece kept,
 * with why. The answer also says what changed since the list, what a step could not take, what joined the work after
 * the list, and whether the scope stays paused; the record keeps those as kept pieces, in fixed words.
 */
export function outcomeOf(said: AbandonAnswer | AbandonEntry): AbandonOutcome {
  const answer = 'did' in said ? said : null;
  const stopped = answer ? answer.stopped.map((stop) => stop.session) : (said as AbandonEntry).stopped;
  const trees = answer ? answer.discarded : (said as AbandonEntry).trees;

  const went: Line[] = [];
  if (said.declined.length > 0) went.push({ key: 'work.abandon.went.declined', values: { quests: ids(said.declined) } });
  if (said.closed) went.push({ key: 'work.abandon.went.closed' });
  if (stopped.length > 0) went.push({ key: 'work.abandon.went.stopped', values: { sessions: stopped.join(', ') } });
  for (const tree of trees) {
    went.push({
      key: tree.alone ? 'work.abandon.went.deleted' : 'work.abandon.went.discarded',
      values: { repository: tree.repository, branch: tree.branch, tip: tree.tip ?? '' },
    });
  }
  if (said.archived.length > 0) went.push({ key: 'work.abandon.went.archived', values: { sessions: said.archived.join(', ') } });
  for (const decline of said.declines) went.push({ key: `work.abandon.declines.${decline.answer}`, values: { quest: decline.quest } });

  const kept = (keep: AbandonKeep): StayingRow => ({ piece: keep.piece, name: pieceName(keep.piece), why: whyLine(keep) });
  const stayed: StayingRow[] = said.stayed.map(kept);
  if (answer) {
    stayed.push(...answer.changed.map((keep) => kept({ ...keep, changed: true })));
    stayed.push(...answer.failed.map(kept));
    stayed.push(...answer.joined.map((piece) => ({ piece, name: pieceName(piece), why: { key: 'work.abandon.why.joined' } })));
  }
  return { went, stayed, stillPaused: answer?.stillPaused === true };
}

/**
 * The last abandon a page shows (design §4.2): the second press's answer, said at once, else this machine's record of it,
 * which a later look reads. Null where there is neither.
 */
export function lastAbandon(work: Pick<WorkDoor, 'plan' | 'outcome'> | null | undefined): { outcome: AbandonOutcome; at: string } | null {
  if (work?.outcome && work.outcome.answer.did === 'abandoned') return { outcome: outcomeOf(work.outcome.answer), at: work.outcome.at };
  const entry = work?.plan?.abandon.abandoned;
  return entry ? { outcome: outcomeOf(entry), at: entry.at } : null;
}

/** What a pause says (design §2.1): one line for the pause, and one failure for each session it meant to stop and could not. */
export function pauseNotices(answer: PauseAnswer): WorkNotice[] {
  if (answer.did === 'nothing') return [{ key: 'work.pause.nothing', values: {}, tone: 'ok' }];
  const notices: WorkNotice[] = [answer.stopped.length > 0
    ? { key: 'work.pause.done.stopped', values: { count: answer.stopped.length }, tone: 'ok' }
    : { key: answer.already ? 'work.pause.done.already' : 'work.pause.done.nothingStopped', values: {}, tone: 'ok' }];
  // Parked, an intake and a teammate's are kept by design (§2.1); these three the pause meant to stop.
  for (const kept of answer.kept.filter((each) => ['elsewhere', 'not-running', 'unanswered'].includes(each.why))) {
    notices.push({ key: 'work.pause.unreached', values: { session: kept.session }, tone: 'error' });
  }
  return notices;
}

/**
 * What a resume says (design §2.4): that the driver's next look plans the work, then what still holds a quest of it,
 * once: one quest by its hold's own sentence, several as a count, since each quest's page says why under *Sitting*.
 */
export function resumeNotices(answer: ResumeAnswer): WorkNotice[] {
  if (answer.did === 'not-paused') return [{ key: 'work.pause.notPaused', values: {}, tone: 'ok' }];
  const notices: WorkNotice[] = [{ key: 'work.pause.resumed', values: {}, tone: 'ok' }];
  const holds = answer.holds ?? [];
  if (holds.length === 1) notices.push({ key: 'work.pause.stillSits', values: { quest: holds[0].quest }, hold: holds[0], tone: 'ok' });
  else if (holds.length > 1) notices.push({ key: 'work.pause.stillSitMany', values: { count: holds.length }, tone: 'ok' });
  return notices;
}

/**
 * What an abandon says (design §3.1): how many of the pieces the list held went, and how many changed since and were kept.
 *
 * @remarks
 * **A partial abandon is not a plain success** (PAUSE1h, as HIST1n made a partial clear): only one that took every piece it
 * listed says `ok`. One that kept some, because they changed since the list or the disk would not let them go (the driver's
 * `went` is what was listed less both), says so in the error's tone; the page's *What stayed* says which.
 */
export function abandonNotice(answer: AbandonAnswer): WorkNotice {
  if (answer.did === 'nothing') return { key: 'work.abandon.nothing', values: {}, tone: 'ok' };
  const changed = answer.changed?.length ?? 0;
  const tone = changed === 0 && answer.went >= answer.listed ? 'ok' : 'error';
  return changed > 0
    ? { key: 'work.abandon.done.changed', values: { went: answer.went, count: answer.listed, changed }, tone }
    : { key: 'work.abandon.done.all', values: { went: answer.went, count: answer.listed }, tone };
}
