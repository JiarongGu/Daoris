// What a pin's commit is, read against its producer's checkout: one full commit on the producer's line. A branch, a tag
// or any other name is refused by what it names, an abbreviated id as not full, a commit the checkout does not hold as unknown, and
// one the line does not reach as off it.

import type { Finding, ReadablePin } from './family.ts';
import type { Repository } from './gitread.ts';

/** A pin that reads green, said as the check prints it. */
export interface PinOk {
  verdict: 'ok';
  message: string;
}

const short = (id: string) => id.slice(0, 12);

/**
 * The producer's line, by the checkout's guess the driver makes too (`LineGuesses`): origin's `HEAD` where the checkout is a clone,
 * else `main`, else `master`; null where it names none.
 */
export function lineOf(repository: Repository): string | null {
  const origin = repository.symbolic('refs/remotes/origin/HEAD');
  if (origin?.startsWith('refs/remotes/origin/')) return origin.slice('refs/remotes/origin/'.length);
  for (const line of ['main', 'master']) if (repository.ref(`refs/heads/${line}`)) return line;
  return null;
}

/** One pin judged against its producer's checkout, or the checkout's absence. */
export function judgePin(pin: ReadablePin, repository: Repository | null): Finding | PinOk {
  const { consumer, producer, commit } = pin;
  const edge = `${consumer} → ${producer}`;
  const rule = `a pin is one full commit on ${producer}'s line`;
  const refuse = (code: Finding['code'], message: string): Finding => ({ verdict: 'refused', code, message: `${edge}: ${message}` });

  if (!repository) return refuse('no-repository', `${producer}'s checkout is no git repository, so no pin of it can be read`);
  const line = lineOf(repository);

  if (!new RegExp(`^[0-9a-fA-F]{${repository.idLength}}$`).test(commit)) {
    if (repository.ref(`refs/heads/${commit}`) || repository.ref(`refs/remotes/origin/${commit}`)) {
      return refuse('branch', `the pin \`${commit}\` is a branch; ${rule}`);
    }
    if (repository.ref(`refs/tags/${commit}`)) return refuse('tag', `the pin \`${commit}\` is a tag; ${rule}`);
    return refuse('not-full', `the pin \`${commit}\` is not a full commit; ${rule}`);
  }

  const id = commit.toLowerCase();
  const object = repository.commitOf(id);
  if (object === null) {
    return refuse('unknown-commit', `${producer}'s checkout holds no commit ${short(id)}: fetch it, or pin a commit landed on its line`);
  }
  if (object !== id) return refuse('not-commit', `the pin ${short(id)} is a tag object; ${rule}`);
  if (!line) return refuse('no-line', `${producer}'s checkout names no line (no origin HEAD, no main, no master)`);

  const tips = [repository.ref(`refs/heads/${line}`), repository.ref(`refs/remotes/origin/${line}`)].filter((tip): tip is string => tip !== null);
  if (!repository.isAncestor(id, tips)) {
    const cut = repository.shallow ? ` (its history is shallow: fetch it whole to tell)` : '';
    return refuse('off-line', `the pin ${short(id)} is not on ${producer}'s line ${line}${cut}; ${rule}`);
  }
  return { verdict: 'ok', message: `${edge} at ${short(id)}, on ${producer}'s line ${line}` };
}
