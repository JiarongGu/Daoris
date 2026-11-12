import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { PressReceiptNow } from '../../person/pressReceipt';
import type { HelpPressNow } from '../../person/standing';
import { receiptsBeneath, receiptsOfTurn } from './receiptsBeneath';
import { goDoorOf } from './goDoorOf';

// What a conversation's presses made, drawn under the turn that pressed them: the receipts of its own session, within the turn's time and no
// other's, each said once, and a turn that opened nothing draws nothing.

const OPENED: PressReceiptNow = {
  kind: 'opened', repository: 'ledger', workspace: 'aurora', folder: '/work/aurora/ledger', line: 'main', room: null, quests: ['q7'], done: true,
};
const press = (id: string, session: string, at: string, receipt?: PressReceiptNow): HelpPressNow => ({
  id, module: 'DAORIS.DRIVER', route: 'REGISTRY_CREATE', session, scope: null, target: null, at, outcome: 'done', ...(receipt ? { receipt } : {}),
});

const PRESSES = [
  press('a', 's1', '2026-11-12T10:05:00Z', OPENED),
  press('b', 's2', '2026-11-12T10:06:00Z', { ...OPENED, repository: 'elsewhere' }),
  press('c', 's1', '2026-11-12T10:07:00Z'),
  press('d', 's1', '2026-11-12T10:20:00Z', { ...OPENED, repository: 'later' }),
];

describe("a turn's receipts under it", () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it("holds the conversation's own receipts from the turn's words until the next turn's", () => {
    expect(receiptsOfTurn(PRESSES, 's1', '2026-11-12T10:00:00Z', '2026-11-12T10:15:00Z').map((each) => each.id)).toEqual(['a']);
    expect(receiptsOfTurn(PRESSES, 's1', '2026-11-12T10:15:00Z', undefined).map((each) => each.id)).toEqual(['d']);
    expect(receiptsOfTurn(undefined, 's1', '2026-11-12T10:00:00Z', undefined)).toEqual([]);
  });

  it('draws one line for each, and nothing for a turn that opened nothing', () => {
    const beneath = receiptsBeneath(PRESSES, 's1', undefined);

    expect(beneath({ at: '2026-11-12T10:30:00Z' }, undefined)).toBeNull();
    render(<div>{beneath({ at: '2026-11-12T10:00:00Z' }, '2026-11-12T10:15:00Z')}</div>);
    expect(screen.getAllByText(/^Opened /)).toHaveLength(1);
    expect(screen.getByText('Opened ledger in aurora: folder /work/aurora/ledger, line main, set-up quest #q7.')).toBeInTheDocument();
  });

  it("goes to the repository's page and the quest as the window's own doors", async () => {
    const go = vi.fn();
    const beneath = receiptsBeneath(PRESSES, 's1', (scope, id) => go(goDoorOf(scope, id)));

    render(<div>{beneath({ at: '2026-11-12T10:00:00Z' }, '2026-11-12T10:15:00Z')}</div>);
    await userEvent.click(screen.getByRole('button', { name: 'Open ledger' }));
    await userEvent.click(screen.getByRole('button', { name: 'Open quest #q7' }));

    expect(go.mock.calls).toEqual([[{ view: 'projects', item: 'ledger' }], [{ view: 'quests', item: 'q7' }]]);
  });

  it('says it in 中文 under the same turn', async () => {
    await i18n.changeLanguage('zh');
    render(<div>{receiptsBeneath(PRESSES, 's1', undefined)({ at: '2026-11-12T10:00:00Z' }, '2026-11-12T10:15:00Z')}</div>);

    expect(screen.getByText('已开设 ledger（工作区 aurora）：文件夹 /work/aurora/ledger，主线 main，配置委托 #q7。')).toBeInTheDocument();
  });
});
