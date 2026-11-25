/**
 * The reached run's step clocks: the census, the plan, each project's restore and build, and each suite's discovery and run. Each
 * step says one machine line as it starts and one as it ends (`reached-step: {…}`), so a merge that ends the gate over its budget
 * still reads which step it is in and for how long (`parseSteps`); the merge record keeps them beside the gate's time
 * (`steps`), and a breach's note names the step.
 */

/** One step's clock: its name, its milliseconds, and `open` where the run ended before the step did. */
export type Step = { step: string; ms: number; open?: true };

const MARK = 'reached-step: ';

const BUILT = 'reached-built: ';

export const builtLine = (): string => `${BUILT}{}`;

export const seconds =(ms: number): string => {
  const whole = Math.round(ms / 1000);
  return whole < 60 ? `${whole} s` : `${Math.floor(whole / 60)} m ${whole % 60} s`;
};

/** The clocks of one run: each step's start and end said as it happens, and the steps in the order they started. */
export class StepClock {
  readonly #say: (line: string) => void;
  readonly #now: () => number;
  readonly #started: number;
  readonly #steps = new Map<string, Step>();

  constructor(say: (line: string) => void, now: () => number = Date.now) {
    this.#say = say;
    this.#now = now;
    this.#started = now();
  }

  /**
   * Runs `fn` as the step `name`, said as it starts and ends; a step run again adds to its clock. `carve` gives the part of its
   * time another step took (a suite's discovery, read from its report once its command ends), said as that step's.
   */
  async time<T>(name: string, fn: () => T | Promise<T>, carve?: (result: T, ms: number) => { step: string; ms: number } | null): Promise<T> {
    const at = this.#now() - this.#started;
    this.#say(`${MARK}${JSON.stringify({ step: name, at })}`);
    let result: T | undefined;
    let done = false;
    try {
      result = await fn();
      done = true;
      return result;
    } finally {
      const ms = this.#now() - this.#started - at;
      const part = done && carve ? carve(result as T, ms) : null;
      const carved = part ? Math.min(ms, Math.max(0, part.ms)) : 0;
      if (part) this.add(part.step, carved, at);
      this.add(name, ms - carved, at + carved);
    }
  }

  /** A step measured outside a `time` (a suite's discovery, read from its report), said as it ends. */
  add(name: string, ms: number, at = this.#now() - this.#started - ms): void {
    const before = this.#steps.get(name)?.ms ?? 0;
    this.#steps.set(name, { step: name, ms: before + Math.max(0, Math.round(ms)) });
    this.#say(`${MARK}${JSON.stringify({ step: name, at, ms: Math.max(0, Math.round(ms)) })}`);
  }

  steps(): Step[] {
    return [...this.#steps.values()];
  }
}

/**
 * The steps a gate's log says, in the order they started: each ended one with its time summed over its runs, and the one still
 * running where the log stops, `open`, timed to `endedAt` (the gate's own time, from its start) where that is known.
 */
export function parseSteps(log: string, endedAt: number | null = null): Step[] {
  const steps = new Map<string, Step>();
  const open = new Map<string, number>();
  for (const line of log.split(/\r?\n/)) {
    if (!line.startsWith(MARK)) continue;
    let said: { step?: unknown; at?: unknown; ms?: unknown };
    try {
      said = JSON.parse(line.slice(MARK.length));
    } catch {
      continue;
    }
    if (typeof said.step !== 'string' || !said.step) continue;
    const at = Number(said.at) || 0;
    if (said.ms === undefined) {
      open.set(said.step, at);
      if (!steps.has(said.step)) steps.set(said.step, { step: said.step, ms: 0 });
      continue;
    }
    open.delete(said.step);
    const before = steps.get(said.step)?.ms ?? 0;
    steps.set(said.step, { step: said.step, ms: before + (Number(said.ms) || 0) });
  }
  for (const [name, at] of open) {
    const step = steps.get(name) as Step;
    steps.set(name, { step: name, ms: step.ms + (endedAt === null ? 0 : Math.max(0, endedAt - at)), open: true });
  }
  return [...steps.values()].filter((step) => step.ms > 0 || step.open);
}

/** The steps as one line people read: each with its time, the one still running marked so. */
export const stepsLine = (steps: readonly Step[]): string => steps.map((step) => `${step.step} ${seconds(step.ms)}${step.open ? ' (running when it ended)' : ''}`).join(', ');

/** The time the run spent outside its tests: every step but a suite's run. */
export const outsideTests = (steps: readonly Step[]): number => steps.filter((step) => !step.step.startsWith('run ')).reduce((sum, step) => sum + step.ms, 0);
