// The web gate's port (`playwright.config.ts`, `scripts/e2e-host.mjs`): one of its own for each checkout, so two
// checkouts of the repository run the suite at once (two checks of one repository in two verification trees), and
// neither's readiness probe answers against the other's host. On one fixed port (5196) the second tree's host failed
// to bind and its gate went red for a change it did not make. Hashed from the web folder's path, as the
// measured check's is (`playwright.measured.config.ts`), in a band of its own: 7200 to 7899, clear of the measured
// check's 5200 to 5899, the rehearsals' 20000 to 31999, the dev loop's 5188 and 9333, and every port a browser refuses
// to load (`tools/listen-port.mjs`'s `BAD_PORTS`, which put 6566 and 6665 to 6697 out of an earlier band). `DAORIS_E2E_PORT`
// names one by hand (the driver hands a gate no `DAORIS_` variable of its own, so there the probe chooses). The hash only
// starts the walk: `freeE2ePort` binds the first free port from it, so two checkouts hashing to one port both bind.
import { createHash } from 'node:crypto';
import { createServer } from 'node:net';
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

/** Whether nothing listens on `port` on either loopback address the host's `localhost` binds. */
export async function portIsFree(port) {
  const free = (host) => new Promise((resolve) => {
    const server = createServer();
    server.once('error', (error) => resolve(error.code === 'EADDRNOTAVAIL' || error.code === 'EAFNOSUPPORT'));
    server.listen(port, host, () => server.close(() => resolve(true)));
  });
  return (await free('127.0.0.1')) && (await free('::1'));
}

/**
 * The port the suite's host binds: `DAORIS_E2E_PORT` where it names one, else the first free port from the one the folder's path
 * hashes to, walking the band and wrapping, so two checkouts whose paths hash to one port both bind (the hash alone chose the same
 * one for both and the second's host failed to bind). The choice is written to `env.DAORIS_E2E_PORT`, so the config read again (by
 * each worker) and the host it starts keep it, never probing past their own server. Fails naming the band where every port is taken.
 */
export async function freeE2ePort(root = webRoot, env = process.env, isFree = portIsFree) {
  if (Number.isInteger(Number(env.DAORIS_E2E_PORT)) && Number(env.DAORIS_E2E_PORT) > 0 && Number(env.DAORIS_E2E_PORT) < 65536) {
    return Number(env.DAORIS_E2E_PORT);
  }
  const first = e2ePort(root, {}) - E2E_BAND.from;
  for (let step = 0; step < E2E_BAND.size; step++) {
    const port = E2E_BAND.from + ((first + step) % E2E_BAND.size);
    if (await isFree(port)) {
      env.DAORIS_E2E_PORT = String(port);
      return port;
    }
  }
  throw new Error(`no port from ${E2E_BAND.from} to ${E2E_BAND.from + E2E_BAND.size - 1} is free for the web gate's host`);
}
