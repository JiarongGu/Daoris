// Rooms: a folder whose `AGENTS.md` tells an agent where it is (LAYOUT3; D117 §2.2, §5.4's pointer table).
//
// A room's text is the repository's own — its package's conventions, traps and gates, which Daoris
// cannot know and never writes. What `sync` keeps is the pointer beside it, `<room>/CLAUDE.md` holding
// the import region exactly as at the root (D59 §4), because the one harness that reads that name
// follows the import and the others read the room's `AGENTS.md` themselves. Declared in the manifest
// rather than found: the CLI never runs git, and a walk of the tree meets build output and worktrees.

import type { Harness, Lock, Manifest, RoomPlan } from './types.ts';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { readText } from './fsx.ts';
import { firstHeading } from './document.ts';
import { ensureImport, findRegion, IMPORT, removeRegion } from './region.ts';
import { contained, isFile } from './layout.ts';
import { DaorisError } from './errors.ts';

/** The instruction file a room holds, and the one its pointer is written to. */
const instructionsOf = (harness: Harness) => harness.pointer?.imports ?? 'AGENTS.md';

/** A marker refusal, naming the file it is about: a room's pointer is one of many files with that name. */
function naming<T>(path: string, read: () => T): T {
  try {
    return read();
  } catch (error) {
    if (error instanceof DaorisError) throw new DaorisError(`${path}: ${error.message}`, error.exitCode);
    throw error;
  }
}

/** The pointer table of D117 §5.4, decided. Refusals on damaged markers throw here, before anything is written. */
export function planRooms(
  { root, manifest, lock, harness }: { root: string; manifest: Manifest; lock: Lock | null; harness: Harness },
): RoomPlan {
  const plan: RoomPlan = { pointers: [], unpoint: [], drop: [], missing: [], records: [] };
  const pointer = harness.pointer;
  const instructions = instructionsOf(harness);
  // `readManifest` always sets it; a manifest built by hand for one question may not.
  const declared = manifest.rooms ?? [];

  for (const room of declared) {
    plan.records.push(room);
    if (!isFile(join(root, room, instructions))) {
      plan.missing.push(`${room}/${instructions}`);
      continue;
    }
    if (!pointer) continue;
    const path = `${room}/${pointer.file}`;
    const abs = join(root, path);
    const held = isFile(abs) ? readFileSync(abs, 'utf8') : null;
    const made = naming(path, () => ensureImport(held, pointer.imports));
    plan.pointers.push({
      room, path, content: made, state: held === null ? 'create' : made === null ? 'unchanged' : 'append',
    });
  }

  for (const room of lock?.rooms ?? []) {
    if (declared.includes(room)) continue;
    const path = `${room}/${pointer?.file ?? 'CLAUDE.md'}`;
    // D18 for the lock's half: a recorded room is generated text nobody reads closely.
    const abs = contained(root, '', path);
    if (!isFile(abs)) {
      plan.drop.push(room);
      continue;
    }
    const text = readFileSync(abs, 'utf8');
    const found = naming(path, () => findRegion(text, IMPORT));
    if (found.kind === 'absent') plan.drop.push(room);
    else plan.unpoint.push({ room, path, content: naming(path, () => removeRegion(text, IMPORT)) });
  }
  return plan;
}

/**
 * A room's first heading, for its row in the roster (D117 §2.2): telling rather than loading, so the
 * row says what the room is about and the agent reads it when it works there. Read as `firstHeading`
 * reads one, with its pipes escaped for the table; null when the file or a heading is missing.
 */
export function roomHeading(root: string, room: string, harness: Harness): string | null {
  const abs = join(root, room, instructionsOf(harness));
  if (!isFile(abs)) return null;
  return firstHeading(readText(abs))?.replace(/\|/g, '\\|') ?? null;
}

/** Each declared room with its heading, as the roster renders it. */
export const roomRows = (root: string, rooms: readonly string[], harness: Harness) =>
  rooms.map((path) => ({ path, heading: roomHeading(root, path, harness) }));
