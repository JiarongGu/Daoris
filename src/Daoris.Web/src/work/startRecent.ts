import { store, stored } from '../lib/stored';

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
