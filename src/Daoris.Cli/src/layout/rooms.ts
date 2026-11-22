// Rooms: a folder whose `AGENTS.md` tells an agent where it is.
//
// A room's text is the repository's own — its package's conventions, traps and gates, which Daoris
// cannot know and never writes. What `sync` keeps of a room is a row for it in the roster, and it refuses a
// declared room that has no text. Every agent in use reads a room's `AGENTS.md` itself
// (`docs/2026-11-22-instruction-file-evidence.md`), so a room has no pointer. Declared in the manifest
// rather than found: the CLI never runs git, and a walk of the tree meets build output and worktrees.

import type { Manifest, RoomPlan } from '../doctrine/records.ts';
import { join } from 'node:path';
import { readText } from '../platform/fsx.ts';
import { firstHeading } from '../doctrine/document.ts';
import { isFile } from './layout.ts';

/** The instruction file a room holds. */
export const ROOM_FILE = 'AGENTS.md';

/** The declared rooms with no instructions of their own, decided. */
export function planRooms({ root, manifest }: { root: string; manifest: Pick<Manifest, 'rooms'> }): RoomPlan {
  // `readManifest` always sets it; a manifest built by hand for one question may not.
  return { missing: (manifest.rooms ?? []).filter((room) => !isFile(join(root, room, ROOM_FILE))).map((room) => `${room}/${ROOM_FILE}`) };
}

/**
 * A room's first heading, for its row in the roster: telling rather than loading, so the
 * row says what the room is about and the agent reads it when it works there. Read as `firstHeading`
 * reads one, with its pipes escaped for the table; null when the file or a heading is missing.
 */
export function roomHeading(root: string, room: string): string | null {
  const abs = join(root, room, ROOM_FILE);
  if (!isFile(abs)) return null;
  return firstHeading(readText(abs))?.replace(/\|/g, '\\|') ?? null;
}

/** Each declared room with its heading, as the roster renders it. */
export const roomRows = (root: string, rooms: readonly string[]) =>
  rooms.map((path) => ({ path, heading: roomHeading(root, path) }));
