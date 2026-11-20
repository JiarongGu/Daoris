import { readFileSync } from 'node:fs';
import { createServer, type Server } from 'node:net';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { E2E_BAND, e2ePort, freeE2ePort, webRoot } from '../../scripts/e2e-port.mjs';

/**
 * The web gate's port is the checkout's own: two checks of one repository run in two verification trees at once,
 * and on one fixed port the second tree's host failed to bind, a red its change did not make.
 */
describe('the web gate port', () => {
  const inBand = (port: number) => port >= E2E_BAND.from && port < E2E_BAND.from + E2E_BAND.size;

  it('gives two checkouts of one repository two ports, each in the band', () => {
    const first = e2ePort('D:\\trees\\verify\\abc\\src\\Daoris.Web', {});
    const second = e2ePort('D:\\trees\\verify\\abc-2\\src\\Daoris.Web', {});
    expect(first).not.toBe(second);
    expect(inBand(first)).toBe(true);
    expect(inBand(second)).toBe(true);
  });

  it('gives one folder the same port however its case is spelled', () => {
    expect(e2ePort('D:\\Trees\\A\\src\\Daoris.Web', {})).toBe(e2ePort('d:\\trees\\a\\src\\daoris.web', {}));
  });

  it('takes a port named by hand, and passes over one that is no port', () => {
    expect(e2ePort('D:\\a', { DAORIS_E2E_PORT: '7123' })).toBe(7123);
    expect(inBand(e2ePort('D:\\a', { DAORIS_E2E_PORT: 'seven' }))).toBe(true);
    expect(inBand(e2ePort('D:\\a', { DAORIS_E2E_PORT: '70000' }))).toBe(true);
  });

  it('gives a second checkout whose path hashes to a taken port another in the band, and both bind', async () => {
    const root = 'D:/trees/verify/same/src/Daoris.Web';
    const servers: Server[] = [];
    const bind = (port: number) => new Promise<Server>((resolve, reject) => {
      const server = createServer();
      server.once('error', reject);
      server.listen(port, '127.0.0.1', () => resolve(server));
    });
    try {
      const first = await freeE2ePort(root, {});
      servers.push(await bind(first));
      // The same hash, the first's host now running on its port: the probe moves on within the band.
      const second = await freeE2ePort(root, {});
      servers.push(await bind(second));
      expect(second).not.toBe(first);
      expect(inBand(first)).toBe(true);
      expect(inBand(second)).toBe(true);
    } finally {
      await Promise.all(servers.map((server) => new Promise((done) => server.close(done))));
    }
  });

  it('wraps past the end of the band, and names the choice in the environment so a config read again keeps it', async () => {
    const env: Record<string, string | undefined> = {};
    const from = e2ePort('D:/wrap', {});
    // Only the port just before the hash's own is free, so the probe walks the whole band round to it.
    const before = E2E_BAND.from + ((from - E2E_BAND.from + E2E_BAND.size - 1) % E2E_BAND.size);
    const port = await freeE2ePort('D:/wrap', env, async (candidate: number) => candidate === before);
    expect(port).toBe(before);
    expect(env.DAORIS_E2E_PORT).toBe(String(port));
    expect(await freeE2ePort('D:/wrap', env, async () => false)).toBe(port);
  });

  it('fails saying so where the whole band is taken', async () => {
    await expect(freeE2ePort('D:/none', {}, async () => false)).rejects.toThrow(/7200/);
  });

  it('is what the config and the host both read, with no fixed port left in either', () => {
    for (const [file, call] of [['playwright.config.ts', 'freeE2ePort('], [join('scripts', 'e2e-host.mjs'), 'e2ePort(']]) {
      const text = readFileSync(join(webRoot, file), 'utf8');
      expect(text).toContain(call);
      expect(text).not.toMatch(/localhost:\d{2,5}/);
    }
  });
});
