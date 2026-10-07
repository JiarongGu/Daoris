import { describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from '../i18n';
import {
  WORKSPACE_EMPTY, WORKSPACE_KEPT, WORKSPACE_LEFT_OVER, WORKSPACE_PLAN, WORKSPACE_RECORDS_EMPTY_FILES, WORKSPACE_RECORDS_ONLY,
} from '../work/historyFixtures';
import type { Answered } from '../work/InlineConfirm';
import { KeptHistory } from './KeptHistory';

/** How a clear's ask hears its end (UXFIX2, ACCTEDIT1's contract). */
const ANSWERED = expect.objectContaining({ done: expect.any(Function), refused: expect.any(Function) });

// A workspace's *Kept on this machine* (HIST1e, D153; the history-clearing design §2.4, §6.1): always the reading of what the
// home keeps of its finished work, then *Clear history…* while anything may go, listing first and sending on its second
// press exactly the units it listed. A molecule: every state is reached by passing it, and every press goes out.

describe('a workspace’s Kept on this machine (design §2.4)', () => {
  it('reads what the home keeps, what a clear would take, and what keeps the rest, each with its door', async () => {
    const doors = { branches: vi.fn(), sync: vi.fn() };
    render(<KeptHistory workspace="aurora" plan={WORKSPACE_PLAN} doors={doors} onClear={() => {}} />);

    const section = screen.getByRole('region', { name: 'Kept on this machine' });
    expect(section).toHaveTextContent('11 closed quests, 2 asks, 14 sessions, 1 copy of a teammate’s record: 23.6 MB.');
    expect(section).toHaveTextContent('A clear would take 4 closed quests, 1 ask, 6 sessions, 1 copy of a teammate’s record: 20.6 MB.');
    expect(section).toHaveTextContent('3 conversations that served no quest, 1.2 MB: only Delete… in Sessions takes them, one at a time.');
    expect(section).toHaveTextContent('3.1 MB left over from records already gone: any workspace’s clear takes it.');
    expect(section).toHaveTextContent('The machine log, 2.4 MB, keeps its own 30 days; a clear never touches it.');

    const kept = within(section).getByRole('list', { name: 'What stays' });
    expect(within(kept).getAllByRole('listitem').map((row) => row.firstChild?.textContent)).toEqual([
      '1 kept: a session still running', '2 kept: waiting on you', '1 kept: a landing’s branch still stands',
      '1 kept: last moves not yet synced',
    ]);
    await userEvent.click(within(kept).getByRole('button', { name: 'Open branches' }));
    expect(doors.branches).toHaveBeenCalledOnce();
    await userEvent.click(within(kept).getByRole('button', { name: 'Sync now' }));
    expect(doors.sync).toHaveBeenCalledOnce();
  });

  it('offers Clear history… while anything may go, lists first, and sends exactly the units it listed', async () => {
    const clear = vi.fn();
    const { rerender } = render(<KeptHistory workspace="aurora" plan={WORKSPACE_PLAN} onClear={clear} />);

    await userEvent.click(screen.getByRole('button', { name: 'Clear history…' }));
    const ask = screen.getByRole('group', { name: 'clear from this machine' });
    expect(screen.queryByRole('button', { name: 'Clear history…' })).toBeNull();
    // The reading is asked again while the list is open: the press still sends what was listed.
    rerender(<KeptHistory workspace="aurora" plan={WORKSPACE_KEPT} onClear={clear} />);
    await userEvent.click(within(ask).getByRole('button', { name: 'Clear 3' }));
    expect(clear).toHaveBeenCalledWith(
      [{ kind: 'ask', id: 'a1b2c3' }, { kind: 'quest', id: '0c1d2e' }, { kind: 'quest', id: '3f4a5b' }], ANSWERED,
    );
    // Until the driver answers, the list stays, waiting (UXFIX2); once it answers, the list is put down.
    expect(within(ask).getByRole('button', { name: 'Never mind' })).toBeDisabled();
    act(() => (clear.mock.calls[0]![1] as Answered).done());
    expect(await screen.findByRole('region', { name: 'Kept on this machine' })).toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'clear from this machine' })).toBeNull();
  });

  /**
   * UXFIX2 (the second-opinion review, `ClearAsk.tsx:101`): what goes and what stays take the focus and describe *Clear N*,
   * so a keyboard reaches it having heard them; a refusal is said inside the list, which stays; *Never mind* gives the
   * focus back to *Clear history…*, drawn again.
   */
  it('takes the focus to what goes, says a refusal inside the list, and gives the focus back to Clear history…', async () => {
    const clear = vi.fn();
    const user = userEvent.setup();
    render(<KeptHistory workspace="aurora" plan={WORKSPACE_PLAN} onClear={clear} />);

    await user.click(screen.getByRole('button', { name: 'Clear history…' }));
    const ask = screen.getByRole('group', { name: 'clear from this machine' });
    const said = within(ask).getByRole('list', { name: 'What goes' }).closest('[tabindex="-1"]');
    await waitFor(() => expect(said).toHaveFocus());
    expect(within(ask).getByRole('button', { name: 'Clear 3' })).toHaveAccessibleDescription(/4 closed quests/);

    await user.click(within(ask).getByRole('button', { name: 'Clear 3' }));
    act(() => (clear.mock.calls[0]![1] as Answered).refused('The driver is not running on this machine.'));
    expect(within(ask).getByRole('alert')).toHaveTextContent('The driver is not running on this machine.');

    await user.click(within(ask).getByRole('button', { name: 'Never mind' }));
    expect(screen.queryByRole('group', { name: 'clear from this machine' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Clear history…' })).toHaveFocus();
  });

  /**
   * UXFIX2c (the second-opinion review, `InlineConfirm.tsx:163`, `KeptHistory.tsx:69`): *Clear history…* is not offered
   * while it asks, so a clear that landed gave the focus back to a press no longer drawn, and it fell to the page's body. It
   * goes to *Clear history…* drawn again, and where nothing more may go and none is drawn, to the section.
   */
  it('gives the focus to Clear history… drawn again once a clear lands, or to the section where none is', async () => {
    const clear = vi.fn();
    const user = userEvent.setup();
    const { rerender } = render(<KeptHistory workspace="aurora" plan={WORKSPACE_PLAN} onClear={clear} />);

    await user.click(screen.getByRole('button', { name: 'Clear history…' }));
    await user.click(within(screen.getByRole('group', { name: 'clear from this machine' })).getByRole('button', { name: 'Clear 3' }));
    act(() => (clear.mock.calls[0]![1] as Answered).done());
    expect(screen.queryByRole('group', { name: 'clear from this machine' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Clear history…' })).toHaveFocus();

    // Read again while it asked, the plan offers nothing more: no press is drawn, and the section holds the focus.
    await user.click(screen.getByRole('button', { name: 'Clear history…' }));
    rerender(<KeptHistory workspace="aurora" plan={WORKSPACE_KEPT} onClear={clear} />);
    await user.click(within(screen.getByRole('group', { name: 'clear from this machine' })).getByRole('button', { name: 'Clear 3' }));
    act(() => (clear.mock.calls[1]![1] as Answered).done());
    expect(screen.queryByRole('button', { name: 'Clear history…' })).toBeNull();
    expect(screen.getByRole('region', { name: 'Kept on this machine' })).toHaveFocus();
    expect(document.activeElement).not.toBe(document.body);
  });

  it('sends no unit where only left-over files and the intake’s room go', async () => {
    const clear = vi.fn();
    render(<KeptHistory workspace="aurora" plan={WORKSPACE_LEFT_OVER} onClear={clear} />);
    expect(screen.getByRole('region')).toHaveTextContent('A clear would take 3.1 MB: files no record holds any more.');

    await userEvent.click(screen.getByRole('button', { name: 'Clear history…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Clear 5' }));
    expect(clear).toHaveBeenCalledWith([], ANSWERED);
  });

  it('says what goes beside the press where the records it takes hold no file (HIST1k)', () => {
    render(<KeptHistory workspace="aurora" plan={WORKSPACE_RECORDS_ONLY} onClear={() => {}} />);
    const section = screen.getByRole('region');
    expect(section).toHaveTextContent('A clear would take 1 closed quest: 0 B.');
    expect(section).not.toHaveTextContent('A clear would take nothing now.');
    expect(screen.getByRole('button', { name: 'Clear history…' })).toBeInTheDocument();
  });

  /**
   * HIST1n (the second-opinion review's afternoon round): 0 B was said as *no files here* beside left-over files of 0 B, which
   * the next line and *What goes* name. The reading says what it read, the records and their size, and nothing of files.
   */
  it('says the records and 0 B, and no claim about files, beside left-over files that hold nothing', async () => {
    render(<KeptHistory workspace="aurora" plan={WORKSPACE_RECORDS_EMPTY_FILES} onClear={() => {}} />);
    const section = screen.getByRole('region');
    expect(section).toHaveTextContent('A clear would take 1 closed quest: 0 B.');
    expect(section).toHaveTextContent('0 B left over from records already gone: any workspace’s clear takes it.');
    expect(section).not.toHaveTextContent(/no files/);

    await userEvent.click(screen.getByRole('button', { name: 'Clear history…' }));
    expect(within(section).getByRole('list', { name: 'What goes' })).toHaveTextContent('0 B left over from records already gone');
  });

  it('offers nothing where the plan lists nothing that may go, and says so', () => {
    const { unmount } = render(<KeptHistory workspace="aurora" plan={WORKSPACE_KEPT} onClear={() => {}} />);
    expect(screen.getByRole('region')).toHaveTextContent('A clear would take nothing now.');
    expect(screen.queryByRole('button', { name: 'Clear history…' })).toBeNull();
    unmount();

    render(<KeptHistory workspace="aurora" plan={WORKSPACE_EMPTY} onClear={() => {}} />);
    expect(screen.getByRole('region')).toHaveTextContent('Nothing finished is kept here.');
    expect(screen.queryByRole('button', { name: 'Clear history…' })).toBeNull();
  });

  it('says it is reading, says a refusal in place, and is absent where the host answers no plan', () => {
    const { rerender, container } = render(<KeptHistory workspace="aurora" plan={null} reading onClear={() => {}} />);
    expect(screen.getByRole('region')).toHaveTextContent('Reading what this machine keeps…');
    rerender(<KeptHistory workspace="aurora" plan={null} refusal="The service has no history door." onClear={() => {}} />);
    expect(screen.getByRole('region')).toHaveTextContent('The service has no history door.');
    rerender(<KeptHistory workspace="aurora" plan={null} onClear={() => {}} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('names itself and its press in 中文', () => {
    render(
      <I18nextProvider i18n={i18n.cloneInstance({ lng: 'zh' })}>
        <KeptHistory workspace="aurora" plan={WORKSPACE_PLAN} onClear={() => {}} />
      </I18nextProvider>,
    );
    const section = screen.getByRole('region', { name: '本机保留的记录' });
    expect(section).toHaveTextContent('2 项保留：正在等你');
    expect(within(section).getByRole('button', { name: '清除记录…' })).toBeInTheDocument();
  });
});
