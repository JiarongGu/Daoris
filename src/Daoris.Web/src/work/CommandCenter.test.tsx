import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CommandCenter } from './CommandCenter';

describe('CommandCenter', () => {
  const open = () => screen.getByRole('button', { name: 'Search and run commands' });

  it('says where you are, which is what the empty middle was for', () => {
    render(<CommandCenter scope="testbed" onOpen={() => {}} label="Search and run commands" />);

    expect(screen.getByText('testbed')).toBeInTheDocument();
  });

  it('names what is attended beside the scope when something is', () => {
    render(
      <CommandCenter
        scope="testbed"
        detail="testbed-core · displayName"
        onOpen={() => {}}
        label="Search and run commands"
      />,
    );

    expect(screen.getByText('testbed-core · displayName')).toBeInTheDocument();
  });

  it('is a real target rather than a glyph — pressing it opens the palette', async () => {
    const onOpen = vi.fn();
    render(<CommandCenter scope="testbed" onOpen={onOpen} label="Search and run commands" />);

    await userEvent.click(open());

    expect(onOpen).toHaveBeenCalledOnce();
  });

  /**
   * 🔴 The shortcut is PRINTED, not implied. The palette existed before this and was reachable only
   * by a key nothing displayed, which is the same discoverability debt SURF10 took on and named.
   */
  it('prints the keyboard way in, so it is learnable rather than folklore', () => {
    render(
      <CommandCenter scope="testbed" shortcut="Ctrl K" onOpen={() => {}} label="Search and run commands" />,
    );

    expect(screen.getByText('Ctrl K')).toBeInTheDocument();
  });

  it('offers no shortcut where there is none to offer', () => {
    render(<CommandCenter scope="testbed" onOpen={() => {}} label="Search and run commands" />);

    expect(screen.queryByText(/Ctrl/)).not.toBeInTheDocument();
  });

  /**
   * 🔴 The centring wrapper spans the whole strip, and the strip is the window's drag handle. A
   * wrapper that took pointers would make the title bar undraggable everywhere except its ends —
   * which is invisible in a browser, where there is no window to move, and immediately wrong in the
   * shell. Asserted here because the shell is the one surface the unit loop cannot run in.
   */
  it('lets the title bar keep its drag everywhere except the pill itself', () => {
    const { container } = render(
      <CommandCenter scope="testbed" onOpen={() => {}} label="Search and run commands" />,
    );

    expect(container.firstElementChild).toHaveClass('pointer-events-none');
    expect(open()).toHaveClass('pointer-events-auto');
  });
});
