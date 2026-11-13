/**
 * The verdict store's key and its exact-input reuse. A verdict is keyed by the gate, the digest of the inputs that reach it, its
 * configuration (the gate's declaration) and the toolchain; a pass stands for another tree only where all three digests are
 * equal. The records are `local/gate-verdicts.json`'s, the one store; this module only builds keys and judges them.
 */
import { createHash } from 'node:crypto';

export type GateDeclaration = { name: string; run: string; memoryGb?: number; clock?: boolean; regenerate?: string; reuse?: string };
export type TreeEntry = { path: string; blob: string };
export type Toolchain = { node: string; dotnet: string };
export type VerdictKey = { inputs: string; config: string; toolchain: string };
export type StoredVerdict = { gate: string; verdict: string; tree: string; at: string; key?: VerdictKey; commit?: string | null };
export type Replay = { from: string; at: string; note: string };

const sha = (text: string): string => createHash('sha1').update(text).digest('hex');

/** The digest of the tree's entries the gate reaches (`reaching` names them among the paths), in path order, with `extra` beside. */
export function inputsDigest(entries: TreeEntry[], reaching: (paths: string[]) => string[], extra = ''): string {
  const reached = new Set(reaching(entries.map((entry) => entry.path)));
  const lines = entries.filter((entry) => reached.has(entry.path)).map((entry) => `${entry.path}\0${entry.blob}`).sort();
  return sha([...lines, `extra\0${extra}`].join('\n'));
}

/** The digest of what the declaration says the gate is: its command and every setting that shapes the run. */
export function configDigest(gate: GateDeclaration): string {
  return sha(JSON.stringify([gate.name, gate.run, gate.memoryGb ?? null, gate.clock ?? false, gate.regenerate ?? null, gate.reuse ?? null]));
}

export const toolchainDigest = (toolchain: Toolchain): string => sha(`${toolchain.node}\0${toolchain.dotnet}`);

/**
 * The key a gate's verdict is given on this tree. `extra` is whatever else the gate's answer depends on beyond the tree (the
 * changed paths a reached run chooses its units by) and `reachTable` the digest of the table that placed the inputs.
 */
export function keyOf({ gate, entries, reaching, toolchain, extra = '', reachTable = '' }: {
  gate: GateDeclaration; entries: TreeEntry[]; reaching: (paths: string[]) => string[]; toolchain: Toolchain; extra?: string; reachTable?: string;
}): VerdictKey {
  return { inputs: inputsDigest(entries, reaching, `${extra}\0${reachTable}`), config: configDigest(gate), toolchain: toolchainDigest(toolchain) };
}

const sameKey = (a: VerdictKey | undefined, b: VerdictKey): boolean => a !== undefined && a.inputs === b.inputs && a.config === b.config && a.toolchain === b.toolchain;

/**
 * The standing pass a verdict can be replayed from: the newest verdict of this key, if it passed. A failure of the same key given
 * after a pass withdraws it. FLAKE is a pass said, and is run again, never replayed.
 */
export function replayFor(stored: StoredVerdict[], key: VerdictKey): Replay | null {
  const same = stored.filter((entry) => sameKey(entry.key, key));
  const newest = same.at(-1);
  if (!newest || newest.verdict !== 'PASS') return null;
  return {
    from: newest.tree,
    at: newest.at,
    note: `replayed from tree ${newest.tree} at ${newest.at}${newest.commit ? ` (run of ${newest.commit.slice(0, 7)})` : ''}: its reached inputs, command and toolchain are unchanged`,
  };
}
