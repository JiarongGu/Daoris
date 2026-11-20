import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { E2E_BAND, e2ePort, webRoot } from '../../scripts/e2e-port.mjs';

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

  it('is what the config and the host both read, with no fixed port left in either', () => {
    for (const file of ['playwright.config.ts', join('scripts', 'e2e-host.mjs')]) {
      const text = readFileSync(join(webRoot, file), 'utf8');
      expect(text).toContain('e2ePort(');
      expect(text).not.toMatch(/localhost:\d{2,5}/);
    }
  });
});
