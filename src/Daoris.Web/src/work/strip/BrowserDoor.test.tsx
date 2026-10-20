import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
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

    await userEvent.click(screen.getByRole('button', { name: "Open Daoris's browser" }));

    expect(onOpen).toHaveBeenCalledOnce();
  });
});

/**
 * BRW8: whose hands are on the page, said beside the door in words, before the person types into it —
 * and a way to the session, so they can watch it or stop it.
 */
describe('who is driving', () => {
  const ONE = [{ id: 's1a2b3c4', name: 'engine · Read the ticket' }];
  const TWO = [...ONE, { id: 'c0ffee00', name: 'game · conversation' }];

  it('says nothing when no session is driving it', () => {
    render(<BrowserDoor onOpen={() => {}} drivers={[]} onAttend={() => {}} />);

    expect(screen.queryByText(/driven by/)).toBeNull();
    expect(screen.getAllByRole('button')).toHaveLength(1);
  });

  it('names the one session driving it, and opens that session on a press', async () => {
    const onAttend = vi.fn();
    render(<BrowserDoor onOpen={() => {}} drivers={ONE} onAttend={onAttend} />);

    const chip = screen.getByRole('button', { name: 'Daoris\'s browser is driven by engine · Read the ticket — open the session' });
    expect(chip).toHaveTextContent('browser driven by engine · Read the ticket');
    await userEvent.click(chip);

    expect(onAttend).toHaveBeenCalledWith('s1a2b3c4');
  });

  it('counts two, and lists each to open', async () => {
    const onAttend = vi.fn();
    const user = userEvent.setup();
    render(<BrowserDoor onOpen={() => {}} drivers={TWO} onAttend={onAttend} />);

    const chip = screen.getByRole('button', { name: "Daoris's browser is driven by 2 sessions" });
    expect(chip).toHaveTextContent('browser driven by 2 sessions');
    chip.focus();
    await user.keyboard('{Enter}');
    const items = await screen.findAllByRole('menuitem');
    expect(items.map((item) => item.textContent)).toEqual(['engine · Read the ticket', 'game · conversation']);

    await user.click(items[1]!);
    expect(onAttend).toHaveBeenCalledWith('c0ffee00');
  });

  it('still says who is driving where there is nowhere to open it, as words rather than a door', () => {
    render(<BrowserDoor onOpen={() => {}} drivers={ONE} />);

    expect(screen.getByText('browser driven by engine · Read the ticket')).toBeTruthy();
    expect(screen.getAllByRole('button')).toHaveLength(1);
  });

  /**
   * SESSBADGE1: in Chinese the chip said *2 个会话正在操作* ("2 sessions are working") beside the browser's door, one more count of
   * sessions working that disagreed with the others. It names the browser, first, so a long name is cut after it, never before.
   */
  describe('names the browser it counts for, in both languages', () => {
    afterEach(() => i18n.changeLanguage('en'));

    it('in English', () => {
      render(<BrowserDoor onOpen={() => {}} drivers={TWO} />);
      expect(screen.getByText('browser driven by 2 sessions')).toBeTruthy();
    });

    it('in Chinese', async () => {
      await i18n.changeLanguage('zh');
      const { unmount } = render(<BrowserDoor onOpen={() => {}} drivers={TWO} />);
      expect(screen.getByText('浏览器由 2 个会话操作')).toBeTruthy();
      unmount();

      render(<BrowserDoor onOpen={() => {}} drivers={ONE} />);
      expect(screen.getByText('浏览器由 engine · Read the ticket 操作')).toBeTruthy();
    });
  });
});
