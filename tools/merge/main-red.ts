/**
 * Which of a merge's failures are main's own. A red main already stands in under the same signature never fails a branch's merge:
 * it is named in the summary and stays owed to main. Only a failure the branch brings fails the merge.
 */

export type Failure = { gate: string; unit: string; key: string };
export type MainRed = { gate: string; key: string; from: 'finding' | 'verdict'; at: string; commit: string | null };
export type Held = Failure & { from: MainRed['from']; at: string; commit: string | null };
export type Attribution = { main: Held[]; branch: Failure[] };

type Finding = { state: string; gate?: string | undefined; test: string; key: string; expired?: boolean; lastSeen?: { commit?: string | null } | null; at?: string };
type GateVerdict = { gate: string; verdict: string; tree: string; at: string; commit?: string | null; signatures?: string[] };

const label = (failure: Failure): string => failure.unit;

/** Each failure set down as main's where main stands in the same gate and signature, the rest the branch's. */
export function attribute(failures: Failure[], mains: MainRed[]): Attribution {
  const main: Held[] = [];
  const branch: Failure[] = [];
  for (const failure of failures) {
    const found = mains.find((each) => each.gate === failure.gate && each.key === failure.key);
    if (found) main.push({ ...failure, from: found.from, at: found.at, commit: found.commit });
    else branch.push(failure);
  }
  return { main, branch };
}

/** A gate's outcome: `main` when every failure it has is main's, else `branch`. A failure no one could read is the branch's. */
export function gateOutcome(failures: Failure[], mains: MainRed[]): 'main' | 'branch' {
  if (failures.length === 0) return 'branch';
  return attribute(failures, mains).branch.length === 0 ? 'main' : 'branch';
}

/** The open, unexpired reds the findings record (the CI's and the queue's) stands for on the line. */
export function findingReds(findings: Finding[]): MainRed[] {
  return findings
    .filter((each) => each.state === 'red' && each.expired !== true && typeof each.gate === 'string')
    .map((each) => ({ gate: each.gate as string, key: each.key, from: 'finding' as const, at: each.at ?? '', commit: each.lastSeen?.commit ?? null }));
}

/** The signatures of main's failing verdicts on its own tree, a gate's newest verdict there deciding whether it still stands. */
export function verdictReds(verdicts: GateVerdict[], mainTree: string | null): MainRed[] {
  if (!mainTree) return [];
  const newest = new Map<string, GateVerdict>();
  for (const each of verdicts) if (each.tree === mainTree) newest.set(each.gate, each);
  return [...newest.values()].filter((each) => each.verdict === 'FAIL')
    .flatMap((each) => (each.signatures ?? []).map((key) => ({ gate: each.gate, key, from: 'verdict' as const, at: each.at, commit: each.commit ?? null })));
}

/** The summary's lines for the reds set down as main's. */
export function mainRedLines(held: Held[]): string[] {
  if (held.length === 0) return [];
  return [
    ...held.map((each) => `  ${each.gate}: ${label(each)} is main's red, seen at ${each.commit ?? 'main'} on ${each.at}; it does not fail this merge`),
    '  main\'s reds stay owed to main: this merge neither fixes nor hides them.',
  ];
}
