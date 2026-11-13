/**
 * Which of a merge's failures are main's own. A failure main already stands in, the same test under the same signature, never
 * fails a branch's merge: it is named in the summary and stays owed to main. A failure read with no test (a gate's run read
 * whole, by one line of its output) is always the branch's, and a finding that names no test excuses nothing: a line such as
 * "2 tests failed" says nothing of which test, so a new test's failure would pass under it. Only a failure the branch brings
 * fails the merge.
 */

export type Failure = { gate: string; unit: string; key: string; test: string | null };
export type MainRed = { gate: string; key: string; test: string | null; from: 'finding' | 'verdict'; at: string; commit: string | null; stale: boolean };
export type Held = Failure & { from: MainRed['from']; at: string; commit: string | null; stale: boolean };
export type Attribution = { main: Held[]; branch: Failure[] };

type Finding = {
  state: string; gate?: string | undefined; test: string; key: string; expired?: boolean; since?: string;
  lastSeen?: { commit?: string | null; at?: string } | null;
};
type GateVerdict = { gate: string; verdict: string; tree: string; at: string; commit?: string | null; signatures?: string[] };

/** A finding last seen longer ago than this, never cleared since, is stale: main's CI has not looked at its test again. */
export const STALE_FINDING_MS = 24 * 60 * 60 * 1000;

/** The steps a gate's own signature names in its second field, where a test's names its exception. */
const STEPS = new Set(['run', 'build']);

/** The test a failure's signature key names (its first field), or null for a gate's key, read whole from its run. */
export function keyTest(key: string): string | null {
  try {
    const parsed: unknown = JSON.parse(key);
    if (!Array.isArray(parsed) || typeof parsed[0] !== 'string' || STEPS.has(String(parsed[1]))) return null;
    return parsed[0];
  } catch {
    return null;
  }
}

const excuses = (red: MainRed, failure: Failure): boolean =>
  failure.test !== null && red.test === failure.test && red.gate === failure.gate && red.key === failure.key;

/** Each failure set down as main's where main stands in the same test, gate and signature, the rest the branch's. */
export function attribute(failures: Failure[], mains: MainRed[]): Attribution {
  const main: Held[] = [];
  const branch: Failure[] = [];
  for (const failure of failures) {
    const found = mains.find((each) => excuses(each, failure));
    if (found) main.push({ ...failure, from: found.from, at: found.at, commit: found.commit, stale: found.stale });
    else branch.push(failure);
  }
  return { main, branch };
}

/** A gate's outcome: `main` when every failure it has is main's, else `branch`. A failure no one could read is the branch's. */
export function gateOutcome(failures: Failure[], mains: MainRed[]): 'main' | 'branch' {
  if (failures.length === 0) return 'branch';
  return attribute(failures, mains).branch.length === 0 ? 'main' : 'branch';
}

/** The open, unexpired reds the findings record (the CI's and the queue's) stands for on the line, as at `now`. */
export function findingReds(findings: Finding[], now = Date.now()): MainRed[] {
  return findings.flatMap((each) => {
    if (each.state !== 'red' || each.expired === true || typeof each.gate !== 'string') return [];
    const at = each.lastSeen?.at ?? each.since ?? '';
    const seen = Date.parse(at);
    return [{
      gate: each.gate, key: each.key, test: each.test === each.gate ? null : each.test, from: 'finding' as const, at,
      commit: each.lastSeen?.commit ?? null, stale: Number.isFinite(seen) && now - seen > STALE_FINDING_MS,
    }];
  });
}

/** The signatures of main's failing verdicts on its own tree, a gate's newest verdict there deciding whether it still stands. */
export function verdictReds(verdicts: GateVerdict[], mainTree: string | null): MainRed[] {
  if (!mainTree) return [];
  const newest = new Map<string, GateVerdict>();
  for (const each of verdicts) if (each.tree === mainTree) newest.set(each.gate, each);
  return [...newest.values()].filter((each) => each.verdict === 'FAIL').flatMap((each) => (each.signatures ?? []).map((key) => ({
    gate: each.gate, key, test: keyTest(key), from: 'verdict' as const, at: each.at, commit: each.commit ?? null, stale: false,
  })));
}

const hours = (ms: number): number => Math.round(ms / 3_600_000);

/** The summary's lines for the reds set down as main's. */
export function mainRedLines(held: Held[]): string[] {
  if (held.length === 0) return [];
  return [
    ...held.map((each) => `  ${each.gate}: ${each.test ?? each.unit} is main's red, last seen at ${each.commit ?? 'main'} on ${each.at}; it does not fail this merge${
      each.stale ? `; stale: main's CI has not seen it for over ${hours(STALE_FINDING_MS)} h, nor a pass to clear it` : ''}`),
    '  main\'s reds stay owed to main: this merge neither fixes nor hides them.',
  ];
}

/** One line for each failed gate on which main's findings name no test, which therefore excuse none of its failures. */
export function unspecificLines(mains: MainRed[], failedGates: string[]): string[] {
  return failedGates.flatMap((gate) => {
    const count = mains.filter((each) => each.from === 'finding' && each.test === null && each.gate === gate).length;
    if (count === 0) return [];
    const said = count === 1 ? 'a finding of main\'s names no test, so it excuses' : `${count} of main's findings name no test, so they excuse`;
    return [`  ${gate}: ${said} none of its failures; a red on main excuses only the same test`];
  });
}
