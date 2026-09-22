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
