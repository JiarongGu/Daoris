// The CLI's floor on the Node that runs it, kept as plain JavaScript beside the entry for the same reason the entry is one: an older
// Node cannot strip types, so the check that refuses it cannot live in the sources it would refuse to load.

/** The oldest Node major the CLI runs on: `package.json`'s `engines`, which a test holds equal. */
export const NODE_FLOOR = 22;

/**
 * The refusal for a Node below the floor, one sentence in the CLI's own words, or `null` where it is at or above it.
 * @param {string} version the running Node's version (`process.versions.node`), a parameter so a test can stand one in.
 * @returns {string | null}
 */
export function nodeFloorRefusal(version) {
  const major = /^v?(\d+)\./.exec(version)?.[1];
  if (major !== undefined && Number(major) >= NODE_FLOOR) return null;
  return `daoris: the doctrine commands run on Node.js ${NODE_FLOOR} or later, and this is Node.js ${version}; nothing was done.`;
}
