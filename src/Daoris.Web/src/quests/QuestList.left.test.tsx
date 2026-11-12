import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../locales/i18n';
import { DONE, OPEN } from './fixtures';
import { QuestList } from './QuestList';

// A done that departed from a requirement is unfinished work: Quests' list keeps it where open work is, apart from finished work.

describe("Quests' list, a done with part of the work left", () => {
  it('lists it in a group of its own saying done with part left, with closed quests hidden', () => {
    const left = {
      ...DONE, id: 'le0ft0', title: 'Stream the tiles', accepted: '2026-10-02T10:00:00Z', held: false,
      answers: [{ requirement: 1, met: 'built' }, { requirement: 2, departed: 'a cold cache cannot', quote: 'keep the budget' }],
    };
    const finished = { ...DONE, id: 'fi0ni0', title: 'Finished work' };

    render(
      <Tooltip.Provider>
        <QuestList
          asks={[]} quests={[OPEN, left, finished].map((quest) => ({ quest }))} closed={false} empty="Nothing" chosen={null}
          onChoose={vi.fn()} onResume={vi.fn()}
        />
      </Tooltip.Provider>,
    );

    expect(screen.getByText('Done, part left (1)')).toBeInTheDocument();
    expect(within(screen.getByText('Stream the tiles').closest('li')!).getByText('done, part left')).toBeInTheDocument();
    expect(screen.queryByText('Finished work')).toBeNull();
  });
});
