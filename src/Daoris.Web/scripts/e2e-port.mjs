// The web gate's port (`playwright.config.ts`, `scripts/e2e-host.mjs`): one of its own for each checkout, so two
// checkouts of the repository run the suite at once (two checks of one repository in two verification trees), and
// neither's readiness probe answers against the other's host. On one fixed port (5196) the second tree's host failed
// to bind and its gate went red for a change it did not make. Hashed from the web folder's path, as the
// measured check's is (`playwright.measured.config.ts`), in a band of its own: 7200 to 7899, clear of the measured
// check's 5200 to 5899, the rehearsals' 20000 to 31999, the dev loop's 5188 and 9333, and every port a browser refuses
// to load (`tools/listen-port.mjs`'s `BAD_PORTS`, which put 6566 and 6665 to 6697 out of an earlier band). `DAORIS_E2E_PORT`
// names one by hand (the driver hands a gate no `DAORIS_` variable of its own, so there the hash always chooses).
import { createHash } from 'node:crypto';
import { dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

/** The web folder this checkout's suite runs from. */
export const webRoot = dirname(dirname(fileURLToPath(import.meta.url)));

/** The first port of the band and how many it holds. */
export const E2E_BAND = { from: 7200, size: 700 };

/**
 * The port the suite's host listens on for the web folder at `root`: `DAORIS_E2E_PORT` where it names a port, else the band's port
 * the folder's path hashes to, its case folded so one folder spelled two ways is one port.
 */
export function e2ePort(root = webRoot, env = process.env) {
  const named = Number(env.DAORIS_E2E_PORT);
  if (Number.isInteger(named) && named > 0 && named < 65536) return named;
  return E2E_BAND.from + (createHash('sha1').update(root.toLowerCase()).digest().readUInt16BE(0) % E2E_BAND.size);
}
