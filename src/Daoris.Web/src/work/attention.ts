import i18n from '../i18n';
import { type Ask, canBeAsked, type GoAhead, type Quest, type Registration, type Session } from '../api';
import { firstLine } from '../asks/AskRow';
import type { RuleProposal } from '../settings/AgentRules';
import { proposalAuthor, proposalChange } from '../settings/proposals';
import { type Consideration, type TrustHold, sittingSentence } from '../signals';
import { answeredPark } from '../ui';
import { workspaceOf } from '../workspaces';
import { accountAttention, type AccountsKnown } from './accountAttention';
import type { SessionGrouping } from './groups';
import { questName, sessionOrigin, sessionTitle } from './identity';
import type { Attention, AttentionKind } from './AttentionRow';
import { type OpinionWait, opinionState } from './opinion';
import { opinionSays } from './OpinionGate';
import { newestSetUp, setUpRef, waitsForLook } from './review';
import { placedFact } from './SessionRow';

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
  unanswerable: 'word',
  review: 'ready',
};

/**
 * The rows by group, only the groups that hold any, in `GROUP_ORDER`, each keeping the order `needsAPerson` gave it.
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
  | 'sign-in' | 'read' | 'let-run' | 'reviewed' | 'not-yet' | 'show-again' | 'opinion-again' | 'opinion-anyway';

/** An act a row offers, and for an account's act which account it is for (UX6d): a row may sign two in. */
export type AttentionOffer = { act: AttentionActId; account?: string };

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
 * Two at most: the rest are on the agent's page, the row's door, and its line says them.
 */
function accountOffers(item: Attention): AttentionOffer[] {
  const facts = item.account;
  if (!facts) return [];
  const signIns: AttentionOffer[] = facts.signsIn
    ? facts.named.filter((one) => one.state === 'out' && one.id).map((one) => ({ act: 'sign-in', account: one.id! }))
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
    // A second opinion that waits on the person (XAGENT1g, §8.5): *Go on anyway…* only where no press of theirs is coming (the
    // work lands by itself, or none could be had and the rule requires one), and a pass asked again; the rest are its session's.
    case 'opinion': return opinionOffers(item);
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

/** A go-ahead's act, as its ask's page names it: its kind in the reader's language where the page has a word, and the session's words. */
function goAheadTitle(goAhead: GoAhead): string {
  const known = `asks.goAhead.kind.${goAhead.kind}`;
  const kind = i18n.exists(known) ? i18n.t(known) : goAhead.kind;
  return i18n.t('work.attention.goAheadTitle', { act: i18n.t('asks.goAhead.act', { kind, on: goAhead.on }), words: goAhead.act });
}

/** Which requirement a held done departed from and why, in its own words; how many where it departed from several. */
function departureWhy(quest: Quest): string | null {
  const departed = (quest.answers ?? []).filter((answer) => !answer.met?.trim() && answer.departed?.trim());
  const [first] = departed;
  if (!first) return null;
  return departed.length === 1
    ? i18n.t('work.attention.departureWhy', { number: first.requirement, reason: first.departed })
    : i18n.t('work.attention.departureWhyMore', { departed: departed.length, number: first.requirement, reason: first.departed });
}

/** The intake states in which an ask is the harness's to answer, not yet the person's (D65 §1b). */
const INTAKE_BUSY: ReadonlySet<Session['state']> = new Set(['queued', 'starting', 'working']);

/**
 * How many of Sessions' own sessions wait on the person: the Sessions icon's badge (UX5 U20).
 *
 * @remarks
 * **A badge counts what its place holds** (D76 as amended, the owner's choice). The icon carried the
 * whole of {@link needsAPerson}, six on the scratch window, while Sessions held one of them: the asks
 * are on Quests and in Overview's band, so the press the badge invited showed a sixth of what it
 * counted. Overview carries the whole now. A parked intake counts here, because it is in the rail,
 * even though the band lets its ask stand for it.
 *
 * **It counts the list's first group, *Waiting on you*** (SESSUX1c, D126 §2.5): this machine's sessions parked to ask,
 * and each quest parked on its failed sessions, whose last session here the list shows *parked*. A teammate's parked
 * session waits on them and is listed under Working (SESSUX1a), and so is one the person answered, which the same
 * session goes on from at the driver's next look (ANSWER1c). The parked quests are the planner's verdicts the tick
 * hands the page (`useConsidered`), as Overview's band reads them, so both counts read the same two facts; the list's
 * reader would walk git in every tree to review on every view, for a number it does not need trees for.
 */
export function waitingInSessions(
  sessions: readonly Session[], considered: readonly Pick<Consideration, 'verdict'>[] = [],
): number {
  const asking = sessions
    .filter((session) => session.state === 'awaiting-person' && !answeredPark(session) && !sessionOrigin(session)).length;
  return asking + considered.filter((consideration) => consideration.verdict === 'Exhausted').length;
}

/**
 * What is waiting on a person: by group, and the longest waiting first within each (UX6c, design §6.2).
 *
 * @remarks
 * **Holding work first, then what waits for the person's word, then what is ready for them** (`ATTENTION_GROUP`).
 * Within a group what has waited longest comes first, whatever its kind: the groups say what matters most, and inside
 * one a parked session is not more urgent than a folder held for trust a week longer. Rows that waited exactly as long
 * keep §6.2's order of kinds.
 *
 * **No row starts a process to find out** (design §6.3): every input is a list the page already holds or the tick hands
 * it. The sessions, quests, asks and registry are the service's; the trust holds and the planner's verdicts the tick's;
 * the rules the machine's file; and the groups (`SESSION_GROUPS`) the answer Sessions' list reads, which the frame holds
 * on every view of a shell. A browser has none of the last four, so it lists what it can know: asks and quests.
 *
 * **An ask waits on the person unless its intake is busy with it.** Proposed with nothing serving it
 * (or its intake ended without publishing), only a person publishes, so it is a `proposal`. An intake
 * that parked asking the person makes it `intake`. The page cannot know whether this machine names
 * an intake harness, so an ask the driver has not picked up yet is a proposal until it is. It is the
 * person's to take either way, and the band never guesses what a driver will do next.
 *
 * **One thing is counted once.** A parked intake is a session awaiting a person too, and its ask's
 * row stands for it, because the answer is on the ask (publish or close). It leaves the parked list
 * only while its ask is in hand and live, so a list that did not arrive never hides it.
 *
 * **A quest is unanswerable when this deployment has no registration for its receiver.** The
 * publish door already refuses a quest addressed to a non-adopter, so the way one comes to exist is
 * afterwards: the receiver retired, or the registration never arrived here. Either way no agent
 * will pull it, and "who cannot be asked" is the same question as "who can" (the Projects view
 * already argues this for repositories).
 *
 * **A quest parked on its failed sessions here holds work** (SESSUX1i, D126 §4.6): only the person's *Try again* starts
 * it again, and on 1 October the owner's work stood there while nothing here said so. It is read from the planner's
 * verdicts the tick hands the page (`Exhausted`), so a browser has none: its time is when its last session ended, and its
 * detail the driver's sitting sentence. A person's stop holds its quest too, and is never here: the person caused it.
 *
 * **A folder waiting on the person's trust (D73) holds work.** The driver is holding a start there because the agent
 * ignores that folder's own permissions until trusted, and the grant is the person's alone. One row per folder, since the
 * oldest thing it holds. Only the shell's tick says so, so a browser has none.
 *
 * **A go-ahead a session asked holds work** (KNOWUSE1a): the session's act waits for the person's yes or no, which every
 * session on the ask is handed. One row per go-ahead still asked, read from its ask's `goAheads`.
 *
 * **An agent's proposal to widen the rules (PERM2, D74) waits for the person's word.** 🔴 A widening never applies
 * without the person, so the driver holds it as `waiting` and only they can settle it. The session that proposed it
 * carries on meanwhile, so it holds nothing up. A narrowing applied itself, and one the driver has not judged yet may be
 * one, so neither is here. Only the shell reads the rules, so a browser has none.
 *
 * **A departure waits for the person's word** (DRIFT1d2, D133 §4): a done held for their yes because it departed from
 * what they required, which the quests list carries (`held`) until they accept it.
 *
 * **A set-up shown waits for the person's look** (REVIEWENV1g, D154 point 8): a set-up step whose newest set-up has no verdict,
 * from the quests list's own record, with *Reviewed*, *Not yet…* and *Show it again* on its row, the set-up named as the row
 * drew it. A step held for its review is never a departure's row, since no yes lifts a review's hold.
 *
 * **A second opinion that waits on the person waits for their word** (XAGENT1g, the second-agent design §9): a dispute, a
 * required one none could be had for, and commits nobody read where the work lands by itself, oldest first, from the driver's
 * own list (`OPINION_WAITS`), which a browser has none of. Its row says what the gate says, and its door is its session's page.
 *
 * **Work to review is ready for the person** (D126): each session Sessions' list places *To review*, named from its
 * record where the page holds it and by its id where it does not, so nothing waiting is dropped.
 *
 * **The accounts hold work** (UX6d, TOOL4m's row): a start waiting on its accounts, an ask's intake among them, and a
 * signed-out account a list or a default holds that no waiting start names (`accountAttention`). From the tick's waits and
 * considerations, the roster's last readings with their times and the accounts' files: nothing a row would have to ask
 * for. A browser has none of them. **A signed-out account's row has no wait** (UXFIX3): only when it was read is known, and
 * sorted by that, reading it again sent an old blocker to the end of its group. It holds no start that waits now (a waiting
 * start would stand for it), so it comes after every row with a wait, in the roster's order.
 */
export function needsAPerson(
  sessions: readonly Session[],
  quests: readonly Quest[],
  registry: readonly Registration[],
  asks: readonly Ask[],
  untrusted: readonly TrustHold[] = [],
  proposals: readonly RuleProposal[] = [],
  considered: readonly Consideration[] = [],
  groups: readonly SessionGrouping[] = [],
  accounts: AccountsKnown = { tools: [] },
  opinions: readonly OpinionWait[] = [],
): Attention[] {
  const live = asks.filter((ask) => ask.state === 'Open' || ask.state === 'Proposed');
  const intakeOf = (ask: Ask) =>
    ask.intake ? sessions.find((session) => session.id === ask.intake) : undefined;
  // Whom an ask in this workspace could be published to: what the host says can be asked, as the ask's page offers (D70).
  const receiversIn = (workspace: string) => registry
    .filter((row) => canBeAsked(row) && workspaceOf(row) === workspace)
    .map((row) => row.repository)
    .sort();

  const waitingAsks = live
    .filter((ask) => !INTAKE_BUSY.has(intakeOf(ask)?.state ?? 'completed'))
    .map((ask): Attention => {
      const intake = intakeOf(ask);
      const asked = intake?.state === 'awaiting-person';
      const row = { id: ask.id, title: firstLine(ask.sentence), where: ask.workspace };
      // The intake's own note, which the row words in the reader's language (LANG1b).
      if (asked) return { ...row, kind: 'intake', since: intake.updated, note: { note: intake.note, parts: intake.noteParts } };
      return {
        ...row,
        kind: 'proposal',
        since: ask.asked,
        // A refused receiver's sentence is the service's, verbatim; otherwise what was proposed.
        detail: ask.note ?? (ask.proposal.length > 0
          ? i18n.t('work.attention.proposalWhy', {
            repositories: ask.proposal.map((match) => match.repository).join(', '),
          })
          : i18n.t('work.attention.proposalNobody')),
        publishTo: ask.proposal.map((match) => match.repository),
        choices: receiversIn(ask.workspace),
      };
    });

  // Every go-ahead still asked on an ask that is not closed: one row each, since each is its own act (KNOWUSE1a).
  const goAheads = asks
    .filter((ask) => ask.state !== 'Closed')
    .flatMap((ask) => (ask.goAheads ?? [])
      .filter((goAhead) => goAhead.state === 'asked')
      .map((goAhead): Attention => ({
        id: `${ask.id}#${goAhead.number}`,
        kind: 'go-ahead',
        ask: ask.id,
        number: goAhead.number,
        title: goAheadTitle(goAhead),
        where: i18n.t('work.attention.askWhere', { id: ask.id }),
        since: goAhead.asked[0]?.at ?? ask.updated,
        // Why the first session needed it, in its words.
        detail: goAhead.asked[0]?.why ?? null,
        // The session that asked it first, whose run its door opens (WORKFLOW1c).
        session: goAhead.asked[0]?.session ?? null,
      })));

  // A done its departure holds for the person's yes (DRIFT1d2), until they accept it. A set-up step its review holds waits
  // for their review instead (REVIEWENV1g), which no yes lifts, so it is the row below and never this one.
  const departures = quests
    .filter((quest) => quest.held === true && !quest.accepted && quest.hold !== 'unreviewed')
    .map((quest): Attention => ({
      id: quest.id,
      kind: 'departure',
      title: questName(quest),
      where: quest.to,
      since: quest.updated,
      detail: departureWhy(quest),
    }));

  // A set-up step whose newest set-up waits for the person's look (REVIEWENV1g, design §3.3), oldest first with the rest: what
  // it showed, and the set-up the row drew, which its verdict names.
  const setUps = quests
    .filter(waitsForLook)
    .map((quest): Attention => {
      const newest = newestSetUp(quest)!;
      return {
        id: quest.id,
        kind: 'set-up',
        title: questName(quest),
        where: quest.to,
        since: newest.at ?? quest.updated,
        detail: newest.shows
          ? i18n.t('work.attention.setUpShows', { environment: quest.setUpIn, shows: newest.shows })
          : i18n.t('work.attention.setUpShown', { environment: quest.setUpIn }),
        setUp: setUpRef(newest)!,
        local: newest.local !== false,
        session: newest.session ?? null,
      };
    });

  // A second opinion that waits on the person (XAGENT1g, design §9), from the driver's own list: a dispute, a required one none
  // could be had for, and commits nobody read where the work lands by itself. Its row says what the gate says.
  const secondOpinions = opinions.map((wait): Attention => {
    const quest = quests.find((one) => one.id === wait.quest);
    const record = sessions.find((one) => one.id === wait.session);
    return {
      id: wait.session,
      kind: 'opinion',
      title: quest ? questName(quest) : record ? sessionTitle(record) : `#${wait.quest}`,
      where: wait.repository,
      since: wait.since,
      detail: opinionSays(i18n.t, wait.opinion).join(' '),
      opinion: wait,
    };
  });

  // What Sessions' list places To review (D126), from the one reader the frame holds: never a reader of its own.
  const reviews = groups
    .filter((placed) => placed.group === 'review')
    .map((placed): Attention => {
      const record = sessions.find((one) => one.id === placed.session);
      const fact = placedFact(placed, placed.shown);
      return {
        id: placed.session,
        kind: 'review',
        title: record
          ? sessionTitle(record, quests.find((quest) => quest.id === record.quest))
          : `#${placed.session}`,
        where: record?.repository ?? '',
        since: record?.updated ?? new Date().toISOString(),
        detail: fact ? i18n.t(fact.line, fact.values) : null,
      };
    });

  const standsFor = new Set(live.map((ask) => ask.intake).filter(Boolean));
  // A park the person answered goes on at the driver's next look (ANSWER1c): it no longer needs them.
  const parked = sessions
    .filter((session) => session.state === 'awaiting-person' && !answeredPark(session) && !standsFor.has(session.id))
    .map((session): Attention => ({
      id: session.id,
      kind: 'parked',
      title: sessionTitle(session, quests.find((quest) => quest.id === session.quest)),
      where: session.repository,
      since: session.updated,
      // Its note whole, parts beside the English, which the row words in the reader's language (LANG1b).
      note: { note: session.note, parts: session.noteParts },
    }));

  const parkedQuests = considered
    .filter((consideration) => consideration.verdict === 'Exhausted')
    .map((consideration): Attention => {
      const held = quests.find((one) => one.id === consideration.quest);
      return {
        id: consideration.quest,
        kind: 'parked-quest',
        title: held?.title ?? `#${consideration.quest}`,
        where: consideration.repository,
        // When its last session ended, which the tick names; a shell older than that names none, and the quest's last
        // move is the nearest the page holds.
        since: consideration.since ?? held?.updated ?? new Date().toISOString(),
        detail: sittingSentence(consideration),
      };
    });

  const registered = new Set(registry.map((row) => row.repository.toLowerCase()));
  const unanswerable = quests
    .filter((quest) => quest.status === 'Open' && !registered.has(quest.to.toLowerCase()))
    .map((quest): Attention => ({
      id: quest.id,
      kind: 'unanswerable',
      title: questName(quest),
      where: quest.to,
      since: quest.filed,
      detail: i18n.t('work.attention.unanswerableWhy', { repository: quest.to }),
    }));

  // One row per folder in one file: two quests held on one untrusted tree are one grant.
  const folders = new Map<string, Attention & { since: string }>();
  for (const hold of untrusted) {
    const held = hold.quest ? quests.find((quest) => quest.id === hold.quest) : undefined;
    const asked = hold.ask ? asks.find((one) => one.id === hold.ask) : undefined;
    const where = held?.to
      ?? (hold.ask ? i18n.t('work.attention.askWhere', { id: hold.ask }) : `#${hold.quest ?? ''}`);
    const since = held?.filed ?? asked?.asked ?? new Date().toISOString();
    const key = `${hold.trustFile}\n${hold.folder}`;
    const seen = folders.get(key);
    if (seen) {
      if (since.localeCompare(seen.since) < 0) seen.since = since;
      continue;
    }
    folders.set(key, {
      // The row's identity is what makes it one row: the file AND the folder (REV3). The folder alone
      // gave one folder held in two accounts' files two rows with one key.
      id: key,
      kind: 'trust',
      title: hold.folder,
      where,
      since,
      detail: i18n.t('work.attention.trustWhy'),
      trust: { folder: hold.folder, trustFile: hold.trustFile },
      // What its door opens: the quest it holds, or the ask whose intake it holds (D73).
      ...(hold.quest ? { quest: hold.quest } : hold.ask ? { ask: hold.ask } : {}),
    });
  }

  const widenings = proposals
    .filter((proposal) => proposal.state === 'waiting')
    .map((proposal): Attention => ({
      id: proposal.id,
      kind: 'rule',
      title: proposalChange(proposal),
      where: proposalAuthor(proposal),
      since: proposal.proposed,
      // The session's own reason, verbatim.
      detail: proposal.why,
    }));

  // The starts waiting on their accounts, and the signed-out accounts no waiting start names (UX6d).
  const accountRows = accountAttention(accounts, considered, quests, asks, registry);

  // Each group's kinds in §6.2's order, then the longest waiting first: a stable sort keeps §6.2's order for a tie. A row
  // with no known wait (a signed-out account's, UXFIX3) comes after those that have one, in the order it was listed, so a
  // reading moves nothing.
  const oldestFirst = (a: Attention, b: Attention) =>
    (a.since === null || b.since === null ? Number(a.since === null) - Number(b.since === null) : a.since.localeCompare(b.since));
  return [
    [...accountRows, ...parked, ...parkedQuests, ...goAheads, ...folders.values()],
    [...waitingAsks, ...departures, ...setUps, ...secondOpinions, ...widenings, ...unanswerable],
    reviews,
  ].flatMap((group) => group.sort(oldestFirst));
}
