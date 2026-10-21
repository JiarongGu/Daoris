import i18n from '../../locales/i18n';
import type { Ask, GoAhead, Quest, SetUpRef, Session } from '../../data/api';
import type { RuleProposal } from '../../settings/repository/AgentRules';
import { proposalAuthor, proposalChange } from '../../settings/repository/proposals';
import { type Consideration, type TrustHold, sittingSentence } from '../../data/signals';
import type { Tool } from '../../agents/tools';
import type { SessionGrouping } from '../groups';
import { askName, questName, sessionTitle } from '../identity';
import type { Attention, AttentionKind } from './AttentionRow';
import { accountRowWorded, type PushedAccountFacts } from './accountWords';
import {
  CLOSE_SENDER, endOffersStop, endSentences, entryDetail, entryName, type GraphEnd, type HeldAttempt, heldWhy, heldWords, type MilestoneEntry,
} from '../head/milestone';
import { type OpinionWait, opinionState } from '../gates/opinion';
import { boundMinutes, type PermissionWait } from '../waits/PermissionAsks';
import type { AnswerWait } from '../waits/AnswerAlone';
import { opinionSays } from '../gates/OpinionGate';
import { placedFact } from '../rail/SessionRow';
import type { NoteRecord } from '../noteLines';

/**
 * *What needs you*'s three groups (UX6c, design §6.2), in the order a person acts on them: what holds work up until they
 * act, what waits for their word, and what is ready for them to look at.
 */
export type AttentionGroup = 'holding' | 'word' | 'ready';

/** The groups in the order the band shows them. */
export const GROUP_ORDER: readonly AttentionGroup[] = ['holding', 'word', 'ready'];

/**
 * Each kind's group (design §6.2). **Holding work**: a parked session holds its tree, a parked quest its repository's next
 * start, a go-ahead its session's act, a folder to trust the start the driver holds. **Waiting for your word**: an ask to
 * publish, an intake's question, a departure, a widening, a quest nobody can take, none of which holds anything already
 * running. **Ready for you**: finished work to review.
 */
export const ATTENTION_GROUP: Readonly<Record<AttentionKind, AttentionGroup>> = {
  parked: 'holding',
  'parked-quest': 'holding',
  'go-ahead': 'holding',
  // A tool call a session holds for the person's answer under asking (AUTONOMY1b3c, D184 point 3): its turn stands still until they
  // answer, or the bound refuses it, as a go-ahead holds its session's act.
  permission: 'holding',
  // A third answer in a row with nobody between, which the answers-alone bound holds for the person (COWORK1e, D188 point 4): its
  // line stands still until they let it go on or keep it.
  answer: 'holding',
  trust: 'holding',
  // A start waiting on its accounts holds that start; a signed-out account a list holds, the next start the list takes (UX6d).
  'account-wait': 'holding',
  'signed-out': 'holding',
  proposal: 'word',
  intake: 'word',
  departure: 'word',
  // A set-up waiting for the person's look (REVIEWENV1g): its chain's landing waits on their verdict, as a departure's next
  // step waits on their yes, and nothing already running is held by it.
  'set-up': 'word',
  // A second opinion that waits on the person (XAGENT1g, the second-agent design §9): its landing waits on their answer, and
  // nothing already running is held by it.
  opinion: 'word',
  rule: 'word',
  // An agent's ask for a helper that waits on the person's yes (MIXAGENT2c, D163 point 3): the session that asked goes on
  // meanwhile, as one that proposed a rule does, so it holds nothing up.
  helper: 'word',
  // A unit only the person's press settles (FABRIC-MILESTONE1b, the fabric events design §D0; OVERVIEWNOISE1b): one whose owner
  // nothing records, or a tree its session recorded that no branch record follows. Nothing running is held by it, and nothing of it
  // is removed until they press. A guard the look keeps a branch by is said on its session's page, never here.
  work: 'word',
  unanswerable: 'word',
  review: 'ready',
};

/**
 * The rows by group, only the groups that hold any, in `GROUP_ORDER`, each keeping the order `attentionInOrder` gave it.
 * A group with no rows is not shown (design §6.2).
 */
export function attentionGroups(items: readonly Attention[]): { group: AttentionGroup; items: Attention[] }[] {
  return GROUP_ORDER
    .map((group) => ({ group, items: items.filter((item) => ATTENTION_GROUP[item.kind] === group) }))
    .filter(({ items: held }) => held.length > 0);
}

/**
 * What a row's door into its run names (WORKFLOW1c, the workflow design §7: *What needs you*'s row for a waiting step opens that
 * step): the session it stands for, or the quest whose newest session here does. A row that is no step of a run (an ask still to
 * publish, a folder's trust, an account, a rule) has none. The run opens where it stands, which is the step the row waits at.
 */
export function rowRun(item: Attention): { session?: string | null; quest?: string | null } | null {
  switch (item.kind) {
    // The work's session waits on the person's answer; the landing on their accept, in its review.
    case 'parked':
    case 'review': return { session: item.id };
    // The work's act waits on the person's go-ahead, asked by the session the row names.
    case 'go-ahead': return item.session ? { session: item.session } : null;
    // The look waits on the person: the set-up step's session, else the step's newest here.
    case 'set-up': return { session: item.session ?? null, quest: item.id };
    // The work's done waits on the person's yes; a quest parked on its failed sessions waits on their *Try again*.
    case 'departure':
    case 'parked-quest': return { quest: item.id };
    default: return null;
  }
}

/** An act a row offers (design §6.2–§6.3), named as the row's press. */
export type AttentionActId =
  | 'publish' | 'choose' | 'retry' | 'approve' | 'refuse' | 'trust' | 'accept-departure' | 'accept-rule' | 'decline-rule'
  | 'sign-in' | 'sign-in-new' | 'read' | 'let-run' | 'reviewed' | 'not-yet' | 'show-again' | 'opinion-again' | 'opinion-anyway'
  | 'let-ask' | 'not-now'
  // AUTONOMY1b3c: the person's answer to a tool call held for them, one per choice its agent offered.
  | 'allow-once' | 'refuse-once' | 'refuse-always'
  // COWORK1e: the person's word on a third answer alone held for them: *Go on with this answer*, *Not now*.
  | 'go-on-answer' | 'keep-answer'
  // FABRIC-PERFORM1g: the person's word on a plugin step's attempt held for them.
  | 'happened' | 'did-not' | 'stop-step'
  // FABRIC-CLOSE1q: the person's word on Daoris's own close held for them: its recoveries, and the rest of the work stopped apart.
  | 'record-close' | 'resend-close' | 'stop-close'
  // FABRIC-CLOSE1j: the stop of a close found closed only in part, its node waiting again.
  | 'stop-partial';

/** The act each word offered on a held attempt is pressed by (FABRIC-PERFORM1g, FABRIC-CLOSE1q): a close's stop is not a step's. */
export function heldActs(held: HeldAttempt): AttentionActId[] {
  const close = held.plugin === CLOSE_SENDER;
  return heldWords(held).map((word): AttentionActId => {
    switch (word) {
      case 'record': return 'record-close';
      case 'resend': return 'resend-close';
      case 'stop': return close ? 'stop-close' : 'stop-step';
      default: return word;
    }
  });
}

/**
 * An act a row offers, and for an account's act which account it is for (UX6d): a row may sign two in; for a held tool call's,
 * the agent's option it answers with (AUTONOMY1b3c).
 */
export type AttentionOffer = { act: AttentionActId; account?: string; option?: string };

/** The act each kind of choice the person may make on a held tool call is pressed by (AUTONOMY1b3c); any other kind is no act here. */
const CHOICE_ACTS: Readonly<Record<string, AttentionActId>> = {
  allow_once: 'allow-once',
  reject_once: 'refuse-once',
  reject_always: 'refuse-always',
};

/**
 * What a held tool call's row offers (AUTONOMY1b3c, D184 point 3): a press per choice its agent offered, in the agent's order, each
 * answering with that choice's id. A kind the page cannot name is left to the session's page, which says it by the agent's name; and
 * where no allow was offered, none is.
 */
function permissionOffers(item: Attention): AttentionOffer[] {
  return (item.permission?.choices ?? [])
    .filter((choice) => CHOICE_ACTS[choice.kind] !== undefined)
    .map((choice) => ({ act: CHOICE_ACTS[choice.kind]!, option: choice.id }));
}

/**
 * The acts that ask once under the row before they do anything (design §6.3, D41 §4): a go-ahead's yes or no, which every
 * session on its ask is handed; a folder's trust, a widening of the rules and an account let into a list (D130 §3.3), which
 * widen what Daoris may do or spend; and a choice of receiver, which is a choice before it is a press.
 */
export const ASKS_ONCE: ReadonlySet<AttentionActId> = new Set([
  'choose', 'approve', 'refuse', 'trust', 'accept-rule', 'let-run',
  // A *not yet* asks for the person's words, which are what the set-up step's session acts on (REVIEWENV1g).
  'not-yet',
  // *Go on anyway…* says what stays unsettled and takes the person's words (XAGENT1g, the second-agent design §8.5).
  'opinion-anyway',
]);

/** How many acts an account's row offers beside its door: three controls a row at most (design §9.3). */
const ACCOUNT_ACTS = 2;

/**
 * What an account's row offers (UX6d, design §6.2–§6.3), each for one account: *Sign in* to each account the start passed
 * signed out, in the order it names them; *Let … run …* for an account outside its list known ready (D130 §3.3), or in its
 * place *Read* for one there never read (UXFIX3), since letting run an account nobody looked at widens what Daoris may spend
 * on a guess; and *Read* for one it names that no read answered (§5.3: a reading only on the press). A signed-out account's
 * own row signs it in. Never the tool's own sign-in, which is the person's, nor where Daoris runs no sign-in for the agent.
 * Where no Daoris account is signed in, *Sign in for Daoris* leads (OWNSPACE1b-B, D175 point 7): it names no account, and runs
 * the add flow's sign-in in place. Two at most: the rest are on the agent's page, the row's door, and its line says them.
 */
function accountOffers(item: Attention): AttentionOffer[] {
  const facts = item.account;
  if (!facts) return [];
  const signIns: AttentionOffer[] = facts.signsIn
    ? [
      ...(facts.noAccount ? [{ act: 'sign-in-new' } as const] : []),
      ...facts.named.filter((one) => one.state === 'out' && one.id).map((one) => ({ act: 'sign-in' as const, account: one.id! })),
    ]
    : [];
  if (item.kind === 'signed-out') return signIns.slice(0, 1);
  const outside: AttentionOffer[] = facts.outside
    ? [{ act: 'let-run', account: facts.outside.id }]
    : facts.readFirst ? [{ act: 'read', account: facts.readFirst.id }] : [];
  return [
    ...signIns,
    ...outside,
    ...facts.named.filter((one) => one.state === 'unknown' && one.id && !one.read).map((one) => ({ act: 'read' as const, account: one.id! })),
  ].slice(0, ACCOUNT_ACTS);
}

/**
 * What a row offers, in the order it shows them (design §6.2–§6.3): an act only where one press is safe and the row says
 * what it does, and nothing where the answer needs reading first (a park's question, an intake's, a review), whose door is
 * the row's one control. **One rule per kind**, read by every row and the tests, so two rows of a kind cannot differ, and
 * each mirrors the record's own: a quest's page offers *Try again* on the planner's park and *Accept the departure* on a
 * held done; an ask's page publishes to what its declarations proposed and to any receiver its workspace can ask; an
 * agent's page signs an account in and reads it on its row.
 */
export function attentionOffers(item: Attention): AttentionOffer[] {
  const plain = (...acts: AttentionActId[]) => acts.map((act): AttentionOffer => ({ act }));
  switch (item.kind) {
    case 'proposal': return (item.publishTo?.length ?? 0) > 0 ? plain('publish', 'choose') : plain('choose');
    case 'parked-quest': return plain('retry');
    case 'go-ahead': return plain('approve', 'refuse');
    case 'trust': return item.trust ? plain('trust') : [];
    case 'departure': return plain('accept-departure');
    // A set-up shown and waiting (REVIEWENV1g, design §3.3): the strip's chip's and the step's page's own presses, *Show it
    // again* only for a local set-up, which Daoris serves; *Skip…* is the step's page's, where the work is read.
    case 'set-up': return item.setUp ? plain('reviewed', 'not-yet', ...(item.local ? ['show-again' as const] : [])) : [];
    case 'rule': return plain('accept-rule', 'decline-rule');
    // An agent's ask for a helper (MIXAGENT2c): its session's page's own yes and no, each one press, as they are there.
    case 'helper': return plain('let-ask', 'not-now');
    // A tool call held for the person (AUTONOMY1b3c): its session's card's own presses, one each, as `PermissionAsks` says why.
    case 'permission': return permissionOffers(item);
    // A third answer alone held for the person (COWORK1e, §8.3): its session's page's own two presses, each one press.
    case 'answer': return item.answer ? plain('go-on-answer', 'keep-answer') : [];
    // A second opinion that waits on the person (XAGENT1g, §8.5): *Go on anyway…* only where no press of theirs is coming (the
    // work lands by itself, or none could be had and the rule requires one), and a pass asked again; the rest are its session's.
    case 'opinion': return opinionOffers(item);
    // A plugin step's attempt held for the person (FABRIC-PERFORM1g, §D6): their three words, until one is recorded and the driver's next
    // look applies it; a held close (FABRIC-CLOSE1q) its own, as the driver offers them. A unit only the clean-up's press settles has
    // that list, on its workspace's Branches, for its door (OVERVIEWNOISE1b), and no act of its own here.
    // FABRIC-CLOSE1j: a close found closed only in part offers its node's stop alone, while it is offered and no word of theirs waits.
    case 'work': return item.held && !item.held.resolution ? plain(...heldActs(item.held))
      : item.ended && endOffersStop(item.ended) ? plain('stop-partial') : [];
    case 'account-wait':
    case 'signed-out': return accountOffers(item);
    default: return [];
  }
}

/**
 * What a second opinion's row offers (XAGENT1g; the second-agent design §8.2, §8.5): a dispute where a press of the person's is
 * coming (*Accept…* on its session's page) asks again only, since that press answers it; where none is (the work lands by
 * itself), *Go on anyway…* too. None to be had where the rule requires one, *Try again* and *Go on anyway…*; commits nobody read
 * where the work lands by itself, the same. The same agent fresh, *I looked myself…* and *Send back…* are on its session's page.
 */
function opinionOffers(item: Attention): AttentionOffer[] {
  const wait = item.opinion;
  if (!wait) return [];
  const anyway = wait.auto || opinionState(wait.opinion.state) === 'unavailable';
  return [...(anyway ? [{ act: 'opinion-anyway' as const }] : []), { act: 'opinion-again' as const }];
}

/** The acts a row offers, without the accounts they are for: what each kind's rule says, read by the tests. */
export function attentionActs(item: Attention): AttentionActId[] {
  return attentionOffers(item).map(({ act }) => act);
}

/**
 * One row of *What needs you* as the shell pushes it (PUSH1d4, D182's PUSH1d4 note): who needs the person is judged in .NET
 * (`NeedsAPerson`, `AccountAttention`), and the page orders and words its rows. Each carries its kind, its id, since when it has
 * waited (null where nothing says), the values its words are made of, and the workspace of the record it was judged from where a
 * window's scope filters it. What it is called is named here from the records the page holds (`attentionWorded`).
 */
export type PushedAttention = {
  kind: AttentionKind;
  id: string;
  since: string | null;
  /** The record's workspace, for a row judged from the service's sessions, quests or asks: a scoped window shows its own. */
  workspace?: string;
  /** Where it waits, where that is a value: a repository, a circle, an agent's product; absent or null where the page words it. */
  where?: string | null;
  /** A parked session's or a parked intake's note, its parts beside its English (LANG1b). */
  note?: NoteRecord;
  /** Authored words shown as written: a refused receiver's sentence, a go-ahead's why, a widening's why. */
  detail?: string | null;
  publishTo?: string[];
  choices?: string[];
  ask?: string;
  number?: number;
  /** A go-ahead's act, which its title says. */
  goAhead?: Pick<GoAhead, 'kind' | 'on' | 'act'>;
  session?: string | null;
  /** A held done's first departed requirement, its reason, and how many it departed from. */
  departed?: { count: number; number: number; reason: string } | null;
  setUp?: SetUpRef;
  local?: boolean;
  /** A set-up's environment and what its newest set-up showed. */
  environment?: string;
  shows?: string | null;
  opinion?: OpinionWait;
  /** Work to review's place in Sessions' list, which its line says. */
  placed?: SessionGrouping;
  /** A parked quest's verdict, which says why it sits. */
  sitting?: Consideration;
  trust?: TrustHold;
  quest?: string;
  proposal?: RuleProposal;
  /** A title that is a value: a helper's question's first line, a unit's branch or its tree's folder. */
  title?: string;
  permission?: PermissionWait;
  /** A third answer alone held for the person (COWORK1e): its wait whole, the agent that answered among it. */
  answer?: AnswerWait;
  /**
   * A unit only the person's press settles (OVERVIEWNOISE1b): whether its owner is unknown, and the entries it is blocked on, each with
   * the code the page words it by and its facts, where the driver gave one.
   */
  blocked?: { unknown: boolean; entries: Pick<MilestoneEntry, 'kind' | 'resource' | 'detail' | 'code' | 'facts'>[] };
  held?: HeldAttempt;
  /** A closure graph closed only in part whose close node's stop is offered (FABRIC-CLOSE1j), its end whole. */
  ended?: GraphEnd;
  /** A start waiting on accounts that holds an intake, or quests in several repositories: `where` is its workspace. */
  wholeWorkspace?: boolean;
  /** What a start waiting on accounts holds, which its title says. */
  holding?: { quests: string[]; asks: string[] };
  /** When a signed-out account was read (UXFIX3). */
  read?: string | null;
  /** Where a signed-out account runs, each a workspace or null for this machine. */
  runs?: (string | null)[];
  account?: PushedAccountFacts;
};

/**
 * The slice's answer composed (`attentionSlice`): the rows in the band's order; the keys of what waits on the person in Sessions,
 * which its badge marks; and the keys of the sessions the driver runs, which the badge and the status bar count (SESSBADGE1).
 */
export type AttentionPushed = {
  rows: PushedAttention[];
  sessions: { key: string; workspace?: string | null }[];
  working: { key: string; workspace?: string | null }[];
};

/**
 * Each kind's place among the kinds of its group when two waited as long (design §6.2's order of kinds): the accounts', parks,
 * parked quests, go-aheads, held calls, held answers (COWORK1e) and folders; then asks, departures, set-ups, second opinions, widenings, helpers, a unit's
 * blocked clean-up, a held attempt and quests nobody can take; then review.
 */
const KIND_ORDER: readonly AttentionKind[] = [
  'account-wait', 'signed-out', 'parked', 'parked-quest', 'go-ahead', 'permission', 'answer', 'trust',
  'proposal', 'departure', 'set-up', 'opinion', 'rule', 'helper', 'work', 'unanswerable', 'review',
];

/** A row's place among its group's kinds; an intake is an ask's, and a held attempt comes after a unit's blocked clean-up. */
function kindRank(row: PushedAttention): number {
  const kind = row.kind === 'intake' ? 'proposal' : row.kind;
  return KIND_ORDER.indexOf(kind) + (row.kind === 'work' && row.held ? 0.5 : 0);
}

/**
 * The band's order (UX6c, design §6.2; D182's PUSH1d4 note): by group, holding work first; within a group what has waited longest
 * first, whatever its kind, a row with no known wait (a signed-out account's, UXFIX3) after every row with one; then §6.2's order
 * of kinds; then the row's key. A place in the list is never pushed (D182's PUSH1c3a note): one move would push every row after it.
 */
export function attentionInOrder(rows: readonly PushedAttention[]): PushedAttention[] {
  const groupOf = (row: PushedAttention) => GROUP_ORDER.indexOf(ATTENTION_GROUP[row.kind] ?? 'word');
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

/** Which requirement a held done departed from and why, in its own words; how many where it departed from several. */
function departureWhy(departed: PushedAttention['departed']): string | null {
  if (!departed) return null;
  return departed.count === 1
    ? i18n.t('work.attention.departureWhy', { number: departed.number, reason: departed.reason })
    : i18n.t('work.attention.departureWhyMore', { departed: departed.count, number: departed.number, reason: departed.reason });
}

/**
 * A pushed row worded (PUSH1d4): what it is called, from the records the page holds (a session as Sessions names it, a quest and an
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
        // A refused receiver's sentence is the service's, verbatim; otherwise what was proposed.
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
        detail: row.detail ?? null,
        ask: row.ask,
        number: row.number,
        session: row.session ?? null,
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
    case 'unanswerable':
      return { ...base, title: quested(row.id), detail: i18n.t('work.attention.unanswerableWhy', { repository: row.where ?? '' }) };
    case 'trust':
      return {
        ...base,
        title: row.trust?.folder ?? row.id,
        where: row.where ?? (row.ask ? askWhere(row.ask) : `#${row.quest ?? ''}`),
        detail: i18n.t('work.attention.trustWhy'),
        ...(row.trust ? { trust: { folder: row.trust.folder, trustFile: row.trust.trustFile } } : {}),
        // What its door opens: the quest it holds, or the ask whose intake it holds (D73).
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
    case 'permission': {
      // The bound as the driver answered it for this request (PERMUNTIL1); a host that does not say leaves it out.
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
    // COWORK1e: named as Sessions names the session whose line it would take on, said by the agent that answered.
    case 'answer': {
      const agent = row.answer?.agent;
      return {
        ...base,
        title: named(row.session ?? row.id),
        detail: agent ? i18n.t('work.attention.answerWhy', { agent }) : i18n.t('work.attention.answerWhySomeone'),
        session: row.session ?? row.id,
        ...(row.answer ? { answer: row.answer } : {}),
      };
    }
    case 'work': {
      if (row.held) return heldWorded(row, row.held);
      if (row.ended) return endedWorded(row, row.ended);
      const entries = row.blocked?.entries ?? [];
      // OVERVIEWNOISE1b: named by the session that recorded its tree, as Sessions names it, else its branch or its tree's folder; each
      // entry worded by its code in the reader's language, the reason said once. An unknown owner says so first only where an entry
      // carries no code (a shell before them), whose own sentence then follows as recorded (FABRIC-UNKNOWN1).
      const recorder = sessionOf(row.session);
      return {
        ...base,
        title: recorder ? named(row.session) : row.title ?? row.id,
        detail: [
          ...(row.blocked?.unknown && !entries.some((entry) => entry.code) ? [i18n.t('work.milestone.wait.owner')] : []),
          ...entries.map((entry) => i18n.t('work.milestone.cleanup.blocked', { what: entryName(i18n.t, entry), detail: entryDetail(i18n.t, entry) })),
        ].join(' '),
        session: row.session ?? null,
      };
    }
    case 'account-wait':
    case 'signed-out':
      return accountRowWorded(row, names, now);
    default:
      return { ...base, title: row.title ?? row.id };
  }
}

/**
 * A plugin step's attempt held for the person (FABRIC-PERFORM1g, the fabric events design §D6): the step and its attempt, its unit,
 * and why nothing else can say what became of it; a word recorded stays a row, saying so, until the driver's next look applies it.
 */
function heldWorded(row: PushedAttention, attempt: HeldAttempt): Attention {
  return {
    id: row.id,
    kind: 'work',
    since: row.since,
    where: row.where ?? attempt.unit,
    title: i18n.t('work.milestone.held.title', { node: attempt.node, attempt: attempt.attempt }),
    detail: attempt.resolution
      ? i18n.t('work.milestone.held.worded', { node: attempt.node, attempt: attempt.attempt, word: i18n.t(`work.milestone.held.word.${attempt.resolution}`) })
      // FABRIC-CLOSE1q: a held close's outcome is its door's answer, so its row says what the door answered and that no second close is made.
      : attempt.plugin === CLOSE_SENDER
        ? i18n.t('work.milestone.held.close.detail', { why: heldWhy(i18n.t, attempt) })
        : i18n.t('work.milestone.held.detail', { why: heldWhy(i18n.t, attempt), step: attempt.step }),
    held: attempt,
  };
}

/**
 * A closure graph closed only in part (FABRIC-CLOSE1j, the fabric events design §E5): the quest lost and why, and its close node, which
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
