import type { KeptReads } from '../../bridge/shell';

// This machine's list of old records as `KEPT_READS` answers it (ONEWAY1ad-ii), for the section's tests and stories: the workflow part's census, an
// old way that has ended and one still kept for an older build, with no floor yet.

export const KEPT: KeptReads = {
  floor: { build: null, storeWriter: 3 },
  floorNeeded: null,
  lines: [
    { kind: 'part', id: 'workflows', ended: false, count: 4 },
    { kind: 'read', id: 'git-moved-key', ended: true, reaches: 'The `gitMoved` key in driver.json.', ends: 'The home holds no `gitMoved` key.', row: 'ONEWAY1af' },
    {
      kind: 'read', id: 'solicited-older-host', ended: false, reaches: 'A host with no `answers` mark.',
      ends: 'The floor passes COMMS1b\'s build.', row: 'ONEWAY1af', waits: 'the builds before COMMS1b\'s, which wrote no answers mark and no keyed attempt',
    },
  ],
};

/** The same home once the workflow part has ended, a floor read from a standing receipt, and a build that refuses the conversion's Undo. */
export const KEPT_ENDED: KeptReads = {
  floor: { build: '0.43.0', storeWriter: 3 },
  floorNeeded: '0.50.0',
  lines: [{ ...KEPT.lines[0]!, ended: true, count: 0 }, ...KEPT.lines.slice(1)],
};
