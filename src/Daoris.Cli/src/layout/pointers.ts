// The retired pointer: the import region `sync` once wrote into `CLAUDE.md`, at the root and in each declared room.
//
// Every agent in use reads `AGENTS.md` itself, and Claude Code reads it where no `CLAUDE.md` exists
// (`docs/2026-11-22-instruction-file-evidence.md`), so `sync` writes no pointer and takes out the one it wrote.
// A `CLAUDE.md` that holds text of its own and no import hides `AGENTS.md` from Claude Code, so that is named too.
// This module only reads and decides; the write is `syncapply.ts`'s.

import type { Lock, Manifest, PointerPlan } from '../doctrine/records.ts';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { hasImport, retireImport } from '../doctrine/region.ts';
import { DaorisError } from '../platform/errors.ts';
import { contained, isFile } from './layout.ts';
import { ROOM_FILE } from './rooms.ts';

/** The file an earlier `sync` wrote its import region into. */
export const POINTER_FILE = 'CLAUDE.md';

/**
 * The rooms an earlier lock recorded its pointers in, so a room taken out of the manifest in the same sync that
 * retires pointers loses its pointer too. This reads a field the current lock never writes; it ends when no lock
 * this family keeps holds `rooms` (`grep '"rooms"' daoris.lock`).
 */
function recordedRooms(lock: Lock | null | undefined): string[] {
  const held = (lock as { rooms?: unknown } | null | undefined)?.rooms;
  return Array.isArray(held) ? held.filter((room): room is string => typeof room === 'string') : [];
}

/** Every place a pointer is looked for: the root, then each declared room and each room an earlier lock recorded. */
export function pointerPaths(manifest: Pick<Manifest, 'rooms'>, lock?: Lock | null): string[] {
  const rooms = new Set([...(manifest.rooms ?? []), ...recordedRooms(lock)]);
  return [POINTER_FILE, ...[...rooms].map((room) => `${room}/${POINTER_FILE}`)];
}

/** The pointer files as they stand, decided. A damaged marker throws here, before anything is written. */
export function planPointers(
  { root, manifest, lock }: { root: string; manifest: Pick<Manifest, 'rooms'>; lock?: Lock | null },
): PointerPlan {
  const plan: PointerPlan = { retire: [], kept: [], hidden: [] };
  const declared = new Set(manifest.rooms ?? []);
  for (const path of pointerPaths(manifest, lock)) {
    const folder = path === POINTER_FILE ? '' : path.slice(0, -POINTER_FILE.length - 1);
    // A room the lock recorded is held inside the repository, like every lock path.
    const abs = folder === '' || declared.has(folder) ? join(root, path) : contained(root, '', path);
    if (!isFile(abs)) continue;
    const text = readFileSync(abs, 'utf8');
    let verdict;
    try {
      verdict = retireImport(text);
    } catch (error) {
      if (error instanceof DaorisError) throw new DaorisError(`${path}: ${error.message}`, error.exitCode);
      throw error;
    }
    if (verdict === 'remove') plan.retire.push(path);
    else if (verdict === 'kept') plan.kept.push(path);
    // Hides the brief it sits beside: the root's is the region this sync writes, a room's is its own text. An undeclared room is not the tool's to read.
    else if (!hasImport(text, ROOM_FILE) && (folder === '' || (declared.has(folder) && isFile(join(root, folder, ROOM_FILE))))) {
      plan.hidden.push(path);
    }
  }
  return plan;
}
