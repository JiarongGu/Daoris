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

import { installPageErrors, logEvent, LOG_TEXT, pageShows, reportPageStopped, STACK_FRAMES, stackTop } from './shell';

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

/**
 * PAGELOOP1 (D217): a render the error screen caught names where it broke, which the window's `error` line could not, since React
 * hands the component stack to a boundary alone. The top frames' component names, never a frame's address; and the view.
 */
describe("a render's failure, as the log keeps it", () => {
  afterEach(() => {
    bridge.isAvailable = false;
    bridge.invoke.mockReset();
  });

  /** A component stack as React writes it in a development build, each frame with the module it came from. */
  const DEVELOPMENT = [
    '',
    '    at Thrower (http://localhost:5178/src/work/frame/tabFit.ts?t=1730000000000:55:3)',
    '    at div',
    '    at new TabRow (http://localhost:5178/src/work/frame/TabRow.tsx:12:7)',
    '    at WorkFrame (http://localhost:5178/src/work/WorkFrame.tsx:1730:9)',
    '    at Suspense',
    '    at App (http://localhost:5178/src/app/App.tsx:178:3)',
    '    at PageBoundary (http://localhost:5178/src/app/PageBoundary.tsx:61:1)',
  ].join('\n');

  it('keeps the first frames by their names alone, innermost first, never where each was loaded from', () => {
    expect(STACK_FRAMES).toBe(5);
    expect(stackTop(DEVELOPMENT)).toBe('Thrower < div < TabRow < WorkFrame < Suspense');
  });

  it("keeps a production build's minified names as they are, and cuts a long top short", () => {
    expect(stackTop('\n    at Ue (https://daoris.local/assets/index-Bx2k.js:40:1234)\n    at $t (https://daoris.local/assets/index-Bx2k.js:40:99)'))
      .toBe('Ue < $t');
    const long = Array.from({ length: 5 }, (_, i) => `    at ${String.fromCharCode(65 + i).repeat(40)} (x.js:1:1)`).join('\n');
    expect(stackTop(long)).toBe(`${['A', 'B', 'C', 'D', 'E'].map((letter) => letter.repeat(40)).join(' < ').slice(0, LOG_TEXT)}…`);
  });

  it('names nothing where the stack names no frame', () => {
    expect(stackTop(undefined)).toBeNull();
    expect(stackTop(null)).toBeNull();
    expect(stackTop('\n\n')).toBeNull();
  });

  it('reports the message, the view the page shows and the top of the stack, as `render`', () => {
    bridge.isAvailable = true;
    bridge.invoke.mockResolvedValue(null);
    pageShows('sessions');

    reportPageStopped(new Error('Maximum update depth exceeded.'), DEVELOPMENT, 'window:monitor');

    expect(bridge.invoke).toHaveBeenCalledWith('DAORIS.LOG', 'EVENT', {
      payload: {
        event: 'page.error',
        data: {
          where: 'render', message: 'Maximum update depth exceeded.', view: 'sessions', stack: 'Thrower < div < TabRow < WorkFrame < Suspense',
        },
      },
    });
  });
});
