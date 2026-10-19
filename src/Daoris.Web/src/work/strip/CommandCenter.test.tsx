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
   * 🔴 It is the pill and nothing around it. It used to bring a centring wrapper that spanned the
   * whole strip, which had to refuse pointers or the title bar would lose its drag; the strip lays
   * the pill out now (UX5 U15), so there is no wrapper to take a press.
   */
  it('is the pill alone, so no wrapper of its own can take the title bar\'s drag', () => {
    const { container } = render(
      <CommandCenter scope="testbed" onOpen={() => {}} label="Search and run commands" />,
    );

    expect(container.firstElementChild).toBe(open());
  });

  /**
   * UXFIX1b: a target of 28 px at least, the platform language's floor (§6), where `h-6` drew it 24 px tall; and no
   * taller than the 36 px strip it sits in (`AppStrip`'s `h-9`), so the strip keeps its height and centres it. A minimum
   * rather than a fixed height, as the menu bar's names have (UXFIX1); jsdom lays nothing out, so this holds the classes
   * and the window shows the box.
   */
  it('is a target of 28 px at least, and no taller than the strip', () => {
    render(<CommandCenter scope="testbed" onOpen={() => {}} label="Search and run commands" />);
    /** A Tailwind spacing class's size in px, on the 4 px step. */
    const px = (prefix: string) => {
      const step = new RegExp(`(?:^|\\s)${prefix}-(\\d+(?:\\.\\d+)?)(?:\\s|$)`).exec(open().className)?.[1];
      return step === undefined ? undefined : Number(step) * 4;
    };

    expect(px('min-h')).toBeGreaterThanOrEqual(28);
    expect(px('min-h')).toBeLessThanOrEqual(36);
    // Narrowed to its glyph alone, it is still a target as wide as it is tall.
    expect(px('min-w')).toBeGreaterThanOrEqual(28);
    // One rule for its height: a fixed one under the floor beside the minimum would say 24 px and draw 28.
    expect(px('h')).toBeUndefined();
  });

  /**
   * 🔴 Narrowed to its least (a 500px window, UX5 U15), the pill clipped its own words: `defau`,
   * cut mid-letter, with the shortcut pushed out of sight. The shortcut goes first below 12rem of
   * pill, then the detail, then the scope ends in an ellipsis. jsdom has no container queries, so
   * this holds the rules and the window shows them.
   */
  it('gives up the shortcut first when narrowed, and ends the scope in an ellipsis rather than a cut', () => {
    render(
      <CommandCenter scope="testbed" detail="a chat" shortcut="Ctrl K" onOpen={() => {}} label="Search and run commands" />,
    );

    expect(open()).toHaveClass('@container');
    expect(screen.getByText('Ctrl K')).toHaveClass('@max-[12rem]:hidden');
    expect(screen.getByText('testbed')).toHaveClass('truncate', 'min-w-0');
    expect(screen.getByText('testbed')).not.toHaveClass('shrink-0');
    expect(screen.getByText('a chat')).toHaveClass('truncate');
  });
});
