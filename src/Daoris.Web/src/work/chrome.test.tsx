import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { ActivityBar, AppStrip } from './frame';

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
  it('carries the wordmark, and the serif appears exactly once (D41 §1)', () => {
    const { container } = render(
      <AppStrip mode="manage" modeAvailable onMode={() => {}} />,
    );
    expect(screen.getByText('Daoris')).toBeTruthy();
    expect(screen.getByText('道衍')).toBeTruthy();
    expect(container.querySelectorAll('.font-serif')).toHaveLength(1);
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
