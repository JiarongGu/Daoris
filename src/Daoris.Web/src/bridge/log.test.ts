import { afterEach, describe, expect, it, vi } from 'vitest';

// The page's report into the machine log (LOG1b, D94). The bridge is mocked, and switched on and off,
// because the whole contract is those two cases: a shell hears one request, a browser nothing at all.

const { bridge } = vi.hoisted(() => ({ bridge: { isAvailable: false, invoke: vi.fn() } }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => bridge.isAvailable,
  getBridge: () => bridge,
  useShenora: () => ({ isAvailable: bridge.isAvailable, bridge }),
  useShenoraEvent: () => {},
}));

import { installPageErrors, logEvent, LOG_TEXT } from './shell';

/** Whatever the report's promise does, let it settle before looking. */
const settled = () => new Promise((resolve) => setTimeout(resolve, 0));

describe('the page\'s report into the machine log', () => {
  afterEach(() => {
    bridge.isAvailable = false;
    bridge.invoke.mockReset();
  });

  it('says nothing where there is no shell: a browser logs nothing', () => {
    logEvent('view.opened', { view: 'quests' });

    expect(bridge.invoke).not.toHaveBeenCalled();
  });

  it('hands the shell one request with the event and its data', () => {
    bridge.isAvailable = true;
    bridge.invoke.mockResolvedValue(null);

    logEvent('message.sent', { session: 'c0ffee11', kind: 'chat', length: 28, files: 0 });

    expect(bridge.invoke).toHaveBeenCalledTimes(1);
    expect(bridge.invoke).toHaveBeenCalledWith('DAORIS.LOG', 'EVENT', {
      payload: { event: 'message.sent', data: { session: 'c0ffee11', kind: 'chat', length: 28, files: 0 } },
    });
  });

  it('never throws, whatever the bridge does with it', async () => {
    bridge.isAvailable = true;
    bridge.invoke.mockRejectedValue(new Error('NO_HANDLER'));
    expect(() => logEvent('view.opened', { view: 'map' })).not.toThrow();
    await settled();

    bridge.invoke.mockImplementation(() => { throw new Error('the bridge broke'); });
    expect(() => logEvent('view.opened', { view: 'map' })).not.toThrow();

    // A mock that answers nothing at all, as a test's bridge often does.
    bridge.invoke.mockImplementation(() => undefined);
    expect(() => logEvent('view.opened', { view: 'map' })).not.toThrow();
  });

  it('reports the page\'s own failures once however often it is installed, the message only and cut short', async () => {
    bridge.isAvailable = true;
    bridge.invoke.mockResolvedValue(null);
    installPageErrors();
    installPageErrors();

    window.dispatchEvent(new ErrorEvent('error', { message: 'x'.repeat(300), error: new Error('with a stack') }));
    const rejected = Object.assign(new Event('unhandledrejection'), { reason: new Error('the request failed') });
    window.dispatchEvent(rejected);
    await settled();

    expect(bridge.invoke).toHaveBeenCalledTimes(2);
    expect(bridge.invoke).toHaveBeenNthCalledWith(1, 'DAORIS.LOG', 'EVENT', {
      payload: { event: 'page.error', data: { where: 'window', message: `${'x'.repeat(LOG_TEXT)}…` } },
    });
    expect(bridge.invoke).toHaveBeenNthCalledWith(2, 'DAORIS.LOG', 'EVENT', {
      payload: { event: 'page.error', data: { where: 'promise', message: 'the request failed' } },
    });
  });
});
