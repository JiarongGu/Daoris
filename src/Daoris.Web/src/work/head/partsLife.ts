import type { FlowEdge, FlowNode } from '../../design/Flow';
import { SESSION_TONE, type ShownState, shownKey } from '../../design/ui';
import { ago, sessionTool } from '../../locales/format';
import type { EntryPart, SessionEntry } from '../entries';
import { shownOf } from '../groups';
import { ownTree } from '../identity';
import type { EntryAcceptPlan } from '../review/EntryAccept';
import type { PartFacts } from './PartsStrip';

type Translate = (key: string, values?: Record<string, unknown>) => string;

/** The ways the driver says a part began (`PartBegan`), held to its source by `partsLife.test.ts`. */
export const PART_BEGAN = ['opened', 'retried', 'resumed', 'carried-on', 'handed-over', 'asked'] as const;
type Began = (typeof PART_BEGAN)[number];
const isBegan = (word: string): word is Began => (PART_BEGAN as readonly string[]).includes(word);

/**
 * How a part began, in the reader's words (D167, SESSONE1b's `began`): started, retried after another part, resumed or carried
 * on from one, handed over from one, or asked by a repository. The earlier part is named by its number in the strip, and by its
 * id where the strip does not hold it. A word a newer host adds reads as a start rather than as nothing.
 */
export function beganLine(t: Translate, part: EntryPart, parts: readonly EntryPart[]): string {
  const at = part.after ? parts.findIndex((other) => other.session === part.after) : -1;
  const after = at >= 0 ? t('work.parts.number', { n: at + 1 }) : part.after ?? '';
  switch (part.began) {
    case 'retried': return t('work.parts.began.retried', { after });
    case 'resumed': return t('work.parts.began.resumed', { after });
    case 'carried-on': return part.after ? t('work.parts.began.carriedOn', { after }) : t('work.parts.began.carriedOnAlone');
    case 'handed-over': return t('work.parts.began.handedOver', { after });
    case 'asked': return part.askedBy ? t('work.parts.began.asked', { repository: part.askedBy }) : t('work.parts.began.askedUnnamed');
    default: return t('work.parts.began.opened');
  }
}

/** Where a part's work is now, in the row's words: where its landing put it, or the tree it opened; null where the page holds no record. */
export function partPlace(t: Translate, facts: PartFacts | undefined): string | null {
  if (!facts) return null;
  const landed = facts.where?.landed && (facts.where.treeGone || facts.where.landed.state === 'standing') ? facts.where.landed : null;
  if (landed) return t(landed.state === 'gone' ? 'work.rail.landedGone' : 'work.rail.landedOn', { branch: landed.branch });
  const tree = !facts.where?.treeGone ? ownTree(facts.session, facts.root) : null;
  return tree ? t('work.rail.inTree', { tree }) : null;
}

/** What the page holds of a part's session: its record and its place, by the part's id. */
export type PartsFacts = Readonly<Record<string, PartFacts | undefined>>;

/** What the parts' page draws: the flow's own props, and the plain lines inside each part's node beside its words. */
export type PartsLife = {
  label: string;
  nodes: FlowNode[];
  edges: FlowEdge[];
  entries: string[];
  /** Per part: its quest, where its work is, and whether its row leads the list, each a line of its own. */
  lines: Record<string, string[]>;
};

const START = 'start';
const LANDING = 'landing';

/**
 * **A line of work's parts as a flow** (FLOWCHART1b; the flowchart design §2.3): each part a node oldest first, hung by what it
 * followed (`EntryPart.After`) and labelled by how it began, an asked part from a faint node for the repository that asked it,
 * and the plan where Accept would land them as a prospective node, dashed.
 *
 * @remarks
 * **Lineage, never cause or concurrency** (trap 19): a part keeps no start, end or overlap, so the order of the list is the
 * ledger's and neither it nor two parts with no `after` between them is drawn as one following another or running beside it.
 * Parts with no `after` branch from the entry's start, which is where a part carried on from a chain's parent hangs too, since it
 * keeps none. A part with an `after` the page does not hold hangs from the start and says which it followed by its id.
 * - **What a part says of itself**: its word as the list shows it, and its quest. Its agent and when it began only where the page
 *   holds that part's session record, since the part keeps neither (`SessionEntries.cs`).
 * - **What is lit**: the part the page is on, and a line says which part's record is the list's row.
 * - **The landing is a plan, not a join**: it is drawn only once Accept's plan is read, as where the parts *would* land, with what
 *   holds or leaves out each on its edge; what did land is the landing flow's.
 * - A way of beginning this page does not know is shown as recorded (trap 10), hung by what it follows.
 */
export function partsFlow(
  t: Translate, entry: SessionEntry,
  { attended, rows, plan }: { attended: string; rows: PartsFacts; plan?: EntryAcceptPlan | null },
): PartsLife {
  const { parts } = entry;
  const held = new Set(parts.map((part) => part.session));
  const numberOf = (session: string) => t('work.parts.number', { n: parts.findIndex((part) => part.session === session) + 1 });

  /** What a part hangs from: the node for the repository that asked, else the part it followed if held, else the start. */
  const source = (part: EntryPart): string => {
    if (part.began === 'asked') return `asked:${part.askedBy ?? ''}`;
    return part.after && part.after !== part.session && held.has(part.after) ? part.after : START;
  };
  const sources = parts.map(source);

  const nodes: FlowNode[] = [];
  const edges: FlowEdge[] = [];
  const lines: Record<string, string[]> = {};
  const entries: string[] = [];
  if (sources.includes(START)) {
    nodes.push({ id: START, title: t('work.parts.start') });
    entries.push(START);
  }

  const drawn = new Set<string>();
  parts.forEach((part, index) => {
    const from = sources[index]!;
    if (from.startsWith('asked:') && !drawn.has(from)) {
      drawn.add(from);
      entries.push(from);
      nodes.push({
        id: from, faint: true,
        title: part.askedBy ? t('work.parts.began.asked', { repository: part.askedBy }) : t('work.parts.began.askedUnnamed'),
      });
    }

    const facts = rows[part.session];
    const shown = facts ? shownOf(facts.session, { shown: part.shown }, undefined) : part.shown in SESSION_TONE ? (part.shown as ShownState) : null;
    nodes.push({
      id: part.session,
      title: t('work.parts.node', { n: index + 1 }),
      current: part.session === attended,
      state: shown ? { word: t(shownKey(shown)), tone: SESSION_TONE[shown] } : { word: part.shown, tone: 'neutral' },
      ...(facts
        ? { actor: t('work.parts.actor', { agent: sessionTool({ adapter: facts.session.adapter }), when: ago(facts.session.created) }) }
        : {}),
    });
    lines[part.session] = [
      ...(part.quest ? [`#${part.quest}`] : []),
      ...(partPlace(t, facts) ? [partPlace(t, facts)!] : []),
      ...(part.session === entry.leading ? [t('work.parts.leads')] : []),
    ];

    const known = isBegan(part.began);
    edges.push({
      id: `${from}>${part.session}`, from, to: part.session,
      label: known ? t(`work.parts.edge.${part.began}`) : part.began,
      says: known ? beganLine(t, part, parts) : t('work.parts.began.recorded', { began: part.began }),
    });
  });

  // Where Accept would land them: a plan, not a join, and not drawn until it is read.
  if (plan) {
    nodes.push({
      id: LANDING, reached: false,
      title: t(plan.form === 'merge' ? 'work.parts.landing.titleMerges' : 'work.parts.landing.titleLands', { target: plan.target }),
    });
    for (const each of plan.parts) {
      if (!held.has(each.session)) continue;
      const what = [
        ...(each.held ? [t('work.parts.landing.held', { says: each.held })] : []),
        ...(each.problem ? [t('work.parts.landing.problem', { says: each.problem })] : []),
        ...(each.conflict ? [t('work.parts.landing.conflict', { says: each.conflict.says })] : []),
        ...(each.sameTreeAs ? [t('work.parts.landing.sameTree', { other: numberOf(each.sameTreeAs) })] : []),
        ...(each.superseded ? [t('work.parts.landing.superseded', { by: numberOf(each.superseded.by) })] : []),
        ...(each.leftOut && each.leftOut.length > 0 ? [t('work.parts.landing.leavesOut', { count: each.leftOut.length })] : []),
      ];
      edges.push({
        id: `${each.session}>${LANDING}`, from: each.session, to: LANDING, style: 'conditional',
        says: what.length > 0
          ? what.join('; ')
          : each.own && each.target ? t('work.parts.landing.own', { branch: each.target }) : t('work.parts.landing.lands'),
      });
    }
  }

  return { label: t('work.parts.label'), nodes, edges, entries, lines };
}
