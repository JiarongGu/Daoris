import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { ActivityBar, AppStrip, StatusBar } from './frame';

/** The provider the application mounts once (`main.tsx`); a tooltip outside one throws. */
const render = (node: ReactElement) => {
  const result = rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);
  return {
    ...result,
    rerender: (next: ReactElement) => result.rerender(<Tooltip.Provider>{next}</Tooltip.Provider>),
  };
};

// The application's chrome (D56), as molecules: props in, states out. No shell, no service, no
// arranged world — which is what lets "in a browser", "in Work" and "with attention" all be
// ordinary assertions rather than integration setup (components plan §2).

const DOMAINS = [
  { tab: 'overview' as const, label: 'Overview', icon: 'overview' as const },
  { tab: 'quests' as const, label: 'Quests', icon: 'quests' as const, badge: 3 },
  { tab: 'settings' as const, label: 'Machine', icon: 'settings' as const },
];

describe('AppStrip', () => {
  /**
   * 🔴 The strip carries the MARK and no name (owner, 2026-09-22). The wordmark was 20px of serif in
   * every window forever, saying the name of the thing you are already looking at; a title bar in an
   * IDE says what you can DO, and the name moved to About.
   *
   * D41 §1's rule survives and is now stricter: the serif's one appearance is not spent here at all.
   */
  it('carries the mark and not the name — the serif is not spent on the strip', () => {
    const { container } = render(
      <AppStrip mode="manage" modeAvailable onMode={() => {}} />,
    );
    expect(screen.queryByText('Daoris')).toBeNull();
    expect(container.querySelector('svg')).toBeTruthy();
    expect(container.querySelectorAll('.font-serif')).toHaveLength(0);
  });

  it('offers no mode switch in a browser — absent, not disabled', () => {
    render(<AppStrip mode="manage" modeAvailable={false} onMode={() => {}} />);
    expect(screen.queryByRole('button', { name: /work/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /manage/i })).toBeNull();
  });

  it('switches frames', async () => {
    const onMode = vi.fn();
    render(<AppStrip mode="manage" modeAvailable onMode={onMode} />);
    await userEvent.click(screen.getByRole('button', { name: /work/i }));
    expect(onMode).toHaveBeenCalledWith('work');
  });

  it('holds the scope slot — which is empty while the family is one circle (WSP5)', () => {
    const { rerender } = render(<AppStrip mode="manage" modeAvailable onMode={() => {}} />);
    expect(screen.queryByText('every circle')).toBeNull();
    rerender(
      <AppStrip mode="manage" modeAvailable onMode={() => {}} scope={<span>every circle</span>} />,
    );
    expect(screen.getByText('every circle')).toBeTruthy();
  });

  /**
   * The room is reserved only where a window will claim it. SURF7 filled it: those slots are now the
   * rectangles the window paints its caption buttons into, which is why they are empty and why there
   * are exactly three. What is DONE with them belongs to `windowChrome.test.tsx`.
   */
  it('reserves the caption room only when asked, so nothing shifts when a window claims it', () => {
    const { container, rerender } = render(
      <AppStrip mode="manage" modeAvailable onMode={() => {}} />,
    );
    expect(container.querySelectorAll('[data-caption]')).toHaveLength(0);
    rerender(<AppStrip mode="manage" modeAvailable onMode={() => {}} captionRoom />);
    expect(container.querySelectorAll('[data-caption]')).toHaveLength(3);
  });
});

describe('ActivityBar', () => {
  it('names every domain, because an icon-only control has no name otherwise (D41 §6)', () => {
    render(<ActivityBar label="Domains" items={DOMAINS} active="quests" onSelect={() => {}} />);
    for (const { label } of DOMAINS) {
      expect(screen.getByRole('button', { name: label })).toBeTruthy();
    }
    expect(screen.getByRole('navigation', { name: 'Domains' })).toBeTruthy();
  });

  it('marks the current domain, and marks nothing in a frame that has none', () => {
    const { rerender } = render(
      <ActivityBar label="Domains" items={DOMAINS} active="quests" onSelect={() => {}} />,
    );
    expect(screen.getByRole('button', { name: 'Quests' }).getAttribute('aria-current')).toBe('page');

    // In Work no domain is current — the current thing is the other frame (D56).
    rerender(<ActivityBar label="Domains" items={DOMAINS} active={null} onSelect={() => {}} />);
    expect(screen.queryByRole('button', { current: 'page' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Quests' })).toBeTruthy();
  });

  it('carries the outstanding count, and wears nothing at zero', () => {
    const { rerender } = render(
      <ActivityBar label="Domains" items={DOMAINS} active="overview" onSelect={() => {}} />,
    );
    expect(screen.getByText('3')).toBeTruthy();
    rerender(
      <ActivityBar
        label="Domains"
        items={DOMAINS.map((item) => ({ ...item, badge: item.badge && 0 }))}
        active="overview"
        onSelect={() => {}}
      />,
    );
    expect(screen.queryByText('3')).toBeNull();
    expect(screen.queryByText('0')).toBeNull();
  });

  it('selects a domain — the door back into Manage from Work', async () => {
    const onSelect = vi.fn();
    render(<ActivityBar label="Domains" items={DOMAINS} active={null} onSelect={onSelect} />);
    await userEvent.click(screen.getByRole('button', { name: 'Machine' }));
    expect(onSelect).toHaveBeenCalledWith('settings');
  });

  it('shows only the domains it is given — a browser gets no Machine at all', () => {
    render(
      <ActivityBar
        label="Domains"
        items={DOMAINS.filter((item) => item.tab !== 'settings')}
        active="overview"
        onSelect={() => {}}
      />,
    );
    expect(screen.queryByRole('button', { name: 'Machine' })).toBeNull();
  });

  it('holds its footer actions below the domains', () => {
    render(
      <ActivityBar
        label="Domains"
        items={DOMAINS}
        active="overview"
        onSelect={() => {}}
        footer={<button type="button">refresh</button>}
      />,
    );
    expect(screen.getByRole('button', { name: 'refresh' })).toBeTruthy();
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
