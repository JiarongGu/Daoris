import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { QuickAsk } from './QuickAsk';

// DOCK1d: Ask Daoris's conversation in a box at the palette's place — VS Code's Quick Chat.

function show(open = true) {
  const onClose = vi.fn();
  const onExpand = vi.fn();
  render(
    <Tooltip.Provider>
      <QuickAsk open={open} onClose={onClose} onExpand={onExpand}>
        <p>the conversation</p>
        <textarea aria-label="message" />
      </QuickAsk>
    </Tooltip.Provider>,
  );
  return { onClose, onExpand };
}

afterEach(cleanup);

describe('Quick Ask', () => {
  it('holds the conversation it is handed, named, with the message box ready to type in', () => {
    show();
    expect(screen.getByRole('dialog', { name: 'Quick Ask' })).toHaveAttribute('aria-modal', 'true');
    expect(screen.getByText('the conversation')).toBeTruthy();
    // Opened for a question, so the box, not the first button, has the keys.
    expect(document.activeElement).toBe(screen.getByRole('textbox', { name: 'message' }));
  });

  it('carries the conversation to the side bar, or closes, on the person\'s press', async () => {
    const { onClose, onExpand } = show();
    await userEvent.click(screen.getByRole('button', { name: 'open in the side bar' }));
    expect(onExpand).toHaveBeenCalledOnce();

    await userEvent.click(screen.getByRole('button', { name: 'close Quick Ask' }));
    expect(onClose).toHaveBeenCalled();
  });

  it('closes on Escape, as the palette does', async () => {
    const { onClose } = show();
    await userEvent.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalled();
  });

  it('draws nothing while closed', () => {
    show(false);
    expect(screen.queryByRole('dialog')).toBeNull();
  });
});
