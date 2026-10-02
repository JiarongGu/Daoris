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
 * (`NotDrivable`, `Held`, `NoRoot`), and `Stopped`, whose session the tick names as a fact (`heldBy`,
 * SESSUX1d). The rest keep the driver's words, since their sentences name a session or a cap the tick
 * does not carry, and so does a verdict the page has not heard of, and a stop on a shell that names no
 * session. English passes the driver's sentence through as its only copy, as the rules' defaults do
 * (POLISH2).
 */
export function sittingSentence(sitting: Consideration): string {
  if (sitting.verdict === 'Stopped' && !sitting.heldBy) return sitting.reason;
  return i18n.t(`work.sitting.${sitting.verdict}`, {
    why: sitting.reason, session: sitting.heldBy, quest: sitting.quest, defaultValue: sitting.reason,
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
