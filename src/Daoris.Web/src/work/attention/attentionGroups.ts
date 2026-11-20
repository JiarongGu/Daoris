import type { Attention, AttentionKind } from './AttentionRow';
import { theAgents } from './attentionCounts';
import type { AttentionClass, StaleSince } from './attentionPushed';

/**
 * *What needs you*'s groups, in the order a person acts on them: what holds work up until they act, what waits for their word, and
 * what is ready for them to look at; then what the agents take, listed and never counted as the owner's.
 */
export type AttentionGroup = 'holding' | 'word' | 'ready' | 'agents';

/** The groups in the order the band shows them, the agents' last. */
export const GROUP_ORDER: readonly AttentionGroup[] = ['holding', 'word', 'ready', 'agents'];

/**
 * Each kind's group. **Holding work**: a parked session holds its tree, a parked quest its repository's next
 * start, a go-ahead its session's act, a folder to trust the start the driver holds. **Waiting for your word**: an ask to
 * publish, an intake's question, a departure, a widening, a quest nobody can take, none of which holds anything already
 * running. **Ready for you**: finished work to review.
 */
export const ATTENTION_GROUP: Readonly<Record<AttentionKind, Exclude<AttentionGroup, 'agents'>>> = {
  parked: 'holding',
  'parked-quest': 'holding',
  // A quest the steward's stop holds for a Try again, the steward's or the person's.
  'steward-stopped': 'holding',
  // A quest its role's policy holds for the person's yes to one move to another choice.
  'policy-hold': 'holding',
  // A quest its finished session left taken holds its tree until the person's done or word.
  unclosed: 'holding',
  // A run held at a request the receiver declined stands still until the person passes it over.
  'request-declined': 'holding',
  step: 'holding',
  // A stale one holds nothing and waits for the person's word instead (`attentionGroup`).
  'go-ahead': 'holding',
  // A tool call a session holds for the person's answer under asking: its turn stands still until they
  // answer, or the bound refuses it, as a go-ahead holds its session's act.
  permission: 'holding',
  // A third answer in a row with nobody between, which the answers-alone bound holds for the person: its
  // line stands still until they let it go on or keep it.
  answer: 'holding',
  // A press a terminal sent that waits for the person's Confirm on the window's card: its terminal holds still until
  // they answer, or its time ends, which refuses it.
  confirmation: 'holding',
  trust: 'holding',
  // A start waiting on its accounts holds that start; a signed-out account a list holds, the next start the list takes.
  'account-wait': 'holding',
  'signed-out': 'holding',
  proposal: 'word',
  intake: 'word',
  departure: 'word',
  // A set-up waiting for the person's look: its chain's landing waits on their verdict, as a departure's next
  // step waits on their yes, and nothing already running is held by it.
  'set-up': 'word',
  // A second opinion that waits on the person: its landing waits on their answer, and
  // nothing already running is held by it.
  opinion: 'word',
  rule: 'word',
  // An agent's ask for a helper that waits on the person's yes: the session that asked goes on
  // meanwhile, as one that proposed a rule does, so it holds nothing up.
  helper: 'word',
  // An agent's ask to hand its session over: while its turn runs it holds nothing up yet; once its turn has
  // ended the session stands still on the person's yes or no, and the row holds work (`attentionGroup`).
  'hand-over': 'word',
  // A unit only the person's press settles: one whose owner
  // nothing records, or a tree its session recorded that no branch record follows. Nothing running is held by it, and nothing of it
  // is removed until they press. A guard the look keeps a branch by is said on its session's page, never here.
  work: 'word',
  // A volume still below its free-space floor once the floor freed what Daoris can make again: the
  // rest waits for the person's choice on *Free up space…*, and nothing running is held by it yet.
  space: 'word',
  // A git remote held because it could not authenticate: nothing connects for it
  // until the person's *Try again* or a change lifts it, and nothing already running is held by this row.
  'git-held': 'word',
  // A git go-ahead: its door holds the act on the remote until the person allows or
  // refuses it, or its bound passes, as a held call holds its session's turn.
  'git-ask': 'holding',
  // A second check's flag on one of Ask Daoris's acts, which has run: the person should know before going on, and
  // nothing is held by it.
  'second-check': 'word',
  // A session the watcher brings to the person: it holds its slot and its tree, and
  // its work stands still until they press, or it is heard again.
  quiet: 'holding',
  // A session whose helper ended unanswered and whose move back to the person the service refused: the park row it would have had, holding its tree until they look.
  'helper-ended': 'holding',
  // An end, a replacement or a stopped session's rows that memory keeps for the person: nothing running is held by it.
  memory: 'word',
  // An integration's wait on the owner: a superseded commit's answer holds nothing running; a candidate held for their decide
  // holds its line's next, and a landing waiting for their card its unit's flow, which hold work (`attentionGroup`).
  integration: 'word',
  unanswerable: 'word',
  // A question the ask's delivery plan asks that only the person can settle: nothing running is held by it, but a release waits.
  'plan-question': 'word',
  'design-question': 'word',
  left: 'word',
  review: 'ready',
};

/**
 * A row's group: its kind's, but an agent's ask to hand its session over whose session stands still on it, which holds work,
 * and a stale go-ahead, which holds no session's act since nothing on its ask could still use it, so it
 * waits for the person's word with what holds nothing running. An agents' row, whatever its kind, is *With the agents*. The
 * band, its order and Overview's summary read this one answer, so a row is counted in the group it is listed in.
 */
export function attentionGroup(row: { kind: AttentionKind; stands?: boolean; stale?: StaleSince | null; class?: AttentionClass }): AttentionGroup {
  if (theAgents(row)) return 'agents';
  // A held git remote holds work where a session of one of its repositories runs or waits on the person.
  if ((row.kind === 'hand-over' || row.kind === 'integration' || row.kind === 'git-held') && row.stands) return 'holding';
  if (row.kind === 'go-ahead' && row.stale) return 'word';
  return ATTENTION_GROUP[row.kind] ?? 'word';
}

/**
 * The rows by group, only the groups that hold any, in `GROUP_ORDER`, each keeping the order `attentionInOrder` gave it.
 * A group with no rows is not shown.
 */
export function attentionGroups(items: readonly Attention[]): { group: AttentionGroup; items: Attention[] }[] {
  return GROUP_ORDER
    .map((group) => ({ group, items: items.filter((item) => attentionGroup(item) === group) }))
    .filter(({ items: held }) => held.length > 0);
}
