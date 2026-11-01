/**
 * A workflow draft's fingerprint (FLOWCHART2d, D223; the flowchart design §8.7): the digest of exactly the graph the editor holds, which
 * its judgement keys on and Ask Daoris's draft door proposes against. Not a secret: an identity for this draft's state.
 */

/** A JSON value with each object's keys in order and each array as it stands: the same graph is one text whoever wrote it. */
export function canonicalJson(value: unknown): string {
  if (Array.isArray(value)) return `[${value.map((each) => canonicalJson(each ?? null)).join(',')}]`;
  if (value !== null && typeof value === 'object') {
    const entries = Object.entries(value as Record<string, unknown>).filter(([, each]) => each !== undefined);
    entries.sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0));
    return `{${entries.map(([key, each]) => `${JSON.stringify(key)}:${canonicalJson(each)}`).join(',')}}`;
  }
  return JSON.stringify(value ?? null);
}

const OFFSET = 0xcbf29ce484222325n;
const PRIME = 0x100000001b3n;
const MASK = 0xffffffffffffffffn;

/** The 64-bit FNV-1a hash of a text's UTF-16 code units. */
function fnv1a64(text: string): bigint {
  let hash = OFFSET;
  for (let at = 0; at < text.length; at++) {
    hash ^= BigInt(text.charCodeAt(at));
    hash = (hash * PRIME) & MASK;
  }
  return hash;
}

/** The fingerprint of a draft's graph: `g1-` and sixteen hex digits. */
export function fingerprintOf(graph: unknown): string {
  return `g1-${fnv1a64(canonicalJson(graph)).toString(16).padStart(16, '0')}`;
}
