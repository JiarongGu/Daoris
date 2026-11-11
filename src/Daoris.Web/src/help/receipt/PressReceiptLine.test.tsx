import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { PressReceiptNow } from '../../person/pressReceipt';
import { PressReceiptLine } from './PressReceiptLine';

// What a press that opened a place says it made, as one line with the places it made go to: a department's folder, line and set-up quest, or a
// room's one quest alone.

const OPENED: PressReceiptNow = {
  kind: 'opened', repository: 'ledger', workspace: 'aurora', folder: '/work/aurora/ledger', line: 'main', room: null, quests: ['q7'], done: true,
};
const ROOM: PressReceiptNow = { kind: 'room', repository: 'engine', workspace: null, folder: null, line: null, room: 'tools/web', quests: ['q9'], done: true };

describe('a press that opened a place, said once', () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('says what a department was opened with, and goes to its page and its quest', async () => {
    const onOpen = vi.fn();
    render(<PressReceiptLine receipt={OPENED} onOpen={onOpen} />);

    expect(screen.getByText('Opened ledger in aurora: folder /work/aurora/ledger, line main, set-up quest #q7.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Open ledger' }));
    await userEvent.click(screen.getByRole('button', { name: 'Open quest #q7' }));
    expect(onOpen.mock.calls).toEqual([['repository', 'ledger'], ['quest', 'q7']]);
  });

  it("says a room's receipt as its quest's line alone", async () => {
    const onOpen = vi.fn();
    render(<PressReceiptLine receipt={ROOM} onOpen={onOpen} />);

    expect(screen.getByText("Asked engine's own session, as quest #q9, for a room at tools/web.")).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Open engine' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Open quest #q9' }));
    expect(onOpen).toHaveBeenCalledWith('quest', 'q9');
  });

  it('says a folder department has no line or quest, and a creation that stopped is not said opened', () => {
    const { rerender } = render(<PressReceiptLine receipt={{ ...OPENED, line: null, quests: [] }} onOpen={vi.fn()} />);
    expect(screen.getByText('Opened ledger in aurora: folder /work/aurora/ledger.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Open quest/ })).toBeNull();

    rerender(<PressReceiptLine receipt={{ ...OPENED, quests: [], done: false }} onOpen={vi.fn()} />);
    expect(screen.getByText('Did not finish opening ledger in aurora: folder /work/aurora/ledger; press the same again to go on.')).toBeInTheDocument();

    rerender(<PressReceiptLine receipt={{ ...ROOM, quests: [], done: false }} onOpen={vi.fn()} />);
    expect(screen.getByText('No quest was asked of engine for a room at tools/web.')).toBeInTheDocument();
  });

  it('draws as words alone where nothing opens a place', () => {
    render(<PressReceiptLine receipt={OPENED} />);

    expect(screen.getByText(/^Opened ledger in aurora/)).toBeInTheDocument();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('words it in 中文, the names and the folder as written', async () => {
    await i18n.changeLanguage('zh');
    const onOpen = vi.fn();
    render(<PressReceiptLine receipt={OPENED} onOpen={onOpen} />);

    expect(screen.getByText('已开设 ledger（工作区 aurora）：文件夹 /work/aurora/ledger，主线 main，配置委托 #q7。')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: '打开 ledger' }));
    await userEvent.click(screen.getByRole('button', { name: '打开委托 #q7' }));
    expect(onOpen.mock.calls).toEqual([['repository', 'ledger'], ['quest', 'q7']]);

    cleanup();
    render(<PressReceiptLine receipt={ROOM} onOpen={onOpen} />);
    expect(screen.getByText('已以委托 #q9 请 engine 自己的会话在 tools/web 建一个房间。')).toBeInTheDocument();
  });
});
