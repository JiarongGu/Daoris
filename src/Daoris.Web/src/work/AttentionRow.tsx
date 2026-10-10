import { type ReactNode, useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { SetUpRef } from '../api';
import { clockOf, elapsed } from '../format';
import { cn } from '../lib/cn';
import { shellWord } from '../shellWord';
import type { TrustHold } from '../signals';
import { Button, Dot, Icon, Inline, SelectField } from '../ui';
import type { AccountRowFacts } from './accountAttention';
import { ASKS_ONCE, type AttentionActId, type AttentionOffer, attentionOffers } from './attention';
import { Note } from './Note';
import { type Answered, InlineConfirm } from './InlineConfirm';
import { hasNote, type NoteRecord } from './noteLines';
import { type OpinionWait, opinionState } from './opinion';
import { unsettledWords } from './OpinionGate';
import { TrustAsk } from './TrustAsk';

/** The kinds of thing *What needs you* lists (design §6.2), each in one of its three groups (`ATTENTION_GROUP`). */
export type AttentionKind =
  | 'parked' | 'parked-quest' | 'go-ahead' | 'trust' | 'account-wait' | 'signed-out'
  | 'proposal' | 'intake' | 'departure' | 'set-up' | 'rule' | 'unanswerable'
  | 'review' | 'opinion';

/**
 * One thing that is waiting on a person, as Overview's band shows it.
 *
 * @remarks
 * **Every kind is a different question.** A `parked` session is stopped at a checkpoint
 * only a person can clear (D46). An ask is a `proposal` while nothing serves it: the declarations
 * proposed, and only a person publishes (INT4a). An ask is `intake` when its intake session parked
 * asking the person (D65 §1b). That session is counted as the ask, never twice. An `unanswerable`
 * quest is addressed to a repository this deployment has no registration for. It will sit forever,
 * and nothing else says so. A `trust` row is a folder the agent has not been trusted in, where the
 * driver is holding a start (D73): only the person can give that grant. A `rule` row is an agent's
 * proposal to widen what agents may do (PERM2, D74), which never applies without the person. A
 * `parked-quest` row is a quest parked on its failed sessions here (SESSUX1i, D126 §4.6), which only the
 * person's *Try again* starts again.
 *
 * **UX6c added three** (design §6.2): a `go-ahead` a session asked on an ask (KNOWUSE1a), which holds that session's
 * act; a `departure`, a done held for the person's yes because it departed from what they required (DRIFT1d2); and
 * `review`, finished work Sessions' list places *To review* (D126), which design §4 named first and could not build
 * until looking was recorded.
 *
 * **UX6d added the accounts** (design §6.2, TOOL4m's row): an `account-wait`, a start held because every account it may
 * use is cooling or signed out, naming each account that would serve it as last known; and `signed-out`, an account a list
 * or a default holds that read signed out, which no waiting start names.
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
  /** The ask a `go-ahead` is on, or a `trust` row's held intake is for: what the door opens. */
  ask?: string;
  /** The quest a `trust` row holds: what the door opens. */
  quest?: string;
  /** A `go-ahead`'s number on its ask, which the answer names. */
  number?: number;
  /** A `set-up` row's set-up, named whole as the row drew it: what its verdict answers (REVIEWENV1g, REVIEWENV1b3). */
  setUp?: SetUpRef;
  /** A `set-up` row's set-up is local, so Daoris serves it and *Show it again* can. */
  local?: boolean;
  /** The session that said a `set-up` row's set-up, which a *not yet*'s words go to; null for the person's own. */
  session?: string | null;
  /** An `opinion` row's work and its gate (XAGENT1g): what its presses act on, and whether a press of the person's is coming. */
  opinion?: OpinionWait;
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
  /** An account row's agent, the accounts it names as last known, and the one it may let in (UX6d). */
  account?: AccountRowFacts;
  /**
   * When it started waiting — a park's last move, a parked quest's last session's end, an ask's asking, a quest's filing, a
   * go-ahead's first asking, a held done's close, a reviewed session's end. Null where nothing the page holds says when
   * (UXFIX3): a signed-out account's row knows only when the account was read, which is not a wait, so it says `read`.
   */
  since: string | null;
  /** When a signed-out account's row read it (UXFIX3): said as *read …* where a row with a wait says how long. */
  read?: string | null;
  /** The sentence explaining what the person is being asked to settle. */
  detail?: string | null;
  /**
   * A parked session's or a parked intake's note, its English and its parts (LANG1b): worded by the row in the reader's
   * language, the agent's question as written, in `detail`'s place.
   */
  note?: NoteRecord;
};

/**
 * What the band hands a row to settle it where it stands (UX6c, design §6.2–§6.3), each reporting the row it was pressed
 * on. A row offers an act only where `attentionActs` names it for its kind AND it is handed here: a browser, which has no
 * driver and no rules file, is handed none of those, so it offers none.
 */
export type AttentionActs = {
  /** An ask published to these receivers: its declarations' in one press, or the one chosen under the row. */
  publish?: (item: Attention, to: readonly string[], answered?: Answered) => void;
  /** A quest parked on its failed sessions, started again (D126 §3.4): one press, which starts a session. */
  retry?: (item: Attention) => void;
  /** A go-ahead's yes or no, with the person's words where they gave any (KNOWUSE1a). */
  answer?: (item: Attention, approved: boolean, words: string | undefined, answered: Answered) => void;
  /** A folder trusted for the agent, after its own question (D73). */
  trust?: (item: Attention) => void;
  /** A held done's departure accepted (DRIFT1d2): what it held goes on. */
  acceptDeparture?: (item: Attention) => void;
  /** An agent's widening of the rules accepted, after saying what it widens (D74). */
  acceptRule?: (item: Attention, answered: Answered) => void;
  /** An agent's widening of the rules declined: the rules stay as they were. */
  declineRule?: (item: Attention) => void;
  /** An account's sign-in started (UX6d): the agent's own, through its door, as the agent's page starts it. */
  signIn?: (item: Attention, account: string) => void;
  /** One account read on the press (§5.3, ROSTER1): the one reading a person may ask for, never a look. */
  read?: (item: Attention, account: string) => void;
  /** A ready account let into the list the waiting start reads (D130 §3.3), after its question. */
  letRun?: (item: Attention, account: string, answered: Answered) => void;
  /** A set-up the person looked at said reviewed (REVIEWENV1g): one press, naming the set-up the row drew. */
  reviewed?: (item: Attention) => void;
  /** A set-up said not yet, with the person's words, which go to its session as its next turn (REVIEWENV1g). */
  notYet?: (item: Attention, words: string, answered: Answered) => void;
  /** A set-up's build served to its tab again, the tab brought forward (REVIEWENV1d). */
  showAgain?: (item: Attention) => void;
  /** A second opinion asked again, or tried again where none could be had (XAGENT1g): a pass the driver's next look starts. */
  opinionAgain?: (item: Attention) => void;
  /** *Go on anyway…* at a second opinion's gate, with the person's words where they gave any (XAGENT1g). */
  opinionAnyway?: (item: Attention, words: string | undefined, answered: Answered) => void;
};

/** Which handler each act needs. */
const HANDLER: Record<AttentionActId, keyof AttentionActs> = {
  publish: 'publish',
  choose: 'publish',
  retry: 'retry',
  approve: 'answer',
  refuse: 'answer',
  trust: 'trust',
  'accept-departure': 'acceptDeparture',
  'accept-rule': 'acceptRule',
  'decline-rule': 'declineRule',
  'sign-in': 'signIn',
  read: 'read',
  'let-run': 'letRun',
  reviewed: 'reviewed',
  'not-yet': 'notYet',
  'show-again': 'showAgain',
  'opinion-again': 'opinionAgain',
  'opinion-anyway': 'opinionAnyway',
};

/** The kinds that wait in a circle rather than in a repository. */
const IN_A_CIRCLE: ReadonlySet<Attention['kind']> = new Set(['proposal', 'intake']);

/** The kinds whose door is an agent's page, named by the agent rather than *Open* (UX6d): the door says where it goes. */
const AT_AN_AGENT: ReadonlySet<Attention['kind']> = new Set(['account-wait', 'signed-out']);

/**
 * The kinds whose door is their one control, since their answer needs reading first (design §6.3): a park's question in
 * Sessions, an intake's on its ask, a review in Sessions with its review open (D113). Each is a button naming that.
 */
const DOOR_ONLY: Partial<Record<Attention['kind'], (item: Attention) => [string, Record<string, unknown>?]>> = {
  parked: () => ['work.attention.door.answer'],
  intake: (item) => ['work.attention.door.intake', { id: item.id }],
  review: () => ['work.attention.door.review'],
};

/**
 * A row in *what needs you*: what waits, why, the acts that settle it where one press is safe, and its door.
 *
 * @remarks
 * **A row reads `kind · what · where · how long`, then why** (design §6.3, D41 §4 *status leads*), two lines of why at
 * most, whole in its record. Its kind wears open's hue: it waits on the person. Below 48rem of main area the title takes
 * its own line under the rest, as the design draws it at 680 px.
 *
 * **An act is on the row only where one press is safe and the row says what it does** (UX6c). *Publish to …* names whom
 * it publishes to, *Try again* says it starts a session, *Accept the departure* is the quest page's press. What widens
 * what Daoris may do (a folder's trust, a widening of the rules), a go-ahead's answer, which every session on its ask is
 * handed, and a choice of receiver ask once under the row, the move then *Never mind* (D41 §4). Which acts a kind offers is
 * `attentionActs`, the one rule.
 *
 * **A door opens something, or it is not a door** (platform language §4). Handed no `onOpen`, the row has nowhere to go,
 * as a parked session in a browser has no Sessions, and it shows no door: it still says what is waiting, because knowing
 * is the half that travels. A row whose answer needs reading has its door as its one control (*Answer…*, *Review*); every
 * other row's door is *Open* at its end, beside its acts, and an account's row's is its agent's name (UX6d).
 *
 * **An account's row settles what one press can** (UX6d, §6.2): *Sign in* to each account read signed out, *Read* an
 * account no read answered, and *Let … run …* (D130 §3.3) for an account known ready, which widens what Daoris may spend and
 * so asks once, naming the list it joins and its terminal twin; where the one outside the list was never read, *Read* it
 * first (UXFIX3). Its sign-in's steps are the band's to hand (`below`), since they follow a process. A signed-out account's
 * row says when it was read where the others say how long (*read 10:42*), since a reading is not a wait (UXFIX3).
 *
 * A molecule: it is handed the item, the acts and the door, and reports each press.
 */
export function AttentionRow({ item, onOpen, onRun, acts = {}, busy = false, opened = null, below = null }: {
  item: Attention;
  onOpen?: (item: Attention) => void;
  /**
   * Its door into the run whose step waits on the person (WORKFLOW1c, the workflow design §7): the session attended, the side
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
  const offered = attentionOffers(item).filter((offer) => acts[HANDLER[offer.act]] !== undefined);
  const doorOnly = DOOR_ONLY[item.kind];
  const where = IN_A_CIRCLE.has(item.kind) || item.circle ? t('work.attention.circle', { circle: item.where }) : item.where;
  // An account's name as the row says it: the person's, from what the row names (ACCT2), or the one outside its list.
  const called = (account?: string) =>
    [...(item.account?.named ?? []), item.account?.outside, item.account?.readFirst]
      .find((one) => one && one.id === account)?.label ?? account ?? '';
  // How long it has waited; where no wait is known, when its account was read, which is not a wait (UXFIX3).
  const when = item.since
    ? t('work.attention.since', { span: elapsed(item.since) })
    : item.read ? t('agents.read.at', { when: clockOf(item.read) }) : null;

  const done = () => { setAsking(null); setWords(''); setChoice(''); };
  const press = ({ act, account }: AttentionOffer) => {
    if (ASKS_ONCE.has(act)) { setAsking(act); return; }
    if (act === 'publish') acts.publish?.(item, item.publishTo ?? []);
    else if (act === 'retry') acts.retry?.(item);
    else if (act === 'accept-departure') acts.acceptDeparture?.(item);
    else if (act === 'decline-rule') acts.declineRule?.(item);
    else if (act === 'sign-in' && account) acts.signIn?.(item, account);
    else if (act === 'read' && account) acts.read?.(item, account);
    else if (act === 'reviewed') acts.reviewed?.(item);
    else if (act === 'show-again') acts.showAgain?.(item);
    else if (act === 'opinion-again') acts.opinionAgain?.(item);
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
      case 'read': return t('agents.act.readFor', { account: called(account) });
      case 'let-run': return t('work.attention.act.letRun', {
        account: item.account?.outside?.label ?? '', workspace: item.account?.outside?.workspace ?? '',
      });
      // The review's own words, as the step's page and the strip's chip say them (REVIEWENV1g).
      case 'reviewed': return t('review.act.reviewed');
      case 'not-yet': return t('review.act.notYet');
      case 'show-again': return t('review.act.showAgain');
      // The second opinion's own words, as its gate says them (XAGENT1g).
      case 'opinion-again': return t(item.opinion?.opinion.state === 'unavailable' ? 'opinion.act.tryAgain' : 'opinion.act.askAgain');
      case 'opinion-anyway': return t('opinion.act.anyway');
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
      {/* What waits and why on the left; its acts and its door at its right where the main area is wide, as the design
          draws them at 1546 px, and under it where it is narrow, except a row whose door is its one control, which keeps
          it at its right at any width. A row of acts on a line of its own doubled each row. */}
      <div className="min-w-0">
        {/* Status leads (D41 §4): the kind, then what, where and how long. The title moves to its own line where the main
            area is narrow, so the kind, where and how long keep one line above it. */}
        <p className="m-0 flex flex-wrap items-baseline gap-x-2 gap-y-0.5 text-small">
          <Dot tone="parked" label={t(`work.attention.${item.kind}`)} />
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
              <Note note={item.note.note} parts={item.note.parts} compact />
            </p>
          )
          : item.detail && (
            <p className="m-0 mt-0.5 line-clamp-2 text-small text-ink-soft"><Inline text={item.detail} /></p>
          )}
      </div>
      {(offered.length > 0 || onOpen || onRun) && (
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1.5 @3xl/main:justify-end">
          {offered.map((offer) => (
            <Button key={`${offer.act}:${offer.account ?? ''}`} disabled={busy || asking !== null} onClick={() => press(offer)}>
              {label(offer.act, offer.account)}
            </Button>
          ))}
          {/* What the press does, said beside it: a retry starts a session, which spends an account (D126 §3.4). */}
          {offered.some((offer) => offer.act === 'retry') && (
            <span className="text-small text-ink-faint">{t('work.attention.act.retryDoes', { repository: item.where })}</span>
          )}
          {/* Where the step it waits at stands in its workflow (WORKFLOW1c): a door of its own, quieter than the row's. */}
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
                {/* An account's row opens its agent's page, and says whose (D150 §6.2: *Claude Code ›*). */}
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
            // The ask stays open while its act runs, and is told how it ended (UXFIX2b3a, D153's note).
            onConfirm={(answered) => {
              if (asking === 'approve' || asking === 'refuse') {
                acts.answer?.(item, asking === 'approve', words.trim() || undefined, answered);
              } else if (asking === 'choose') acts.publish?.(item, [choice], answered);
              else if (asking === 'accept-rule') acts.acceptRule?.(item, answered);
              else if (asking === 'let-run' && item.account?.outside) acts.letRun?.(item, item.account.outside.id, answered);
              else if (asking === 'not-yet') acts.notYet?.(item, words.trim(), answered);
              else if (asking === 'opinion-anyway') acts.opinionAnyway?.(item, words.trim() || undefined, answered);
              else answered.done();
            }}
            onGrant={() => { acts.trust?.(item); done(); }}
          />
        </div>
      )}
      {below && <div className="min-w-0 @3xl/main:col-span-2">{below}</div>}
    </li>
  );
}

/**
 * What letting an account run a workspace does (D130 §3.3), before the press that does it: the list it joins, what Daoris
 * may then start and spend, and the terminal's twin (D50, ACCT1's `profile join`), its account and workspace spelled for
 * any shell (ACCTQUOTE1). This machine's list is said as one every workspace naming no account of its own runs on.
 */
function letRunSentence(
  t: ReturnType<typeof useTranslation>['t'], agent: string, outside: NonNullable<AccountRowFacts['outside']>,
): string {
  const where = outside.list ? shellWord(outside.list, '<workspace>') : '--machine';
  const command = `\`daoris agent profile join ${agent} ${shellWord(outside.id, '<account>')} ${where}\``;
  return outside.list
    ? t('work.attention.ask.letRun', { account: outside.label, workspace: outside.list, command })
    : t('work.attention.ask.letRunMachine', { account: outside.label, workspace: outside.workspace, command });
}

/**
 * The question an act asks once under its row (D41 §4): what the second press will do, then the move and *Never mind*.
 * A folder's trust is the agent's own question (`TrustAsk`); a go-ahead's yes or no takes the person's words; a choice of
 * receiver is the workspace's receivers. Named by the act's own label, so a person and a test find it as the press they made.
 */
function AskOnce({ act, item, label, busy, words, onWords, choice, onChoice, onCancel, onConfirm, onGrant }: {
  act: AttentionActId;
  item: Attention;
  label: string;
  busy: boolean;
  words: string;
  onWords: (words: string) => void;
  choice: string;
  onChoice: (choice: string) => void;
  onCancel: () => void;
  onConfirm: (answered: Answered) => void;
  /** A folder's trust, which keeps its own question until UXFIX2b3b: it closes on the press. */
  onGrant: () => void;
}) {
  const { t } = useTranslation();
  if (act === 'trust' && item.trust) {
    return (
      <div className="mt-2">
        <TrustAsk
          hold={{ ...item.trust, ...(item.quest ? { quest: item.quest } : {}), ...(item.ask ? { ask: item.ask } : {}) }}
          busy={busy}
          onGrant={onGrant}
          onCancel={onCancel}
        />
      </div>
    );
  }
  const answering = act === 'approve' || act === 'refuse';
  // A *not yet* needs words: they are what the set-up step's session acts on (REVIEWENV1g, design §3.3).
  const notYet = act === 'not-yet';
  // *Go on anyway…* says what stays unsettled, and takes words the person may give (XAGENT1g, the second-agent design §8.5).
  const anyway = act === 'opinion-anyway' && item.opinion ? item.opinion.opinion : null;
  const outside = act === 'let-run' ? item.account?.outside ?? null : null;
  const sentence = anyway
    ? t('opinion.ask.anyway', { unsettled: unsettledWords(t, opinionState(anyway.state), anyway) })
    : notYet
    ? t(item.session ? 'review.ask.notYet' : 'review.ask.notYetOwn', { quest: item.id })
    : outside
    ? letRunSentence(t, item.account!.agent, outside)
    : act === 'approve'
      ? t('work.attention.ask.approve', { id: item.ask ?? '' })
      : act === 'refuse'
        ? t('work.attention.ask.refuse', { id: item.ask ?? '' })
        : act === 'accept-rule'
          ? t('work.attention.ask.acceptRule', { change: item.title })
          : t('work.attention.ask.choose', { workspace: item.where });
  const move = anyway
    ? t('opinion.ask.anywayMeanIt')
    : notYet
    ? t('review.ask.notYetMeanIt')
    : outside
    ? (outside.list ? t('agents.account.addTo', { workspace: outside.list }) : t('agents.account.addToMachine'))
    : act === 'approve'
      ? t('asks.goAhead.approve')
      : act === 'refuse'
        ? t('asks.goAhead.refuse')
        : act === 'accept-rule'
          ? t('settings.rules.proposals.accept')
          : t('asks.record.publish');
  // The words a go-ahead's answer, a second opinion's gate or a *not yet* takes, in the one field each names.
  const wordsLabel = answering ? t('asks.goAhead.words') : anyway ? t('opinion.ask.words') : notYet ? t('review.ask.notYetWords') : null;
  return (
    // Only a refusal ends or removes something (platform UX §4): every other ask here is outward and loses nothing.
    <InlineConfirm
      className="mt-2"
      tone={act === 'refuse' ? 'danger' : 'primary'}
      label={label}
      says={<Inline text={sentence} />}
      meanIt={move}
      busy={busy}
      ready={!(act === 'choose' && !choice) && !(notYet && !words.trim())}
      onConfirm={onConfirm}
      onClose={onCancel}
    >
      {wordsLabel && (
        <input
          aria-label={wordsLabel}
          placeholder={wordsLabel}
          value={words}
          onChange={(event) => onWords(event.target.value)}
          className="min-h-[1.9rem] min-w-0 flex-1 basis-48 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
        />
      )}
      {act === 'choose' && (
        <SelectField
          value={choice}
          onChange={onChoice}
          ariaLabel={t('work.attention.ask.choosePlaceholder')}
          placeholder={t('work.attention.ask.choosePlaceholder')}
          options={(item.choices ?? []).map((name) => ({ value: name, label: name }))}
        />
      )}
    </InlineConfirm>
  );
}
