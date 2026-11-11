// This plugin's own credential, kept in its private storage: DAORIS_PLUGIN_PRIVATE, the folder Daoris hands this plugin's processes
// alone. Written whole beside and renamed over, read at each API call, never logged and never put in a step's evidence.
import { existsSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const FILE = 'credential.json';
const folder = () => process.env.DAORIS_PLUGIN_PRIVATE || null;

/** Whether Daoris handed this process its storage: the person may switch it off in Plugins. */
export const stored = () => folder() !== null;

/** The kept credential `{ token, kept, refused? }`, or null where none is kept. */
export function kept() {
  const at = folder();
  if (!at) return null;
  try {
    const held = JSON.parse(readFileSync(join(at, FILE), 'utf8'));
    return typeof held?.token === 'string' && held.token ? held : null;
  } catch {
    return null;
  }
}

function write(held) {
  const path = join(folder(), FILE);
  writeFileSync(`${path}.new`, `${JSON.stringify(held)}\n`, { mode: 0o600 });
  renameSync(`${path}.new`, path);
}

/** Keep a token, any earlier refusal of the one before it forgotten. */
export const keep = (token) => write({ token, kept: new Date().toISOString() });

/** Forget the kept token, answering whether one is gone. */
export function forget() {
  const at = folder();
  if (!at || !existsSync(join(at, FILE))) return false;
  rmSync(join(at, FILE), { force: true });
  return true;
}

/** The platform refused the kept token: said by `status` until a new one is kept. */
export function refused() {
  const held = kept();
  if (held && !held.refused) write({ ...held, refused: new Date().toISOString() });
}
