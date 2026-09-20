// Argument parsing, in one place. Nothing here reads a file or opens a socket — which is why both an
// offline doctrine command and a management verb may import it without dragging anything along.

import { DaorisError } from './errors.ts';

/**
 * The value after a flag, refusing a flag with nothing after it.
 *
 * @remarks
 * `--workspace` followed by nothing is a mistake with a silent wrong answer available — taking the
 * default would wire the repository somewhere the person did not ask for and say it worked. A flag
 * followed by another flag is the same mistake with a typo in it.
 */
export function flagValue(argv: string[], flag: string): string | undefined {
  const at = argv.indexOf(flag);
  if (at === -1) return undefined;

  const value = argv[at + 1];
  if (!value || value.startsWith('--')) {
    throw new DaorisError(`${flag} needs a name — e.g. \`${flag} aurora\``);
  }

  return value;
}
