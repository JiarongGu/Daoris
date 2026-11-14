/**
 * What a reached run says of each class that failed: a flake (it passed when run alone again), main's red (main's CI findings or
 * class verdicts already stand it red) or a new red. The run ends with one machine line (`outcomeLine`) the merge reads back
 * (`parseOutcome`), so the record counts classes run and replayed, and a flake or main's red is named in the summary, never
 * silent.
 */

/** A class's identity in a reached run: its suite and the unit the census names (a class's full name, a test file's path). */
export type FailedUnit = { suite: string; unit: string };

/** What a reached run came to, by unit ids (`suite|unit`). */
export type ClassOutcome = { ran: number; replayed: number; flaked: string[]; mainRed: string[]; red: string[]; deferred: string[] };

const MARK = 'reached-classes: ';

export const outcomeLine = (outcome: ClassOutcome): string => `${MARK}${JSON.stringify(outcome)}`;

/** The last outcome line of a gate's log, or null where the run did not reach the end. */
export function parseOutcome(log: string): ClassOutcome | null {
  const line = log.split(/\r?\n/).reverse().find((each) => each.startsWith(MARK));
  if (!line) return null;
  try {
    const parsed = JSON.parse(line.slice(MARK.length)) as Partial<ClassOutcome>;
    const names = (value: unknown): string[] => (Array.isArray(value) ? value.filter((each): each is string => typeof each === 'string') : []);
    return { ran: Number(parsed.ran) || 0, replayed: Number(parsed.replayed) || 0, flaked: names(parsed.flaked), mainRed: names(parsed.mainRed), red: names(parsed.red), deferred: names(parsed.deferred) };
  } catch {
    return null;
  }
}

/** The failed units split by what a rerun alone and main's standing say: passed alone, main's already, or new. */
export function classify({ failed, passedAlone, mainRedOf }: {
  failed: readonly FailedUnit[]; passedAlone: ReadonlySet<string>; mainRedOf: (unit: FailedUnit) => boolean;
}): Pick<ClassOutcome, 'flaked' | 'mainRed' | 'red'> {
  const out = { flaked: [] as string[], mainRed: [] as string[], red: [] as string[] };
  for (const unit of failed) {
    const id = `${unit.suite}|${unit.unit}`;
    if (passedAlone.has(id)) out.flaked.push(id);
    else if (mainRedOf(unit)) out.mainRed.push(id);
    else out.red.push(id);
  }
  return out;
}

type Finding = { state?: string; gate?: string; unit?: string; test?: string };
type Verdicts = Record<string, Record<string, { verdict?: string }[]>>;

/**
 * Whether main already stands `unit` red: an open finding of main's CI naming it (a class by its full name, a file by its path), or
 * its newest class verdict in a gate of the suite failing. Only these two records are main's; a class main has never run is new.
 */
export function mainStandsRed(unit: FailedUnit, findings: readonly Finding[], verdicts: Verdicts): boolean {
  const name = unit.unit.replaceAll('\\', '/');
  const open = findings.some((each) => each.state === 'red' && [each.unit, each.test].some((text) => typeof text === 'string' && (text === name || text.startsWith(`${name}.`) || text.endsWith(`/${name}`))));
  if (open) return true;
  const newest = verdicts[unit.suite]?.[unit.unit]?.at(-1);
  return newest?.verdict === 'FAIL';
}
