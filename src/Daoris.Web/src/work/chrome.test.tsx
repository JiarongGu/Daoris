import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { ActivityBar, AppStrip, OutputPanel, StatusBar } from './frame';

/** The provider the application mounts once (`main.tsx`); a tooltip outside one throws. */
const render = (node: ReactElement) => {
  const result = rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);
  return {
    ...result,
    rerender: (next: ReactElement) => result.rerender(<Tooltip.Provider>{next}</Tooltip.Provider>),
  };
};

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
   * 🔴 The strip carries the MARK and no name (owner, 2026-09-22). The wordmark was 20px of serif in
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
 * The status bar, after the owner's *"the bottom styling still not really close to vscode which have
 * better design with display and action (on click or on hover)"* (2026-09-22).
 *
 * The complaint has two halves and only one of them is visual. **Display** is the part a screenshot
 * settles, and it did: an 11px run of `·`-joined prose on a 2px-tall strip reads as a caption on the
 * window, not as a bar. **Action** is what these assertions are for — the reference console's status
 * items are *targets*: each is a full-height box that lights on hover and does something on click,
 * and every one of them says what it will do before you press it.
 */
describe('StatusBar', () => {
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
describe('the output panel', () => {
  it('says why a session has nothing to show, instead of an empty well at the height it keeps', () => {
    // No shell in a unit test, so the console holds nothing and is not live: an ended session after
    // the app restarted, which is exactly the case the window showed.
    const { container } = render(
      <OutputPanel sessionId="s1a2b3c4" height={180} collapsed={false} onResize={() => {}} onToggle={() => {}} />,
    );
    expect(screen.getByText(/keeps what a session prints while this app runs/)).toBeTruthy();
    expect(container.querySelector('pre')).toBeNull();
  });
});
