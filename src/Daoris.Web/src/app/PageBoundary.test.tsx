import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { StrictMode, useLayoutEffect, useState } from 'react';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// The page's own error screen (PAGELOOP1): a render that throws anywhere below the root draws this screen rather than a blank
// window, reports itself once into the machine log, and offers a reload of the page alone. The bridge is mocked and switched on
// and off, as `bridge/log.test.ts` does, since a shell logs and a browser does not.

const { bridge, closeSplash } = vi.hoisted(() => ({
  bridge: { isAvailable: true, invoke: vi.fn((..._: unknown[]) => Promise.resolve(null)) },
  closeSplash: vi.fn(() => Promise.resolve()),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => bridge.isAvailable,
  getBridge: () => bridge,
  useShenora: () => ({ isAvailable: bridge.isAvailable, bridge }),
  useShenoraEvent: () => {},
  WindowCommands: class { closeSplash = closeSplash; },
}));

import i18n from '../locales/i18n';
import { installPageErrors } from '../bridge/shell';
import { PageBoundary } from './PageBoundary';

/** A component whose render throws, as React's own update-depth guard does in the middle of a commit. */
function Thrower(): never {
  throw new Error('Maximum update depth exceeded. This can happen when a component calls setState inside useEffect.');
}

/** A component between the thrower and the boundary, so the stack has a frame of its own to name. */
function Holder() {
  return <Thrower />;
}

/** What the page reported as `page.error`, each report's data. */
const pageErrors = () => bridge.invoke.mock.calls
  .map((call) => call as unknown[])
  .filter(([module, type, request]) =>
    module === 'DAORIS.LOG' && type === 'EVENT' && (request as { payload: { event: string } }).payload.event === 'page.error')
  .map(([, , request]) => (request as { payload: { data: Record<string, unknown> } }).payload.data);

describe("the page's error screen", () => {
  beforeEach(() => {
    // React says a caught error on the console as well, which is the test's noise and nothing it checks.
    vi.spyOn(console, 'error').mockImplementation(() => {});
  });
  afterEach(async () => {
    cleanup();
    vi.restoreAllMocks();
    bridge.isAvailable = true;
    bridge.invoke.mockClear();
    closeSplash.mockClear();
    await i18n.changeLanguage('en');
  });

  it('draws what it holds while nothing throws, and reports nothing', () => {
    render(<PageBoundary><p>the window</p></PageBoundary>);

    expect(screen.getByText('the window')).toBeInTheDocument();
    expect(pageErrors()).toEqual([]);
    expect(closeSplash).not.toHaveBeenCalled();
  });

  it('says the page stopped, what broke and that nothing behind it did, and reports it once', async () => {
    installPageErrors();
    render(<StrictMode><PageBoundary><Thrower /></PageBoundary></StrictMode>);

    expect(screen.getByRole('heading', { level: 1, name: "This window's page stopped on an error" })).toBeInTheDocument();
    expect(screen.getByText(/^Maximum update depth exceeded\./)).toBeInTheDocument();
    expect(screen.getByText(/^Only the page stopped\. The sessions, the driver and everything else running behind it go on/))
      .toBeInTheDocument();
    expect(screen.getByText('page.error')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reload the page' })).toBeInTheDocument();

    // Once, as a render's failure, and never again as the window's: a caught error is no uncaught one.
    await Promise.resolve();
    expect(pageErrors()).toHaveLength(1);
    expect(pageErrors()[0]).toMatchObject({ where: 'render', message: expect.stringMatching(/^Maximum update depth exceeded\./) });
    // A page that stopped before its first screen lifts the splash itself, rather than at the window's bound.
    expect(closeSplash).toHaveBeenCalledTimes(1);
  });

  /** PAGELOOP1 (D217): where it broke, which the window's `error` line never knew, and before any view said what it shows, none. */
  it('names the components it broke in, innermost first, and no view before one was shown', async () => {
    render(<PageBoundary><section><Holder /></section></PageBoundary>);

    await Promise.resolve();
    const [report] = pageErrors();
    expect(report).toMatchObject({ where: 'render', stack: expect.stringMatching(/^Thrower < Holder < section < PageBoundary/) });
    expect(report).not.toHaveProperty('view');
  });

  /** The install's own failure: a layout effect that sets its state at every commit, which React stops at its update depth (#185). */
  it("catches React's update-depth error, and names the component that looped", async () => {
    function Looping() {
      const [n, setN] = useState(0);
      useLayoutEffect(() => setN(n + 1));
      return <p>{n}</p>;
    }
    render(<PageBoundary><Looping /></PageBoundary>);

    expect(await screen.findByRole('heading', { level: 1, name: "This window's page stopped on an error" })).toBeInTheDocument();
    expect(screen.getByText(/^Maximum update depth exceeded/)).toBeInTheDocument();
    expect(pageErrors()).toEqual([expect.objectContaining({ where: 'render', stack: expect.stringMatching(/^Looping < PageBoundary/) })]);
  });

  it("names a secondary window's view, which its root knows before anything is drawn", async () => {
    render(<PageBoundary view="window:monitor"><Thrower /></PageBoundary>);

    await Promise.resolve();
    expect(pageErrors()[0]).toMatchObject({ where: 'render', view: 'window:monitor', stack: expect.stringMatching(/^Thrower < PageBoundary/) });
  });

  it('reloads the page alone when asked', async () => {
    const reload = vi.fn();
    render(<PageBoundary reload={reload}><Thrower /></PageBoundary>);

    await userEvent.click(screen.getByRole('button', { name: 'Reload the page' }));

    expect(reload).toHaveBeenCalledTimes(1);
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<PageBoundary><Thrower /></PageBoundary>);

    expect(screen.getByRole('heading', { level: 1, name: '此窗口的页面因错误而停止' })).toBeInTheDocument();
    expect(screen.getByText(/^停止的只是页面。会话、驱动和它背后运行的其他一切都在继续/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '重新加载页面' })).toBeInTheDocument();
  });

  it('in a browser reports nothing and says nothing of a log it has not written', () => {
    bridge.isAvailable = false;
    render(<PageBoundary><Thrower /></PageBoundary>);

    expect(screen.getByRole('heading', { level: 1, name: "This window's page stopped on an error" })).toBeInTheDocument();
    expect(screen.queryByText('page.error')).toBeNull();
    expect(bridge.invoke).not.toHaveBeenCalled();
  });
});
