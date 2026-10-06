import { moment } from './format';
import i18n from './i18n';

/**
 * Which of a driver tick's lines are NEWS, and which the person has already been told.
 *
 * @remarks
 * 🔴 **Found on the deployed application** (D62): four identical toasts stacked up the right side of
 * the window, all saying the same repository has never been trusted. The driver reports what each
 * tick did, and a tick that holds a quest reports the hold — correctly, and *every tick*. The page
 * turned each of those reports into an interruption, and the condition behind this one is stable by
 * construction: it needs a person to run a command, so it would have said so every tick forever.
 *
 * **A tick report is a LOG; a toast is an interruption.** The two were the same thing here, and
 * that is the whole defect. The driver is not wrong to repeat itself — a headless machine printing
 * one line per tick is a log doing its job — so the judgement belongs at the boundary where a line
 * becomes a notice, which is this function.
 *
 * The rule is *changed since last tick*, not *seen before*. A line that stops and returns later is
 * news again, because the state it reports went away and came back; only an unbroken repeat is
 * silence. That also keeps the memory to one tick, so nothing accumulates for a window left open
 * for a week.
 */
export function newsFrom(previous: readonly string[], current: readonly string[]): string[] {
  const told = new Set(previous);
  const fresh: string[] = [];
  for (const line of current) {
    // Within one tick, a line repeated is still one piece of news.
    if (told.has(line) || fresh.includes(line)) continue;
    fresh.push(line);
  }
  return fresh;
}

/**
 * One open quest as the driver judged it on its last tick: whether it starts, and if not, why — the
 * driver's own sentence (D46 §3: "sitting must always say why"). Shape of the tick's `considered`.
 */
export interface Consideration {
  quest: string;
  repository: string;
  verdict: string;
  reason: string;
  /**
   * For a quest the person's stop holds (verdict `Stopped`, SESSUX1b): the session they stopped, which *Try again*
   * releases and the sentence names (SESSUX1d). Absent for every other verdict, and on a shell older than the fact.
   */
  heldBy?: string;
  /**
   * For a quest parked on its failed sessions (verdict `Exhausted`, SESSUX1i): how many failed, as the planner counted to
   * park it, which the sentence names. Absent for every other verdict, a park the shell has not read yet, and an older shell.
   */
  strikes?: number;
  /**
   * And when its last session ended (SESSUX1i): what *What needs you* counts its wait from. Absent as `strikes` is.
   */
  since?: string;
  /**
   * For a quest held at spawn because every account its start may use is cooling (TOOL4g, D125 §4): whose account, which
   * (null for the tool's own sign-in), until when, and whether the agent named the time. It waits for an account: not
   * parked, no Retry, and it starts by itself at the reset. Absent for every other hold, and on an older shell.
   */
  waitsFor?: {
    agent: string; account?: string | null;
    /** The name the person gave the account (ACCT2b), read as the look said it; null where none, absent on an older shell. */
    name?: string | null;
    until: string; stated: boolean;
  };
  /**
   * For a start held at spawn that passed accounts not signed in (TOOL6g): whose, and which, so the page names each with
   * its sign-in, beside a wait on a cooling account or with none cooling. Absent where it passed none, and on an older shell.
   * `names` (ACCT2b) is the name the person gave each, in the same order, null where one has none; absent on an older shell.
   */
  signedOut?: { agent: string; accounts: string[]; names?: (string | null)[] | null } | null;
  /**
   * For a quest a pause holds (verdict `Paused`, PAUSE1b): whose pause, an ask's or a quest's, which the sentence names and
   * whose *Resume* moves it (D132 §2.3). Absent for every other verdict, and on an older shell.
   */
  pausedBy?: { scope: 'ask' | 'quest'; id: string } | null;
  /**
   * For a quest an update's drain holds (verdict `Blocked`, UPDATE1, D139 §2): true, so the sentence is said from the fact.
   * Absent for every other hold, and on an older shell.
   */
  forUpdate?: boolean | null;
}

/** Whether a quest waits for an account (TOOL4g, D125 §4): held at spawn on a cooling account, never parked. */
export function waitsForAccount(sitting: Consideration | null | undefined): boolean {
  return sitting?.verdict === 'Blocked' && Boolean(sitting.waitsFor);
}

/**
 * A start the driver held because the agent has not been trusted where it would run (D73) — the
 * folder, the agent's own file that said so, and what it held. Shape of the tick's `untrusted`.
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
}

/**
 * Why a quest is sitting, in the driver's words — or null when the driver is starting it, or has
 * said nothing about it.
 *
 * @remarks
 * 🔴 **Found on the deployed application** (D62). The driver has said this every tick since D46 and
 * the shell forwarded it in every tick; the page read the tick's `events` and dropped the rest. So
 * the Overview led with *"is anything sitting"* and never once said why, while the reason sat in the
 * payload it had just handled — and the one surface that DID say it was a toast, gone in seconds.
 * A `Start` verdict is not sitting, and saying "starting" under a quest would be noise.
 */
export function sittingBecause(considered: readonly Consideration[], quest: string): Consideration | null {
  const found = considered.find((c) => c.quest === quest);
  return found && found.verdict !== 'Start' ? found : null;
}

/**
 * Why a quest is sitting, in the reader's language (UX5 U27).
 *
 * @remarks
 * The driver's sentence is Daoris's own voice, so it is chrome, and chrome translates. In 中文 every
 * outstanding row said *搁置 —* over the driver's English. **It translates by the VERDICT**, the
 * driver's typed half, and never by matching the English, which would turn a rewording into a silent
 * change (D48 §6). Only the verdicts whose words need nothing the page lacks have a translation
 * (`NotDrivable`, `Held`, `NoRoot`), `Stopped`, whose session the tick names as a fact (`heldBy`,
 * SESSUX1d), `Exhausted`, whose number of failed sessions it names (`strikes`, SESSUX1i), `Paused`, whose ask or
 * quest it names (`pausedBy`, PAUSE1e), and a `Blocked` hold that is an update's drain (`forUpdate`, UPDATE1). The rest
 * keep the driver's words, since their sentences name a session or a cap the tick does not carry, and
 * so does a verdict the page has not heard of, and a stop or a park on a shell that names no session or
 * number. English passes the driver's sentence through as its only copy, as the rules' defaults do
 * (POLISH2).
 */
export function sittingSentence(sitting: Consideration): string {
  if (sitting.verdict === 'Stopped' && !sitting.heldBy) return sitting.reason;
  if (sitting.verdict === 'Exhausted' && sitting.strikes == null) return sitting.reason;
  // A pause (PAUSE1e, D132 §2.3) is said from whose it is, an ask's or a quest's own; with none named, the driver's words.
  if (sitting.verdict === 'Paused') {
    if (!sitting.pausedBy) return sitting.reason;
    return i18n.t('work.sitting.Paused', {
      why: sitting.reason, pause: sitting.pausedBy.id, quest: sitting.quest,
      context: sitting.pausedBy.scope === 'quest' ? 'quest' : undefined, defaultValue: sitting.reason,
    });
  }
  // An update's drain (UPDATE1) is a `Blocked` hold the tick marks, so it is said from that fact.
  if (sitting.verdict === 'Blocked' && sitting.forUpdate) {
    return i18n.t('work.sitting.forUpdate', { why: sitting.reason, defaultValue: sitting.reason });
  }
  // A wait for an account (TOOL4g) is a `Blocked` hold the tick names the account of, so it is said from those facts, with
  // the accounts it passed not signed in and each one's sign-in (TOOL6g); a hold on accounts not signed in with none cooling
  // is said from those alone. Any other `Blocked` hold keeps the driver's words, since its sentence names what the tick does
  // not carry.
  // Each account by the name the person gave it, which the tick carries beside its id (ACCT2b), else by its id; a sign-in
  // keeps the id, which a terminal takes whatever the account is called by then.
  const signedOut = sitting.verdict === 'Blocked' && sitting.signedOut?.accounts?.length ? sitting.signedOut : null;
  const signIn = signedOut ? {
    accounts: signedOut.accounts.map((id, at) => signedOut.names?.[at]?.trim() || id).join(i18n.t('work.sitting.signedOutJoin')),
    logins: signedOut.accounts.map((name) => `\`daoris agent login ${signedOut.agent} --profile ${name}\``).join(i18n.t('work.sitting.signedOutJoin')),
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

/**
 * How many notices may be on screen at once.
 *
 * 🔴 A viewport that grows without limit is its own defect: the four stacked toasts had reached the
 * repositories card and were climbing. A cap means the newest are always readable, which is the
 * only thing a corner of the screen can promise.
 */
export const TOAST_LIMIT = 3;

/** The notices to show, newest kept, oldest dropped once the cap is spent. */
export function capped<T>(items: readonly T[], limit = TOAST_LIMIT): T[] {
  return limit <= 0 ? [] : items.slice(-limit);
}

/**
 * The notices on screen with one more: added, THEN capped, so the one just added counts toward the
 * limit. Both windows capped first and added after, and the corner held four (REV3).
 */
export function withNotice<T>(
  items: readonly T[], item: T, limit = TOAST_LIMIT, same?: (shown: T, added: T) => boolean,
): T[] {
  // A sentence already on screen is said once (UX5 U30): with the service stopped, the page's own
  // request and the driver's tick said the same thing in the same moment, and it showed twice.
  const kept = same ? items.filter((shown) => !same(shown, item)) : items;
  return capped([...kept, item], limit);
}
