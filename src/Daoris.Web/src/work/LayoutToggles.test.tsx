import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { LayoutToggles } from './LayoutToggles';

// DOCK1c (SURF11): the region toggles on the strip, beside the window controls, as VS Code's are. D118
// §3a: the first is the view's list, named for the view, and absent where the view has none.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

describe('the layout toggles', () => {
  it('names each region with its key, the list by the view\'s own name for it, and says which are shown', () => {
    render(
      <LayoutToggles
        regions={['list', 'panel', 'right']}
        list="the session list"
        closed={{ list: false, panel: true, right: true }}
        onToggle={vi.fn()}
      />,
    );

    expect(screen.getByRole('button', { name: 'show or hide the session list (Ctrl+B)' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'show or hide the panel (Ctrl+J)' })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('button', { name: 'show or hide the right side bar (Ctrl+Alt+B)' })).toHaveAttribute('aria-pressed', 'false');
  });

  it('names another view\'s list by that view\'s name for it', () => {
    render(<LayoutToggles regions={['list', 'panel', 'right']} list="the quest list" closed={{ list: true, panel: false, right: false }} onToggle={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'show or hide the quest list (Ctrl+B)' })).toHaveAttribute('aria-pressed', 'false');
  });

  it('offers no list toggle where the view has no list: absent, never disabled', () => {
    render(<LayoutToggles regions={['list', 'panel', 'right']} closed={{ list: false, panel: false, right: false }} onToggle={vi.fn()} />);
    expect(screen.getAllByRole('button')).toHaveLength(2);
    expect(screen.queryByRole('button', { name: /Ctrl\+B\)$/ })).toBeNull();
  });

  it('toggles the region pressed, and offers only the regions it is given', async () => {
    const onToggle = vi.fn();
    render(<LayoutToggles regions={['right']} closed={{ list: false, panel: false, right: false }} onToggle={onToggle} />);

    expect(screen.getAllByRole('button')).toHaveLength(1);
    await userEvent.click(screen.getByRole('button', { name: /right side bar/ }));
    expect(onToggle).toHaveBeenCalledWith('right');
  });

  it('toggles the list by its own region, as a door to it that a list laid over leaves to it', async () => {
    const onToggle = vi.fn();
    render(<LayoutToggles regions={['list', 'right']} list="the session list" closed={{ list: false, panel: false, right: false }} onToggle={onToggle} />);
    const toggle = screen.getByRole('button', { name: /session list/ });
    expect(toggle).toHaveAttribute('data-list-door');
    expect(screen.getByRole('button', { name: /right side bar/ })).not.toHaveAttribute('data-list-door');
    await userEvent.click(toggle);
    expect(onToggle).toHaveBeenCalledWith('list');
  });
});
