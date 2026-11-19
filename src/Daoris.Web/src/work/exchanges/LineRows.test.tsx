import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, within } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../../locales/i18n';
import type { SessionEvent } from '../conversation/conversationTypes';
import { toTurns } from '../conversation/conversation';
import { ConversationView } from '../conversation/ConversationView';
import { lineRowsOf, rowsWithin } from './rows';
import { CHANNEL_WORDS, HELPER, OPINION, PIECE, at } from './rows.fixtures';
import { LineRows, type LineRowsProps } from './LineRows';

// A session of two turns whose line asked a helper, had its work read by a reviewer and took a message on its channel: all three
// read in its one timeline, each where it fell.

const SESSION: SessionEvent[] = [
  { seq: 1, at: at(0), kind: 'user', origin: 'person', text: 'make the users table read the new schema' },
  { seq: 2, at: at(1), kind: 'message', id: 'm-a', text: 'Asking a helper about the schema first.' },
  { seq: 3, at: at(3), kind: 'user', origin: 'person', text: 'go on' },
  { seq: 4, at: at(8), kind: 'message', id: 'm-b', text: 'Done; the reviewer read it.' },
];

const draw = (over: Partial<LineRowsProps> = {}, exchanges = [HELPER, OPINION]) => {
  const rows = lineRowsOf({ exchanges, messages: CHANNEL_WORDS });
  const props: LineRowsProps = { rows: [], attended: 's1', docked: [], onToDock: vi.fn(), ...over };
  render(
    <Tooltip.Provider>
      <ConversationView
        turns={toTurns(SESSION).turns}
        beneath={(ask, until, first) => <LineRows {...props} rows={rowsWithin(rows, first ? undefined : ask.at, until)} />}
      />
    </Tooltip.Provider>,
  );
  return props;
};

const rowOf = (id: string, side: 'asked' | 'back') => document.querySelector<HTMLElement>(`[data-exchange-row="${id}"][data-side="${side}"]`)!;

describe("a line's helper, second opinion and channel message in its one timeline", () => {
  it('reads all three, each by its actor, between the turns it fell between', () => {
    draw({ findings: { 'opinion:o1': 2 } });

    expect(rowOf('helper:h1', 'asked').textContent).toContain('Asked codex, as helper');
    expect(rowOf('helper:h1', 'back').textContent).toContain('codex, as helper: answered · taken');
    // The opinion's verdict beside what it judged.
    expect(rowOf('opinion:o1', 'back').textContent).toContain('claude, as reviewer: answered · taken · 2 findings · on this session’s work');
    const message = document.querySelector<HTMLElement>('[data-channel-row="m1"]')!;
    expect(message.textContent).toContain('db · claude');
    expect(message.textContent).toContain('The schema moved');

    // The helper was asked in the first turn; its answer, the message and the opinion came in the second.
    const [first, second] = [...document.querySelectorAll('[data-exchange-row], [data-channel-row]')].reduce<Element[][]>((turns, row) => {
      const turn = row.closest('ol')!.parentElement!;
      const index = turns.findIndex((each) => each[0]!.closest('ol')!.parentElement === turn);
      if (index < 0) turns.push([row]); else turns[index]!.push(row);
      return turns;
    }, []);
    expect(first!.map((row) => row.getAttribute('data-exchange-row') ?? row.getAttribute('data-channel-row'))).toEqual(['helper:h1']);
    expect(second!.map((row) => row.getAttribute('data-exchange-row') ?? row.getAttribute('data-channel-row')))
      .toEqual(['m1', 'helper:h1', 'opinion:o1', 'opinion:o1']);
  });

  it('opens the session that took a request up: a piece its own, a reviewer its own', () => {
    const onOpenSession = vi.fn();
    draw({ onOpenSession }, [{ ...PIECE, session: 'p-sess', agent: 'gemini' }, OPINION]);
    fireEvent.click(within(rowOf('piece:p1', 'asked')).getByRole('button', { name: 'Open its session' }));
    expect(onOpenSession).toHaveBeenCalledWith('p-sess');
    fireEvent.click(within(rowOf('opinion:o1', 'back')).getByRole('button', { name: 'Open its session' }));
    expect(onOpenSession).toHaveBeenLastCalledWith('r-sess');
  });

  it('answers a channel message in place, to its poster’s repository, and sends it on to its session', () => {
    const onPost = vi.fn();
    const onForward = vi.fn();
    const rows = lineRowsOf({ messages: [{ ...CHANNEL_WORDS[0]!, receipts: [{ repository: 'web', state: 'waiting' }] }] });
    render(<LineRows rows={rows} attended="s1" docked={[]} onToDock={vi.fn()} channel={{ onPost, onForward }} />);
    const row = document.querySelector<HTMLElement>('[data-channel-row="m1"]')!;

    fireEvent.click(within(row).getByRole('button', { name: 'Reply' }));
    fireEvent.change(within(row).getByRole('textbox'), { target: { value: 'Read it, thanks.' } });
    fireEvent.click(within(row).getByRole('button', { name: 'Send' }));
    expect(onPost).toHaveBeenCalledWith({ text: 'Read it, thanks.', to: 'db', re: 'm1' }, expect.anything());

    fireEvent.click(within(row).getByRole('button', { name: /Send to its session/ }));
    expect(onForward).toHaveBeenCalledWith('m1', 'web', expect.anything());
  });
});

describe('a request that waits on the person', () => {
  it('leads to its line in the dock and offers no press of its own', () => {
    const onToDock = vi.fn();
    const waiting = { ...HELPER, outcome: 'asked', holds: true, answered: undefined, session: undefined };
    draw({ docked: ['helper:h1'], onToDock }, [waiting]);
    const row = rowOf('helper:h1', 'asked');
    expect(within(row).getAllByRole('button').map((press) => press.textContent)).toEqual(['Waits on you, in the dock']);
    fireEvent.click(within(row).getByRole('button'));
    expect(onToDock).toHaveBeenCalledWith('helper:h1');
  });
});
