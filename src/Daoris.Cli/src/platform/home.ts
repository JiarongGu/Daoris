import { existsSync, linkSync, mkdirSync, readFileSync, unlinkSync, writeFileSync } from 'node:fs';
import { randomBytes } from 'node:crypto';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { DaorisError } from './errors.ts';

/**
 * Where every machine-local file Daoris owns lives (D63): one home, named by `DAORIS_HOME`, and **no
 * default under the user profile**. The application's own folder is the home — the installed desktop
 * sets the variable for itself and everything it spawns — and this CLI reads the same variable from
 * the user's environment, or, run from an install with none, that install's own `data` (D171). Absent
 * both, there is no home: the management class refuses with one sentence rather than writing
 * somewhere nobody pointed it; doctrine commands never needed a home.
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

/** The file `desktop-publish` writes at an install's root, and nothing else does (the driver's and the service's `DaorisHome.InstallMarker`). */
export const INSTALL_MARKER = 'INSTALLED.md';

/** The folder an install's programs run from (D93), and its home beside it (D63). */
export const INSTALL_APP = 'app';
export const INSTALL_DATA = 'data';

/**
 * The install a folder sits in (FINDDAORIS1, D171 point 1): the folder above the `app/` it is in, however deep (this package is
 * `app/cli/node_modules/daoris/`), where that folder holds the marker. Null for a checkout, which is no install.
 */
export function installOf(from: string): string | null {
  for (let directory = resolve(from); ;) {
    const parent = dirname(directory);
    if (parent === directory) return null;
    if (basename(directory).toLowerCase() === INSTALL_APP && existsSync(join(parent, INSTALL_MARKER))) return parent;
    directory = parent;
  }
}

/** The install this package runs from, or null for a checkout. */
export const THIS_INSTALL: string | null = installOf(dirname(fileURLToPath(import.meta.url)));

/**
 * The home, or null when there is none. Blank is none. The variable still wins (the dev loop, a rehearsal, a second home);
 * with none, a `daoris` the install carries finds the install's own `data` folder, as the driver host does (FINDDAORIS1),
 * since the install's launchers set nothing.
 */
export function daorisHome(env: Env = process.env, install: string | null = THIS_INSTALL): string | null {
  const home = env[HOME_VARIABLE]?.trim();
  if (home) return home;
  return install ? join(install, INSTALL_DATA) : null;
}

/**
 * A home's identity (HOMEID1, the terminal RPC design §3.1): `<home>/home.json`, `{ "id": "<32 lower-case hex>", "minted": "…" }`,
 * which every path to the folder reads alike, so a running service is told to be this home's by it and never by a path. The
 * driver's and the service's `HomeIdentity` hold the same file in C# and share no code (`twins.md`).
 */
export const HOME_ID_FILE = 'home.json';

/** Whether a value is an id as `home.json` holds one. */
export function isHomeId(value: unknown): value is string {
  return typeof value === 'string' && /^[0-9a-f]{32}$/.test(value);
}

/** The home's id, or null where it has none or its file does not read. A read: discovery mints nothing. */
export function homeId(home: string): string | null {
  try {
    const file: unknown = JSON.parse(readFileSync(join(home, HOME_ID_FILE), 'utf8'));
    const id = typeof file === 'object' && file !== null ? (file as Record<string, unknown>).id : null;
    return isHomeId(id) ? id : null;
  } catch {
    return null;
  }
}

/**
 * The home's id, minted where it has none (HOMELEASE1: a lease records it): written beside, then linked into place, which
 * refuses where a file is, so of two minting at once one stands and both read it. Null where its file does not read, which is
 * never replaced, or none could be written.
 */
export function ensureHomeId(home: string): string | null {
  const path = join(home, HOME_ID_FILE);
  if (existsSync(path)) return homeId(home);
  const beside = `${path}.${randomBytes(8).toString('hex')}.writing`;
  try {
    mkdirSync(home, { recursive: true });
    const minted = { id: randomBytes(16).toString('hex'), minted: new Date().toISOString() };
    writeFileSync(beside, `${JSON.stringify(minted, null, 2)}\n`, 'utf8');
    linkSync(beside, path);
  } catch {
    // Almost always: another process minted it first. Its file is the home's id, read below like any other.
  } finally {
    try { unlinkSync(beside); } catch { /* never written */ }
  }
  return homeId(home);
}

/** A file under the home, or null when there is no home. */
export function homeFile(env: Env, name: string, install: string | null = THIS_INSTALL): string | null {
  const home = daorisHome(env, install);
  return home ? join(home, name) : null;
}

/** A file under the home, or a refusal naming the variable and the file. */
export function requireHomeFile(env: Env, name: string, install: string | null = THIS_INSTALL): string {
  const file = homeFile(env, name, install);
  if (!file) throw new DaorisError(`${HOME_SENTENCE} (wanted: ${name})`);
  return file;
}
