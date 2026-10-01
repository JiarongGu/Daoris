import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { PROPOSED } from '../asks/fixtures';
import { HELD_BY_PERSON, OPEN, SITTING, TAKEN } from './fixtures';
import { QuestList } from './QuestList';

// Quests' list as a molecule (FRAME1d, D118 §2): rows arrive with what each says, and every press goes out.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const list = (over: Partial<Parameters<typeof QuestList>[0]> = {}) => {
  const presses = { onChoose: vi.fn(), onResume: vi.fn() };
  render(
    <QuestList
      asks={[]} quests={[{ quest: OPEN }, { quest: TAKEN }]} closed={false} empty="No open quests anywhere" chosen={null}
      {...presses} {...over}
    />,
  );
  return presses;
};

describe("Quests' list", () => {
  it('puts the asks first, then the quests by state, each group counted, each row a row of its list', () => {
    list({ asks: [{ ask: PROPOSED }] });

    expect(screen.getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent)).toEqual([
      'Asks (1)', 'Open — waiting to be taken (1)', 'Taken (1)',
    ]);
    for (const item of screen.getAllByRole('listitem')) expect(item).toHaveAttribute('data-list-row');
  });

  it('chooses a quest by its id and an ask as an ask', async () => {
    const { onChoose } = list({ asks: [{ ask: PROPOSED }] });

    await userEvent.click(screen.getByRole('button', { name: /Expose a streaming budget/ }));
    await userEvent.click(screen.getByRole('button', { name: /The chunk streamer stalls/ }));
    expect(onChoose.mock.calls).toEqual([[OPEN.id], [`ask:${PROPOSED.id}`]]);
  });

  it('marks a week of silence on its row', () => {
    list({ quests: [{ quest: SITTING }] });
    expect(screen.getByText('sat 9d')).toBeInTheDocument();
  });

  /** USE1: a hold is lifted from the row, beside its door and never inside it, since a button holds no button. */
  it('offers a held repository\'s resume beside the row\'s door, and lifts it without choosing the quest', async () => {
    const { onChoose, onResume } = list({ quests: [{ quest: OPEN, sitting: HELD_BY_PERSON }] });
    const resume = screen.getByRole('button', { name: 'Resume engine' });

    expect(screen.getByRole('button', { name: /Expose a streaming budget/ })).not.toContainElement(resume);
    await userEvent.click(resume);
    expect(onResume).toHaveBeenCalledWith('engine');
    expect(onChoose).not.toHaveBeenCalled();
  });

  it('says the receiver it is filtered to at its head', () => {
    list({ filteredTo: 'engine' });
    expect(screen.getByText('Showing quests to engine')).toBeInTheDocument();
  });

  it('says no quest is open under the asks, where there are asks and no quests', () => {
    list({ asks: [{ ask: PROPOSED }], quests: [] });
    expect(screen.getByText('No open quests anywhere')).toBeInTheDocument();
  });

  it('says the sentence of a list that never had an answer, rather than being blank', () => {
    list({ quests: [], unanswered: 'Daoris could not reach this machine\'s host.' });
    expect(screen.getByText('Daoris could not reach this machine\'s host.')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 3 })).toBeNull();
  });

  it('wears the list\'s choice on the chosen row', () => {
    list({ chosen: TAKEN.id });
    const rows = screen.getAllByRole('listitem');
    expect(within(rows[1]!).getByRole('button')).toHaveAttribute('aria-current', 'true');
    expect(within(rows[0]!).getByRole('button')).not.toHaveAttribute('aria-current');
  });
});
