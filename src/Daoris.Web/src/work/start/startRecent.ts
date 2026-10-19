import { HELP_REPOSITORY, type Session } from '../../data/api';
import { store, stored } from '../../lib/stored';
import { sessionOrigin } from '../identity';

// What this machine remembers of its starts (NEW1b, the entry design §1): the repositories its last conversations started in,
// for the picker's *Recent*, and the one last chosen, which a new conversation opens on. Per viewer, like a draft (D42): never
// machine wiring and never shared, and a storage that is absent or refused costs the memory and nothing else.

/** Where the last starts are kept: repository names, newest first. */
export const RECENT_STARTS = 'daoris.start.recent';

/** Where the repository last chosen is kept. */
export const CHOSEN_START = 'daoris.start.chosen';

/** How many recent starts the picker lists: a few, since the workspaces' groups follow. */
export const MAX_RECENT = 5;

/** The last starts' repositories, newest first, or none when what is kept is not a list of names. */
export function readRecent(): string[] {
  try {
    const held: unknown = JSON.parse(stored(RECENT_STARTS) ?? '[]');
    return Array.isArray(held)
      ? held.filter((name): name is string => typeof name === 'string' && name.length > 0).slice(0, MAX_RECENT)
      : [];
  } catch {
    return [];
  }
}

/** The recent starts with one more, newest first, none twice, at most `MAX_RECENT`. Pure. */
export function withRecent(recent: readonly string[], repository: string): string[] {
  return [repository, ...recent.filter((name) => name !== repository)].slice(0, MAX_RECENT);
}

/**
 * The picker's *Recent* (NEW1f): the composer's own starts merged with the repositories of this machine's own sessions, newest
 * first by the repository's newest session `updated` (parsed, not compared as text), one entry a repository, at most
 * `MAX_RECENT`. A start has no time of its own: it is dated by its repository's sessions (none left, none), and on a tie the
 * composer's starts come first, in their order. Ask Daoris's chats and a session from another machine are no place the person
 * worked here; an ended session counts. Pure, and nothing is written: the sessions are the record.
 */
export function mergeRecent(
  starts: readonly string[], sessions: readonly Pick<Session, 'id' | 'repository' | 'updated'>[],
): string[] {
  const newest = new Map<string, number>();
  for (const session of sessions) {
    if (session.repository === HELP_REPOSITORY || sessionOrigin(session) !== null) continue;
    const time = Date.parse(session.updated);
    newest.set(session.repository, Math.max(newest.get(session.repository) ?? 0, Number.isNaN(time) ? 0 : time));
  }
  // Starts take the first ranks, so a stable sort keeps them ahead where the times are equal.
  const names = [...new Set([...starts, ...newest.keys()])];
  return names.sort((a, b) => (newest.get(b) ?? 0) - (newest.get(a) ?? 0)).slice(0, MAX_RECENT);
}

/** Remember a start that opened a conversation, and give back the list as it now stands. */
export function keepRecent(repository: string): string[] {
  const next = withRecent(readRecent(), repository);
  store(RECENT_STARTS, JSON.stringify(next));
  return next;
}

/** The repository last chosen, or null. */
export function readChosen(): string | null {
  return stored(CHOSEN_START) || null;
}

/** Remember the repository chosen. */
export function keepChosen(repository: string): void {
  store(CHOSEN_START, repository);
}
