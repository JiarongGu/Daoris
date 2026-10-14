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

/**
 * The positional operands, in order: every token that is neither a flag nor the value of one.
 *
 * @remarks
 * `valued` names the flags that take a value, because only the command knows which of its flags do.
 * A command that counted positions instead read a flag's value as an operand whenever the flag came
 * first — `agent pin --workspace aurora claude-code 2.1.87` pinned the version `aurora`, and
 * `driver retry --at 2 42` retried quest `2` (REV3). Five hand-rolled scanners became this one.
 */
export function operands(argv: readonly string[], valued: ReadonlySet<string>): string[] {
  const found: string[] = [];
  for (let at = 0; at < argv.length; at += 1) {
    const token = argv[at]!;
    if (valued.has(token)) at += 1;
    else if (!token.startsWith('--')) found.push(token);
  }

  return found;
}
