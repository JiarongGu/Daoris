// A repository's lanes (D115 §2.1–§2.2, DEV4): the domains inside it that work is addressed to, as
// `repository:lane`. `connect` reads the repository's own `daoris.lanes.json` with this code and sends
// each lane's WORDS to the registry, never its globs: the globs are development mechanics the driver
// reads from the line, and the registration is what an asker reads to know what it may address.
//
// Twins (`.claude/knowledge/twins.md`): the service's `Declared.Lanes` keeps what it is sent by the
// same words rule, and `RegistrationStoreTests` holds the table `lanes.test.ts` holds. The file's own
// rules are the merge tool's too (`tools/merge-branch.mjs`, `lanesProblems`), in the same sentences;
// only the `gates` check is left to the queue, since connect sends no gates.

import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { DaorisError } from './errors.ts';
import { readText } from './fsx.ts';

/** The repository's lanes, at its root beside `daoris.json` (D115 §2.1). */
export const LANES_FILE = 'daoris.lanes.json';

/** How a quest addresses a lane (`repository:lane`), so an id is one word of a known alphabet. */
const LANE_ID = /^[a-z][a-z0-9-]*$/;

/** What a registration keeps of one lane: its words, never its paths. */
export interface LaneWords {
  id: string;
  title: string;
  summary: string;
  /** The one lane that keeps the repository's records (§5). Explicit on the wire, false when unsaid. */
  steward: boolean;
}

const text = (value: unknown): string => (typeof value === 'string' ? value.trim() : '');

/**
 * The words of each lane, by the rule the service's `Declared.Lanes` holds too: trimmed; an id outside
 * the alphabet dropped; a repeated id dropped, the first kept; the steward's mark kept by the first lane
 * that carries it; a missing title or summary empty. Absent is none.
 */
export function laneWords(lanes: unknown): LaneWords[] {
  const kept: LaneWords[] = [];
  if (!Array.isArray(lanes)) return kept;
  for (const lane of lanes) {
    if (lane === null || typeof lane !== 'object') continue;
    const entry = lane as Record<string, unknown>;
    const id = text(entry.id);
    if (!LANE_ID.test(id) || kept.some((other) => other.id === id)) continue;
    kept.push({
      id,
      title: text(entry.title),
      summary: text(entry.summary),
      steward: entry.steward === true && !kept.some((other) => other.steward),
    });
  }
  return kept;
}

/**
 * Why a parsed lanes file cannot be read, one line each, or none. Absence is a rule (§2.1): no `lanes`
 * and an empty list both mean no lanes. A malformed or repeated id, a lane that owns nothing, and two
 * stewards each make it unreadable. Fields it has no rule for are left alone.
 */
export function lanesProblems(parsed: unknown): string[] {
  if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) return ['the file is not a JSON object'];
  const lanes = (parsed as Record<string, unknown>).lanes;
  if (lanes === undefined) return [];
  if (!Array.isArray(lanes)) return ["'lanes' is not a list"];

  const problems: string[] = [];
  const seen = new Set<string>();
  lanes.forEach((lane, i) => {
    const entry = (lane ?? {}) as Record<string, unknown>;
    const id = entry.id;
    const name = typeof id === 'string' ? `lane '${id}'` : `lane ${i + 1}`;
    if (typeof id !== 'string' || !LANE_ID.test(id)) {
      problems.push(`${name}: its id must be lower-case letters, digits and dashes, starting with a letter`);
    } else if (seen.has(id)) problems.push(`${name}: the id is used twice`);
    else seen.add(id);
    const paths = Array.isArray(entry.paths) ? entry.paths : [];
    if (!paths.some((path) => typeof path === 'string' && path !== '' && !path.startsWith('!'))) {
      problems.push(`${name}: it has no paths`);
    }
  });
  const stewards = lanes
    .map((lane, i) => {
      const entry = (lane ?? {}) as Record<string, unknown>;
      if (entry.steward !== true) return null;
      return typeof entry.id === 'string' ? entry.id : `lane ${i + 1}`;
    })
    .filter((steward): steward is string => steward !== null);
  if (stewards.length > 1) problems.push(`two stewards (${stewards.join(', ')}): at most one lane keeps the records`);
  return problems;
}

/**
 * The words of the lanes a repository declares, or null where it has no `daoris.lanes.json`. An
 * unreadable file is refused as policy (exit 1), naming each problem: registering half of a broken
 * declaration would let a quest address a lane the repository never meant.
 */
export function readLanes(root: string): LaneWords[] | null {
  const file = join(root, LANES_FILE);
  if (!existsSync(file)) return null;

  let parsed: unknown;
  try {
    parsed = JSON.parse(readText(file));
  } catch (error) {
    throw new DaorisError(`${LANES_FILE} is not valid JSON: ${(error as Error).message}`, 1);
  }

  const problems = lanesProblems(parsed);
  if (problems.length > 0) {
    throw new DaorisError(`${LANES_FILE} cannot be read:\n${problems.map((problem) => `  ${problem}`).join('\n')}`, 1);
  }
  return laneWords((parsed as Record<string, unknown>).lanes);
}
