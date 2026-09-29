import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { LayoutToggles } from './LayoutToggles';

// DOCK1c (SURF11): the region toggles on the strip, beside the window controls, as VS Code's are.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

describe('the layout toggles', () => {
  it('names each region with its key, and says which are shown', () => {
    render(<LayoutToggles regions={['rail', 'panel', 'right']} closed={{ rail: false, panel: true, right: true }} onToggle={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'show or hide the session list (Ctrl+B)' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'show or hide the panel (Ctrl+J)' })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('button', { name: 'show or hide the right side bar (Ctrl+Alt+B)' })).toHaveAttribute('aria-pressed', 'false');
  });

  it('toggles the region pressed, and offers only the regions it is given', async () => {
    const onToggle = vi.fn();
    render(<LayoutToggles regions={['right']} closed={{ rail: false, panel: false, right: false }} onToggle={onToggle} />);

    expect(screen.getAllByRole('button')).toHaveLength(1);
    await userEvent.click(screen.getByRole('button', { name: /right side bar/ }));
    expect(onToggle).toHaveBeenCalledWith('right');
  });
});
