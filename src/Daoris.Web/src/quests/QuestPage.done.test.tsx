import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { FINISHED_HERE as FINISHED, REQUIRING, TAKEN } from './fixtures';
import { QuestPage } from './QuestPage';

// The person's done on a quest's page (QUESTCLOSE1, D126's note). On the install (2026-10-08) two driven sessions were finished
// at a checkpoint and each quest stayed taken, with nothing on its page that said so or closed it as the person's. Its page now
// says a taken quest whose last session here ended sits with nothing to carry it on, and *Mark done…* asks once, with the
// person's note, before it sends their done.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

/** How the ask hears its end (UXFIX2's contract). */
const ANSWERED = expect.objectContaining({ done: expect.any(Function), refused: expect.any(Function) });

const page = (over: Partial<Parameters<typeof QuestPage>[0]> = {}) => {
  const props = { quest: TAKEN, session: FINISHED, onRespond: vi.fn(), onDismiss: vi.fn(), onOpenQuest: vi.fn(), ...over };
  render(<QuestPage {...props} />);
  return props;
};

const header = () => within(screen.getByRole('main').querySelector('header')!);

describe('the person’s done on a quest’s page (QUESTCLOSE1)', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says a taken quest whose last session here ended sits with nothing to carry it on, and offers Mark done… there and in its head', () => {
    page();

    expect(screen.getByText(
      'Its last session here, f1n15h3d, ended, and the quest is still taken: nothing here carries it on. Mark it done once its work is done.',
    )).toBeInTheDocument();
    expect(header().getAllByRole('button').map((button) => button.textContent || button.getAttribute('aria-label')))
      .toEqual(['Mark done…', 'Decline…', 'More actions']);
    expect(header().getByRole('button', { name: 'Mark done…' }).className).toContain('bg-accent');
    expect(screen.getAllByRole('button', { name: 'Mark done…' })).toHaveLength(2);
  });

  it('asks once, then sends the person’s done with their note, and closes once it landed', async () => {
    const onRespond = vi.fn();
    page({ onRespond });

    await userEvent.click(header().getByRole('button', { name: 'Mark done…' }));
    const ask = screen.getByRole('group', { name: 'mark this quest done' });
    expect(ask).toHaveTextContent(
      `Marks #${TAKEN.id} done as yours: its record says you marked it done, with your note if you write one. A done does not move again.`,
    );
    // Its first press is not offered twice while it asks.
    expect(header().queryByRole('button', { name: 'Mark done…' })).toBeNull();
    await userEvent.type(within(ask).getByLabelText('Your note (optional)'), '  The write-up is in the shared folder.  ');
    await userEvent.click(within(ask).getByRole('button', { name: 'Mark done' }));

    expect(onRespond).toHaveBeenCalledWith('done', 'The write-up is in the shared folder.', ANSWERED);
    const answered = onRespond.mock.calls[0]![2] as { done: () => void };
    act(() => answered.done());
    expect(await screen.findAllByRole('button', { name: 'Mark done…' })).toHaveLength(2);
    expect(screen.queryByRole('group', { name: 'mark this quest done' })).toBeNull();
  });

  it('sends no words where none were written, and Never mind sends nothing', async () => {
    const { onRespond } = page();

    await userEvent.click(header().getByRole('button', { name: 'Mark done…' }));
    await userEvent.click(within(screen.getByRole('group', { name: 'mark this quest done' })).getByRole('button', { name: 'Never mind' }));
    expect(onRespond).not.toHaveBeenCalled();

    await userEvent.click(header().getByRole('button', { name: 'Mark done…' }));
    await userEvent.click(within(screen.getByRole('group', { name: 'mark this quest done' })).getByRole('button', { name: 'Mark done' }));
    expect(onRespond).toHaveBeenCalledWith('done', null, ANSWERED);
  });

  it('says a refusal inside the ask, and lets it be pressed again', async () => {
    const onRespond = vi.fn((_action: string, _note?: string | null, answered?: { refused: (sentence: string) => void }) => {
      answered?.refused('Quest `#def456` is Done — a closed quest does not move; a new ask is a new title.');
    });
    page({ onRespond });

    await userEvent.click(header().getByRole('button', { name: 'Mark done…' }));
    const ask = screen.getByRole('group', { name: 'mark this quest done' });
    await userEvent.click(within(ask).getByRole('button', { name: 'Mark done' }));

    expect(await within(ask).findByRole('alert')).toHaveTextContent('a closed quest does not move');
    expect(within(ask).getByRole('button', { name: 'Mark done' })).toBeEnabled();
  });

  it('says that its requirements are not answered one by one, where it carries some', async () => {
    page({ quest: REQUIRING, session: { ...FINISHED, quest: REQUIRING.id } });

    await userEvent.click(header().getByRole('button', { name: 'Mark done…' }));
    expect(screen.getByRole('group', { name: 'mark this quest done' }))
      .toHaveTextContent('Its requirements are not answered one by one: your done is your word on them.');
  });

  it('says nothing of a session still working, a teammate’s, a declined quest, or a failure the driver carries on', () => {
    for (const over of [
      { session: { ...FINISHED, state: 'working' as const } },
      { session: { ...FINISHED, id: 'laptop/f1n15h3d' } },
      { session: { ...FINISHED, state: 'failed' as const } },
      { quest: { ...TAKEN, status: 'Declined' as const } },
      {
        sitting: {
          quest: TAKEN.id, repository: 'engine', verdict: 'Stopped', reason: 'you stopped session `f1n15h3d`; Try again carries it on.',
        },
      },
    ]) {
      const { unmount } = render(
        <QuestPage quest={TAKEN} session={FINISHED} onRespond={vi.fn()} onDismiss={vi.fn()} onOpenQuest={vi.fn()} {...over} />,
      );
      expect(screen.queryByText(/nothing here carries it on/)).toBeNull();
      unmount();
    }
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    const { onRespond } = page();

    expect(screen.getByText(
      '它在本机的最后一个会话 f1n15h3d 已不再运行，而委托仍是已接下：本机不会再继续它。工作完成后，把它标为完成。',
    )).toBeInTheDocument();
    expect(header().getAllByRole('button').map((button) => button.textContent || button.getAttribute('aria-label')))
      .toEqual(['标为完成…', '谢绝…', '更多操作']);

    await userEvent.click(header().getByRole('button', { name: '标为完成…' }));
    const ask = screen.getByRole('group', { name: '将这项委托标为完成' });
    expect(ask).toHaveTextContent(`以你的名义将 #${TAKEN.id} 标为完成：记录会写明是你标的完成，附上你写下的备注。完成之后不会再变动。`);
    await userEvent.type(within(ask).getByLabelText('你的备注（可选）'), '已放在共享文件夹。');
    await userEvent.click(within(ask).getByRole('button', { name: '标为完成' }));
    expect(onRespond).toHaveBeenCalledWith('done', '已放在共享文件夹。', ANSWERED);
  });
});
