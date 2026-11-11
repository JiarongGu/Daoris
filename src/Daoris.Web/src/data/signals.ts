import { moment } from '../locales/format';
import i18n from '../locales/i18n';
import { shellWord } from '../lib/shellWord';

/**
 * One open quest as the driver judged it on its last tick: whether it starts, and if not, why — the
 * driver's own sentence ("sitting must always say why"). Shape of the tick's `considered`.
 */
export interface Consideration {
  quest: string;
  repository: string;
  verdict: string;
  reason: string;
  /**
   * For a quest the person's stop holds (verdict `Stopped`): the session they stopped, which *Try again*
   * releases and the sentence names. Absent for every other verdict.
   */
  heldBy?: string;
  /**
   * For a quest parked on its failed sessions (verdict `Exhausted`): how many failed, as the planner counted to
   * park it, which the sentence names. Absent for every other verdict and a park the shell has not read yet.
   */
  strikes?: number;
  /** The number of failed sessions its line's failures rule parks it at, which the sentence names with the count. */
  parksAt?: number | null;
  /**
   * And when its last session ended: what *What needs you* counts its wait from. Absent as `strikes` is.
   */
  since?: string;
  /**
   * For a quest held at spawn because every account its start may use is cooling: whose account, which
   * (null for the tool's own sign-in), until when, and whether the agent named the time. It waits for an account: not
   * parked, no Retry, and it starts by itself at the reset. Absent for every other hold.
   */
  waitsFor?: {
    agent: string; account?: string | null;
    /** The name the person gave the account, read as the look said it; null where none, left out where there is none. */
    name?: string | null;
    until: string; stated: boolean;
  };
  /**
   * For a start held at spawn that passed accounts not signed in: whose, and which, so the page names each with
   * its sign-in, beside a wait on a cooling account or with none cooling. Absent where it passed none.
   * `names` is the name the person gave each, in the same order, null where one has none; left out where there is none.
   * `noAccount` is true where the hold is the one for no Daoris account signed in: the page says it from
   * this fact, never from `accounts` being empty.
   */
  signedOut?: { agent: string; accounts: string[]; names?: (string | null)[] | null; noAccount?: boolean } | null;
  /**
   * For a quest a pause holds (verdict `Paused`): whose pause, an ask's or a quest's, which the sentence names and
   * whose *Resume* moves it. Absent for every other verdict.
   */
  pausedBy?: { scope: 'ask' | 'quest'; id: string } | null;
  /**
   * For a quest an update's drain holds (verdict `Blocked`): true, so the sentence is said from the fact.
   * Absent for every other hold.
   */
  forUpdate?: boolean | null;
  /**
   * For a quest whose take is not this machine's (verdict `TakenElsewhere`): whose it is, the machine that
   * took it and its session where a teammate's record names them, else null; `here` where it is taken on this machine after
   * its session ended, by a chat or by work outside Daoris; and `last`, the session here not carried on over it. Absent for
   * every other verdict.
   */
  takenBy?: { machine?: string | null; session?: string | null; here?: boolean; reopened?: boolean; last: string } | null;
  /**
   * For a set-up step the planner cannot show (verdict `CannotShow`): why, as a code (`unnamed`: no review rule
   * here names the environment; `no-address`: a local environment declares no address; `no-window`: a local environment and no
   * window to show it in), the environment's name and its address where it has one. Null or absent otherwise.
   */
  cannotShow?: { code: string; environment: string; address: string | null } | null;
  /**
   * For the verdicts the page has no words for (`AtCapacity`, `RepositoryBusy`, `Waiting`, `WaitsForOpinion`, `NotAdopted`,
   * `SourceReopened`): which of the planner's sentences `reason` is, as a code, with the facts its words are said
   * from — the cap as a number, the session, the other quest (one queued ahead, the question waits on, the step before, the
   * reopened source), the repository a question is asked of, a review's environment and the second opinion's state (null where
   * it is not judged yet). Each fact is null where the code names none. Null or absent otherwise.
   */
  said?: {
    code: string; cap?: number | null; session?: string | null; quest?: string | null; repository?: string | null;
    environment?: string | null; state?: string | null;
    /** Its declared paths that meet the earlier quest's work, under `overlaps`. Left out where there is none. */
    paths?: readonly string[] | null;
  } | null;
}

/**
 * Starts the look held because every account they may use is cooling, one per account, as the tick hands
 * them to the page; then the starts it held on signed-out accounts with none cooling, one per agent, with
 * no time and no account. Shape of the tick's `waits`, read defensively: the bridge leaves a null out.
 *
 * @remarks
 * A consideration names a quest alone; this names an ask's intake too, which waits on its accounts with nothing on the page
 * saying so. Facts only, the driver's sentence left with the driver: the page says them in the reader's language. **A wait
 * with no `until` is no cool-off**: it waits for a sign-in, its accounts its `signedOut`, and its null account is no
 * account at all, never the tool's own sign-in.
 */
export interface AccountWaitTick {
  /** Whose accounts: the owner a door runs as. */
  agent: string;
  /**
   * The account whose cool-off ends first, or null for the tool's own sign-in; null too, and no account, where no `until` is.
   */
  account?: string | null;
  /** The name the person gave it, null where none. */
  name?: string | null;
  workspace?: string | null;
  /** When that account is offered again; absent or null where none cools and a sign-in is the wait. */
  until?: string | null;
  stated: boolean;
  /** The quests held. */
  quests?: string[];
  /** The asks whose intakes are held. */
  asks?: string[];
  /** The accounts the held starts passed signed out, in the order the walk tried them. */
  signedOut?: string[];
  /** The starts are held because no Daoris account of the agent's is signed in: a fact, never an empty `signedOut`. */
  noAccount?: boolean;
}

/** Whether a quest waits for an account: held at spawn on a cooling account, never parked. */
export function waitsForAccount(sitting: Consideration | null | undefined): boolean {
  return sitting?.verdict === 'Blocked' && Boolean(sitting.waitsFor);
}

/**
 * A start the driver held because the agent has not been trusted where it would run — the
 * folder, the agent's own file that says so, and what it held. Shape of the tick's `untrusted`.
 *
 * @remarks
 * Machine-local paths, so they arrive over the shell's bridge only; a browser never has one. The
 * pair is what a person's grant writes, and the shell grants nothing it is not holding.
 */
export interface TrustHold {
  folder: string;
  trustFile: string;
  quest?: string;
  ask?: string;
  /**
   * Its file is the person's own sign-in's: a conversation that began there goes on there. Daoris writes
   * nothing in it, so the question offers no grant and `TRUST_FOLDER` refuses one as `TRUST_OWN_HOME`. Sent only where true.
   */
  own?: boolean;
}

/**
 * Why a quest is sitting, in the driver's words — or null when the driver is starting it, or has
 * said nothing about it.
 *
 * @remarks
 * 🔴 The driver says this every tick and the shell forwards it in every tick; a page that reads only
 * the tick's `events` drops the rest, so the Overview would lead with *"is anything sitting"* and never
 * once say why, while the reason sits in the payload it had just handled — and the one surface that DID
 * say it would be a toast, gone in seconds.
 * A `Start` verdict is not sitting, and saying "starting" under a quest would be noise.
 */
export function sittingBecause(considered: readonly Consideration[], quest: string): Consideration | null {
  const found = considered.find((c) => c.quest === quest);
  return found && found.verdict !== 'Start' ? found : null;
}

/**
 * Why a quest is sitting, in the reader's language.
 *
 * @remarks
 * The driver's sentence is Daoris's own voice, so it is chrome, and chrome translates. In 中文 a row
 * would otherwise say *搁置 —* over the driver's English. **It translates by the VERDICT**, the
 * driver's typed half, and never by matching the English, which would turn a rewording into a silent
 * change. Only the verdicts whose words need nothing the page lacks have a translation
 * (`NotDrivable`, `Held`, `NoRoot`), `Stopped`, whose session the tick names as a fact (`heldBy`),
 * `Exhausted`, whose number of failed sessions it names (`strikes`), `Paused`, whose ask or
 * quest it names (`pausedBy`), a `Blocked` hold that is an update's drain (`forUpdate`),
 * `TakenElsewhere`, whose take it names (`takenBy`), and `CannotShow`, a set-up step with nowhere to
 * show its work here, said from the environment and the reason the tick names (`cannotShow`). The rest
 * keep the driver's words, since their sentences name a session or a cap the tick does not carry, and
 * so does a verdict the page has not heard of, and a stop, a park or a take on a shell that names no session,
 * number or machine. English passes the driver's sentence through as its only copy, as the rules' defaults do.
 * The six verdicts `AtCapacity`, `RepositoryBusy`, `Waiting`,
 * `WaitsForOpinion`, `NotAdopted` and `SourceReopened` are worded by the code the tick names beside them (`said`), in English
 * as well, as `CannotShow`'s are; with no code, or one the page has not heard of, the driver's words.
 */
export function sittingSentence(sitting: Consideration): string {
  const coded = saidSentence(sitting);
  if (coded) return coded;
  if (sitting.verdict === 'Stopped' && !sitting.heldBy) return sitting.reason;
  if (sitting.verdict === 'Exhausted' && sitting.strikes == null) return sitting.reason;
  // The park is the person's failures rule's, said with its number beside the count that reached it.
  if (sitting.verdict === 'Exhausted' && sitting.parksAt != null) {
    return i18n.t('work.sitting.ExhaustedRule', { quest: sitting.quest, failed: sitting.strikes, at: sitting.parksAt });
  }
  // A take that is not this machine's is said from whose it is: a teammate's machine and session where a record
  // names both, another machine where none does, or a take made here; with no facts, the driver's words.
  if (sitting.verdict === 'TakenElsewhere') {
    const taken = sitting.takenBy;
    if (!taken?.last) return sitting.reason;
    const named = Boolean(taken.machine && taken.session);
    return i18n.t('work.sitting.TakenElsewhere', {
      why: sitting.reason, quest: sitting.quest, machine: taken.machine, session: taken.session, last: taken.last,
      context: taken.reopened ? 'reopened' : taken.here ? 'here' : named ? undefined : 'another', defaultValue: sitting.reason,
    });
  }
  // A set-up step with nowhere to show its work is said from the fact the tick carries: which of the three
  // reasons, the environment and its address; with no fact, or a code the page has not heard of, the driver's words.
  if (sitting.verdict === 'CannotShow') {
    const fact = sitting.cannotShow;
    const context = fact && ({ unnamed: 'unnamed', 'no-address': 'noAddress', 'no-window': 'noWindow' } as Record<string, string>)[fact.code];
    if (!fact || !context) return sitting.reason;
    return i18n.t('work.sitting.CannotShow', {
      why: sitting.reason, environment: fact.environment, address: fact.address ?? '', context, defaultValue: sitting.reason,
    });
  }
  // A pause is said from whose it is, an ask's or a quest's own; with none named, the driver's words.
  if (sitting.verdict === 'Paused') {
    if (!sitting.pausedBy) return sitting.reason;
    return i18n.t('work.sitting.Paused', {
      why: sitting.reason, pause: sitting.pausedBy.id, quest: sitting.quest,
      context: sitting.pausedBy.scope === 'quest' ? 'quest' : undefined, defaultValue: sitting.reason,
    });
  }
  // An update's drain is a `Blocked` hold the tick marks, so it is said from that fact.
  if (sitting.verdict === 'Blocked' && sitting.forUpdate) {
    return i18n.t('work.sitting.forUpdate', { why: sitting.reason, defaultValue: sitting.reason });
  }
  // A wait for an account is a `Blocked` hold the tick names the account of, so it is said from those facts, with
  // the accounts it passed not signed in and each one's sign-in; a hold on accounts not signed in with none cooling
  // is said from those alone. Any other `Blocked` hold keeps the driver's words, since its sentence names what the tick does
  // not carry.
  // Each account by the name the person gave it, which the tick carries beside its id, else by its id; a sign-in
  // keeps the id, which a terminal takes whatever the account is called by then.
  // No Daoris account signed in is a fact the tick carries, said apart from accounts that are passed.
  if (sitting.verdict === 'Blocked' && sitting.signedOut?.noAccount) {
    return i18n.t('work.sitting.noAccount', { why: sitting.reason, agent: sitting.signedOut.agent, defaultValue: sitting.reason });
  }
  const signedOut = sitting.verdict === 'Blocked' && sitting.signedOut?.accounts?.length ? sitting.signedOut : null;
  const signIn = signedOut ? {
    accounts: signedOut.accounts.map((id, at) => signedOut.names?.[at]?.trim() || id).join(i18n.t('work.sitting.signedOutJoin')),
    // Each a command a person pastes, so the id is spelled for a shell.
    logins: signedOut.accounts.map((name) => `\`daoris agent login ${signedOut.agent} --account ${shellWord(name, '<account>')}\``)
      .join(i18n.t('work.sitting.signedOutJoin')),
  } : null;
  if (sitting.verdict === 'Blocked' && sitting.waitsFor) {
    const { agent, account, name, until, stated } = sitting.waitsFor;
    const key = account ? 'work.sitting.waitsFor' : 'work.sitting.waitsForOwn';
    return i18n.t(signIn ? `${key}SignedOut` : key, {
      why: sitting.reason, agent, account: name?.trim() || account, when: moment(until),
      because: i18n.t(stated ? 'harness.cooling.why.stated' : 'harness.cooling.why.default'),
      ...signIn,
      defaultValue: sitting.reason,
    });
  }
  if (signIn) return i18n.t('work.sitting.signedOut', { why: sitting.reason, agent: signedOut!.agent, ...signIn, defaultValue: sitting.reason });
  return i18n.t(`work.sitting.${sitting.verdict}`, {
    why: sitting.reason, session: sitting.heldBy, quest: sitting.quest, failed: sitting.strikes,
    defaultValue: sitting.reason,
  });
}

/** The codes the page words for each verdict that names one, as the planner's `SittingFact` spells them. */
const SAID: Record<string, readonly string[]> = {
  AtCapacity: ['cap'],
  // `overlaps`: a start held under `overlap: wait` until the earlier quest's work is integrated.
  // `place`: the place's writer holds it, its turn running or, with paths, parked with uncommitted paths of its own.
  // `apart`: the place's isolate step's count of sessions working apart is full.
  RepositoryBusy: ['serving', 'opening', 'tree', 'treeOpening', 'treeThisLook', 'place', 'apart', 'queued', 'repositoryOpening', 'overlaps'],
  Waiting: ['asked', 'unreadRunning', 'unread', 'unreadAtCommit', 'missing', 'unreviewed', 'departed'],
  NotAdopted: ['unregistered', 'noRoot', 'noConnector'],
  WaitsForOpinion: ['landing', 'steps'],
  SourceReopened: ['withdrawn', 'held'],
  // Its last session's provider kept failing past its bound (`held.transient`).
  TransientHeld: ['transient'],
  // Its role's policy asks before it moves the quest to another choice, or the line has made its bound of moves.
  PolicyHeld: ['policy-ask', 'moves-bound'],
  // Its finished session left it taken, and nothing carries it on (`unclosed`).
  Unclosed: ['unclosed'],
  // The steward's stop, held for a Try again by the steward or the owner; never said as the person's.
  Stopped: ['steward'],
  // A start held because its earlier work is in the owner's checkout (`held.checkout`).
  Blocked: ['checkout'],
};

/**
 * A verdict's sentence worded from the code and facts the tick carries, or null where it names none the page
 * knows, so the caller says the driver's words. A review waited for in an environment, and an opinion not judged yet, are their
 * code's other wording; the opinion's state is said in the gate's own words.
 */
function saidSentence(sitting: Consideration): string | null {
  const said = sitting.said;
  if (!said || !SAID[sitting.verdict]?.includes(said.code)) return null;
  if (said.code === 'cap' && said.cap == null) return null;
  if (said.code === 'overlaps' && !said.paths?.length) return null;
  const context = said.code === 'unreviewed' && said.environment ? 'unreviewedIn'
    : said.code === 'place' && said.paths?.length ? 'placeKept'
    : sitting.verdict === 'WaitsForOpinion' && !said.state ? `${said.code}Unjudged`
    : said.code;
  return i18n.t(`work.sitting.${sitting.verdict}`, {
    context, why: sitting.reason, quest: sitting.quest, cap: said.cap, session: said.session ?? '', other: said.quest ?? '',
    to: said.repository ?? '', environment: said.environment ?? '',
    paths: (said.paths ?? []).map((path) => `\`${path}\``).join(' · '),
    state: said.state ? i18n.t(`opinion.state.${said.state}`, { defaultValue: said.state }) : '',
    defaultValue: sitting.reason,
  });
}

/**
 * How many notices may be on screen at once.
 *
 * 🔴 A viewport that grows without limit is its own defect: stacked toasts would reach the
 * repositories card and keep climbing. A cap means the newest are always readable, which is the
 * only thing a corner of the screen can promise.
 */
export const TOAST_LIMIT = 3;

/** The notices to show, newest kept, oldest dropped once the cap is spent. */
export function capped<T>(items: readonly T[], limit = TOAST_LIMIT): T[] {
  return limit <= 0 ? [] : items.slice(-limit);
}

/**
 * The notices on screen with one more: added, THEN capped, so the one just added counts toward the
 * limit. Capping first and adding after would let the corner hold one more than the limit.
 */
export function withNotice<T>(
  items: readonly T[], item: T, limit = TOAST_LIMIT, same?: (shown: T, added: T) => boolean,
): T[] {
  // A sentence already on screen is said once: with the service stopped, the page's own
  // request and the driver's tick say the same thing in the same moment, and it would show twice.
  const kept = same ? items.filter((shown) => !same(shown, item)) : items;
  return capped([...kept, item], limit);
}
