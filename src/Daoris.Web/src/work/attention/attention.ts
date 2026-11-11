import type { TFunction } from 'i18next';
import i18n from '../../locales/i18n';
import { elapsed, size } from '../../locales/format';
import type { Ask, GoAhead, Quest, Session } from '../../data/api';
import { proposalAuthor, proposalChange } from '../../settings/repository/proposals';
import { sittingSentence } from '../../data/signals';
import type { Tool } from '../../agents/tools';
import { askName, questName, sessionTitle } from '../identity';
import type { Attention, AttentionKind } from './AttentionRow';
import { accountRowWorded } from './accountWords';
import { askerSaid } from '../../person/askerSaid';
import { endSentences, entryDetail, entryName, type GraphEnd, type UnreadableRecord } from '../head/milestone';
import { heldWorded } from './heldWorded';
import { taskRead } from './taskRead';
import { boundMinutes } from '../waits/PermissionAsks';
import { opinionSays } from '../gates/OpinionGate';
import { placedFact } from '../rail/SessionRow';
import { workCutFor } from './attentionCounts';
import type { GitAskWait, GitHeld, PushedAttention, StaleSince } from './attentionPushed';

/**
 * *What needs you*'s three groups, in the order a person acts on them: what holds work up until they
 * act, what waits for their word, and what is ready for them to look at.
 */
export type AttentionGroup = 'holding' | 'word' | 'ready';

/** The groups in the order the band shows them. */
export const GROUP_ORDER: readonly AttentionGroup[] = ['holding', 'word', 'ready'];

/**
 * Each kind's group. **Holding work**: a parked session holds its tree, a parked quest its repository's next
 * start, a go-ahead its session's act, a folder to trust the start the driver holds. **Waiting for your word**: an ask to
 * publish, an intake's question, a departure, a widening, a quest nobody can take, none of which holds anything already
 * running. **Ready for you**: finished work to review.
 */
export const ATTENTION_GROUP: Readonly<Record<AttentionKind, AttentionGroup>> = {
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
  // A run waiting on a person's task stands still until they press it.
  task: 'holding',
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
  // A done its own record says is unfinished: part of its work is left, and the session that finished it asks for the next step.
  left: 'word',
  review: 'ready',
};

/**
 * A row's group: its kind's, but an agent's ask to hand its session over whose session stands still on it, which holds work,
 * and a stale go-ahead, which holds no session's act since nothing on its ask could still use it, so it
 * waits for the person's word with what holds nothing running. The band, its order and Overview's summary read this one answer, so a
 * row is counted in the group it is listed in.
 */
export function attentionGroup(row: { kind: AttentionKind; stands?: boolean; stale?: StaleSince | null }): AttentionGroup {
  // A held git remote holds work where a session of one of its repositories runs or waits on the person.
  if ((row.kind === 'hand-over' || row.kind === 'integration' || row.kind === 'git-held') && row.stands) return 'holding';
  if (row.kind === 'go-ahead' && row.stale) return 'word';
  return ATTENTION_GROUP[row.kind] ?? 'word';
}

export { ATTENTION_PLACE, bellCount, needsListed, obligationOf, placeCounts, placeOf, workCutFor } from './attentionCounts';
export type { AttentionPlace } from './attentionCounts';

/**
 * The rows by group, only the groups that hold any, in `GROUP_ORDER`, each keeping the order `attentionInOrder` gave it.
 * A group with no rows is not shown.
 */
export function attentionGroups(items: readonly Attention[]): { group: AttentionGroup; items: Attention[] }[] {
  return GROUP_ORDER
    .map((group) => ({ group, items: items.filter((item) => attentionGroup(item) === group) }))
    .filter(({ items: held }) => held.length > 0);
}

/**
 * The stale go-aheads a list holds, in its order: what the one refusal refuses, each through its own door with the one
 * reason the person gave.
 */
export function staleGoAheads(items: readonly Attention[]): Attention[] {
  return items.filter((item) => item.kind === 'go-ahead' && item.stale && item.ask && typeof item.number === 'number');
}

/**
 * An ask's stale go-aheads by number, from the rows .NET pushed: what its page marks, so the ask and What needs you say
 * the one judgement.
 */
export function staleOnAsk(rows: readonly PushedAttention[], ask: string): Record<number, StaleSince> {
  const stale: Record<number, StaleSince> = {};
  for (const row of rows) {
    if (row.kind === 'go-ahead' && row.ask === ask && row.stale && typeof row.number === 'number') stale[row.number] = row.stale;
  }
  return stale;
}

/**
 * What a row's door into its run names (*What needs you*'s row for a waiting step opens that
 * step): the session it stands for, or the quest whose newest session here does. A row that is no step of a run (an ask still to
 * publish, a folder's trust, an account, a rule) has none. The run opens where it stands, which is the step the row waits at.
 */
export function rowRun(item: Attention): { session?: string | null; quest?: string | null } | null {
  switch (item.kind) {
    // The work's session waits on the person's answer; the landing on their accept, in its review.
    case 'parked':
    case 'review':
    case 'left': return { session: item.id };
    // The session that would have parked, its move back to the person refused.
    case 'helper-ended': return { session: item.session ?? item.id };
    // The session whose memory waits; a sweep's end has none.
    case 'memory': return item.memory?.end ? null : { session: item.session ?? item.id };
    // The work's act waits on the person's go-ahead, asked by the session the row names.
    case 'go-ahead': return item.session ? { session: item.session } : null;
    // The look waits on the person: the set-up step's session, else the step's newest here.
    case 'set-up': return { session: item.session ?? null, quest: item.id };
    // The work's done waits on the person's yes; a quest parked on its failed sessions waits on their *Try again*.
    case 'departure':
    case 'parked-quest': return { quest: item.id };
    // The steward's stop: its quest, and the session it stopped.
    case 'steward-stopped': return { session: item.session ?? null, quest: item.id };
    // A policy's hold: its quest, as a parked quest's.
    case 'policy-hold': return { quest: item.id };
    case 'unclosed': return { session: item.session ?? null, quest: item.id };
    // The run's newest work quest here, whose run the step waits in.
    case 'request-declined': return item.declined?.work ? { quest: item.declined.work } : null;
    case 'task': return item.task?.work ? { quest: item.task.work } : null;
    default: return null;
  }
}

export { ASKS_ONCE, attentionActs, attentionOffers, gitHeldOffers, heldActs } from './attentionOffers';
export type { AttentionActId, AttentionOffer } from './attentionOffers';

export type {
  AttentionPushed, DeclinedRequest, HumanTaskWait, GitAskWait, GitHeld, GitHeldClass, GitServed, IntegrationWait, MemoryWait, PushedAttention,
  PushedConfirmation, QuietWait, StaleSince,
} from './attentionPushed';

/**
 * The detail's key for a held group: an `own-input` hold is lifted by removing the repository's setting, not by a retry, and git said
 * nothing, since nothing failed; one that names a setting says its name (a certificate hold's).
 */
function gitHeldDetailKey(held: Pick<GitHeld, 'class' | 'words'>): string {
  if (held.class === 'own-input') return ownSetting(held) ? 'work.attention.gitHeld.detailOwnSetting' : 'work.attention.gitHeld.detailOwnInput';
  return 'work.attention.gitHeld.detail';
}

/**
 * The setting an `own-input` hold names (a certificate hold, a scoped header or cookie file): its words are the
 * setting's name (`http.sslCert`, `http.<url>.extraHeader`), never its value; an ssh command's hold says none.
 */
const ownSetting = (held: Pick<GitHeld, 'class' | 'words'>) => (held.class === 'own-input' && held.words?.startsWith('http.') ? held.words : null);

/**
 * Each kind's place among the kinds of its group when two waited as long (the order of kinds): the accounts', parks, a
 * move back to the person refused, parked quests, go-aheads, held calls, git go-aheads, held answers, a terminal's presses
 * and folders; then asks, departures, set-ups, second opinions, widenings, helpers, hand-overs, a unit's blocked clean-up, a held attempt,
 * a volume below its floor, a held git remote and quests nobody can take; then unfinished work, then review.
 */
const KIND_ORDER: readonly AttentionKind[] = [
  'account-wait', 'signed-out', 'parked', 'helper-ended', 'parked-quest', 'steward-stopped', 'policy-hold', 'unclosed', 'request-declined', 'task', 'go-ahead', 'permission', 'git-ask', 'answer', 'confirmation', 'trust',
  'quiet', 'proposal', 'departure', 'set-up', 'second-check', 'opinion', 'rule', 'helper', 'hand-over', 'memory', 'integration', 'work', 'space', 'git-held', 'unanswerable',
  'left', 'review',
];

/** A row's place among its group's kinds; an intake is an ask's, and a held attempt comes after a unit's blocked clean-up. */
function kindRank(row: PushedAttention): number {
  const kind = row.kind === 'intake' ? 'proposal' : row.kind;
  return KIND_ORDER.indexOf(kind) + (row.kind === 'work' && row.held ? 0.5 : 0);
}

/**
 * The band's order: by group, holding work first; within a group what has waited longest
 * first, whatever its kind, a row with no known wait (a signed-out account's) after every row with one; then the order
 * of kinds; then the row's key. A place in the list is never pushed: one move would push every row after it.
 */
export function attentionInOrder(rows: readonly PushedAttention[]): PushedAttention[] {
  const groupOf = (row: PushedAttention) => GROUP_ORDER.indexOf(attentionGroup(row));
  const key = (row: PushedAttention) => `${row.kind}:${row.id}`;
  return [...rows].sort((a, b) => groupOf(a) - groupOf(b)
    || (a.since === null || b.since === null ? Number(a.since === null) - Number(b.since === null) : a.since.localeCompare(b.since))
    || kindRank(a) - kindRank(b)
    || (key(a) < key(b) ? -1 : key(a) > key(b) ? 1 : 0));
}

/** What the page names a pushed row's records by: the lists it holds and the roster, each optional. A record not in hand is named by its id. */
export type AttentionNames = {
  sessions?: readonly Session[];
  quests?: readonly Quest[];
  asks?: readonly Ask[];
  /** The roster by agent (`byTool`), which names an account as the person knows it. */
  tools?: readonly Tool[];
};

/** A go-ahead's act, as its ask's page names it: its kind in the reader's language where the page has a word, and the session's words. */
function goAheadTitle(goAhead: Pick<GoAhead, 'kind' | 'on' | 'act'>): string {
  const known = `asks.goAhead.kind.${goAhead.kind}`;
  const kind = i18n.exists(known) ? i18n.t(known) : goAhead.kind;
  return i18n.t('work.attention.goAheadTitle', { act: i18n.t('asks.goAhead.act', { kind, on: goAhead.on }), words: goAhead.act });
}

/**
 * A stale go-ahead's line: that it is stale, for how long nothing on its ask has needed it where a moment says, and that
 * no session on the ask runs or waits and no quest of it is open; then why its session asked, as written.
 */
function staleDetail(ask: string, stale: StaleSince, why: string | null, now: Date): string {
  const first = stale.since
    ? i18n.t('work.attention.staleWhy', { span: elapsed(stale.since, now.toISOString()), id: ask })
    : i18n.t('work.attention.staleWhyUndated', { id: ask });
  return why ? i18n.t('work.composer.twoSentences', { first, second: why }) : first;
}

/** Which requirement a held done departed from and why, in its own words; how many where it departed from several. */
function departureWhy(departed: PushedAttention['departed']): string | null {
  if (!departed) return null;
  return departed.count === 1
    ? i18n.t('work.attention.departureWhy', { number: departed.number, reason: departed.reason })
    : i18n.t('work.attention.departureWhyMore', { departed: departed.count, number: departed.number, reason: departed.reason });
}

/**
 * A pushed row worded: what it is called, from the records the page holds (a session as Sessions names it, a quest and an
 * ask by their names, an account as the roster names it), where it waits, and its line of why in the reader's language, with the
 * values its acts and its door read. Pure: the judge is .NET's, and nothing here decides whether a row is.
 */
export function attentionWorded(row: PushedAttention, names: AttentionNames = {}, now: Date = new Date()): Attention {
  const sessionOf = (id: string | null | undefined) => (id ? names.sessions?.find((one) => one.id === id) : undefined);
  const questOf = (id: string | null | undefined) => (id ? names.quests?.find((one) => one.id === id) : undefined);
  const named = (id: string | null | undefined) => {
    const record = sessionOf(id);
    return record ? sessionTitle(record, questOf(record.quest)) : `#${id ?? ''}`;
  };
  const quested = (id: string) => {
    const quest = questOf(id);
    return quest ? questName(quest) : `#${id}`;
  };
  const base = { id: row.id, kind: row.kind, since: row.since, where: row.where ?? '' };
  const askWhere = (ask: string) => i18n.t('work.attention.askWhere', { id: ask });

  switch (row.kind) {
    case 'parked':
      return { ...base, title: named(row.id), ...(row.note ? { note: row.note } : {}) };
    case 'intake': {
      const ask = names.asks?.find((one) => one.id === row.id);
      return { ...base, title: ask ? askName(ask) : `#${row.id}`, ...(row.note ? { note: row.note } : {}) };
    }
    case 'proposal': {
      const ask = names.asks?.find((one) => one.id === row.id);
      const publishTo = row.publishTo ?? [];
      return {
        ...base,
        title: ask ? askName(ask) : `#${row.id}`,
        // A refused receiver's sentence is the service's, verbatim; otherwise what the declarations proposed.
        detail: row.detail ?? (publishTo.length > 0
          ? i18n.t('work.attention.proposalWhy', { repositories: publishTo.join(', ') })
          : i18n.t('work.attention.proposalNobody')),
        publishTo,
        choices: row.choices ?? [],
      };
    }
    case 'go-ahead':
      return {
        ...base,
        title: row.goAhead ? goAheadTitle(row.goAhead) : row.id,
        where: askWhere(row.ask ?? ''),
        // A stale one says so first, with how long nothing on its ask has needed it, then why its session asked.
        detail: row.stale ? staleDetail(row.ask ?? '', row.stale, row.detail ?? null, now) : row.detail ?? null,
        ask: row.ask,
        number: row.number,
        session: row.session ?? null,
        ...(row.stale ? { stale: row.stale } : {}),
      };
    case 'departure':
      return { ...base, title: quested(row.id), detail: departureWhy(row.departed) };
    case 'set-up':
      return {
        ...base,
        title: quested(row.id),
        detail: row.shows
          ? i18n.t('work.attention.setUpShows', { environment: row.environment, shows: row.shows })
          : i18n.t('work.attention.setUpShown', { environment: row.environment }),
        ...(row.setUp ? { setUp: row.setUp } : {}),
        local: row.local !== false,
        session: row.session ?? null,
      };
    case 'opinion': {
      const wait = row.opinion;
      const quest = questOf(wait?.quest);
      const record = sessionOf(row.id);
      return {
        ...base,
        title: quest ? questName(quest) : record ? sessionTitle(record) : `#${wait?.quest ?? ''}`,
        detail: wait ? opinionSays(i18n.t, wait.opinion).join(' ') : null,
        ...(wait ? { opinion: wait } : {}),
      };
    }
    case 'left':
    case 'review': {
      const fact = row.placed ? placedFact(row.placed, row.placed.shown) : null;
      return { ...base, title: named(row.id), detail: fact ? i18n.t(fact.line, fact.values) : null };
    }
    case 'parked-quest':
      return {
        ...base,
        title: questOf(row.id)?.title ?? `#${row.id}`,
        detail: row.sitting ? sittingSentence(row.sitting) : null,
      };
    // The steward's stop, its session said by the planner's sentence, else by itself.
    case 'steward-stopped':
      return {
        ...base,
        title: questOf(row.id)?.title ?? `#${row.id}`,
        session: row.session ?? null,
        detail: row.sitting ? sittingSentence(row.sitting)
          : row.session ? i18n.t('work.attention.stewardStoppedSession', { session: row.session }) : null,
      };
    // The policy's hold, said by the planner's typed sentence (`policy-ask`, `moves-bound`).
    case 'policy-hold':
      return {
        ...base,
        title: questOf(row.id)?.title ?? `#${row.id}`,
        detail: row.sitting ? sittingSentence(row.sitting) : null,
      };
    case 'unclosed':
      return {
        ...base,
        title: questOf(row.id)?.title ?? `#${row.id}`,
        session: row.session ?? null,
        detail: row.sitting ? sittingSentence(row.sitting) : null,
      };
    // The run's work as its quest names it, and the quest declined, to where it went, held until passed over.
    case 'request-declined': {
      const declined = row.declined;
      return {
        ...base,
        title: declined?.work ? questOf(declined.work)?.title ?? `#${declined.work}` : `#${row.id}`,
        detail: declined
          ? i18n.t(declined.request ? 'work.attention.requestAskedDeclinedWhy' : 'work.attention.requestDeclinedWhy', { quest: declined.quest, to: declined.to })
          : null,
        ...(declined ? { declined } : {}),
      };
    }
    // The run's work as its quest names it, and the presses its task waits for.
    case 'task':
      return row.task ? { ...base, ...taskRead(row.task, questOf(row.task.work)?.title) } : { ...base, title: `#${row.id}` };
    case 'unanswerable':
      return { ...base, title: quested(row.id), detail: i18n.t('work.attention.unanswerableWhy', { repository: row.where ?? '' }) };
    case 'trust':
      return {
        ...base,
        title: row.trust?.folder ?? row.id,
        where: row.where ?? (row.ask ? askWhere(row.ask) : `#${row.quest ?? ''}`),
        // On the person's own sign-in's file, it says to trust the folder with the tool itself.
        detail: i18n.t(row.trust?.own ? 'trust.ownHome' : 'work.attention.trustWhy'),
        ...(row.trust
          ? { trust: { folder: row.trust.folder, trustFile: row.trust.trustFile, ...(row.trust.own ? { own: true } : {}) } }
          : {}),
        // What its door opens: the quest it holds, or the ask whose intake it holds.
        ...(row.quest ? { quest: row.quest } : row.ask ? { ask: row.ask } : {}),
      };
    case 'rule':
      return {
        ...base,
        title: row.proposal ? proposalChange(row.proposal) : row.id,
        where: row.proposal ? proposalAuthor(row.proposal) : '',
        detail: row.detail ?? null,
        ...(row.proposal ? { proposal: row.proposal } : {}),
      };
    case 'helper':
      return {
        ...base,
        title: row.title ?? row.id,
        detail: i18n.t('work.attention.helperWhy', { session: named(row.session) }),
        session: row.session ?? null,
      };
    // The session as Sessions names it, what happened, and, where the person's words wait,
    // that ending it hands them on. Its words say only what happened, so they hold before and after Daoris's word is built.
    case 'quiet': {
      const quiet = row.quiet;
      const values = { session: named(row.session ?? row.id), minutes: quiet?.minutes ?? 0 };
      return {
        ...base,
        title: named(row.session ?? row.id),
        detail: [
          i18n.t(quiet?.busy ? 'work.attention.quietBusyWhy' : 'work.attention.quietWhy', values),
          // It waits behind another run of its repository's heavy gates: named by its session, or said to be a terminal's.
          ...(typeof quiet?.heavy === 'string'
            ? [quiet.heavy
              ? i18n.t('work.attention.quietHeavy', { holder: named(quiet.heavy) })
              : i18n.t('work.attention.quietHeavyTerminal')]
            : []),
          ...(quiet?.words && !quiet.busy ? [i18n.t('work.attention.quietWords')] : []),
        ].join(' '),
        session: row.session ?? row.id,
        ...(row.quest ? { quest: row.quest } : {}),
        ...(quiet ? { quiet } : {}),
      };
    }
    // The session as Sessions names it, that its helper ended unanswered, and the
    // service's refusal as it said it, a value passed through, never re-worded.
    case 'helper-ended':
      return {
        ...base,
        title: named(row.session ?? row.id),
        detail: i18n.t('work.attention.helperEndedWhy', { session: named(row.session ?? row.id), words: row.detail ?? '' }),
        session: row.session ?? row.id,
        ...(row.quest ? { quest: row.quest } : {}),
      };
    // The session as Sessions names it, and what waits, counted by the kind of wait; a sweep's end the entry and revision it ends, and
    // the reason as the sweep wrote it, a value passed through, never re-worded.
    case 'memory': {
      const wait = row.memory ?? { ends: 0, replacements: 0, staged: 0 };
      if (wait.end) {
        return {
          ...base,
          title: i18n.t('work.memory.end', { target: `${wait.end.repository}:${wait.end.target}`, revision: wait.end.revision ?? '?' }),
          detail: wait.end.reason,
          session: null,
          memory: wait,
        };
      }
      return {
        ...base,
        title: named(row.session ?? row.id),
        detail: [
          wait.ends > 0 ? i18n.t('work.attention.memoryEnds', { count: wait.ends }) : '',
          wait.replacements > 0 ? i18n.t('work.attention.memoryReplacements', { count: wait.replacements }) : '',
          wait.staged > 0 ? i18n.t('work.attention.memoryStaged', { count: wait.staged }) : '',
        ].filter(Boolean).join(' · '),
        session: row.session ?? row.id,
        ...(row.memory ? { memory: row.memory } : {}),
      };
    }
    // A held candidate by its line, a superseded commit and a waiting landing by their branch, each said by its wait.
    case 'integration': {
      const wait = row.integration;
      const short = (commit?: string | null) => (commit ?? '').slice(0, 7);
      const title = wait?.wait === 'decide'
        ? i18n.t('integration.view.candidate', { line: wait.line ?? '' })
        : wait?.wait === 'declaration' ? wait.repository : wait?.branch ?? row.title ?? row.id;
      const detail = wait?.wait === 'decide'
        ? i18n.t('work.attention.integrationDecideWhy', { branches: (wait.branches ?? []).join(', '), why: wait.why ?? '' }).trim()
        : wait?.wait === 'superseded'
          ? i18n.t('work.attention.integrationSupersededWhy', { commit: short(wait.commit), subject: wait.subject ?? '', line: wait.line ?? '' })
          : wait?.wait === 'card'
            ? i18n.t('work.attention.integrationCardWhy', { tip: short(wait.tip) })
            // A declaration never accepted runs nothing; one changed since goes on under the last accepted blob.
            : wait?.wait === 'declaration'
              ? i18n.t(wait.state === 'changed' ? 'work.attention.integrationDeclarationChangedWhy' : 'work.attention.integrationDeclarationUnacceptedWhy',
                { line: wait.line ?? '', tip: short(wait.tip) })
              : null;
      return { ...base, title, detail, stands: row.stands === true, ...(wait ? { integration: wait } : {}) };
    }
    // Its note's first line as the agent wrote it, the session as Sessions names it, and the agent as the roster names it.
    case 'hand-over': {
      const agent = row.agent ? agentNamed(row.agent, names.tools) : null;
      return {
        ...base,
        title: row.title ?? row.id,
        detail: i18n.t('work.attention.handOverWhy', { session: named(row.session), agent: agent ?? '' }),
        session: row.session ?? null,
        ...(agent ? { agent } : {}),
        stands: row.stands === true,
      };
    }
    case 'permission': {
      // The bound as the driver answered it for this request; a host that does not say leaves it out.
      const minutes = row.permission ? boundMinutes(row.permission) : null;
      return {
        ...base,
        title: row.permission?.title ?? i18n.t('work.wait.permission.untitled'),
        detail: minutes === null
          ? i18n.t('work.attention.permissionWhyUnbounded', { session: named(row.session) })
          : i18n.t('work.attention.permissionWhy', { session: named(row.session), minutes }),
        session: row.session ?? null,
        ...(row.permission ? { permission: row.permission } : {}),
      };
    }
    // Named as Sessions names the session whose line it would take on, said by the agent that answered.
    case 'answer': {
      const agent = row.answer?.agent;
      const words = agent ? i18n.t('work.attention.answerWhy', { agent }) : i18n.t('work.attention.answerWhySomeone');
      // The person's rule that held it, said in the one sentence the head and the foot say.
      const rule = row.answer?.rule;
      return {
        ...base,
        title: named(row.session ?? row.id),
        detail: rule
          ? i18n.t('work.attention.answerRuleWhy', {
            words, n: rule.n, at: rule.at, place: rule.place === 'machine' ? i18n.t('settings.supervisor.machine') : rule.place.slice(rule.place.indexOf(':') + 1),
          })
          : words,
        session: row.session ?? row.id,
        ...(row.answer ? { answer: row.answer } : {}),
      };
    }
    case 'work': {
      if (row.unreadable) return unreadableWorded(row, row.unreadable);
      if (row.held) return heldWorded(row, row.held);
      if (row.ended) return endedWorded(row, row.ended);
      const entries = row.blocked?.entries ?? [];
      // Named by the session that recorded its tree, as Sessions names it, else its branch or its tree's folder; each
      // entry worded by its code in the reader's language, the reason said once. An unknown owner says so first.
      const recorder = sessionOf(row.session);
      const cut = workCutFor(row, names.sessions);
      return {
        ...base,
        title: recorder ? named(row.session) : row.title ?? row.id,
        detail: [
          ...(row.blocked?.unknown && !entries.some((entry) => entry.code === 'unrecorded' || entry.code?.startsWith('unknown-')) ? [i18n.t('work.milestone.wait.owner')] : []),
          ...entries.map((entry) => i18n.t('work.milestone.cleanup.blocked', { what: entryName(i18n.t, entry), detail: entryDetail(i18n.t, entry) })),
        ].join(' '),
        session: row.session ?? null,
        ...(cut ? { cutFor: cut.id } : {}),
      };
    }
    case 'account-wait':
    case 'signed-out':
      return accountRowWorded(row, names, now);
    // Named as its card names it, by its act where the page words its route and by its route otherwise; the terminal as
    // the system names it, and until when it waits.
    case 'confirmation':
      return { ...base, ...(row.confirmation ? confirmationWorded(row.confirmation) : { title: row.id, detail: null }) };
    // Named by the volume's free space and where it is mounted; why, its floor, what Daoris freed, and what the press could.
    case 'space':
      return { ...base, ...(row.space ? spaceWorded(row.space) : { title: row.id, detail: null }) };
    // Named by its host and why, said once; its repositories, that nothing tries it again
    // until the person's *Try again* or a change, and git's own words, as written. What its press lifts is its git and its workspace.
    case 'git-held':
      return {
        ...base,
        where: row.where ?? row.git?.remote ?? '',
        ...(row.git ? gitHeldWorded(row.git) : { title: row.id, detail: null }),
        ...(row.git ? { git: row.git } : {}),
        ...(row.workspace ? { workspace: row.workspace } : {}),
      };
    // Who asks to push to or fetch from which remote, how long it has waited and the bound; a door that has gone
    // is said so, its Allow kept for the identical retry.
    case 'git-ask':
      return {
        ...base,
        ...(row.gitAsk ? gitAskWorded(row.gitAsk, row.since) : { title: row.id, detail: null }),
        ...(row.gitAsk ? { gitAsk: row.gitAsk } : {}),
        ...(row.session ? { session: row.session } : {}),
        ...(row.workspace ? { workspace: row.workspace } : {}),
      };
    // The reader's claim, said as its claim with its certainty and the call it rests on; never a fact.
    case 'second-check':
      return {
        ...base,
        title: i18n.t('work.attention.secondCheck.title'),
        detail: row.secondCheck
          ? i18n.t('work.attention.secondCheck.detail', {
            says: row.secondCheck.says, call: row.secondCheck.call,
            certainty: i18n.t(`help.secondCheck.certainty.${row.secondCheck.certainty}`, { defaultValue: row.secondCheck.certainty }),
          })
          : null,
        ...(row.secondCheck ? { secondCheck: row.secondCheck } : {}),
        ...(row.session ? { session: row.session } : {}),
      };
    default:
      return { ...base, title: row.title ?? row.id };
  }
}

/** An agent's door by what a person calls it (the frame's hand-over roster's): its product, else its tool's, else its id. */
function agentNamed(harness: string, tools: readonly Tool[] = []): string {
  const door = tools.flatMap((tool) => tool.doors.map((each) => ({ tool, door: each }))).find((each) => each.door.door === harness);
  return door?.door.product || door?.tool.product || harness;
}

/** A terminal's press waiting for the window, in its card's words: what it asks, what sent it, that it runs only on Confirm; no value. */
function confirmationWorded(waiting: NonNullable<PushedAttention['confirmation']>): Pick<Attention, 'title' | 'detail'> {
  const act = `person.route.${waiting.route}`;
  return {
    title: i18n.exists(act) ? i18n.t('person.ask.to', { act: i18n.t(act) }) : i18n.t('person.ask.for', { act: waiting.route }),
    detail: [askerSaid(i18n.t, waiting.asker), i18n.t('work.attention.confirmationWhy')].join(' '),
  };
}

/**
 * A volume still below its free-space floor, in the card's words: its free space of its size where it
 * is mounted; then the floor, what Daoris freed early of what it can make again, and what *Free up space…* could free where it listed any.
 */
function spaceWorded(low: NonNullable<PushedAttention['space']>): Pick<Attention, 'title' | 'detail'> {
  return {
    title: i18n.t('work.attention.spaceTitle', { free: size(low.free), total: size(low.total), root: low.root ?? '' }),
    detail: [
      ...(low.floor != null ? [i18n.t('work.attention.spaceFloor', { floor: size(low.floor) })] : []),
      low.freed > 0 ? i18n.t('work.attention.spaceFreed', { size: size(low.freed) }) : i18n.t('work.attention.spaceFreedNothing'),
      ...(low.could ? [i18n.t('work.attention.spaceCould', { size: size(low.could) })] : []),
    ].join(' '),
  };
}

/**
 * A held git remote, in the reader's language: its host and why as its title; then the repositories
 * it holds, that Daoris does not try it again until *Try again* or a change, and git's own last line. The host, the repositories and git's
 * words are content.
 */
export function gitHeldWorded(held: GitHeld, t: TFunction = i18n.t.bind(i18n)): Pick<Attention, 'title' | 'detail'> {
  const repositories = held.repositories ?? [];
  // The move: one row for every remote nothing serves, counted, its hosts named, and the repositories whose own configuration
  // names authentication listed apart.
  if (held.class === 'no-identity') {
    const hosts = (held.hosts?.length ? held.hosts : [held.host]).join(', ');
    const own = held.ownInput ?? [];
    return {
      title: t('settings.git.moved.title'),
      detail: [
        t('settings.git.moved.says', { count: repositories.length, hosts }),
        t('settings.git.moved.repositories', { repositories: repositories.join(', ') }),
        ...(own.length ? [t('settings.git.moved.own', { repositories: own.join(', ') })] : []),
      ].join(' '),
    };
  }
  const setting = ownSetting(held);
  const identity = held.served === 'identity' && held.identity ? held.identity : null;
  // The titles: a refusal names the identity the host refused, a push refused the identity it let read, a host's key the identity's
  // or the person's own ssh; an own-input hold that names a setting names the repository's own authentication.
  const title = setting ? t('work.attention.gitHeld.title.own-setting', { host: held.host, repository: repositories[0] ?? '' })
    : identity && (held.class === 'refused' || held.class === 'write-refused')
      ? t(`work.attention.gitHeld.titleOf.${held.class}`, { host: held.host, identity, remote: held.remote })
      : held.served === 'own' && (held.class === 'host-unknown' || held.class === 'host-changed')
        ? t(`work.attention.gitHeld.titleOwn.${held.class}`, { host: held.host })
        : t(`work.attention.gitHeld.title.${held.class}`, { host: held.host });
  return {
    title,
    detail: t(gitHeldDetailKey(held), { repositories: repositories.join(', '), words: held.words ?? '', setting: setting ?? '', host: held.host }),
  };
}

/**
 * A git go-ahead in the reader's language: *{session} asks to push to {remote}*, or a fetch, of its
 * repository; then how long it has waited of its bound, or that its command has stopped and an Allow is kept for its retry. Shared by
 * its row and its session's head.
 */
export function gitAskWorded(ask: GitAskWait, since?: string | null, t: TFunction = i18n.t.bind(i18n)): Pick<Attention, 'title' | 'detail'> {
  const remote = ask.remote ?? ask.host ?? '';
  const act = ask.class === 'write' ? 'push' : 'fetch';
  const title = ask.session
    ? t(`work.attention.gitAsk.title.session.${act}`, { session: ask.session, remote, repository: ask.repository ?? '' })
    : t(`work.attention.gitAsk.title.repository.${act}`, { remote, repository: ask.repository ?? ask.host ?? '' });
  const waited = since ? elapsed(since) : null;
  return {
    title,
    detail: ask.gone ? t('work.attention.gitAsk.gone')
      : waited ? t('work.attention.gitAsk.waits', { waited, bound: GIT_ASK_BOUND_MINUTES }) : t('work.attention.gitAsk.waitsNow', { bound: GIT_ASK_BOUND_MINUTES }),
  };
}

/** How long a door holds a git go-ahead, in minutes: a held call's bound (`GitAsks.Bound`). */
export const GIT_ASK_BOUND_MINUTES = 15;

/**
 * A unit's node record that does not read: named by its unit where it still names one,
 * else by its file; it says that nothing on that unit is judged or started until the person mends it, in the folder its press opens.
 */
function unreadableWorded(row: PushedAttention, record: UnreadableRecord): Attention {
  return {
    id: row.id,
    kind: 'work',
    since: row.since,
    where: row.where ?? '',
    title: record.unit
      ? i18n.t('work.milestone.unreadable.title', { unit: record.unit })
      : i18n.t('work.milestone.unreadable.titleUnnamed', { file: record.file }),
    detail: i18n.t('work.milestone.unreadable.detail', { file: record.file }),
    unreadable: record,
  };
}

/**
 * A closure graph closed only in part: the quest lost and why, and its close node, which
 * waits again and would close that quest over the winner's take; the person's stop recorded stays a row, saying so, until the next look.
 */
function endedWorded(row: PushedAttention, end: GraphEnd): Attention {
  return {
    id: row.id,
    kind: 'work',
    since: row.since,
    where: row.where ?? end.unit,
    title: i18n.t('work.milestone.closed.title', { quest: end.quest ?? '' }),
    detail: endSentences(i18n.t, end).join(' '),
    ended: end,
  };
}

/** Each pushed row worded, in the order given. */
export function attentionOf(rows: readonly PushedAttention[], names: AttentionNames = {}, now: Date = new Date()): Attention[] {
  return rows.map((row) => attentionWorded(row, names, now));
}
