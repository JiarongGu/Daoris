import type { KeyContext } from '../merge/class-reuse.ts';
import type { Narrowed } from '../merge/flow.ts';
import type { TestMap } from '../merge/reached-reuse.ts';

export type World = { map: TestMap; gates: unknown[]; gatesText: string; context: KeyContext };
export function storeFile(checkout: string): string;
export function keyContextOf(root: string): World;
export function fullReplayOf(options: { root: string; checkout: string; dir: string; commit: string; say?: (line: string) => void; world?: World | null }): {
  narrow(gate: { name: string; run: string; reach?: unknown }): Narrowed | null;
  settle(gate: { name: string }, result: { trx?: string; verdict: string }): void;
};
