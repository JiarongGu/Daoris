import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { QuestMove } from '../data/api';
import i18n from '../locales/i18n';
import { DONE, OPEN, TAKEN } from './fixtures';
import type { QuestMovesDoor } from './QuestFlow';
import { QuestPage } from './QuestPage';

// FLOWCHART1a1 (the flowchart design §2.1, §10's row): a quest's statuses drawn as a flowchart on its page, a section after
// the chain strip and before *How this came to be*, folded to its line until it is opened; the status it stands at lit,
// and the moves out of it said. jsdom measures nothing, so the flow is the rail here; its placements are `Flow.test.tsx`'s.

const nothing = () => {};

const page = (props: Partial<Parameters<typeof QuestPage>[0]> = {}) => render(
  <QuestPage quest={TAKEN} onRespond={nothing} onDismiss={nothing} onOpenQuest={nothing} {...props} />,
);

const fold = (name: RegExp) => screen.getByRole('button', { name });

afterEach(async () => { await i18n.changeLanguage('en'); });

describe('a quest\'s flowchart on its page', () => {
  it('is folded to its line, naming the status it stands at, and draws nothing until it is opened', () => {
    page();
    expect(fold(/^Flowchart/)).toHaveAttribute('aria-expanded', 'false');
    expect(fold(/^Flowchart/)).toHaveTextContent('Flowchart · taken');
    expect(screen.queryByRole('list', { name: `How #${TAKEN.id} moves` })).toBeNull();
  });

  it('opens on the four statuses in their order, the one it stands at lit and the moves out of it said', async () => {
    page();
    await userEvent.click(fold(/^Flowchart/));
    expect(fold(/^Flowchart/)).toHaveAttribute('aria-expanded', 'true');
    const flow = screen.getByRole('list', { name: `How #${TAKEN.id} moves` });
    const items = within(flow).getAllByRole('listitem');
    expect(items.map((item) => item.getAttribute('data-flow-node'))).toEqual(['Open', 'Taken', 'Done', 'Declined']);
    const here = within(flow).getByRole('listitem', { current: 'step' });
    expect(here).toHaveAccessibleName('taken here now');
    expect(here).toHaveAccessibleDescription(
      'here now. From open: when a repository\'s agent takes it. Next: done, when its work is done; declined, when it is declined after it was taken.');
    expect(within(here).getAllByRole('button').map((button) => button.textContent)).toEqual([
      '→done, when its work is done', '→declined, when it is declined after it was taken',
    ]);
    expect(screen.getByText('Lit where it stands now, with the moves it can make from there.')).toBeInTheDocument();
  });

  it('lights Open on an open quest, with every move out of it, and a closed one\'s close', async () => {
    const { unmount } = page({ quest: OPEN });
    await userEvent.click(fold(/^Flowchart/));
    const open = within(screen.getByRole('list', { name: `How #${OPEN.id} moves` })).getByRole('listitem', { current: 'step' });
    expect(within(open).getAllByRole('button')).toHaveLength(3);
    unmount();

    page({ quest: DONE });
    expect(fold(/^Flowchart/)).toHaveTextContent('Flowchart · done');
    await userEvent.click(fold(/^Flowchart/));
    const done = within(screen.getByRole('list', { name: `How #${DONE.id} moves` })).getByRole('listitem', { current: 'step' });
    // Nothing leaves a close (D46 §3): its foot says no move.
    expect(within(done).queryAllByRole('button')).toEqual([]);
  });

  it('stands after the chain strip and before how it came to be', () => {
    page({ trace: { open: false, onToggle: nothing } });
    const sections = screen.getAllByRole('region').map((section) => section.getAttribute('aria-label'));
    expect(sections.indexOf('Flowchart')).toBeGreaterThanOrEqual(0);
    expect(sections.indexOf('Flowchart')).toBeLessThan(sections.indexOf('How this came to be'));
  });

  it('says it all in 中文', async () => {
    await i18n.changeLanguage('zh');
    page();
    expect(fold(/^流程图/)).toHaveTextContent('流程图 · 已接下');
    await userEvent.click(fold(/^流程图/));
    const here = within(screen.getByRole('list', { name: `委托 #${TAKEN.id} 如何流转` })).getByRole('listitem', { current: 'step' });
    expect(here).toHaveAccessibleName('已接下 当前所在');
    expect(here).toHaveAccessibleDescription(
      '当前所在。来自 待接：当某个仓库的智能体接下它时。下一步：已完成，当它的工作完成时；已谢绝，在接下之后被谢绝时。');
    expect(screen.getByText('点亮的是它现在所处的状态，以及从那里能走的每一步。')).toBeInTheDocument();
  });
});

// FLOWCHART1a2 (the flowchart design §2.1, traps 18 and 20): the moves the page's organism reads only once the section opens,
// drawn on its flowchart: each move that changed no status inside the status it happened in, three before a fold.
describe('how a quest moved, on its flowchart', () => {
  const at = (minute: number) => `2026-10-20T09:${String(minute).padStart(2, '0')}:00Z`;
  const move = (kind: string, sequence: number, minute: number, rest: Partial<QuestMove> = {}): QuestMove =>
    ({ kind, machine: 'm3', sequence, at: at(minute), applied: true, from: 'Taken', ...rest });
  const MOVES: QuestMove[] = [
    { kind: 'published', machine: 'm2', sequence: 1, at: at(0), applied: true, number: 1 },
    move('taken', 1, 5, { from: 'Open' }),
    move('waited', 2, 6, { waitsOn: 'q2q2q2' }),
    move('bound', 3, 7),
    move('submitted', 4, 8, { session: 's1a2b3c4' }),
    move('reopened', 5, 9),
    move('submitted', 6, 10, { session: 's5e6f7a8' }),
  ];
  const door = (props: Partial<QuestMovesDoor> = {}): QuestMovesDoor => ({ open: true, onToggle: nothing, moves: MOVES, ...props });
  const flow = () => screen.getByRole('list', { name: `How #${TAKEN.id} moves` });
  const taken = () => within(flow()).getByRole('listitem', { current: 'step' });

  it('is folded until the person opens it, and the press is the organism\'s, which reads the moves only then', async () => {
    const onToggle = vi.fn();
    page({ moves: door({ open: false, moves: undefined, onToggle }) });

    expect(fold(/^Flowchart/)).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('list', { name: `How #${TAKEN.id} moves` })).toBeNull();
    await userEvent.click(fold(/^Flowchart/));
    expect(onToggle).toHaveBeenCalledTimes(1);
  });

  it('says it is reading while its moves are on their way, then draws them in their status, three before a fold', async () => {
    const { rerender } = page({ moves: door({ moves: undefined, reading: true }) });
    expect(screen.getByText('Reading its moves…')).toBeInTheDocument();

    rerender(<QuestPage quest={TAKEN} onRespond={nothing} onDismiss={nothing} onOpenQuest={nothing} moves={door()} />);
    expect(screen.getByText('Lit where it stands now. The moves it took are drawn heavier, and what happened inside a status is listed in it, oldest first.'))
      .toBeInTheDocument();
    expect(taken()).toHaveTextContent('taken — machine m3');
    expect(within(taken()).getByText(/^waited on #q2q2q2, whose close resumes it/)).toBeInTheDocument();
    expect(within(taken()).getByText(/^bound to the workflow that closes it/)).toBeInTheDocument();
    expect(within(taken()).getByText(/^submitted by session s1a2b3c4/)).toBeInTheDocument();
    expect(within(taken()).queryByText(/^reopened by you/)).toBeNull();

    await userEvent.click(within(taken()).getByRole('button', { name: '2 more' }));
    expect(within(taken()).getByText(/^reopened by you/)).toBeInTheDocument();
    expect(within(taken()).getByText(/^submitted by session s5e6f7a8/)).toBeInTheDocument();
  });

  it('says the path is not kept where the host keeps no moves, and why where they could not be read (trap 18)', () => {
    const { unmount } = page({ moves: door({ moves: null }) });
    expect(screen.getByText('This service keeps no record of its moves, so where it stands now is drawn and the way it went is not.'))
      .toBeInTheDocument();
    expect(taken()).toHaveAccessibleName('taken here now');
    unmount();

    page({ moves: door({ moves: undefined, refusal: 'the service is not answering' }) });
    expect(screen.getByText('Its moves could not be read: the service is not answering')).toBeInTheDocument();
  });

  it('says how it moved in 中文', async () => {
    await i18n.changeLanguage('zh');
    page({ moves: door() });

    expect(screen.getByText('点亮的是它现在所处的状态。它走过的每一步画得更重，在某个状态里发生的事按先后列在其中。')).toBeInTheDocument();
    const here = within(screen.getByRole('list', { name: `委托 #${TAKEN.id} 如何流转` })).getByRole('listitem', { current: 'step' });
    expect(within(here).getByText(/^由会话 s1a2b3c4 交付/)).toBeInTheDocument();
    expect(within(here).getByRole('button', { name: '另外 2 条' })).toBeInTheDocument();
  });
});
