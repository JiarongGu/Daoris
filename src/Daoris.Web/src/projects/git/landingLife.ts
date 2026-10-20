import type { TFunction } from 'i18next';
import type { EdgeStyle } from '../../design/flowLayout';
import type { FlowEdge, FlowNode } from '../../design/Flow';
import { moment } from '../../locales/format';
import { runTone } from '../../workflow/run';
import { entryDetail, entryName, type MilestoneEntry, type MilestoneUnit } from '../../work/head/milestone';
import { short, type TraceAnswer, type TraceBranch, type TraceDue } from '../../work/head/trace';

type Translate = TFunction;

/** What a try to land a due session came to (`AutoLandingCode`), held to its source by `landingLife.test.ts`. */
export const TRY_CODES = [
  'landed', 'advanced', 'nothing', 'held', 'uncommitted', 'exists', 'completed', 'plugin-unready', 'plugin-failed', 'push-held',
  'already', 'superseded', 'gone', 'undone', 'off', 'refused', 'unreviewed', 'opinion', 'carried',
] as const;

/** The proofs an end rests on (`EndBy`), each worded as the milestone words it (`work.milestone.by.*`). */
export const END_BY = ['empty', 'merged', 'on-line', 'pull-request', 'landed', 'carried', 'inside'] as const;

/** A pull request's states (`PullRequestStates.All`), in its order. */
export const PULL_REQUEST_STATES = ['open', 'completed', 'abandoned', 'unknown'] as const;

/** The proofs the clean-up removes a landed branch by (`LandedKind`'s four that go); every other kind keeps it. */
export const REMOVED_AS = ['on-line', 'merged', 'inside', 'pull-request'] as const;

/** The ends a pull request's completion proves: the edge into the end comes from the pull request, merged on the platform. */
const BY_PLATFORM = new Set(['pull-request', 'carried']);

/** The run's tones for a pull request's answer, as the run's own pull-request step stands for each (`runTone`, trap 8). */
const PULL_REQUEST_TONE: Record<string, string> = { open: 'waiting-on-you', completed: 'done', abandoned: 'stopped', unknown: 'not-known' };

/** The pull request's answer in the run's own sentences (`pullRequestSaid`'s, on the branch's line). */
const PULL_REQUEST_SAID: Record<string, string> = {
  open: 'workflow.run.said.pull-request.merge.open',
  completed: 'workflow.run.said.pull-request.merged',
  abandoned: 'workflow.run.said.pull-request.abandoned',
};

const OPINIONS = new Set(['settled', 'anyway', 'myself', 'answered']);

const has = <T extends string>(list: readonly T[], code: string | null | undefined): code is T => (list as readonly string[]).includes(code ?? '');

/** What the branch's page holds of its landing: the trace's branch for it, or the gap where none is named; its due entry; its units. */
export type LandingOf = {
  /** The branch's name, as its page names it. */
  name: string;
  /** The trace's landing for the session the list says made it; null where the trace names none. */
  branch: TraceBranch | null;
  /** Where the trace names no branch: its code (`merge-accepted` for a landing into the line). */
  gap?: string | null;
  /** That session's entry on the due list. */
  due?: TraceDue | null;
  /** The units of work the branch carries, as `WORK_STATE` names them for it; null where they were not read. */
  units?: readonly MilestoneUnit[] | null;
};

/** What the landing's flow draws: the flow's own props, the plain lines inside each node, and its pull request's address. */
export type LandingLife = {
  label: string;
  nodes: FlowNode[];
  edges: FlowEdge[];
  entries: string[];
  lines: Record<string, string[]>;
  pullRequest: string | null;
};

/**
 * The landing a trace answers for `session`, for the branch named `name` (FLOWCHART1c): the branch of that name its landing made,
 * a standing one before one gone, else the newest; with its due entry. Null where the trace holds no link for the session.
 * The trace reads every landing that names the session, the one made for it and each it moved on (`LandedBranch.Names`), so a
 * shared branch is read from any session it names.
 */
export function landingOf(answer: TraceAnswer | null | undefined, session: string, name: string): Omit<LandingOf, 'name' | 'units'> | null {
  const link = answer?.chain?.links.find((each) => each.session?.id === session)?.session;
  const landing = link?.landing;
  if (!landing) return null;
  const named = landing.branches.filter((each) => each.branch === name);
  const branch = named.find((each) => !each.gone) ?? [...named].sort((a, b) => b.at.localeCompare(a.at))[0] ?? null;
  return { branch, gap: branch ? null : landing.missing ?? null, due: landing.due ?? null };
}

/**
 * **A landed branch's landing and clean-up as a flow** (FLOWCHART1c; D186, the flowchart design §2.7): its due entry and each try
 * by its code; the landing, who accepted it and what let it go, with each later done that moved it on; its push and its pull
 * request; the end each unit it carries reached; the clean-up; the recovery kept.
 *
 * @remarks
 * **Drawn as its records keep it** (trap 19):
 * - *The tries* are the due entry's, oldest first, each like the others of its code so three in a row stack (§3.6). The edge into
 *   the landing comes from the last, saying who accepted it.
 * - *The landing is the branch's*: it names the session it was made for and the units it carries, and each advance is a node of its
 *   own naming its session, like the others of that session. A driver from before them answers no advances, and the landing says so.
 * - *The push is a fact with no time*: the record keeps that it was pushed, not when, so it hangs from the landing and never after an
 *   advance. *The pull request* is one node, its latest answer and when it was asked, or the ask that failed: a snapshot, never a
 *   sequence of states, and no edge joins it to the push, since no date orders them.
 * - *The end, the clean-up and the recovery* are the units' (`WORK_STATE` for the branch), one line per unit where it carries several.
 *   An end its pull request proved comes from the pull request; one not reached is a guard from the branch's tip.
 * - A landing into the line keeps no branch: the trace's gap is drawn in its words, merged and accepted, and nothing is pushed.
 * - A code this page does not know is drawn as recorded (trap 10).
 */
export function landingFlow(t: Translate, read: LandingOf, at: (iso: string) => string = (iso) => moment(iso)): LandingLife {
  const nodes: FlowNode[] = [];
  const edges: FlowEdge[] = [];
  const lines: Record<string, string[]> = {};
  const entries: string[] = [];
  const edge = (from: string, to: string, says: string, label?: string, style?: EdgeStyle) =>
    edges.push({ id: `${from}>${to}`, from, to, says, ...(label ? { label } : {}), ...(style ? { style } : {}) });
  const { branch, due } = read;

  // 1. Due since, and its tries by their codes.
  let last: string | null = null;
  if (due) {
    nodes.push({ id: 'due', title: t('work.trace.due.title') });
    lines.due = [
      due.closed ? t('work.trace.due.closed', { since: at(due.since), closed: at(due.closed) }) : t('work.trace.due.waiting', { since: at(due.since) }),
      ...(due.tries.length === 0 ? [t('work.trace.due.untried')] : []),
    ];
    entries.push('due');
    last = 'due';
    due.tries.forEach((tried, index) => {
      const id = `try:${index}`;
      const word = has(TRY_CODES, tried.code) ? t(`work.trace.try.${tried.code}`) : t('landing.try.recorded', { code: tried.code });
      nodes.push({ id, title: word, stack: { key: tried.code, word, at: at(tried.at) } });
      lines[id] = [
        at(tried.at),
        ...(tried.branch ? [t('work.trace.due.branch', { branch: tried.branch })] : []),
        ...(tried.commits != null ? [t('work.trace.due.commits', { count: tried.commits })] : []),
        ...(tried.uncommitted != null ? [t('work.trace.due.uncommitted', { count: tried.uncommitted })] : []),
        ...(tried.tip ? [t('work.trace.due.tip', { tip: short(tried.tip) })] : []),
      ];
      edge(last!, id, t(index === 0 ? 'landing.edge.tried' : 'landing.edge.again', { at: at(tried.at) }), t(index === 0 ? 'landing.label.tried' : 'landing.label.again'));
      last = id;
    });
  }

  // 2. Landed, or merged into the line from the trace's gap, and the advances that moved it on.
  let tip: string | null = null;
  if (branch) {
    nodes.push({ id: 'landed', title: t('landing.landed.title', { branch: branch.branch }) });
    lines.landed = [
      branch.line ? t('landing.landed.from', { line: branch.line, at: at(branch.at) }) : t('landing.landed.fromNone', { at: at(branch.at) }),
      ...(branch.session ? [t('landing.made', { session: branch.session })] : []),
      ...ruleLines(t, branch),
      ...(branch.units && branch.units.length > 0 ? [t('landing.units', { units: branch.units.join(', ') })] : []),
      ...(branch.advances === undefined ? [t('landing.advances.unread')] : []),
    ];
    if (last) edge(last, 'landed', accepted(t, branch.acceptedBy, branch.rule?.access ?? null), t('landing.label.accepted'));
    else entries.push('landed');
    tip = 'landed';
    (branch.advances ?? []).forEach((advance, index) => {
      const id = `advance:${index}`;
      const title = t('landing.advance.title', { session: advance.session });
      nodes.push({ id, title, stack: { key: `advance:${advance.session}`, word: title, at: at(advance.at) } });
      lines[id] = [
        at(advance.at),
        t('landing.advance.moved', { from: short(advance.from), to: short(advance.to) }),
        ...(advance.unit ? [t('landing.advance.unit', { unit: advance.unit })] : []),
        ...(advance.quest ? [`#${advance.quest}`] : []),
        ...reviewLines(t, advance),
      ];
      // The node names its session; the edge says who accepted the done that moved it, so a foot never says the title twice.
      edge(tip!, id, accepted(t, advance.acceptedBy, null), t('landing.label.advanced'));
      tip = id;
    });

    // 3. Pushed, a fact with no time; and the pull request, a snapshot: each from the landing, and nothing between them.
    if (branch.plugin) {
      nodes.push({ id: 'pushed', title: t(branch.pushed ? 'landing.pushed.title' : 'landing.handed.title') });
      lines.pushed = branch.pushed ? [t('landing.pushed.noTime')] : [];
      edge('landed', 'pushed', t(branch.pushed ? 'landing.edge.pushed' : 'landing.edge.handed', { plugin: branch.plugin }),
        t(branch.pushed ? 'landing.label.pushed' : 'landing.label.handed'));
    }
    const answer = branch.pullRequestState;
    const failed = branch.pullRequestAskFailed;
    if ((branch.pushed && branch.pullRequest) || answer || failed) {
      const state = answer?.state;
      nodes.push({
        id: 'pull-request', title: t('landing.pr.title'),
        ...(state ? {
          state: {
            word: has(PULL_REQUEST_STATES, state) ? t(`landing.pr.state.${state}`) : t('landing.pr.recorded', { state }),
            tone: runTone(PULL_REQUEST_TONE[state] ?? 'not-known'),
          },
        } : {}),
      });
      lines['pull-request'] = [
        ...(answer ? [
          t(PULL_REQUEST_SAID[answer.state] ?? 'workflow.run.said.pull-request.merge.unknown', { plugin: answer.plugin ?? '' }),
          ...((answer.state === 'completed' || answer.state === 'abandoned') && answer.at ? [t('landing.pr.endedAt', { at: at(answer.at) })] : []),
          // A record that did not keep when it asked reads as the year 1: never said as a time.
          ...(Date.parse(answer.askedAt) > 0 ? [t('landing.pr.asked', { plugin: answer.plugin ?? '', at: at(answer.askedAt) })] : []),
        ] : []),
        ...(failed ? [t('landing.pr.failedAt', { said: t('workflow.run.said.pull-request.ask-failed', { plugin: failed.plugin, code: failed.code }), at: at(failed.at) })] : []),
        ...(!answer && !failed ? [t('landing.pr.none')] : []),
      ];
      edge('landed', 'pull-request', branch.plugin ? t('landing.edge.pullRequest', { plugin: branch.plugin }) : t('landing.edge.pullRequestAlone'));
    }
  } else if (read.gap === 'merge-accepted') {
    nodes.push({ id: 'landed', title: t('landing.merged.title') });
    lines.landed = [t('landing.merged.said')];
    if (last) edge(last, 'landed', t('landing.edge.merged'), t('landing.label.accepted'));
    else entries.push('landed');
    tip = 'landed';
  }

  // 4-6. The end each unit it carries reached, the clean-up, and the recovery kept.
  const units = read.units ?? [];
  if (units.length > 0 && tip) {
    const several = units.length > 1;
    const reached = units.every((unit) => unit.end.state === 'ended');
    nodes.push({ id: 'end', title: t('landing.end.title'), reached });
    lines.end = units.map((unit) => (several ? t('landing.end.unit', { unit: unit.unit, said: endSentence(t, unit, at) }) : endSentence(t, unit, at)));
    const onPlatform = reached && units.some((unit) => BY_PLATFORM.has(unit.end.by ?? '')) && nodes.some((node) => node.id === 'pull-request');
    if (onPlatform) edge('pull-request', 'end', t('landing.edge.platform'), t('landing.label.platform'));
    else if (reached) edge(tip, 'end', t('landing.edge.line'), t('landing.label.line'));
    else edge(tip, 'end', t('landing.edge.notYet'), undefined, 'conditional');

    const completed = units.every((unit) => unit.cleanup.state === 'completed');
    const waits = unique(units.flatMap((unit) => unit.cleanup.waits));
    const gone = unique(units.flatMap((unit) => unit.cleanup.gone));
    // Blocked waits on the person, and wears open's hue as the run's own waits do; pending ends by itself (`waitsOnPerson`).
    const cleanup = completed ? 'completed' : waits.some((each) => each.disposition === 'blocked') ? 'blocked' : 'pending';
    nodes.push({
      id: 'cleanup', title: t('work.milestone.cleanup.label'), reached: completed,
      state: { word: t(`landing.cleanup.state.${cleanup}`), tone: runTone(cleanup === 'completed' ? 'done' : cleanup === 'blocked' ? 'waiting-on-you' : 'not-known') },
    });
    lines.cleanup = [
      // OVERVIEWNOISE1b: a blocked entry by its code in the reader's language, the driver's sentence where the page has no words.
      ...waits.map((each) => t(each.disposition === 'blocked' ? 'work.milestone.cleanup.blocked' : 'work.milestone.cleanup.waits', { what: entryName(t, each), detail: entryDetail(t, each) })),
      ...gone.map((each) => t('landing.cleanup.gone', { what: entryName(t, each), detail: each.detail })),
      ...(branch?.gone && has(REMOVED_AS, branch.removedAs) ? [t(`landing.removedAs.${branch.removedAs}`, { on: branch.removedOn ?? '' })] : []),
      ...(completed && gone.length === 0 ? [t('work.milestone.cleanup.completedNone')] : []),
    ];
    if (completed) edge('end', 'cleanup', t('landing.edge.removed'), t('landing.label.removed'));
    else edge('end', 'cleanup', t('landing.edge.waits'), undefined, 'conditional');

    const recovery = unique(units.flatMap((unit) => unit.recovery));
    if (recovery.length > 0) {
      nodes.push({ id: 'recovery', title: t('work.milestone.recovery.label') });
      lines.recovery = recovery.map((each) => (each.until
        ? t('work.milestone.recovery.until', { what: entryName(t, each), when: at(each.until) })
        : t('work.milestone.recovery.kept', { what: entryName(t, each) })));
      edge('cleanup', 'recovery', t('landing.edge.kept'), t('landing.label.kept'));
    }
  }

  return {
    label: t('landing.label', { branch: branch?.branch ?? read.name }),
    nodes, edges, entries, lines,
    pullRequest: branch?.pushed && branch.pullRequest ? branch.pullRequest : branch?.pullRequestState?.pullRequest ?? null,
  };
}

/** Who accepted it, as an edge's cause: the access setup where it turned the switch on and the switch accepted it. */
function accepted(t: Translate, by: string | null | undefined, access: string | null): string {
  if (by === 'auto') return t(access ? 'landing.edge.access' : 'landing.edge.auto');
  if (by === 'person') return t('landing.edge.person');
  return t('landing.edge.unknown');
}

/** The rule it was made under, then what let it go: the review, the second opinion and the named workflow. */
function ruleLines(t: Translate, branch: TraceBranch): string[] {
  const rule = branch.rule;
  return [
    ...(rule ? [[
      t(`work.trace.rule.${rule.source}`, { defaultValue: rule.source }),
      rule.plugin ? t('work.trace.rulePlugin', { plugin: rule.plugin }) : t('work.trace.ruleNoPlugin'),
      t(rule.autoAccept ? 'work.trace.ruleAuto' : 'work.trace.rulePress'),
    ].join(' · ')] : []),
    ...reviewLines(t, branch),
  ];
}

function reviewLines(t: Translate, landed: Pick<TraceBranch, 'review' | 'opinion' | 'workflow'>): string[] {
  const { review, opinion, workflow } = landed;
  return [
    ...(review?.said === 'reviewed' ? [t('landing.review.reviewed', { environment: review.environment ?? '' })] : []),
    ...(review?.said === 'skipped' ? [t('landing.review.skipped')] : []),
    ...(opinion ? [OPINIONS.has(opinion.said) ? t(`landing.opinion.${opinion.said}`) : t('landing.opinion.none', { code: opinion.code ?? opinion.said })] : []),
    ...(workflow ? [t('landing.workflow', { workflow: workflow.workflow, version: workflow.version })] : []),
  ];
}

/** A unit's end in the milestone's own words: reached and by what proof, or why not, with when where it was reached. */
function endSentence(t: Translate, unit: MilestoneUnit, at: (iso: string) => string): string {
  const { end } = unit;
  switch (end.state) {
    case 'ended': {
      const proof = has(END_BY, end.by) ? t(`work.milestone.by.${end.by}`, { on: end.on ?? '' }) : end.said;
      const reached = t('work.milestone.end.ended', { proof });
      return end.at ? `${reached} ${t('work.milestone.end.since', { when: at(end.at) })}` : reached;
    }
    case 'handed': return t('work.milestone.end.handed', { said: end.said });
    case 'not-ended': return t('work.milestone.end.notEnded', { said: end.said });
    case 'dropped': return t('work.milestone.end.dropped', { said: end.said });
    default: return t('work.milestone.end.unknown', { said: end.said });
  }
}

/** Each entry once, by its kind and its name: several units name the same landing branch. */
function unique(entries: readonly MilestoneEntry[]): MilestoneEntry[] {
  const seen = new Set<string>();
  return entries.filter((each) => {
    const key = `${each.kind}\n${each.resource}`;
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}
