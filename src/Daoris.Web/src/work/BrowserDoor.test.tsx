import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { BrowserDoor } from './BrowserDoor';

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

/**
 * BRW7: Daoris's browser has a door on the strip as visible as a region toggle, not only View → Browser
 * and the palette. A molecule: a press goes out, and the shell decides what opens.
 */
describe('the browser door', () => {
  it('is named for what it opens, and opens it on a press', async () => {
    const onOpen = vi.fn();
    render(<BrowserDoor onOpen={onOpen} />);

    await userEvent.click(screen.getByRole('button', { name: "open Daoris's browser" }));

    expect(onOpen).toHaveBeenCalledOnce();
  });
});
