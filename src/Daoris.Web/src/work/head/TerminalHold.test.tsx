import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import { holdsOf } from '../../bridge/hold';
import { TerminalHold } from './TerminalHold';

// A conversation a terminal holds (RPC1g1, D238 point 2): its head says so with the terminal's process, in each shipped language, and
// offers the person *Let go of the terminal*.

const HOLD = { session: '3f2a91c4', asker: { process: 'pwsh', pid: 4120 }, since: '2026-11-06T09:00:00Z' };

describe('a conversation held by a terminal', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it.each([
    ['en', 'Held by a terminal', 'pwsh, process 4120, speaks for you in this conversation until it lets go.', 'Let go of the terminal'],
    ['zh', '由终端持有', 'pwsh（进程 4120）在这段对话中代你发言，直到它放开。', '从终端收回'],
  ])('says who holds it, and offers to let go (%s)', async (language, title, who, letGo) => {
    await i18n.changeLanguage(language);
    render(<TerminalHold hold={HOLD} onLetGo={() => {}} />);
    expect(screen.getByRole('heading', { name: title })).toBeInTheDocument();
    expect(screen.getByText(who)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: letGo })).toBeInTheDocument();
  });

  it('names a terminal the system would not name by its process id alone, or not at all', () => {
    const { rerender } = render(<TerminalHold hold={{ ...HOLD, asker: { process: null, pid: 77 } }} />);
    expect(screen.getByText('Process 77 speaks for you in this conversation until it lets go.')).toBeInTheDocument();
    rerender(<TerminalHold hold={{ ...HOLD, asker: null }} />);
    expect(screen.getByText('A terminal speaks for you in this conversation until it lets go.')).toBeInTheDocument();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('lets go when the person presses it', async () => {
    const onLetGo = vi.fn();
    render(<TerminalHold hold={HOLD} onLetGo={onLetGo} />);
    await userEvent.click(screen.getByRole('button', { name: 'Let go of the terminal' }));
    expect(onLetGo).toHaveBeenCalledOnce();
  });

  it('reads the shell’s telling of the holds, and nothing of another shape', () => {
    expect(holdsOf({ seq: 3, holds: [HOLD, { nope: true }] })).toEqual({ seq: 3, holds: [HOLD] });
    expect(holdsOf({ seq: 1 })).toBeNull();
  });
});
