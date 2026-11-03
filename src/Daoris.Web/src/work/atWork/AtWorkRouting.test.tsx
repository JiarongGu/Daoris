import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { en, zh } from '../../locales';
import { AtWorkView } from './AtWorkView';
import {
  CHAT_TOOK_A_QUEST, JOINED_EACH_WAY, LEAD_WAITING_ON_HELPER, LEAD_WAITING_ON_LANE, OVERFLOW_AND_EARLIER, PUBLISHED_AND_WAITS_ON_NONE,
  RESUMED_AND_EARLIER,
} from './atWork.fixtures';
import type { AtWorkA, AtWorkHeld } from './types';

// COWORKVIEW1b-i: each part's wait as the strip places it, how it joined, and the fold (the cowork view design §R11.3 row 3).

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

const rowOf = (key: number) => screen.getAllByRole('listitem')[key]!;
// Another part attended is another line: its requests are its own read's (COMMS1i), none of the first's.
const attendedAs = (held: AtWorkHeld, attended: string): AtWorkHeld => ({
  ...(held as { answer: AtWorkA }), answer: { ...(held as { answer: AtWorkA }).answer, attended, exchanges: undefined },
});

describe('each part’s wait', () => {
  it('draws the attended part’s wait as the Waiting on line, first, and does not repeat it on its row', () => {
    const { container } = render(<AtWorkView held={LEAD_WAITING_ON_LANE} />);

    const line = screen.getByText('Waiting on #q7 in driver (core lane)');
    expect(container.querySelector('[data-wait-line]')).toBe(line);
    expect(container.querySelector('p')).toBe(line);
    expect(rowOf(0).textContent).not.toContain('waits on');
  });

  it('words a part that is not attended as the rail words it, a helper as the strip’s awaiting helper', () => {
    const { rerender } = render(<AtWorkView held={attendedAs(LEAD_WAITING_ON_LANE, 's2')} />);
    expect(rowOf(0).textContent).toContain('waits on #q7, asked of driver');
    expect(screen.queryByText(/^Waiting on/)).toBeNull();

    // COMMS1i: the line is read from the line's requests, the helper named and where it stands.
    rerender(<AtWorkView held={LEAD_WAITING_ON_HELPER} />);
    expect(screen.getByText('Waiting on codex-acp, as helper · asked')).toBeTruthy();
    rerender(<AtWorkView held={attendedAs(LEAD_WAITING_ON_HELPER, 's2')} />);
    expect(rowOf(0).textContent).toContain('awaiting helper');
  });

  it('draws a waiting lead with its lane part below it, alongside, and how that part joined', () => {
    render(<AtWorkView held={LEAD_WAITING_ON_LANE} />);

    expect(rowOf(1).textContent).toContain('published to the core lane by part 1');
    expect(rowOf(1).textContent).toContain('alongside, in a tree of its own');
    expect(rowOf(1).textContent).toContain('working');
  });

  it('draws a chat that took a quest with what it took and what it waits on, never as nobody', () => {
    render(<AtWorkView held={CHAT_TOOK_A_QUEST} />);

    expect(screen.getByText('Waiting on #q9 in docs')).toBeTruthy();
    expect(rowOf(0).textContent).toContain('took #q1');
    expect(screen.queryByText('Nobody else is working on this')).toBeNull();
  });

  it('says published, waits on none, only where nothing in the entry waits', () => {
    const { rerender } = render(<AtWorkView held={PUBLISHED_AND_WAITS_ON_NONE} />);
    expect(screen.getByText('published 3, waits on none')).toBeTruthy();
    expect(screen.getAllByText(/published to the (core|docs) lane by part 1/)).toHaveLength(2);

    rerender(<AtWorkView held={LEAD_WAITING_ON_LANE} />);
    expect(screen.queryByText(/waits on none/)).toBeNull();
  });
});

describe('how a part joined', () => {
  it('words each door of the design’s table, the strip’s own words where it only began', () => {
    render(<AtWorkView held={JOINED_EACH_WAY} />);

    expect(rowOf(0).textContent).toContain("the ask's quest #q4");
    expect(rowOf(1).textContent).toContain('by ask #a7');
    expect(rowOf(2).textContent).toContain('the next step of #q5');
    expect(rowOf(3).textContent).toContain('asked of driver by part 1');
    expect(rowOf(4).textContent).toContain('asked by core');
  });

  it('puts the resumed record first and the record that waited among the earlier parts, counting the folded', () => {
    render(<AtWorkView held={RESUMED_AND_EARLIER} />);

    expect(rowOf(0).textContent).toContain('resumed after part 4');
    expect(screen.getByText('4 earlier parts')).toBeTruthy();
    expect(screen.getAllByRole('listitem')).toHaveLength(4);
  });

  it('counts the limit’s overflow apart from the fold', () => {
    render(<AtWorkView held={OVERFLOW_AND_EARLIER} />);

    expect(screen.getByText('2 earlier parts')).toBeTruthy();
    expect(screen.getByText('10 more parts')).toBeTruthy();
    expect(rowOf(1).textContent).toContain('awaiting helper');
  });

  it('keeps the keyboard’s order the eye’s: one button per part, in the order drawn', () => {
    render(<AtWorkView held={RESUMED_AND_EARLIER} />);

    const buttons = within(screen.getAllByRole('list')[0]!).getAllByRole('button');
    expect(buttons.map((button) => button.textContent!.charAt(0))).toEqual(['5', '2', '3', '4']);
  });
});

describe('the routing half’s words', () => {
  it('has every key in both catalogues', () => {
    for (const key of [
      'waitingOn', 'what.quest', 'what.questOf', 'what.questLanes_one', 'what.questLanes_other', 'what.questOfLanes_one',
      'what.questOfLanes_other', 'part.alongside', 'part.earlier_one', 'part.earlier_other', 'published_one', 'published_other', 'routed.lane',
      'routed.took', 'routed.askQuest', 'routed.repository', 'routed.ask', 'routed.anotherSession', 'quest.nextStep',
    ]) {
      expect([key, `work.atWork.${key}` in en, `work.atWork.${key}` in zh]).toEqual([key, true, true]);
    }
  });

  it('says the same in 中文', async () => {
    await i18n.changeLanguage('zh');
    const { rerender } = render(<AtWorkView held={LEAD_WAITING_ON_LANE} />);
    expect(screen.getByText('在等driver 中的 #q7（core泳道）')).toBeTruthy();
    expect(rowOf(1).textContent).toContain('由第 1 段发布到core泳道');

    rerender(<AtWorkView held={PUBLISHED_AND_WAITS_ON_NONE} />);
    expect(screen.getByText('已发布 3 个，未等待')).toBeTruthy();
    rerender(<AtWorkView held={RESUMED_AND_EARLIER} />);
    expect(screen.getByText('更早的 4 段')).toBeTruthy();
    rerender(<AtWorkView held={CHAT_TOOK_A_QUEST} />);
    expect(rowOf(0).textContent).toContain('接手了 #q1');
  });
});
