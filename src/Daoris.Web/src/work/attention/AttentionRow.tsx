import { type ReactNode, useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { SetUpRef } from '../../data/api';
import { clockOf, elapsed } from '../../locales/format';
import { cn } from '../../lib/cn';
import type { TrustHold } from '../../data/signals';
import { Button, Dot, Icon, Inline } from '../../design/ui';
import type { AccountRowFacts } from './accountWords';
import type { RuleProposal } from '../../settings/repository/AgentRules';
import type { GraphEnd, HeldAttempt, HeldResolution, UnreadableRecord } from '../head/milestone';
import {
  ASKS_ONCE, type AttentionActId, type AttentionOffer, attentionOffers, type DeclinedRequest, type HumanTaskWait, type GitAskWait, type GitHeld, type IntegrationWait, type MemoryWait,
  type QuietWait,
  type StaleSince,
} from './attention';
import { Note } from '../Note';
import type { Answered } from '../InlineConfirm';
import type { TaskAct } from '../../workflow/task/TaskPress';
import { taskActOf } from './attentionOffers';
import { hasNote, type NoteRecord } from '../noteLines';
import type { OpinionWait } from '../gates/opinion';
import type { PermissionWait } from '../waits/PermissionAsks';
import type { AnswerWait } from '../waits/AnswerAlone';
import { AskOnce } from './AskOnce';
import { AT_AN_AGENT, DOOR_ONLY, HANDLER, IN_A_CIRCLE, INTEGRATION_LABEL, opensNothing } from './rowRules';

/** The kinds of thing *What needs you* lists, each in one of its three groups (`ATTENTION_GROUP`). */
export type AttentionKind =
  | 'parked' | 'parked-quest' | 'steward-stopped' | 'policy-hold' | 'unclosed' | 'go-ahead' | 'trust' | 'account-wait' | 'signed-out'
  | 'proposal' | 'intake' | 'departure' | 'set-up' | 'rule' | 'unanswerable'
  | 'review' | 'opinion' | 'helper' | 'hand-over' | 'work' | 'permission' | 'answer' | 'confirmation' | 'space' | 'git-held' | 'git-ask'
  | 'quiet' | 'helper-ended' | 'request-declined' | 'memory' | 'integration' | 'task' | 'second-check';

/** A second check's flag not yet seen, as a `second-check` row carries it: the reader's claim about one of Ask Daoris's acts. */
export type SecondCheckFlag = {
  session: string; range: string; index: number; says: string; certainty: string; call: string; at: string;
  reader: string | null; independence: string | null;
};

/**
 * One thing that is waiting on a person, as Overview's band shows it.
 *
 * @remarks
 * **Every kind is a different question.** A `parked` session is stopped at a checkpoint
 * only a person can clear. An ask is a `proposal` while nothing serves it: the declarations
 * proposed, and only a person publishes. An ask is `intake` when its intake session parked
 * asking the person. That session is counted as the ask, never twice. An `unanswerable`
 * quest is addressed to a repository this deployment has no registration for. It will sit forever,
 * and nothing else says so. A `trust` row is a folder the agent has not been trusted in, where the
 * driver is holding a start: only the person can give that grant. A `rule` row is an agent's
 * proposal to widen what agents may do, which never applies without the person. A
 * `parked-quest` row is a quest parked on its failed sessions here, which only the
 * person's *Try again* starts again.
 *
 * **`steward-stopped`**: a quest the steward stopped, waiting on a *Try again*, the steward's or the
 * person's; its one act is the parked quest's *Try again*, and its row names the session stopped and the quest's repository.
 *
 * **Three more kinds**: a `go-ahead` a session asked on an ask, which holds that session's
 * act; a `departure`, a done held for the person's yes because it departed from what they required; and
 * `review`, finished work Sessions' list places *To review*.
 *
 * **The accounts**: an `account-wait`, a start held because every account it may
 * use is cooling or signed out, naming each account that would serve it as last known; and `signed-out`, an account a list
 * or a default holds that read signed out, which no waiting start names.
 *
 * **`confirmation`**: a press a terminal sent that waits for the person's Confirm on the window's card, which
 * its terminal holds still for. Its answer needs the card's values read first, so its door to the card is its one control.
 *
 * **`hand-over`**: an agent's ask to hand its session over that waits on the person's yes or no, which holds work once
 * its session's turn has ended and the session stands still on it.
 *
 * **`space`**: a volume still below its free-space floor once Daoris freed what it can make again. What else goes
 * is the person's to choose, so its door, *Free up space…*, is its one control: it opens the *Disk space* card.
 *
 * **`quiet`**: a running session the watcher brings to the person, quiet
 * with nothing running, or busy with nothing heard. Daoris ended nothing: its presses are the person's, *End and carry on* (a quiet
 * quest session's alone), *Hand over…* (a quest session's), *Stop…* and *Leave it running*.
 *
 * **`request-declined`**: a run held at a quest its sessions published that the receiver declined, whose
 * `work.requests` node waits for the person's *Pass over…* (the run's existing skip), with their words. Its `id` is the run.
 *
 * **`git-held`**: a git remote held because it could not authenticate, said once.
 * Nothing connects for it until a change lifts it or the person's *Try again*, its one act, on the row itself.
 */
export type Attention = {
  /**
   * The session, ask or quest id — what the door opens; for a `trust` row, the folder; for a `rule` row, the proposal; for
   * a `go-ahead`, its ask and its number, since one ask holds several.
   */
  id: string;
  kind: AttentionKind;
  /** A `trust` row's hold: the folder and the agent's own file — exactly what a grant writes. */
  trust?: TrustHold;
  /** A `request-declined` row's node and the quest declined, to where it went and the run's newest work quest, which its door reads the run by. */
  declined?: DeclinedRequest;
  /** A `task` row's run, its `human` node and the presses it waits for. */
  task?: HumanTaskWait;
  /** The ask a `go-ahead` is on, or a `trust` row's held intake is for: what the door opens. */
  ask?: string;
  /** The quest a `trust` row holds: what the door opens. */
  quest?: string;
  /** A `go-ahead`'s number on its ask, which the answer names. */
  number?: number;
  /**
   * A `go-ahead` nothing on its ask could still use: named so by its kind's word, its refusal the one answer offered,
   * which runs nothing.
   */
  stale?: StaleSince;
  /** A `set-up` row's set-up, named whole as the row drew it: what its verdict answers. */
  setUp?: SetUpRef;
  /** A `set-up` row's set-up is local, so Daoris serves it and *Show it again* can. */
  local?: boolean;
  /**
   * The session that said a `set-up` row's set-up, which a *not yet*'s words go to; null for the person's own. For a `helper`
   * row, the session that asked, whose ask its yes and no answer and whose page its door opens; for a `hand-over` row,
   * the session whose agent asked to hand it over.
   */
  session?: string | null;
  /** A `review` row's run whose Accept waits, which its door opens the review of; null where the row names none. */
  run?: string | null;
  /** A `hand-over` row's agent, as the roster names it: whom its yes hands the session over to. */
  agent?: string;
  /** A `hand-over` row's session stands still on it, its turn ended: the row holds work. */
  stands?: boolean;
  /**
   * A `work` row whose owner nothing records, whose `daoris/<folder>` branch names a session's tree the page holds:
   * that session, whose page its door opens, where the branch's Discard branch press is too.
   */
  cutFor?: string;
  /** An `opinion` row's work and its gate: what its presses act on, and whether a press of the person's is coming. */
  opinion?: OpinionWait;
  /** A `work` row's plugin step's attempt held for the person: what their word is on. */
  held?: HeldAttempt;
  /** A `work` row's node record that does not read: what its press opens the folder of. */
  unreadable?: UnreadableRecord;
  /** A `work` row's closure graph closed only in part: the stop of its close node is what their word is on. */
  ended?: GraphEnd;
  /**
   * A `permission` row's tool call held for the person's answer: what its presses answer, by the choices its agent
   * offered. Its `session` is the session that holds it, whose page its door opens.
   */
  permission?: PermissionWait;
  /**
   * An `answer` row's third answer alone held for the person: what its two presses answer. Its `session` is the
   * session whose line it would take on, whose page its door opens.
   */
  answer?: AnswerWait;
  /** A `rule` row's proposal, whole: what its *Allow* answers and the session it tells. */
  proposal?: RuleProposal;
  /** A `proposal`'s receivers, as its declarations named them: what *Publish to …* publishes to, in one press. */
  publishTo?: string[];
  /** A `proposal`'s other receivers: every repository its workspace can ask, which *Choose…* offers. */
  choices?: string[];
  /** What it is, derived: a session's identity, an ask's first line, or a quest's title. */
  title: string;
  /**
   * Where it waits: the repository, or for an ask its circle, because an ask has no repository yet.
   * For a `rule` row, who proposed it: the rules are the machine's, so the proposer is the place. For an account's row,
   * the agent; for a start waiting on accounts, its repository, or its workspace where it holds more (`circle`).
   */
  where: string;
  /** `where` is a workspace, said as one: a start waiting on accounts that holds an intake, or quests in several repositories. */
  circle?: boolean;
  /** An account row's agent, the accounts it names as last known, and the one it may let in. */
  account?: AccountRowFacts;
  /**
   * When it started waiting — a park's last move, a parked quest's last session's end, an ask's asking, a quest's filing, a
   * go-ahead's first asking, a held done's close, a reviewed session's end. Null where nothing the page holds says when
   * a signed-out account's row knows only its read time, which is not a wait, so it says `read`.
   */
  since: string | null;
  /** When a signed-out account's row read it: said as *read …* where a row with a wait says how long. */
  read?: string | null;
  /** The sentence explaining what the person is being asked to settle. */
  detail?: string | null;
  /** A `quiet` row's watch: its minutes, whether it is busy (offered no end), an intake, and whether the person's words wait. */
  quiet?: QuietWait;
  /** A `memory` row's counts: ends and replacements to approve, staged rows to keep or drop. */
  memory?: MemoryWait;
  /** An `integration` row's wait: which, its repository and line, and what it is of; its door opens the repository's Integration tab. */
  integration?: IntegrationWait;
  /** A `git-held` row's held group: the repositories its *Try again* lifts, and why, which its mark's ink says. */
  git?: GitHeld;
  /** A `git-ask` row's go-ahead: the act it holds, which its *Allow* and *Refuse* answer. */
  gitAsk?: GitAskWait;
  /** A `second-check` row's flag, which its *Seen* acknowledges. */
  secondCheck?: SecondCheckFlag;
  /** The workspace a `git-held` row is judged in, which its *Try again* names; absent where .NET scoped it to none. */
  workspace?: string;
  /**
   * A parked session's or a parked intake's note, its English and its parts: worded by the row in the reader's
   * language, the agent's question as written, in `detail`'s place.
   */
  note?: NoteRecord;
};

/**
 * What the band hands a row to settle it where it stands, each reporting the row the person pressed
 * on. A row offers an act only where `attentionActs` names it for its kind AND it is handed here: a browser, which has no
 * driver and no rules file, is handed none of those, so it offers none.
 */
export type AttentionActs = {
  /** An ask published to these receivers: its declarations' in one press, or the one chosen under the row. */
  publish?: (item: Attention, to: readonly string[], answered?: Answered) => void;
  /** A quest parked on its failed sessions, started again: one press, which starts a session. */
  retry?: (item: Attention) => void;
  /** A go-ahead's yes or no, with the person's words where they gave any. */
  answer?: (item: Attention, approved: boolean, words: string | undefined, answered: Answered) => void;
  /** A folder trusted for the agent, after its own question. */
  trust?: (item: Attention, answered: Answered) => void;
  /** A held done's departure accepted: what it held goes on. */
  acceptDeparture?: (item: Attention) => void;
  /** An agent's widening of the rules accepted, after saying what it widens. */
  acceptRule?: (item: Attention, answered: Answered) => void;
  /** An agent's widening of the rules declined: the rules stay unchanged. */
  declineRule?: (item: Attention) => void;
  /** An account's sign-in started: the agent's own, through its door, as the agent's page starts it. */
  signIn?: (item: Attention, account: string) => void;
  /** *Sign in for Daoris*: a new account signed in, the add flow's `login-new` in place, with no step 3. */
  signInNew?: (item: Attention) => void;
  /** One account read on the press: the one reading a person may ask for, never a look. */
  read?: (item: Attention, account: string) => void;
  /** A set-up the person looked at said reviewed: one press, naming the set-up the row drew. */
  reviewed?: (item: Attention) => void;
  /** A set-up said not yet, with the person's words, which go to its session as its next turn. */
  notYet?: (item: Attention, words: string, answered: Answered) => void;
  /** A set-up's build served to its tab again, the tab brought forward. */
  showAgain?: (item: Attention) => void;
  /** A second opinion asked again, or tried again where none could be had: a pass the driver's next look starts. */
  opinionAgain?: (item: Attention) => void;
  /** *Go on anyway…* at a second opinion's gate, with the person's words where they gave any. */
  opinionAnyway?: (item: Attention, words: string | undefined, answered: Answered) => void;
  /** An agent's ask for a helper said yes or no to: its session's page's own presses. */
  helper?: (item: Attention, yes: boolean) => void;
  /** An agent's ask to hand its session over said yes or no to: its session's foot's own presses. */
  handOver?: (item: Attention, yes: boolean) => void;
  /** The person's word on a plugin step's attempt held for them: `daoris work held`'s. */
  held?: (item: Attention, resolution: HeldResolution) => void;
  /** The node records' folder opened, where the person mends one that does not read. */
  openRecords?: (item: Attention) => void;
  /** A tool call held for the person answered with the option they pressed, by its agent's id. */
  permission?: (item: Attention, option: string) => void;
  /** A third answer alone held for the person let go on (`goOn`) or kept: its session's page's own presses. */
  answerAlone?: (item: Attention, goOn: boolean) => void;
  /** A held git remote's *Try again*: its repositories' holds lifted, so the next act connects once. */
  gitTryAgain?: (item: Attention) => void;
  /**
   * A held remote's door to Settings → Git on its host: an identity set up or the person's own sign-ins kept, the
   * refused identity's key copied or signed in again, a host's key compared and trusted.
   */
  gitSetUp?: (item: Attention) => void;
  /** A held remote's door to its first repository's git remote row, where another identity or the person's own sign-ins are chosen. */
  gitChoose?: (item: Attention) => void;
  /** The host's own page for SSH keys, opened. */
  gitKeyPage?: (item: Attention) => void;
  /** The identity a push is refused with marked read only on its host, so Daoris never tries a write with it. */
  gitReadOnly?: (item: Attention) => void;
  /** A git go-ahead allowed (`allow`) or refused, as its session's head answers it. */
  gitAnswer?: (item: Attention, allow: boolean) => void;
  /** A second check's flag acknowledged: *Seen*, never accepted, resolved or answered. */
  secondCheckSeen?: (item: Attention) => void;
  /**
   * A quiet session's presses: *End and carry on*, *Hand over…* (its page's hand-over asked), *Stop…* and *Leave it
   * running*, each told how it ended where it asked once.
   */
  quiet?: (item: Attention, act: 'carry-on' | 'hand-over' | 'stop' | 'leave', answered?: Answered) => void;
  /** A run's `work.requests` node passed over, with the person's words, as the run's view passes it. */
  passOver?: (item: Attention, words: string, answered: Answered, act?: 'ask-again' | 'go-on' | TaskAct) => void;
  /** An end the sweep wrote, which names no session, approved or declined by its operation, as a session's Memory section decides one. */
  memoryEnd?: (item: Attention, approve: boolean) => void;
};

/**
 * A row in *what needs you*: what waits, why, the acts that settle it where one press is safe, and its door.
 *
 * @remarks
 * **A row reads `kind · what · where · how long`, then why** (*status leads*), two lines of why at
 * most, whole in its record. Its kind wears open's hue: it waits on the person. Below 48rem of main area the title takes
 * its own line under the rest.
 *
 * **An act is on the row only where one press is safe and the row says what it does.** *Publish to …* names whom
 * it publishes to, *Try again* says it starts a session, *Accept the departure* is the quest page's press. What widens
 * what Daoris may do (a folder's trust, a widening of the rules), a go-ahead's answer, which every session on its ask is
 * handed, and a choice of receiver ask once under the row, the move then *Never mind*. Which acts a kind offers is
 * `attentionActs`, the one rule.
 *
 * **A door opens something, or it is not a door.** Handed no `onOpen`, the row has nowhere to go,
 * as a parked session in a browser has no Sessions, and it shows no door: it still says what is waiting, because knowing
 * is the half that travels. A row whose answer needs reading has its door as its one control (*Answer…*, *Review*); every
 * other row's door is *Open* at its end, beside its acts, and an account's row's is its agent's name.
 *
 * **An account's row settles what one press can**: *Sign in* to each account read signed out, and *Read* an
 * account no read answered; no account is outside a start's list, since every account of Daoris's serves every workspace
 * Its sign-in's steps are the band's to hand (`below`), since they follow a process. A signed-out account's row says
 * its read time where the others say how long (*read 10:42*), since a reading is not a wait.
 *
 * A molecule: it is handed the item, the acts and the door, and reports each press.
 */
export function AttentionRow({ item, onOpen: handed, onRun, acts = {}, busy = false, opened = null, below = null }: {
  item: Attention;
  onOpen?: (item: Attention) => void;
  /**
   * Its door into the run whose step waits on the person: the session attended, the side
   * bar on its *Workflow* at the step the row waits at. Absent for a row that is no step of a run, and in a browser.
   */
  onRun?: () => void;
  acts?: AttentionActs;
  /** One of its acts is on its way: none is offered again until it answers. */
  busy?: boolean;
  /** The act whose question stands open to start with: a story's, since a page opens it by a press. */
  opened?: AttentionActId | null;
  /** What the band shows under the row while it follows one of its acts: an account's sign-in, step by step. */
  below?: ReactNode;
}) {
  const { t } = useTranslation();
  const titleId = useId();
  // The act asking under the row, its words and its choice: one question at a time.
  const [asking, setAsking] = useState<AttentionActId | null>(opened);
  const [words, setWords] = useState('');
  const [choice, setChoice] = useState('');
  const onOpen = opensNothing(item) ? undefined : handed;
  const offered = attentionOffers(item).filter((offer) => acts[HANDLER[offer.act]] !== undefined);
  const doorOnly = DOOR_ONLY[item.kind];
  const where = IN_A_CIRCLE.has(item.kind) || item.circle ? t('work.attention.circle', { circle: item.where }) : item.where;
  // An account's name as the row says it: the person's, from what the row names.
  const called = (account?: string) => (item.account?.named ?? []).find((one) => one.id === account)?.label ?? account ?? '';
  // How long it has waited; where no wait is known, its account's read time, which is not a wait.
  const when = item.since
    ? t('work.attention.since', { span: elapsed(item.since) })
    : item.read ? t('agents.read.at', { when: clockOf(item.read) }) : null;

  const done = () => { setAsking(null); setWords(''); setChoice(''); };
  const press = ({ act, account, option }: AttentionOffer) => {
    if (ASKS_ONCE.has(act)) { setAsking(act); return; }
    if (HANDLER[act] === 'permission') { if (option) acts.permission?.(item, option); return; }
    if (act === 'publish') acts.publish?.(item, item.publishTo ?? []);
    else if (act === 'retry') acts.retry?.(item);
    else if (act === 'accept-departure') acts.acceptDeparture?.(item);
    else if (act === 'decline-rule') acts.declineRule?.(item);
    else if (act === 'sign-in' && account) acts.signIn?.(item, account);
    else if (act === 'sign-in-new') acts.signInNew?.(item);
    else if (act === 'read' && account) acts.read?.(item, account);
    else if (act === 'reviewed') acts.reviewed?.(item);
    else if (act === 'show-again') acts.showAgain?.(item);
    else if (act === 'opinion-again') acts.opinionAgain?.(item);
    else if (act === 'let-ask' || act === 'not-now') acts.helper?.(item, act === 'let-ask');
    else if (act === 'hand-over' || act === 'keep-agent') acts.handOver?.(item, act === 'hand-over');
    else if (act === 'go-on-answer' || act === 'keep-answer') acts.answerAlone?.(item, act === 'go-on-answer');
    else if (act === 'happened' || act === 'did-not') acts.held?.(item, act);
    else if (act === 'stop-step' || act === 'stop-close' || act === 'stop-partial') acts.held?.(item, 'stop');
    else if (act === 'record-close') acts.held?.(item, 'record');
    else if (act === 'resend-close') acts.held?.(item, 'resend');
    else if (act === 'open-records') acts.openRecords?.(item);
    else if (act === 'again-step' || act === 'again-check') acts.held?.(item, 'again');
    else if (act === 'stop-check') acts.held?.(item, 'stop');
    else if (act === 'ask-fix-check') acts.held?.(item, 'ask-fix');
    else if (act === 'git-try-again') acts.gitTryAgain?.(item);
    else if (act === 'git-set-up' || act === 'git-keep-own' || act === 'git-copy-key' || act === 'git-sign-in' || act === 'git-open'
      || act === 'git-compare') acts.gitSetUp?.(item);
    else if (act === 'git-choose' || act === 'git-use-own') acts.gitChoose?.(item);
    else if (act === 'git-key-page') acts.gitKeyPage?.(item);
    else if (act === 'git-read-only') acts.gitReadOnly?.(item);
    else if (act === 'git-allow' || act === 'git-refuse') acts.gitAnswer?.(item, act === 'git-allow');
    else if (act === 'second-check-seen') acts.secondCheckSeen?.(item);
    else if (act === 'quiet-hand-over') acts.quiet?.(item, 'hand-over');
    else if (act === 'quiet-leave') acts.quiet?.(item, 'leave');
    else if (act === 'approve-end' || act === 'decline-end') acts.memoryEnd?.(item, act === 'approve-end');
  };
  const label = (act: AttentionActId, account?: string): string => {
    switch (act) {
      case 'publish': return t('work.attention.act.publish', { repositories: (item.publishTo ?? []).join(', ') });
      case 'choose': return t((item.publishTo?.length ?? 0) > 0 ? 'work.attention.act.choose' : 'work.attention.act.chooseWhere');
      case 'retry': return t('work.attention.act.retry');
      case 'approve': return t('work.attention.act.approve');
      case 'refuse': return t('work.attention.act.refuse');
      case 'trust': return t('work.attention.act.trust');
      case 'accept-departure': return t('work.attention.act.acceptDeparture');
      case 'accept-rule': return t('work.attention.act.acceptRule');
      case 'decline-rule': return t('work.attention.act.declineRule');
      // A signed-out account's own row names it in its title, so its press is the agent page's own word.
      case 'sign-in': return item.kind === 'signed-out'
        ? t('harness.login.action')
        : t('agents.act.signInTo', { account: called(account) });
      // A static label that names no account: whoever signs in names it.
      case 'sign-in-new': return t('harness.login.forDaoris');
      case 'read': return t('agents.act.readFor', { account: called(account) });
      // The review's own words, as the step's page and the strip's chip say them.
      case 'reviewed': return t('review.act.reviewed');
      case 'not-yet': return t('review.act.notYet');
      case 'show-again': return t('review.act.showAgain');
      // The second opinion's own words, as its gate says them.
      case 'opinion-again': return t(item.opinion?.opinion.state === 'unavailable' ? 'opinion.act.tryAgain' : 'opinion.act.askAgain');
      case 'opinion-anyway': return t('opinion.act.anyway');
      case 'let-ask': return t('work.helper.approve');
      case 'not-now': return t('work.helper.decline');
      // The session's foot's own words for its agent's ask to hand it over.
      case 'hand-over': return t('work.handOverAsk.approve', { agent: item.agent ?? '' });
      case 'keep-agent': return t('work.handOverAsk.decline');
      // The session's card's own words, one per choice its agent offered.
      case 'allow-once': return t('work.attention.act.allowOnce');
      case 'refuse-once': return t('work.attention.act.refuseOnce');
      case 'refuse-always': return t('work.attention.act.refuseAlways');
      // The session's page's own words for a third answer alone.
      case 'go-on-answer': return t('work.wait.goOnAnswer');
      case 'keep-answer': return t('work.wait.answer.notNow');
      // Each says what it does, as `daoris work held` names it.
      case 'happened': return t('work.milestone.held.act.happened');
      case 'did-not': return t('work.milestone.held.act.did-not');
      case 'stop-step': return t('work.milestone.held.act.stop');
      // A held close's own, as `daoris work held` names them: record, resend, stop.
      case 'record-close': return t('work.milestone.held.act.record');
      case 'resend-close': return t('work.milestone.held.act.resend');
      case 'stop-close': return t('work.milestone.held.act.stopClose');
      // Where the record is mended.
      case 'open-records': return t('work.milestone.unreadable.act.open');
      // A failure kept for the person, as `daoris work held … again` names it.
      case 'again-step': return t('work.milestone.held.act.again');
      // A held verification: *Run again* and *Not now*, as its run's view words them.
      case 'again-check': return t('workflow.run.check.again');
      case 'stop-check': return t('workflow.run.check.stop');
      case 'ask-fix-check': return t('work.verify.askFix');
      // A close found closed only in part, its node waiting again: its stop alone.
      case 'stop-partial': return t('work.milestone.closed.act.stop');
      // A held git remote's one act, *Try again*, the word a quest's retry is.
      case 'git-try-again': return t('work.attention.act.retry');
      // The two presses of a remote nothing serves, the words of Settings → Git's own.
      case 'git-set-up': return t('settings.git.moved.setUp');
      case 'git-keep-own': return t('settings.git.moved.keep');
      // What mends a held remote, by its class.
      case 'git-copy-key': return t('work.attention.git.copyKey');
      case 'git-key-page': return t('settings.git.keyPage', { host: item.git?.host ?? '' });
      case 'git-sign-in': return t('work.attention.git.signIn');
      case 'git-open': return t('work.attention.git.open');
      case 'git-compare': return t('work.attention.git.compare');
      case 'git-choose': return t('work.attention.git.choose');
      case 'git-use-own': return t('work.attention.git.useOwn');
      case 'git-read-only': return t('work.attention.git.readOnly');
      // A git go-ahead's answers, the head's own words.
      case 'git-allow': return t('work.wait.gitAsk.allow');
      case 'git-refuse': return t('work.wait.gitAsk.refuse');
      case 'second-check-seen': return t('work.attention.secondCheck.seen');
      // The quiet row's own presses, and the session page's own *Hand over…* and *Stop…*.
      case 'quiet-carry-on': return t('work.attention.act.carryOn');
      case 'quiet-hand-over': return t('work.attention.act.handOver');
      case 'quiet-stop': return t('work.act.stop');
      case 'quiet-leave': return t('work.attention.act.leave');
      // The run's own skip, as its Workflow view says it.
      case 'pass-over': return t('workflow.run.node.skip');
      case 'ask-again': return t('workflow.run.node.askAgain');
      case 'go-on': return t('workflow.run.node.goOn');
      // A person's task's presses, as its run's view says them.
      case 'task-done': case 'task-approve': case 'task-reject': return t(`workflow.run.node.${taskActOf(act)}`);
      // The session page's own words for an end awaiting its approver.
      case 'approve-end': return t('work.memory.approve');
      case 'decline-end': return t('work.memory.decline');
    }
  };

  return (
    <li
      aria-labelledby={titleId}
      className={cn(
        'grid gap-x-4 gap-y-1.5 border-l-[3px] border-l-st-open px-3 py-2 @3xl/main:grid-cols-[minmax(0,1fr)_auto] @3xl/main:items-center',
        doorOnly && offered.length === 0 && 'grid-cols-[minmax(0,1fr)_auto] items-center',
      )}
    >
      {/* What waits and why on the left; its acts and its door at its right where the main area is wide,
          and under it where it is narrow, except a row whose door is its one control, which keeps
          it at its right at any width. A row of acts on a line of its own doubled each row. */}
      <div className="min-w-0">
        {/* Status leads: the kind, then what, where and how long. The title moves to its own line where the main
            area is narrow, so the kind, where and how long keep one line above it. */}
        <p className="m-0 flex flex-wrap items-baseline gap-x-2 gap-y-0.5 text-small">
          {/* A host key changed since ssh trusted it wears danger's ink: it may be another host. */}
          <Dot
            tone={item.git?.class === 'host-changed' ? 'failed' : 'parked'}
            label={t(
              item.kind === 'go-ahead' && item.stale ? 'work.attention.goAheadStale'
                : item.kind === 'quiet' && item.quiet?.busy ? 'work.attention.quietBusy'
                  : item.kind === 'integration' && item.integration ? INTEGRATION_LABEL[item.integration.wait]
                    : `work.attention.${item.kind}`,
            )}
          />
          <span
            id={titleId}
            className="order-last min-w-0 max-w-full basis-full truncate text-body font-semibold text-ink @3xl/main:order-none @3xl/main:basis-auto"
          >
            {item.title}
          </span>
          {where && <><span aria-hidden className="text-ink-faint">·</span><span className="text-accent">{where}</span></>}
          {when && (
            <>
              <span aria-hidden className="text-ink-faint">·</span>
              <span className="font-mono text-meta text-ink-faint">{when}</span>
            </>
          )}
        </p>
        {/* 🔴 No display utility beside the clamp: `block` overrode the box it needs, and a parked
            session's whole analysis filled the band (2026-09-29). */}
        {item.note && hasNote(item.note)
          ? (
            <p className="m-0 mt-0.5 line-clamp-2 text-small text-ink-soft">
              <Note parts={item.note.parts} compact />
            </p>
          )
          : item.detail && (
            <p className="m-0 mt-0.5 line-clamp-2 text-small text-ink-soft"><Inline text={item.detail} /></p>
          )}
      </div>
      {(offered.length > 0 || onOpen || onRun) && (
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1.5 @3xl/main:justify-end">
          {offered.map((offer) => (
            <Button key={`${offer.act}:${offer.account ?? offer.option ?? ''}`} disabled={busy || asking !== null} onClick={() => press(offer)}>
              {label(offer.act, offer.account)}
            </Button>
          ))}
          {/* What the press does, said beside it: a retry starts a session, which spends an account. */}
          {offered.some((offer) => offer.act === 'retry') && (
            <span className="text-small text-ink-faint">{t('work.attention.act.retryDoes', { repository: item.where })}</span>
          )}
          {/* Where the step it waits at stands in its workflow: a door of its own, quieter than the row's. */}
          {onRun && (
            <button
              type="button"
              onClick={onRun}
              aria-describedby={titleId}
              title={t('workflow.run.viewNamed')}
              className="inline-flex min-h-[1.75rem] items-center gap-1 rounded-control px-1.5 text-small text-ink-soft hover:bg-raised hover:text-ink"
            >
              <Icon name="workflow" size={13} />
              {t('workflow.run.door.view')}
            </button>
          )}
          {onOpen && (doorOnly
            ? <Button onClick={() => onOpen(item)}>{t(...doorOnly(item))}</Button>
            : (
              <button
                type="button"
                onClick={() => onOpen(item)}
                aria-describedby={titleId}
                className="ml-auto inline-flex min-h-[1.75rem] items-center gap-0.5 rounded-control px-1.5 text-small font-medium text-accent hover:bg-accent-soft @3xl/main:ml-0"
              >
                {/* An account's row opens its agent's page, and says whose (*Claude Code ›*). */}
                {AT_AN_AGENT.has(item.kind) && item.account ? item.account.product : t('work.attention.door.open')}
                <Icon name="chevronRight" size={13} />
              </button>
            ))}
        </div>
      )}
      {asking && (
        // Under the whole row, both columns, where the question has room for its sentence.
        <div className="@3xl/main:col-span-2">
          <AskOnce
            act={asking}
            item={item}
            label={label(asking)}
            busy={busy}
            words={words}
            onWords={setWords}
            choice={choice}
            onChoice={setChoice}
            onCancel={done}
            // The ask stays open while its act runs, and is told how it ended.
            onConfirm={(answered) => {
              if (asking === 'approve' || asking === 'refuse') {
                acts.answer?.(item, asking === 'approve', words.trim() || undefined, answered);
              } else if (asking === 'choose') acts.publish?.(item, [choice], answered);
              else if (asking === 'accept-rule') acts.acceptRule?.(item, answered);
              else if (asking === 'not-yet') acts.notYet?.(item, words.trim(), answered);
              else if (asking === 'pass-over') acts.passOver?.(item, words.trim(), answered);
              else if (asking === 'ask-again') acts.passOver?.(item, '', answered, 'ask-again');
              else if (asking === 'go-on') acts.passOver?.(item, words.trim(), answered, 'go-on');
              else if (taskActOf(asking)) acts.passOver?.(item, words.trim(), answered, taskActOf(asking)!);
              else if (asking === 'opinion-anyway') acts.opinionAnyway?.(item, words.trim() || undefined, answered);
              else if (asking === 'quiet-carry-on') acts.quiet?.(item, 'carry-on', answered);
              else if (asking === 'quiet-stop') acts.quiet?.(item, 'stop', answered);
              else answered.done();
            }}
            // The question closes once the grant is written; a refusal stays in it.
            onGrant={(answered) => acts.trust?.(item, { done: () => { answered.done(); done(); }, refused: answered.refused })}
          />
        </div>
      )}
      {below && <div className="min-w-0 @3xl/main:col-span-2">{below}</div>}
    </li>
  );
}

