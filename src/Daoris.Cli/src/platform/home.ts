import { join } from 'node:path';
import { DaorisError } from './errors.ts';

/**
 * Where every machine-local file Daoris owns lives (D63): one home, named by `DAORIS_HOME`, and **no
 * default under the user profile**. The application's own folder is the home — the installed desktop
 * sets the variable for itself and everything it spawns — and this CLI reads the same variable from
 * the user's environment. Absent, there is no home: the management class refuses with one sentence
 * rather than writing somewhere nobody pointed it; doctrine commands never needed a home.
 *
 * One of three twins — the driver's and the service's `DaorisHome` hold the same contract in C# and
 * share no code, the way the remotes map's three copies do (WSP3). The environment is the contract.
 */

export const HOME_VARIABLE = 'DAORIS_HOME';

/** The one sentence every refusal for a missing home carries. */
export const HOME_SENTENCE =
  'no Daoris home: set DAORIS_HOME to the application\'s data folder (the `data` directory beside '
  + 'Daoris.exe), or name the file\'s own variable. Daoris keeps nothing under the user profile.';

type Env = Record<string, string | undefined>;

/** The home, or null when the environment names none. Blank is none. */
export function daorisHome(env: Env = process.env): string | null {
  const home = env[HOME_VARIABLE]?.trim();
  return home ? home : null;
}

/** A file under the home, or null when there is no home. */
export function homeFile(env: Env, name: string): string | null {
  const home = daorisHome(env);
  return home ? join(home, name) : null;
}

/** A file under the home, or a refusal naming the variable and the file. */
export function requireHomeFile(env: Env, name: string): string {
  const file = homeFile(env, name);
  if (!file) throw new DaorisError(`${HOME_SENTENCE} (wanted: ${name})`);
  return file;
}
