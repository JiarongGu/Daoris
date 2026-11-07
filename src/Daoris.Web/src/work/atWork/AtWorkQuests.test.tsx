import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { en, zh } from '../../locales';
import { AtWorkView } from './AtWorkView';
import { ASKED_BY, CHAIN_AND_QUESTION, CONSIDERED, LANES_ALONGSIDE_AND_UNTAKEN, QUESTS_EACH_TAKER } from './atWork.fixtures';

// The quests a part asked and what asked the attended part.

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

const group = (name: string) => screen.getByRole('heading', { name }).closest('section')!;
const questRows = () => within(group('Asked through quests')).getAllByRole('listitem');

describe('asked through quests', () => {
  it('draws an untaken quest with the tick’s reason beside it, and none before the first look', () => {
    const { rerender } = render(<AtWorkView held={LANES_ALONGSIDE_AND_UNTAKEN} considered={CONSIDERED} />);
    expect(questRows()[0]!.textContent).toContain('#q4');
    expect(questRows()[0]!.textContent).toContain('Tidy the media lane');
    expect(questRows()[0]!.textContent).toContain('not taken yet: The concurrency cap (1) is spent — it frees as sessions finish.');

    rerender(<AtWorkView held={LANES_ALONGSIDE_AND_UNTAKEN} considered={[]} />);
    expect(questRows()[0]!.textContent).toContain('not taken yet');
    expect(questRows()[0]!.textContent).not.toContain('not taken yet:');
    expect(questRows()[0]!.textContent).not.toContain('{{why}}');
  });

  it('changes an untaken quest’s words when the tick’s reason moves, with the answer the same object', () => {
    const { rerender } = render(<AtWorkView held={LANES_ALONGSIDE_AND_UNTAKEN} considered={CONSIDERED} />);
    expect(questRows()[0]!.textContent).toContain('The concurrency cap (1) is spent');

    rerender(<AtWorkView held={LANES_ALONGSIDE_AND_UNTAKEN} considered={[{ quest: 'q4', repository: 'driver', verdict: 'RepositoryBusy', reason: 'driver is busy with another session.' }]} />);
    expect(questRows()[0]!.textContent).toContain('driver is busy with another session.');
    expect(questRows()[0]!.textContent).not.toContain('concurrency cap');
  });

  it('keeps the lane parts under This session and the untaken lane quest only under the quests, never both', () => {
    render(<AtWorkView held={LANES_ALONGSIDE_AND_UNTAKEN} considered={CONSIDERED} />);

    const parts = within(group('This session')).getAllByRole('listitem');
    expect(parts).toHaveLength(3);
    expect(parts[1]!.textContent).toContain('published to the core lane by part 1');
    expect(parts.some((row) => row.textContent!.includes('#q4'))).toBe(false);
    expect(questRows()).toHaveLength(1);
  });

  it('says each taker apart: a session with its agent and state, a conversation, another machine, nobody recorded, nobody yet', () => {
    render(<AtWorkView held={QUESTS_EACH_TAKER} considered={CONSIDERED} />);

    const rows = questRows().map((row) => row.textContent!);
    expect(rows[0]).toContain('claude-code');
    expect(rows[0]).toContain('working');
    expect(rows[0]).toContain('core lane');
    expect(rows[1]).toContain('taken by c7');
    expect(rows[2]).toContain('taken on another machine');
    expect(rows[2]).toContain('engine');
    expect(rows[3]).toContain('taken, by no session recorded here');
    expect(rows[4]).toContain('not taken yet');
    expect(rows[5]).toContain('declined');
    expect(rows.some((row) => row.includes('taken by c7') && row.includes('another machine'))).toBe(false);
  });

  it('puts a chain’s step under its parent and a taker’s question on its row', () => {
    render(<AtWorkView held={CHAIN_AND_QUESTION} />);

    const rows = questRows();
    expect(rows[0]!.textContent).toContain('#q1');
    expect(rows[1]!.textContent).toContain('the next step of #q1');
    expect(rows[1]!.textContent).toContain('waits on #q9, asked of docs');
    expect(rows[1]!.textContent).not.toContain('by you');
  });

  it('opens a quest’s page from its row, one button each in the order drawn', () => {
    const open = vi.fn();
    render(<AtWorkView held={QUESTS_EACH_TAKER} onOpenQuest={open} />);

    const buttons = within(group('Asked through quests')).getAllByRole('button');
    expect(buttons.map((button) => button.textContent!.slice(0, 3))).toEqual(['#q1', '#q2', '#q3', '#q5', '#q4', '#q6']);
    fireEvent.click(buttons[2]!);
    expect(open).toHaveBeenCalledWith('q3');
  });

  it('draws no group where the answer asked none, and is never the nobody sentence beside quests', () => {
    render(<AtWorkView held={QUESTS_EACH_TAKER} />);

    expect(screen.queryByText('Nobody else is working on this')).toBeNull();
    cleanup();
    render(<AtWorkView held={ASKED_BY({ person: true })} />);
    expect(screen.queryByRole('heading', { name: 'Asked through quests' })).toBeNull();
  });
});

describe('asked by', () => {
  it.each([
    [{ session: 's1' }, '#q1 · published by part 1'],
    [{ session: 'elsewhere' }, '#q1 · published by another session'],
    [{ step: 'q0' }, '#q1 · the next step of #q0'],
    [{ ask: 'a7' }, '#q1 · by ask #a7'],
    [{ person: true as const }, '#q1 · by you'],
  ])('words %j as %s', (by, words) => {
    const open = vi.fn();
    render(<AtWorkView held={ASKED_BY(by)} onOpenQuest={open} />);

    const region = group('Asked by');
    expect(region.textContent).toContain(words);
    fireEvent.click(within(region).getByRole('button'));
    expect(open).toHaveBeenCalledWith('q1');
  });
});

describe('the quests half’s words', () => {
  it('has every key in both catalogues', () => {
    for (const key of [
      'group.quests', 'group.askedBy', 'quest.of', 'quest.lanes_one', 'quest.lanes_other', 'quest.untaken', 'quest.untakenWhy',
      'quest.takenBy', 'quest.elsewhere', 'quest.unrecorded', 'quest.declined', 'quest.more_one', 'quest.more_other', 'askedBy.session',
      'askedBy.step', 'askedBy.ask', 'askedBy.person', 'askedBy.anotherSession',
    ]) {
      expect([key, `work.atWork.${key}` in en, `work.atWork.${key}` in zh]).toEqual([key, true, true]);
    }
  });

  it('says the same in 中文', async () => {
    await i18n.changeLanguage('zh');
    const { rerender } = render(<AtWorkView held={QUESTS_EACH_TAKER} considered={CONSIDERED} />);
    const rows = within(group('通过委托请求')).getAllByRole('listitem');
    expect(rows[2]!.textContent).toContain('已在另一台机器上被接手');
    expect(rows[4]!.textContent).toContain('还没有人接手：');

    rerender(<AtWorkView held={ASKED_BY({ person: true })} />);
    expect(group('由谁请求').textContent).toContain('由你发出');
  });
});
