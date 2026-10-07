import { describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from '../i18n';
import { ClearAsk } from './ClearAsk';
import type { Answered } from './InlineConfirm';
import {
  ASK_PLAN, FAILED_PLAN, QUEST_ALONE, QUEST_FORGOTTEN, QUEST_PLAN, WORKSPACE_FILES_ONLY, WORKSPACE_KEPT, WORKSPACE_LEFT_OVER,
  WORKSPACE_PLAN,
} from './historyFixtures';

// A clear's first press (HIST1e, D153; the history-clearing design §5, §6.1), as a molecule: it lists what the clear would
// take and what it would keep, each kept unit with its reason's sentence and its door, and its second press sends exactly
// the units it listed, held from when the list opened. Every state is reached by passing it, and every press goes out.

const QUEST = { scope: 'quest', id: '9a8b7c' } as const;
const WORKSPACE = { scope: 'workspace', id: 'aurora' } as const;

describe('a clear’s first press (design §5 step 1)', () => {
  it('says what a quest’s clear takes and that nothing brings it back, then the move and never mind', async () => {
    const clear = vi.fn();
    const close = vi.fn();
    render(<ClearAsk target={QUEST} plan={QUEST_PLAN} meanIt="Clear quest" onClear={clear} onClose={close} />);

    const ask = screen.getByRole('group', { name: 'clear from this machine' });
    expect(ask).toHaveTextContent(
      'Clears #9a8b7c, its 3 sessions and what this machine kept of them: their words, transcripts and files, 2.1 MB. Nothing brings it back.');
    expect(within(ask).getAllByRole('button').map((button) => button.textContent)).toEqual(['Clear quest', 'Never mind']);
    await userEvent.click(within(ask).getByRole('button', { name: 'Never mind' }));
    expect(close).toHaveBeenCalledOnce();
  });

  it('says the remote keeps the team’s copy where the quest is forgotten here, and a quest that goes alone', () => {
    const { rerender } = render(
      <ClearAsk target={QUEST} plan={QUEST_FORGOTTEN} meanIt="Clear quest" onClear={() => {}} onClose={() => {}} />,
    );
    expect(screen.getByRole('group')).toHaveTextContent("The remote for aurora keeps the team’s copy; this machine will not fetch it again.");

    rerender(<ClearAsk key="alone" target={QUEST} plan={QUEST_ALONE} meanIt="Clear quest" onClear={() => {}} onClose={() => {}} />);
    expect(screen.getByRole('group')).toHaveTextContent('Clears #9a8b7c and what this machine kept of it: its words and files, 12 KB.');
  });

  it('says an ask’s work whole, and a quest’s failed sessions with the teammate’s it keeps', () => {
    const { unmount } = render(
      <ClearAsk target={{ scope: 'ask', id: 'a1b2c3' }} plan={ASK_PLAN} meanIt="Clear ask" onClear={() => {}} onClose={() => {}} />,
    );
    expect(screen.getByRole('group')).toHaveTextContent(
      'Clears ask #a1b2c3, the 2 quests it became, their 5 sessions and what this machine kept of them');
    unmount();

    render(<ClearAsk target={{ scope: 'failed', id: '9a8b7c' }} plan={FAILED_PLAN} meanIt="Clear 2" onClear={() => {}} onClose={() => {}} />);
    const ask = screen.getByRole('group');
    expect(ask).toHaveTextContent('Clears 2 failed sessions of #9a8b7c');
    expect(ask).toHaveTextContent('The quest and its other sessions stay.');
    expect(within(ask).getByRole('list', { name: 'What stays' }))
      .toHaveTextContent('laptop/f9e8d7c6 ran on laptop, and its record is theirs: it stays here.');
  });
});

describe('a workspace’s first press (design §6.1)', () => {
  it('lists the counts by kind, and every unit kept with its reason’s sentence and the door that frees it', async () => {
    const doors = { branches: vi.fn(), sync: vi.fn(), quest: vi.fn(), ask: vi.fn(), session: vi.fn() };
    render(<ClearAsk target={WORKSPACE} plan={WORKSPACE_PLAN} meanIt="Clear 3" doors={doors} onClear={() => {}} onClose={() => {}} />);

    const going = screen.getByRole('list', { name: 'What goes' });
    expect(within(going).getAllByRole('listitem').map((row) => row.textContent)).toEqual([
      '4 closed quests', '1 ask', '6 sessions', '1 copy of a teammate’s record', '3.1 MB left over from records already gone',
    ]);
    const staying = screen.getByRole('list', { name: 'What stays' });
    const landing = within(staying).getByRole('listitem', { name: 'Quest #6c7d8e' });
    expect(landing).toHaveTextContent(
      'The branch feature/chunk-budget-6c7d8e a landing made still stands in engine, so it was not cleared. Clean it up once it has merged.');
    await userEvent.click(within(landing).getByRole('button', { name: 'Open branches' }));
    expect(doors.branches).toHaveBeenCalledOnce();

    await userEvent.click(within(within(staying).getByRole('listitem', { name: 'Ask #b2c3d4' })).getByRole('button', { name: 'Open ask #b2c3d4' }));
    expect(doors.ask).toHaveBeenCalledWith('b2c3d4');
    await userEvent.click(within(within(staying).getByRole('listitem', { name: 'Quest #9f0a1b' })).getByRole('button', { name: 'Open #9f0a1b' }));
    expect(doors.quest).toHaveBeenCalledWith('9f0a1b');
    await userEvent.click(within(within(staying).getByRole('listitem', { name: 'Quest #2b3c4d' })).getByRole('button', { name: 'Open in Sessions' }));
    expect(doors.session).toHaveBeenCalledWith('s2b3c4d5');
    await userEvent.click(within(within(staying).getByRole('listitem', { name: 'Quest #5d6e7f' })).getByRole('button', { name: 'Sync now' }));
    expect(doors.sync).toHaveBeenCalledOnce();
  });

  it('draws no door this page was not handed', () => {
    render(<ClearAsk target={WORKSPACE} plan={WORKSPACE_PLAN} meanIt="Clear 3" onClear={() => {}} onClose={() => {}} />);
    expect(within(screen.getByRole('list', { name: 'What stays' })).queryAllByRole('button')).toEqual([]);
  });

  it('says a kept unit’s sentence in 中文, by its code and variant', () => {
    render(
      <I18nextProvider i18n={i18n.cloneInstance({ lng: 'zh' })}>
        <ClearAsk target={WORKSPACE} plan={WORKSPACE_PLAN} meanIt="清除 3 项" onClear={() => {}} onClose={() => {}} />
      </I18nextProvider>,
    );
    const staying = screen.getByRole('list', { name: '会保留的部分' });
    expect(within(staying).getByRole('listitem', { name: '需求 #b2c3d4' })).toHaveTextContent('需求 #b2c3d4 正等你发布或关闭，所以没有清除。');
    expect(within(staying).getByRole('listitem', { name: '委托 #9f0a1b' })).toHaveTextContent('#9f0a1b 已完成，正等你采纳，所以没有清除。');
    expect(screen.getByRole('button', { name: '取消' })).toBeInTheDocument();
  });

  it('takes the left-over files and the intake’s room with no unit', () => {
    render(<ClearAsk target={WORKSPACE} plan={WORKSPACE_LEFT_OVER} meanIt="Clear 5" onClear={() => {}} onClose={() => {}} />);
    expect(within(screen.getByRole('list', { name: 'What goes' })).getAllByRole('listitem').map((row) => row.textContent))
      .toEqual(['3.1 MB left over from records already gone', 'the intake’s room, 40 KB']);
  });

  /**
   * HIST1o (seen on the install): with only left-over files to take, the ask opened with what the finished work keeps, its
   * words, transcripts and files, while its own list said only the left-over files went. It opens with what goes.
   */
  it('opens with only the left-over files where no unit goes, in both languages, and with the finished work where one does', () => {
    const { unmount } = render(
      <ClearAsk target={WORKSPACE} plan={WORKSPACE_FILES_ONLY} meanIt="Clear" onClear={() => {}} onClose={() => {}} />,
    );
    const ask = screen.getByRole('group', { name: 'clear from this machine' });
    expect(ask).toHaveTextContent(
      'Clears from this machine only the files left over from records already gone, 1.3 KB. Nothing brings it back.');
    expect(ask).not.toHaveTextContent('finished work');
    expect(within(screen.getByRole('list', { name: 'What goes' })).getAllByRole('listitem').map((row) => row.textContent))
      .toEqual(['1.3 KB left over from records already gone']);
    unmount();

    const zh = render(
      <I18nextProvider i18n={i18n.cloneInstance({ lng: 'zh' })}>
        <ClearAsk target={WORKSPACE} plan={WORKSPACE_FILES_ONLY} meanIt="清除" onClear={() => {}} onClose={() => {}} />
      </I18nextProvider>,
    );
    const zhAsk = screen.getByRole('group', { name: '从本机清除' });
    expect(zhAsk).toHaveTextContent('从本机清除的只有已不存在的记录留下的文件，共 1.3 KB。清除后无法找回。');
    expect(zhAsk).not.toHaveTextContent('已完成的工作');
    zh.unmount();

    render(<ClearAsk target={WORKSPACE} plan={WORKSPACE_PLAN} meanIt="Clear 3" onClear={() => {}} onClose={() => {}} />);
    expect(screen.getByRole('group', { name: 'clear from this machine' })).toHaveTextContent(
      'Clears from this machine what it kept of aurora’s finished work: their words, transcripts and files, 20.6 MB.');
  });

  it('says nothing here can be cleared, with only Close, where every unit is kept', async () => {
    const close = vi.fn();
    render(<ClearAsk target={WORKSPACE} plan={WORKSPACE_KEPT} meanIt="Clear 0" onClear={() => {}} onClose={close} />);
    const ask = screen.getByRole('group');
    expect(ask).toHaveTextContent('Nothing here can be cleared now: each is kept for the reason beside it.');
    expect(screen.queryByRole('list', { name: 'What goes' })).toBeNull();
    expect(within(ask).getAllByRole('button').map((button) => button.textContent)).toEqual(['Close']);
    await userEvent.click(within(ask).getByRole('button', { name: 'Close' }));
    expect(close).toHaveBeenCalledOnce();
  });
});

describe('a clear’s second press (design §5 step 2)', () => {
  it('sends exactly the units the first press listed, held from when the list opened', async () => {
    const clear = vi.fn();
    const { rerender } = render(<ClearAsk target={WORKSPACE} plan={WORKSPACE_PLAN} meanIt="Clear 3" onClear={clear} onClose={() => {}} />);
    // The plan is read again while the list is open (a tick, a refetch): the press still sends what was listed.
    rerender(<ClearAsk target={WORKSPACE} plan={WORKSPACE_KEPT} meanIt="Clear 3" onClear={clear} onClose={() => {}} />);

    await userEvent.click(screen.getByRole('button', { name: 'Clear 3' }));
    expect(clear).toHaveBeenCalledWith(
      [{ kind: 'ask', id: 'a1b2c3' }, { kind: 'quest', id: '0c1d2e' }, { kind: 'quest', id: '3f4a5b' }], expect.anything());
  });

  it('sends an empty list where only left-over files and the room go, and waits while a press is on its way', async () => {
    const clear = vi.fn();
    const { rerender } = render(<ClearAsk target={WORKSPACE} plan={WORKSPACE_LEFT_OVER} meanIt="Clear 5" onClear={clear} onClose={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Clear 5' }));
    expect(clear).toHaveBeenCalledWith([], expect.anything());

    rerender(<ClearAsk target={WORKSPACE} plan={WORKSPACE_LEFT_OVER} meanIt="Clear 5" busy onClear={clear} onClose={() => {}} />);
    expect(screen.getByRole('button', { name: 'Clear 5' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Never mind' })).toBeDisabled();
  });
});

/**
 * UXFIX2 (the second-opinion review, `ClearAsk.tsx:101`): the clear's ask is the one inline confirmation. What it would take
 * and keep takes the focus on opening and describes the move; the press keeps it open until the clear answers; a refusal is
 * said inside it, word for word; it closes once the clear lands, or on *Never mind*.
 */
describe('a clear’s ask as the one inline confirmation (UXFIX2)', () => {
  it('takes the focus to what goes and stays, which describes the move', async () => {
    render(<ClearAsk target={WORKSPACE} plan={WORKSPACE_PLAN} meanIt="Clear 3" onClear={() => {}} onClose={() => {}} />);
    const said = screen.getByRole('list', { name: 'What goes' }).closest('[tabindex="-1"]');
    await waitFor(() => expect(said).toHaveFocus());
    const move = screen.getByRole('button', { name: 'Clear 3' });
    expect(move).toHaveAccessibleDescription(/^Clears from this machine what it kept of aurora’s finished work/);
    expect(move).toHaveAccessibleDescription(/What goes 4 closed quests 1 ask 6 sessions/);
    expect(move).toHaveAccessibleDescription(/3\.1 MB left over from records already gone/);
    expect(move).toHaveAccessibleDescription(/a landing made still stands in engine/);
  });

  it('stays open and waiting until the clear answers, says a refusal inside itself, and closes once it lands', async () => {
    const clear = vi.fn();
    const close = vi.fn();
    render(<ClearAsk target={QUEST} plan={QUEST_PLAN} meanIt="Clear quest" onClear={clear} onClose={close} />);
    await userEvent.click(screen.getByRole('button', { name: 'Clear quest' }));
    const ask = screen.getByRole('group', { name: 'clear from this machine' });
    expect(within(ask).getByRole('button', { name: 'Clear quest' })).toBeDisabled();
    expect(within(ask).getByRole('button', { name: 'Never mind' })).toBeDisabled();

    const answered = clear.mock.calls[0]![1] as Answered;
    act(() => answered.refused('s1a2b3c4 is still running, so it was not cleared. Stop it first.'));
    expect(within(ask).getByRole('alert')).toHaveTextContent('s1a2b3c4 is still running, so it was not cleared. Stop it first.');
    expect(close).not.toHaveBeenCalled();

    await userEvent.click(within(ask).getByRole('button', { name: 'Clear quest' }));
    act(() => (clear.mock.calls[1]![1] as Answered).done());
    expect(close).toHaveBeenCalledOnce();
  });
});
