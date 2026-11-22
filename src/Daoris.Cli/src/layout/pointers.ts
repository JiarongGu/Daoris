// The retired pointer: the import region `sync` once wrote into `CLAUDE.md`, at the root and in each declared room.
//
// Every agent in use reads `AGENTS.md` itself, and Claude Code reads it where no `CLAUDE.md` exists
// (`docs/2026-11-22-instruction-file-evidence.md`), so `sync` writes no pointer and takes out the one it wrote.
// This module only reads and decides; the write is `syncapply.ts`'s.

import type { Manifest, PointerPlan } from '../doctrine/records.ts';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { retireImport } from '../doctrine/region.ts';
import { DaorisError } from '../platform/errors.ts';
import { isFile } from './layout.ts';

/** The file an earlier `sync` wrote its import region into. */
export const POINTER_FILE = 'CLAUDE.md';

/** Every place a pointer is looked for: the root, then each declared room. */
export const pointerPaths = (manifest: Pick<Manifest, 'rooms'>): string[] =>
  [POINTER_FILE, ...(manifest.rooms ?? []).map((room) => `${room}/${POINTER_FILE}`)];

/** The pointer files as they stand, decided. A damaged marker throws here, before anything is written. */
export function planPointers({ root, manifest }: { root: string; manifest: Pick<Manifest, 'rooms'> }): PointerPlan {
  const plan: PointerPlan = { retire: [], kept: [] };
  for (const path of pointerPaths(manifest)) {
    const abs = join(root, path);
    if (!isFile(abs)) continue;
    let verdict;
    try {
      verdict = retireImport(readFileSync(abs, 'utf8'));
    } catch (error) {
      if (error instanceof DaorisError) throw new DaorisError(`${path}: ${error.message}`, error.exitCode);
      throw error;
    }
    if (verdict === 'remove') plan.retire.push(path);
    else if (verdict === 'kept') plan.kept.push(path);
  }
  return plan;
}
