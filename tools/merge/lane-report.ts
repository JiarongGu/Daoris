/**
 * The lanes a branch touched, by id and title, from the repository's `daoris.lanes.json`, naming the steward's records if it edited
 * them. It informs; it never refuses on a lane.
 */
import { branchChanges } from './changes.ts';
import { count, pad } from './format.ts';
import { LANES_FILE, classify, readLanes, unionRecords } from './lanes.ts';
import type { Lane } from './lanes.ts';

/** A lane's row label: its id, then its title, so a lane reads as a dispatch prompt names it. */
const laneLabel = (lane: Pick<Lane, 'id' | 'title'>): string => `${pad(lane.id, 14)}${lane.title}`;

export function laneReport(root: string, branch: string): string[] {
  const changed = branchChanges(root, branch);
  const map = readLanes(root);
  const out = [`lanes: ${branch} changes ${count(changed.length, 'file')}`];
  if (!map || map.lanes.length === 0) {
    out.push(`  (${map ? `${LANES_FILE} declares no lanes` : `no ${LANES_FILE} here`})`);
    return out;
  }
  const placed = classify(changed, { ...map, union: unionRecords(root) });
  const steward = map.lanes.find((lane) => lane.steward);
  const row = (label: string, text: string): string => `  ${pad(label, 32)} ${text}`;
  const list = (files: string[], max = 6): string => `${files.slice(0, max).join(', ')}${files.length > max ? `, … ${files.length - max} more` : ''}`;
  for (const lane of placed.lanes) out.push(row(laneLabel(lane), count(lane.files.length, 'file')));
  if (placed.steward.length && steward) out.push(row(laneLabel(steward), `${count(placed.steward.length, 'file')} (the steward's)`));
  if (placed.shared.length) out.push(row('shared records', `${count(placed.shared.length, 'file')}: ${list(placed.shared)} (merge by union)`));
  if (placed.laneless.length) out.push(row('no lane', `${count(placed.laneless.length, 'file')}: ${list(placed.laneless)} (the map declares them laneless)`));
  if (placed.outside.length) out.push(row('outside every lane', `${count(placed.outside.length, 'file')}: ${list(placed.outside)} (the map places no such path; the lanes test refuses it)`));
  if (placed.lanes.length > 1) out.push(`  note: it crosses ${placed.lanes.length} lanes`);
  if (placed.steward.length) out.push(`  note: it edits the steward's records (${placed.steward.join(', ')}); a subagent never does`);
  return out;
}
