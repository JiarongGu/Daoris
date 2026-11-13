/**
 * The repository's lane map as the merge reads it: `daoris.lanes.json` checked and read, the records `.gitattributes` merges
 * on its own, and where each changed path belongs.
 */
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { Refusal } from './args.ts';
import { globToRegExp, laneMatcher } from './lanes-file.ts';
import { readDeclared } from './plan.ts';

/** The repository's lanes, at its root beside `daoris.json` and `daoris.gates.json`. */
export const LANES_FILE = 'daoris.lanes.json';

/** One lane as the map reads it. */
export type Lane = { id: string; title: string; summary: string; steward: boolean; paths: string[]; gates?: string[] };

/** The map: its lanes, and the paths it declares laneless. */
export type LaneMap = { lanes: Lane[]; laneless: string[] };

/** How a quest will address a lane (`repository:lane`), so an id is one word of a known alphabet. */
const LANE_ID = /^[a-z][a-z0-9-]*$/;

type Raw = { id?: unknown; paths?: unknown; gates?: unknown; steward?: unknown; title?: unknown; summary?: unknown } | null | undefined;

/**
 * Why a parsed lanes file cannot be read, one line each, or none. Absence is a rule: no
 * `lanes` and an empty list both mean no lanes. A malformed or repeated id, a lane that owns nothing,
 * two stewards, and a `gates` entry naming no gate in `gateNames` (the declared set) each make it
 * unreadable. Fields it has no rule for are left alone.
 */
export function lanesProblems(parsed: unknown, gateNames: readonly string[] = []): string[] {
  if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) return ['the file is not a JSON object'];
  const file = parsed as { laneless?: unknown; lanes?: unknown };
  const problems: string[] = [];
  if (file.laneless !== undefined && !Array.isArray(file.laneless)) problems.push("'laneless' is not a list");
  if (file.lanes === undefined) return problems;
  if (!Array.isArray(file.lanes)) return [...problems, "'lanes' is not a list"];
  const lanes = file.lanes as Raw[];
  const seen = new Set<string>();
  lanes.forEach((lane, i) => {
    const id = lane?.id;
    const name = typeof id === 'string' ? `lane '${id}'` : `lane ${i + 1}`;
    if (typeof id !== 'string' || !LANE_ID.test(id)) {
      problems.push(`${name}: its id must be lower-case letters, digits and dashes, starting with a letter`);
    } else if (seen.has(id)) problems.push(`${name}: the id is used twice`);
    else seen.add(id);
    const paths = Array.isArray(lane?.paths) ? lane.paths : [];
    if (!paths.some((path) => typeof path === 'string' && path && !path.startsWith('!'))) problems.push(`${name}: it has no paths`);
    if (lane?.gates !== undefined) {
      if (!Array.isArray(lane.gates)) problems.push(`${name}: 'gates' is not a list`);
      else {
        for (const gate of (lane.gates as string[]).filter((entry) => !gateNames.includes(entry))) {
          problems.push(`${name}: its gate '${gate}' is not declared in daoris.gates.json`);
        }
      }
    }
  });
  const stewards = lanes
    .map((lane, i) => (lane?.steward === true ? (typeof lane.id === 'string' ? lane.id : `lane ${i + 1}`) : null))
    .filter(Boolean);
  if (stewards.length > 1) problems.push(`two stewards (${stewards.join(', ')}): at most one lane keeps the records`);
  return problems;
}

/**
 * `daoris.lanes.json`, or null where a repository has none. An unreadable file is refused, naming each
 * problem. `laneless` is read flat: its groups are for the person reading the map, each saying why its
 * paths have no lane.
 */
export function readLanes(root: string): LaneMap | null {
  const file = join(root, LANES_FILE);
  if (!existsSync(file)) return null;
  let parsed: unknown;
  try {
    parsed = JSON.parse(readFileSync(file, 'utf8'));
  } catch (error) {
    throw new Refusal(`${LANES_FILE} is not valid JSON: ${(error as Error).message}`);
  }
  const gateNames = existsSync(join(root, 'daoris.gates.json')) ? readDeclared(root).map((gate) => gate.name) : [];
  const problems = lanesProblems(parsed, gateNames);
  if (problems.length) throw new Refusal(`${LANES_FILE} cannot be read:\n${problems.map((problem) => `  ${problem}`).join('\n')}`);
  const map = parsed as { lanes?: Lane[]; laneless?: { paths?: string[] }[] };
  return {
    lanes: (map.lanes ?? []).map((lane) => ({
      id: lane.id,
      title: typeof lane.title === 'string' ? lane.title : '',
      summary: typeof lane.summary === 'string' ? lane.summary : '',
      steward: lane.steward === true,
      paths: lane.paths,
      ...(lane.gates ? { gates: lane.gates } : {}),
    })),
    laneless: (map.laneless ?? []).flatMap((group) => group?.paths ?? []),
  };
}

/**
 * The paths `.gitattributes` marks `merge=union`, the router it marks `merge=rows` and the
 * decisions it marks `merge=notes`: the records that resolve themselves. The router stays a
 * record when its driver changes, so it is read beside the union ones.
 */
export function unionRecords(root: string): string[] {
  const file = join(root, '.gitattributes');
  if (!existsSync(file)) return [];
  return readFileSync(file, 'utf8').replace(/\r\n/g, '\n').split('\n')
    .map((line) => /^(\S+)\s(?:.*\s)?merge=(?:union|rows|notes)(?:\s|$)/.exec(line)?.[1])
    .filter((pattern): pattern is string => Boolean(pattern) && !pattern!.startsWith('#'));
}

/** A `.gitattributes` pattern with no slash matches at any depth, as git reads it. */
export const attributePattern = (pattern: string): RegExp => (pattern.includes('/')
  ? globToRegExp(pattern.replace(/^\//, ''))
  : new RegExp(`^(?:.*/)?${globToRegExp(pattern).source.slice(1)}`));

/** Where each changed path belongs: the lanes touched, each with its files, and the other places a path can be. */
export type Placed = {
  lanes: { id: string; title: string; files: string[] }[]; steward: string[]; shared: string[]; laneless: string[]; outside: string[];
};

/**
 * Where each changed path belongs, in order: the steward's lane first (its records, which a
 * subagent never edits, the parent's list), then the records that merge by union, then the
 * first other lane that owns it, then the paths the map declares laneless (the docs, the doctrine),
 * else outside every lane: a path the map does not place, which the lanes test refuses. Lanes
 * come back by id and title in the map's order, only those touched; the steward's paths come back on
 * their own, where the parent's did.
 */
export function classify(
  paths: readonly string[],
  { lanes = [], union = [], laneless = [] }: { lanes?: readonly Lane[]; union?: readonly string[]; laneless?: readonly string[] } = {},
): Placed {
  const steward = lanes.find((lane) => lane.steward);
  const stewardOwns = steward ? laneMatcher(steward.paths) : () => false;
  const lanePatterns = lanes.filter((lane) => lane !== steward)
    .map((lane) => ({ id: lane.id, title: lane.title, owns: laneMatcher(lane.paths), files: [] as string[] }));
  const unionPatterns = union.map(attributePattern);
  const declared = laneMatcher([...laneless]);
  const placed: Placed = { lanes: [], steward: [], shared: [], laneless: [], outside: [] };
  for (const path of paths) {
    if (stewardOwns(path)) placed.steward.push(path);
    else if (unionPatterns.some((pattern) => pattern.test(path))) placed.shared.push(path);
    else {
      const lane = lanePatterns.find((candidate) => candidate.owns(path));
      if (lane) lane.files.push(path);
      else if (declared(path)) placed.laneless.push(path);
      else placed.outside.push(path);
    }
  }
  placed.lanes = lanePatterns.filter((lane) => lane.files.length).map(({ id, title, files }) => ({ id, title, files }));
  return placed;
}
