import { afterEach, describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { DONE, OPEN, TAKEN } from './fixtures';
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
