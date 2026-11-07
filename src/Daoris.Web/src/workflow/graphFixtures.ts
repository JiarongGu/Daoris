// Two shipped workflows' graphs, for the drawing's stories. Each equals its file in the driver's `Defaults/workflows`, which
// `graphFlow.test.ts` asserts, so a changed default fails there and these are changed with it.

/** `release.json`: the work, its holds, the checks, your review, the landing and the line, then a pending release found, recorded and copied. */
export const RELEASE_GRAPH = {
  schemaVersion: 2,
  entries: ['work'],
  end: 'line',
  nodes: [
    { id: 'work', kind: 'claimed', version: 1, reach: 'observed' },
    { id: 'requests', kind: 'work.requests', version: 1, reach: 'observed' },
    { id: 'holds', kind: 'quest.holds', version: 1, reach: 'observed' },
    { id: 'content', kind: 'content.holds', version: 1, reach: 'observed' },
    { id: 'check', kind: 'check', version: 1, reach: 'performed' },
    { id: 'review', kind: 'review', version: 1, reach: 'pressed' },
    { id: 'landing', kind: 'land', version: 1, reach: 'performed', options: { method: 'merge' } },
    { id: 'line', kind: 'line', version: 1, reach: 'observed' },
    {
      id: 'find', kind: 'local-release/find', version: 1, reach: 'performed',
      outcomes: ['release', 'none'],
      outputs: { release: 'local-release/release@1' },
      outputsOn: { release: ['release'] },
      evidence: 'local-release/receipt@1',
    },
    {
      id: 'release', kind: 'local-release/release', version: 1, reach: 'performed',
      inputs: { release: 'local-release/release@1' },
      outputs: { release: 'local-release/release@1' },
      evidence: 'local-release/receipt@1',
    },
    {
      id: 'publish', kind: 'local-release/publish', version: 1, reach: 'performed',
      inputs: { release: 'local-release/release@1' },
      evidence: 'local-release/receipt@1',
    },
  ],
  wires: [
    { id: 'a', kind: 'after', from: 'work', to: 'requests' },
    { id: 'l', kind: 'after', from: 'requests', to: 'holds' },
    { id: 'm', kind: 'after', from: 'holds', to: 'content' },
    { id: 'b', kind: 'after', from: 'content', to: 'check' },
    { id: 'c', kind: 'after', from: 'check', to: 'review' },
    { id: 'd', kind: 'after', from: 'review', to: 'landing' },
    { id: 'e', kind: 'after', from: 'landing', to: 'line' },
    { id: 'f', kind: 'back', from: 'review', on: 'rejected', to: 'work' },
    { id: 'g', kind: 'after', from: 'line', to: 'find' },
    { id: 'h', kind: 'when', from: 'find', outcome: 'release', to: 'release' },
    { id: 'i', kind: 'data', from: 'find.release', to: 'release.release' },
    { id: 'j', kind: 'after', from: 'release', to: 'publish' },
    { id: 'k', kind: 'data', from: 'release.release', to: 'publish.release' },
  ],
};

/**
 * A decision whose two outcomes reach one step: each leaves its own port and stays a way of
 * its own, and the third goes nowhere. Not shipped; it stands beside the shipped graphs for the drawing's proof.
 */
export const TWO_OUTCOMES_GRAPH = {
  schemaVersion: 2,
  entries: ['work'],
  end: 'line',
  nodes: [
    { id: 'work', kind: 'claimed', version: 1, reach: 'observed' },
    {
      id: 'find', kind: 'local-release/find', version: 1, reach: 'performed',
      outcomes: ['release', 'again', 'none'],
      outputs: { release: 'local-release/release@1' },
      outputsOn: { release: ['release', 'again'] },
      evidence: 'local-release/receipt@1',
    },
    { id: 'record', kind: 'local-release/release', version: 1, reach: 'performed', evidence: 'local-release/receipt@1' },
    { id: 'line', kind: 'line', version: 1, reach: 'observed' },
  ],
  wires: [
    { id: 'a', kind: 'after', from: 'work', to: 'find' },
    { id: 'b', kind: 'when', from: 'find', outcome: 'release', to: 'record' },
    { id: 'c', kind: 'when', from: 'find', outcome: 'again', to: 'record' },
    { id: 'd', kind: 'after', from: 'record', to: 'line' },
  ],
};

/** `merge-after-accept.json`: the work, its holds, your review, the landing and the line. */
export const MERGE_GRAPH = {
  schemaVersion: 2,
  entries: ['work'],
  end: 'line',
  nodes: [
    { id: 'work', kind: 'claimed', version: 1, reach: 'observed' },
    { id: 'requests', kind: 'work.requests', version: 1, reach: 'observed' },
    { id: 'holds', kind: 'quest.holds', version: 1, reach: 'observed' },
    { id: 'content', kind: 'content.holds', version: 1, reach: 'observed' },
    { id: 'review', kind: 'review', version: 1, reach: 'pressed' },
    { id: 'landing', kind: 'land', version: 1, reach: 'performed', options: { method: 'merge' } },
    { id: 'line', kind: 'line', version: 1, reach: 'observed' },
  ],
  wires: [
    { id: 'a', kind: 'after', from: 'work', to: 'requests' },
    { id: 'h', kind: 'after', from: 'requests', to: 'holds' },
    { id: 'i', kind: 'after', from: 'holds', to: 'content' },
    { id: 'b', kind: 'after', from: 'content', to: 'review' },
    { id: 'e', kind: 'after', from: 'review', to: 'landing' },
    { id: 'c', kind: 'after', from: 'landing', to: 'line' },
  ],
};
