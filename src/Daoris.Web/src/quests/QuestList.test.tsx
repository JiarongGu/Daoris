import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { PROPOSED } from '../asks/fixtures';
import { ContextMenus } from '../menus/ContextMenu';
import { menuActs, rightClick } from '../test/contextMenu';
import { HELD, HELD_BY_PERSON, OPEN, SITTING, TAKEN } from './fixtures';
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

  /** DRIFT1d2: a done a departure holds is listed first, closed quests shown or not, its row saying it awaits the person. */
  it('lists a quest a departure holds first, its row saying it awaits the person’s yes', () => {
    list({ quests: [{ quest: OPEN }, { quest: HELD }] });

    expect(screen.getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent)).toEqual([
      'Waiting on you (1)', 'Open — waiting to be taken (1)',
    ]);
    expect(within(screen.getByRole('button', { name: /Stream the tiles from the cold cache/ })).getByText('awaits your yes'))
      .toBeInTheDocument();
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

  /**
   * CTX1 (D138, design §4): a row offers what it does, its page what is done to it. A quest's row: *Open*, its own act (a
   * held repository's *Resume*), and its id; an ask's row: *Open* and its id.
   */
  it('offers a row’s own acts on a right-click: Open, a hold’s Resume, its id', async () => {
    const copy = vi.fn();
    const { onChoose, onResume } = list({ asks: [{ ask: PROPOSED }], quests: [{ quest: OPEN, sitting: HELD_BY_PERSON }, { quest: TAKEN }] });
    render(<ContextMenus doors={{ copy }} />);

    rightClick(screen.getByRole('button', { name: /Expose a streaming budget/ }));
    expect(await menuActs('Actions for Expose a streaming budget on the chunk API')).toEqual(['Open', 'Resume engine', 'Copy quest ID']);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Resume engine' }));
    expect(onResume).toHaveBeenCalledWith('engine');

    rightClick(screen.getByRole('button', { name: /Read the media field names/ }));
    expect(await menuActs()).toEqual(['Open', 'Copy quest ID']);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Open' }));
    expect(onChoose).toHaveBeenCalledWith(TAKEN.id);

    rightClick(screen.getByRole('button', { name: /The chunk streamer stalls/ }));
    expect(await menuActs()).toEqual(['Open', 'Copy ask ID']);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Copy ask ID' }));
    expect(copy).toHaveBeenCalledWith(PROPOSED.id);
  });

  it('wears the list\'s choice on the chosen row', () => {
    list({ chosen: TAKEN.id });
    const rows = screen.getAllByRole('listitem');
    expect(within(rows[1]!).getByRole('button')).toHaveAttribute('aria-current', 'true');
    expect(within(rows[0]!).getByRole('button')).not.toHaveAttribute('aria-current');
  });
});
