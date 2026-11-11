import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../locales/i18n';
import type { PressReceiptNow } from '../person/pressReceipt';
import type { HelpPressNow, StandingConfirmationNow } from '../person/standing';
import { HelpPressLine } from './HelpPressLine';

// What a press that opened a place made, under its row in Notifications' list of what Ask Daoris pressed: the same line the conversation says,
// for one that ran with no card and, apart under *Asked you*, for one the person confirmed on its card.

const OPENED: PressReceiptNow = {
  kind: 'opened', repository: 'ledger', workspace: 'aurora', folder: '/work/aurora/ledger', line: 'main', room: null, quests: ['q7'], done: true,
};
const press = (id: string, more: Partial<HelpPressNow> = {}): HelpPressNow => ({
  id, module: 'DAORIS.DRIVER', route: 'REGISTRY_CREATE', session: 's1a2b3c4', scope: null, target: null, at: '2026-11-12T10:00:00Z', outcome: 'done', ...more,
});
const standing = (helpPresses: HelpPressNow[]): StandingConfirmationNow =>
  ({ holds: [], holders: [], stewardActs: [], stewardDesk: [], stewardLands: [], helpPresses });

describe("a press that opened a place, in Notifications' list", () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('says what it made under its row, and goes to the place it made', async () => {
    const onOpenTarget = vi.fn();
    render(<HelpPressLine standing={standing([press('h1', { receipt: OPENED }), press('h2', { route: 'SET_HOLD', target: 'engine' })])}
      onOpen={vi.fn()} onReverse={vi.fn()} onOpenTarget={onOpenTarget} defaultOpen />);

    const rows = within(screen.getByRole('list', { name: 'Presses Ask Daoris made' })).getAllByRole('listitem');
    expect(within(rows[0]!).getByText('Opened ledger in aurora: folder /work/aurora/ledger, line main, set-up quest #q7.')).toBeInTheDocument();
    expect(within(rows[1]!).queryByText(/^Opened /)).toBeNull();
    await userEvent.click(within(rows[0]!).getByRole('button', { name: 'Open ledger' }));
    await userEvent.click(within(rows[0]!).getByRole('button', { name: 'Open quest #q7' }));
    expect(onOpenTarget.mock.calls).toEqual([['repository', 'ledger'], ['quest', 'q7']]);
  });

  it('says it for a press the person confirmed, apart under Asked you', () => {
    render(<HelpPressLine standing={standing([press('h3', { asked: true, outcome: 'allowed', receipt: { ...OPENED, repository: 'other' } })])}
      onOpen={vi.fn()} onReverse={vi.fn()} defaultOpen />);

    const asked = within(screen.getByRole('list', { name: 'Asked you' }));
    expect(asked.getByText(/^Opened other in aurora/)).toBeInTheDocument();
  });

  it("says it in 中文 and a room's quest alone", async () => {
    await i18n.changeLanguage('zh');
    const room: PressReceiptNow = { kind: 'room', repository: 'engine', workspace: null, folder: null, line: null, room: 'tools/web', quests: ['q9'], done: true };
    render(<HelpPressLine standing={standing([press('h4', { route: 'PLACE_APPLY', receipt: room })])} onOpen={vi.fn()} onReverse={vi.fn()} defaultOpen />);

    expect(screen.getByText('已以委托 #q9 请 engine 自己的会话在 tools/web 建一个房间。')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^打开委托|^打开 engine/ })).toBeNull();
  });
});
