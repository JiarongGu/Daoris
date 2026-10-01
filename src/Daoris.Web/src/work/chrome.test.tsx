import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render as rtlRender, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { en, zh } from '../locales';
import { SessionConsole } from '../SessionConsole';
import { layTabs } from '../test/tabRoom';
import { SESSION_ACTIVE } from '../ui';
import { ActivityBar, AppStrip, OutputPanel, type PanelTab, Splitter, StatusBar } from './frame';

/** The provider the application mounts once (`main.tsx`); a tooltip outside one throws. */
const render = (node: ReactElement) => {
  const result = rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);
  return {
    ...result,
    rerender: (next: ReactElement) => result.rerender(<Tooltip.Provider>{next}</Tooltip.Provider>),
  };
};

/**
 * A drag's data, as a page sees it (DOCK1e): jsdom has no `DataTransfer`, and a region reads a drag's
 * types while it is over it, so the stand-in keeps the types in step with what was set.
 */
function carried(initial: Record<string, string> = {}) {
  const data: Record<string, string> = { ...initial };
  return {
    setData: (type: string, value: string) => { data[type] = value; },
    getData: (type: string) => data[type] ?? '',
    get types() { return Object.keys(data); },
    effectAllowed: 'all',
    dropEffect: 'none',
  };
}

// The application's chrome (D56, D66), as molecules: props in, states out. No shell, no service, no
// arranged world — which is what lets "in a browser" and "with attention" be ordinary assertions
// rather than integration setup (components plan §2).

const VIEWS = [
  { tab: 'overview' as const, label: 'Overview', icon: 'overview' as const },
  { tab: 'sessions' as const, label: 'Sessions', icon: 'frameWork' as const, badge: 1, tone: 'open' as const },
  { tab: 'quests' as const, label: 'Quests', icon: 'quests' as const, badge: 3 },
];
const SETTINGS = [{ tab: 'settings' as const, label: 'Settings', icon: 'settings' as const }];

describe('AppStrip', () => {
  /**
   * 🔴 The strip carries the MARK and no name. The wordmark was 20px of serif in
   * every window forever, saying the name of the thing you are already looking at; a title bar in an
   * IDE says what you can DO, and the name moved to About.
   *
   * D41 §1's rule survives and is now stricter: the serif's one appearance is not spent here at all.
   */
  it('carries the mark and not the name — the serif is not spent on the strip', () => {
    const { container } = render(<AppStrip />);
    expect(screen.queryByText('Daoris')).toBeNull();
    expect(container.querySelector('svg')).toBeTruthy();
    expect(container.querySelectorAll('.font-serif')).toHaveLength(0);
  });

  /** One navigation (D66): the strip holds the application's menus and no switch between frames. */
  it('carries no mode switch — the activity bar is the one navigation', () => {
    render(<AppStrip menus={<span>menus</span>} />);
    expect(screen.queryByRole('group', { name: /mode/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /^work$/i })).toBeNull();
    expect(screen.getByText('menus')).toBeTruthy();
  });

  it('holds the scope slot — which is empty while the family is one circle (WSP5)', () => {
    const { rerender } = render(<AppStrip />);
    expect(screen.queryByText('every circle')).toBeNull();
    rerender(<AppStrip scope={<span>every circle</span>} />);
    expect(screen.getByText('every circle')).toBeTruthy();
  });

  /**
   * The room is reserved only where a window will claim it. SURF7 filled it: those slots are now the
   * rectangles the window paints its caption buttons into, which is why they are empty and why there
   * are exactly three. What is DONE with them belongs to `windowChrome.test.tsx`.
   */
  it('reserves the caption room only when asked, so nothing shifts when a window claims it', () => {
    const { container, rerender } = render(<AppStrip />);
    expect(container.querySelectorAll('[data-caption]')).toHaveLength(0);
    rerender(<AppStrip captionRoom />);
    expect(container.querySelectorAll('[data-caption]')).toHaveLength(3);
  });

  /**
   * Ask Daoris lives on the right, so its toggle does too. What toggles the right region sits at the strip's right, beside
   * the window's controls, as VS Code's layout toggles do — after the scope, before the caption room.
   */
  it('holds what toggles the right region at its right edge, before the window controls', () => {
    const { container } = render(
      <AppStrip center={<span>center</span>} scope={<span>scope</span>} trailing={<button type="button">ask</button>} captionRoom />,
    );

    const end = container.querySelector('[data-strip-space="end"]')!;
    const order = [...end.querySelectorAll('span, button, [data-caption]')].map((node) =>
      node.getAttribute('data-caption') ? 'caption' : node.textContent);
    expect(order.slice(0, 2)).toEqual(['scope', 'ask']);
    expect(order.slice(2)).toEqual(['caption', 'caption', 'caption']);
    expect(container.querySelector('[data-strip-space="center"]')!.textContent).toBe('center');
  });
});

describe('ActivityBar', () => {
  it('names every view, because an icon-only control has no name otherwise (D41 §6)', () => {
    render(<ActivityBar label="Views" items={VIEWS} end={SETTINGS} active="quests" onSelect={() => {}} />);
    for (const { label } of [...VIEWS, ...SETTINGS]) {
      expect(screen.getByRole('button', { name: label })).toBeTruthy();
    }
    expect(screen.getByRole('navigation', { name: 'Views' })).toBeTruthy();
  });

  /** One list (D66): whichever view is in front is the one marked — Settings at the foot included. */
  it('marks the current view, wherever on the bar it sits', () => {
    const { rerender } = render(
      <ActivityBar label="Views" items={VIEWS} end={SETTINGS} active="quests" onSelect={() => {}} />,
    );
    expect(screen.getByRole('button', { name: 'Quests' }).getAttribute('aria-current')).toBe('page');

    rerender(<ActivityBar label="Views" items={VIEWS} end={SETTINGS} active="settings" onSelect={() => {}} />);
    expect(screen.getByRole('button', { name: 'Settings' }).getAttribute('aria-current')).toBe('page');
    expect(screen.getAllByRole('button', { current: 'page' })).toHaveLength(1);
  });

  /** The one status on the bar wears the status hue; a quantity wears the accent. */
  it('colours a count by what it is', () => {
    render(<ActivityBar label="Views" items={VIEWS} active="overview" onSelect={() => {}} />);
    expect(screen.getByText('1').className).toContain('text-st-open');
    expect(screen.getByText('3').className).toContain('text-accent');
  });

  it('carries the counts, and wears nothing at zero', () => {
    const { rerender } = render(
      <ActivityBar label="Views" items={VIEWS} active="overview" onSelect={() => {}} />,
    );
    expect(screen.getByText('3')).toBeTruthy();
    rerender(
      <ActivityBar
        label="Views"
        items={VIEWS.map((item) => ({ ...item, badge: item.badge && 0 }))}
        active="overview"
        onSelect={() => {}}
      />,
    );
    expect(screen.queryByText('3')).toBeNull();
    expect(screen.queryByText('0')).toBeNull();
  });

  it('selects a view, from the list or from the foot', async () => {
    const onSelect = vi.fn();
    render(<ActivityBar label="Views" items={VIEWS} end={SETTINGS} active="overview" onSelect={onSelect} />);
    await userEvent.click(screen.getByRole('button', { name: 'Sessions' }));
    await userEvent.click(screen.getByRole('button', { name: 'Settings' }));
    expect(onSelect.mock.calls).toEqual([['sessions'], ['settings']]);
  });

  /**
   * D118 §3a (audit F1): pressing the place you are on toggles its list, as VS Code's activity bar toggles
   * its side bar. It is one of the list's four doors, so a view without a list is handed none, and the
   * press then only selects the place again.
   */
  it("toggles the current place's list when it is pressed again, and selects any other place", async () => {
    const onSelect = vi.fn();
    const onToggleCurrent = vi.fn();
    render(
      <ActivityBar label="Views" items={VIEWS} end={SETTINGS} active="sessions" onSelect={onSelect} onToggleCurrent={onToggleCurrent} />,
    );

    // A door to its list, which a list laid over the main area leaves to it rather than closing on its press.
    expect(screen.getByRole('button', { name: 'Sessions' })).toHaveAttribute('data-list-door');
    expect(screen.getByRole('button', { name: 'Quests' })).not.toHaveAttribute('data-list-door');
    await userEvent.click(screen.getByRole('button', { name: 'Sessions' }));
    expect(onToggleCurrent).toHaveBeenCalledOnce();
    expect(onSelect).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: 'Quests' }));
    expect(onSelect).toHaveBeenCalledWith('quests');
    expect(onToggleCurrent).toHaveBeenCalledOnce();
  });

  it('selects the current place again where its view has no list to toggle', async () => {
    const onSelect = vi.fn();
    render(<ActivityBar label="Views" items={VIEWS} end={SETTINGS} active="overview" onSelect={onSelect} />);
    await userEvent.click(screen.getByRole('button', { name: 'Overview' }));
    expect(onSelect).toHaveBeenCalledWith('overview');
  });

  it('shows only the views it is given — a browser gets no Sessions at all', () => {
    render(
      <ActivityBar
        label="Views"
        items={VIEWS.filter((item) => item.tab !== 'sessions')}
        active="overview"
        onSelect={() => {}}
      />,
    );
    expect(screen.queryByRole('button', { name: 'Sessions' })).toBeNull();
  });

  /**
   * 🔴 UX5 U22: at the window's least height (300px at 200%), the bar crushed its places. Every icon
   * shrank until they touched, the counts sat over their neighbours, and Settings went under the
   * status bar. A place keeps its size, and a bar too short for them scrolls instead. jsdom has no
   * layout, so this holds the rules and the window shows them.
   */
  it('keeps every place its full size, and scrolls when the window is too short to hold them', () => {
    render(
      <ActivityBar
        label="Views"
        items={VIEWS}
        end={SETTINGS}
        active="overview"
        onSelect={() => {}}
        footer={<button type="button">refresh</button>}
      />,
    );

    const bar = screen.getByRole('navigation', { name: 'Views' });
    expect(bar).toHaveClass('min-h-0', 'overflow-y-auto');
    for (const { label } of [...VIEWS, ...SETTINGS]) {
      expect(screen.getByRole('button', { name: label })).toHaveClass('shrink-0');
    }
    expect(screen.getByRole('button', { name: 'refresh' }).parentElement).toHaveClass('shrink-0');
  });

  /** Actions, then the places at the foot — Settings last, where every workbench keeps its gear. */
  it('holds its footer actions and then its foot places, below the list', () => {
    render(
      <ActivityBar
        label="Views"
        items={VIEWS}
        end={SETTINGS}
        active="overview"
        onSelect={() => {}}
        footer={<button type="button">refresh</button>}
      />,
    );
    const names = screen.getAllByRole('button').map((button) => button.getAttribute('aria-label') ?? button.textContent);
    expect(names.slice(-2)).toEqual(['refresh', 'Settings']);
  });
});

/**
 * The status bar, brought to VS Code's: what it displays, and what it does on click or hover.
 *
 * That has two halves and only one of them is visual. **Display** is the part a screenshot
 * settles, and it did: an 11px run of `·`-joined prose on a 2px-tall strip reads as a caption on the
 * window, not as a bar. **Action** is what these assertions are for — the reference console's status
 * items are *targets*: each is a full-height box that lights on hover and does something on click,
 * and every one of them says what it will do before you press it.
 */
describe('StatusBar', () => {
  /**
   * 🔴 UX5 U21: the sessions item was *running sessions*, and it counts `SESSION_ACTIVE`, which holds
   * a session parked on its person. On the scratch window it said 2 while both were parked and
   * neither had a process. A claim in a catalogue has no test pointing at the code it describes, so
   * this one points at the set it counts.
   */
  it('names the sessions it counts: a parked one is active and never running', () => {
    expect(SESSION_ACTIVE.has('awaiting-person')).toBe(true);
    for (const catalogue of [en, zh]) {
      expect(catalogue['work.status.sessionsLabel']).not.toMatch(/running|运行中/);
      expect(catalogue['work.status.sessionsTip']).toMatch(/waiting on you|等你/);
    }
  });

  it('states the four ambient facts', () => {
    render(<StatusBar driver="running" sessions={2} workspace="default" remote />);

    expect(screen.getByText('ready')).toBeTruthy();
    expect(screen.getByText('2 sessions')).toBeTruthy();
    expect(screen.getByText('default')).toBeTruthy();
    expect(screen.getByText('wired')).toBeTruthy();
  });

  /**
   * 🔴 An item is a BUTTON when it leads somewhere and plain text when it does not — the distinction
   * a person reads as "this is pressable". A bar where everything looks alike and half of it
   * responds is worse than one where nothing does, because the half that does nothing is the one
   * they will press.
   */
  it('makes an item that leads somewhere a button, and leaves the rest as text', async () => {
    const onDriver = vi.fn();
    render(
      <StatusBar
        driver="running"
        sessions={0}
        workspace="default"
        remote={null}
        onDriver={onDriver}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: /driver/i }));
    expect(onDriver).toHaveBeenCalled();
    // Sessions was given no handler, so it is not a target.
    expect(screen.queryByRole('button', { name: /session/i })).toBeNull();
  });

  it('routes each item to its own destination', async () => {
    const onDriver = vi.fn();
    const onSessions = vi.fn();
    const onRemote = vi.fn();
    render(
      <StatusBar
        driver="running"
        sessions={3}
        workspace="default"
        remote={false}
        onDriver={onDriver}
        onSessions={onSessions}
        onRemote={onRemote}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: /session/i }));
    expect(onSessions).toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: /remote/i }));
    expect(onRemote).toHaveBeenCalled();
    expect(onDriver).not.toHaveBeenCalled();
  });

  /**
   * SETUP1b (D97 §2): a small *setup: n of 5* until the required steps are done, leading to Get started
   * — and nothing once they are, since an always-there all-clear is not read.
   */
  it('counts the setup until it is done, and leads to Get started', async () => {
    const onSetup = vi.fn();
    const { rerender } = render(
      <StatusBar driver="running" sessions={0} workspace={null} remote={null} setup={{ done: 2, of: 5 }} onSetup={onSetup} />,
    );

    await userEvent.click(screen.getByRole('button', { name: /setup/ }));
    expect(onSetup).toHaveBeenCalledOnce();
    expect(screen.getByText('setup: 2 of 5')).toBeTruthy();

    rerender(<StatusBar driver="running" sessions={0} workspace={null} remote={null} setup={{ done: 5, of: 5 }} onSetup={onSetup} />);
    expect(screen.queryByText(/setup:/)).toBeNull();
    rerender(<StatusBar driver="running" sessions={0} workspace={null} remote={null} onSetup={onSetup} />);
    expect(screen.queryByText(/setup:/)).toBeNull();
  });

  /**
   * The absent driver is a browser, which has no driver and never will (D55). Offering to take a
   * person to the machine's driver settings from a window that has no machine is a promise the
   * frame cannot keep, so the item stays text there however it was wired.
   */
  it('is not a target when the fact it states cannot be acted on here', () => {
    render(
      <StatusBar driver="absent" sessions={0} workspace={null} remote={null} onDriver={() => {}} />,
    );

    expect(screen.queryByRole('button', { name: /driver/i })).toBeNull();
    expect(screen.getByText('none here')).toBeTruthy();
  });

  /**
   * LOOK2a: the driver's file answers before its service is up, and the bar said *ready* while every route that reads
   * the service still refused *still coming up*. Starting is its own word, and its settings are still one press away.
   */
  it('says the driver is starting until its service is up', () => {
    const onDriver = vi.fn();
    render(<StatusBar driver="starting" sessions={0} workspace={null} remote={null} onDriver={onDriver} />);

    expect(screen.getByText('starting')).toBeTruthy();
    expect(screen.queryByText('ready')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: /driver/i }));
    expect(onDriver).toHaveBeenCalledTimes(1);
  });

  /**
   * AGT6: the tier leads where it is explained and changed — Daoris's own AI, on Settings — and its
   * words stay the service's, verbatim, with the note one hover away (D24).
   */
  it('lets the tier lead to where it is changed, in the service\'s own words', async () => {
    const onTier = vi.fn();
    render(
      <StatusBar
        driver="absent"
        sessions={0}
        workspace={null}
        remote={null}
        tier={{ label: 'lexical only', note: 'Set DAORIS_EMBED_MODEL to enable semantic recall.', semantic: false }}
        onTier={onTier}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: 'recall' }));
    expect(onTier).toHaveBeenCalled();
    expect(screen.getByText('lexical only')).toBeTruthy();
  });

  /** The scope is a control in its own right (WSP5) and replaces the read-only circle entirely. */
  it('gives the scope control the workspace slot rather than sitting beside it', () => {
    render(
      <StatusBar
        driver="running"
        sessions={0}
        workspace="default"
        remote={null}
        scope={<button type="button">switch circle</button>}
      />,
    );

    expect(screen.getByRole('button', { name: 'switch circle' })).toBeTruthy();
    expect(screen.queryByText('default')).toBeNull();
  });
});

/**
 * The output panel keeps the height the person gave it (D55), and it kept it as an empty bordered
 * well: on the installed window an ended session's console was a blank white box, which reads as a
 * field to type in. SURF8 had already given the console a sentence for this, and the panel never
 * passed one.
 */
/**
 * FRAME6: the edge a column is resized by. Keyboard-operable as the output panel's is — a resize that
 * needs a mouse is one some people do not have — and moving AWAY from its column grows the column,
 * whichever side of it the edge is on.
 */
describe('the splitter', () => {
  const edge = (props: Partial<Parameters<typeof Splitter>[0]> = {}) => {
    const change = vi.fn();
    const reset = vi.fn();
    render(<Splitter label="resize the rail" value={300} min={264} max={420} edge="right" onChange={change} onReset={reset} {...props} />);
    return { change, reset, handle: screen.getByRole('separator', { name: 'resize the rail' }) };
  };

  it('steps with the arrows away from its column, and goes to either end on Home and End', () => {
    const { change, handle } = edge();
    expect(handle).toHaveAttribute('aria-orientation', 'vertical');
    expect(handle).toHaveAttribute('aria-valuenow', '300');

    fireEvent.keyDown(handle, { key: 'ArrowRight' });
    fireEvent.keyDown(handle, { key: 'ArrowLeft' });
    fireEvent.keyDown(handle, { key: 'Home' });
    fireEvent.keyDown(handle, { key: 'End' });
    expect(change.mock.calls.map(([value]) => value)).toEqual([324, 276, 420, 264]);
  });

  it('grows a column on its left edge by moving left, and never past its bounds', () => {
    const { change, handle } = edge({ edge: 'left', value: 410 });
    fireEvent.keyDown(handle, { key: 'ArrowLeft' });
    expect(change).toHaveBeenLastCalledWith(420);
    fireEvent.keyDown(handle, { key: 'ArrowRight' });
    expect(change).toHaveBeenLastCalledWith(386);
  });

  it('follows a drag, and a double-click puts the column back where it started', () => {
    const { change, reset, handle } = edge();
    fireEvent.pointerDown(handle, { clientX: 100, button: 0 });
    fireEvent.pointerMove(window, { clientX: 160 });
    fireEvent.pointerUp(window);
    fireEvent.pointerMove(window, { clientX: 400 });
    expect(change.mock.calls.map(([value]) => value)).toEqual([360]);

    fireEvent.doubleClick(handle);
    expect(reset).toHaveBeenCalledOnce();
  });
});

describe('the output panel', () => {
  it('says why a session has nothing to show, instead of an empty well at the height it keeps', () => {
    // No shell in a unit test, so the console holds nothing and is not live: an ended session after
    // the app restarted, which is exactly the case the window showed.
    const { container } = render(
      <OutputPanel
        console={<SessionConsole id="s1a2b3c4" fill quiet={i18n.t('work.panel.silent')} />}
        height={180} collapsed={false} onResize={() => {}} onToggle={() => {}}
      />,
    );
    expect(screen.getByText(/keeps what a session prints while this app runs/)).toBeTruthy();
    expect(container.querySelector('pre')).toBeNull();
  });

  // CONSOLE2c: a tab for each thing that is running, so no console is missed.
  const TABS: PanelTab[] = [
    { key: 's1', kind: 'session', label: 'session', tone: 'idle', status: "the session's own console" },
    { key: 's1/subagent/a2fe', kind: 'subagent', label: 'Read README first line', tone: 'ended', status: 'subagent · completed' },
    { key: 's1/task/bs00', kind: 'task', label: 'dev server', tone: 'live', status: 'background · running' },
  ];

  it('has a tab for the session and each stream it runs, named by how each stands', () => {
    const onSelect = vi.fn();
    render(
      <OutputPanel
        console={null} height={180} collapsed={false} onResize={() => {}} onToggle={() => {}}
        tabs={TABS} selected="s1/task/bs00" onSelect={onSelect}
      />,
    );

    const tabs = screen.getAllByRole('tab');
    expect(tabs.map((tab) => tab.getAttribute('aria-label'))).toEqual([
      "session — the session's own console",
      'Read README first line — subagent · completed',
      'dev server — background · running',
    ]);
    expect(tabs.map((tab) => tab.getAttribute('aria-selected'))).toEqual(['false', 'false', 'true']);

    fireEvent.click(tabs[1]!);
    expect(onSelect).toHaveBeenCalledWith('s1/subagent/a2fe');
  });

  it('keeps the header it had when the session runs nothing beside itself', () => {
    render(
      <OutputPanel
        console={null} height={180} collapsed={false} onResize={() => {}} onToggle={() => {}}
        tabs={TABS.slice(0, 1)} selected="s1" onSelect={() => {}}
      />,
    );

    expect(screen.queryByRole('tablist')).toBeNull();
  });

  // DOCK1b: a region, not the console's alone — it holds whichever views stand in the panel.
  it('holds whichever views stand in it, a tab each, and draws the shown one', () => {
    const onView = vi.fn();
    render(
      <OutputPanel
        console={<p>the console</p>} height={180} collapsed={false} onResize={() => {}} onToggle={() => {}}
        tabs={TABS} selected="s1" views={['console', 'ask']} view="ask" onView={onView}
      >
        <p>the ask panel</p>
      </OutputPanel>,
    );

    const views = screen.getByRole('tablist', { name: 'views in the panel' });
    expect(Array.from(views.querySelectorAll('[role="tab"]')).map((tab) => [tab.getAttribute('aria-label'), tab.getAttribute('aria-selected')]))
      .toEqual([['Console', 'false'], ['Ask Daoris', 'true']]);
    expect(screen.getByText('the ask panel')).toBeTruthy();
    // The console's streams are the console's: not in the header while another view is shown.
    expect(screen.queryByText('the console')).toBeNull();
    expect(screen.queryByRole('tablist', { name: 'what is running' })).toBeNull();

    fireEvent.click(screen.getByRole('tab', { name: 'Console' }));
    expect(onView).toHaveBeenCalledWith('console');
  });

  it('is named for the one view it holds, with no tabs', () => {
    render(
      <OutputPanel console={null} height={180} collapsed={false} onResize={() => {}} onToggle={() => {}} views={['timeline']}>
        <p>the timeline</p>
      </OutputPanel>,
    );
    expect(screen.queryByRole('tablist')).toBeNull();
    expect(screen.getByText('Timeline')).toBeTruthy();
    expect(screen.getByText('the timeline')).toBeTruthy();
  });

  // DOCK1e: a view's tab dragged from the side bar and dropped here moves it; one of its own does not.
  it('takes a view dragged from the side bar, lit while it is over, and not one of its own', () => {
    const onMove = vi.fn();
    render(
      <OutputPanel console={null} height={180} collapsed={false} onResize={() => {}} onToggle={() => {}} views={['console', 'timeline']} onMove={onMove} />,
    );
    const panel = screen.getByRole('region', { name: 'the panel' });

    // Its own tab carries where it came from, so the panel does not light up for a move to itself.
    const own = carried();
    fireEvent.dragStart(screen.getByRole('tab', { name: 'Timeline' }), { dataTransfer: own });
    fireEvent.dragEnter(panel, { dataTransfer: own });
    expect(panel.querySelector('[aria-hidden].border-accent')).toBeNull();
    fireEvent.drop(panel, { dataTransfer: own });
    expect(onMove).not.toHaveBeenCalled();

    const from = carried({ 'application/x-daoris-view': 'ask', 'application/x-daoris-view-from-right': 'ask' });
    fireEvent.dragEnter(panel, { dataTransfer: from });
    expect(panel.querySelector('[aria-hidden].border-accent')).not.toBeNull();
    fireEvent.drop(panel, { dataTransfer: from });
    expect(onMove).toHaveBeenCalledWith('ask', 'panel');
    expect(panel.querySelector('[aria-hidden].border-accent')).toBeNull();
  });

  it('ignores a drag that is not a view — a file, a selection', () => {
    const onMove = vi.fn();
    render(<OutputPanel console={null} height={180} collapsed={false} onResize={() => {}} onToggle={() => {}} onMove={onMove} />);
    const panel = screen.getByRole('region', { name: 'the panel' });
    const file = carried({ Files: '' });
    fireEvent.dragEnter(panel, { dataTransfer: file });
    fireEvent.drop(panel, { dataTransfer: file });
    expect(panel.querySelector('[aria-hidden].border-accent')).toBeNull();
    expect(onMove).not.toHaveBeenCalled();
  });

  it('says how to fill it when every view has moved out, rather than an empty well', () => {
    render(<OutputPanel console={<p>the console</p>} height={180} collapsed={false} onResize={() => {}} onToggle={() => {}} views={[]} />);
    expect(screen.getByText(/Nothing is here now/)).toBeTruthy();
    expect(screen.queryByText('the console')).toBeNull();
  });

  // TABS1: the side bar's rule, a size down — a view's tab is its whole name or its icon, never a name
  // cut to one character (控…).
  describe('its views\' tabs', () => {
    let room: ReturnType<typeof layTabs> | undefined;
    afterEach(() => {
      room?.restore();
      room = undefined;
    });
    const panel = () => (
      <OutputPanel
        console={<p>the console</p>} height={180} collapsed={false} onResize={() => {}} onToggle={() => {}}
        views={['console', 'terminal']} view="console" onView={() => {}}
      />
    );
    const shown = () => Array.from(screen.getByRole('tablist', { name: 'views in the panel' }).querySelectorAll('[role="tab"]'))
      .map((tab) => [tab.getAttribute('aria-label'), tab.getAttribute('title'), tab.textContent]);

    it('keeps every name whole while the header holds them', () => {
      room = layTabs(900, { buttons: 130 });
      render(panel());
      expect(shown()).toEqual([['Console', 'Console', 'Console'], ['Terminal', 'Terminal', 'Terminal']]);
    });

    it('draws the unselected as its icon when the header cannot hold every name whole, named still', () => {
      // Console and Terminal need 268px, and a 300px header less its buttons holds 170.
      room = layTabs(300, { buttons: 130 });
      render(panel());

      expect(shown()).toEqual([['Console', 'Console', 'Console'], ['Terminal', 'Terminal', '']]);
      for (const tab of screen.getAllByRole('tab')) {
        expect(tab).toHaveClass('shrink-0');
        expect(tab.querySelector('.truncate')).toBeNull();
      }

      room.resize(400);
      expect(shown()).toEqual([['Console', 'Console', 'Console'], ['Terminal', 'Terminal', 'Terminal']]);
    });
  });
});
