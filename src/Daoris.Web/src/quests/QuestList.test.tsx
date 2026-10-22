import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../locales/i18n';
import { PROPOSED } from '../asks/fixtures';
import { ContextMenus } from '../menus/ContextMenu';
import { menuActs, rightClick } from '../test/contextMenu';
import { DONE as DONE_QUEST, HELD, HELD_BY_PERSON, OPEN, REOPENED, SITTING, SUBMITTED, TAKEN } from './fixtures';
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
  /** QUESTLISTS1: one piece of work is one row, the quest's, named by its ask's short title; only an ask with a wait of its own keeps a row. */
  it('places an ask that became one quest once, as that quest’s row, and keeps the asks waiting on their own', () => {
    const became = (ask: typeof PROPOSED, quest: typeof OPEN) => ({ ...PROPOSED, ...ask, state: 'Published' as const, quests: [quest.id] });
    const forOpen = became({ ...PROPOSED, id: 'a1a1a1a1a1a1', short: 'Streaming budget' }, OPEN);
    const forTaken = became({ ...PROPOSED, id: 'b2b2b2b2b2b2', short: 'Config names' }, TAKEN);
    const forDone = { ...became({ ...PROPOSED, id: 'c3c3c3c3c3c3', short: 'Allocator bump' }, DONE_QUEST), state: 'Done' as const };
    const twoQuests = { ...PROPOSED, id: 'd4d4d4d4d4d4', short: 'Two repositories', state: 'Published' as const, quests: [SITTING.id, HELD.id] };
    const openAsk = { ...PROPOSED, id: 'e5e5e5e5e5e5', short: 'Still being read', state: 'Open' as const, proposal: [] };
    const parked = { ...became({ ...PROPOSED, id: 'f6f6f6f6f6f6', short: 'Intake asked you' }, SUBMITTED) };
    list({
      closed: true,
      asks: [
        { ask: forOpen }, { ask: forTaken }, { ask: forDone }, { ask: twoQuests }, { ask: openAsk }, { ask: PROPOSED },
        { ask: parked, intake: 'awaiting-person' },
      ],
      quests: [OPEN, TAKEN, DONE_QUEST, SITTING, HELD, SUBMITTED].map((quest) => ({ quest })),
    });

    // The asks group keeps the open ask, the proposed one, the two-quest ask and the parked intake: four.
    expect(screen.getByText('Asks (4)')).toBeInTheDocument();
    for (const name of ['Still being read', 'Two repositories', 'Intake asked you']) expect(screen.getAllByText(name)).toHaveLength(1);
    // The three single-quest asks are their quests' rows, named by the ask's short title, the ask on the route line.
    for (const [name, ask] of [['Streaming budget', forOpen], ['Config names', forTaken], ['Allocator bump', forDone]] as const) {
      const row = screen.getByText(name).closest('li')!;
      expect(screen.getAllByText(name)).toHaveLength(1);
      expect(within(row).getByText(new RegExp(`ask #${ask.id.slice(0, 6)}`))).toBeInTheDocument();
      expect(within(row).queryByText('Published')).toBeNull();
    }
    expect(screen.queryByText('Expose a streaming budget on the chunk API')).toBeNull();
  });

  it('opens the ask of a merged row from the row’s menu, since the ask has no row of its own', async () => {
    const { onChoose } = list({ asks: [{ ask: { ...PROPOSED, state: 'Published', short: 'Streaming budget', quests: [OPEN.id] } }] });
    render(<ContextMenus doors={{ copy: vi.fn() }} />);

    rightClick(screen.getByRole('button', { name: /Streaming budget/ }));
    expect(await menuActs()).toEqual(['Open', 'Open the ask', 'Copy quest ID']);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Open the ask' }));
    expect(onChoose).toHaveBeenCalledWith(`ask:${PROPOSED.id}`);
  });

  it('names the ask once where the quest already comes from it, as a quest an ask published does', () => {
    const ask = { ...PROPOSED, id: 'a1a1a1a1a1a1', state: 'Published' as const, short: 'Streaming budget', quests: [OPEN.id] };
    list({ asks: [{ ask }], quests: [{ quest: { ...OPEN, from: 'ask #a1a1a1' } }] });

    const row = screen.getByText('Streaming budget').closest('li')!;
    expect(within(row).getAllByText(/ask #a1a1a1/)).toHaveLength(1);
  });

  it('keeps an ask whose one quest this list does not hold, so that no work is lost from it', () => {
    list({ asks: [{ ask: { ...PROPOSED, state: 'Published', short: 'Elsewhere work', quests: ['not-listed'] } }] });

    expect(screen.getByText('Asks (1)')).toBeInTheDocument();
    expect(screen.getByText('Elsewhere work')).toBeInTheDocument();
  });

  /** SESSONE2h: the asks the mirrored messages name, in a group of their own after this machine's, each a row that opens its page. */
  it('lists the asks held elsewhere in a group after this machine’s asks, with their unread, and opens one by its item', async () => {
    const { onChoose } = list({
      asks: [{ ask: PROPOSED }],
      teammates: [
        { id: '7e6f51', workspace: 'default', origins: ['key-a'], latest: '2026-10-15T09:05:00Z', messages: 3, unread: 2 },
        { id: '7e6f50', workspace: 'default', origins: [], latest: '2026-10-15T08:00:00Z', messages: 1, unread: 0 },
      ],
    });

    expect(screen.getByText('Teammates’ asks (2)')).toBeInTheDocument();
    const row = screen.getByText('#7e6f51').closest('li')!;
    expect(within(row).getByText('2 unread')).toBeInTheDocument();
    expect(within(screen.getByText('#7e6f50').closest('li')!).queryByText(/unread/)).toBeNull();
    await userEvent.click(within(row).getByRole('button'));
    expect(onChoose).toHaveBeenCalledWith('ask:7e6f51');
  });

  /**
   * BADGEUX1 (the UI/UX design §3.10): open's hue is the person's waits alone, and news waits on nobody (D196 point 6), so an ask's
   * unread is said with its word in a neutral pill, this machine's ask's and a teammate's alike.
   */
  it('says an ask’s unread with its word, never in open’s hue', () => {
    list({
      asks: [{ ask: PROPOSED, unread: 3 }],
      teammates: [{ id: '7e6f51', workspace: 'default', origins: ['key-a'], latest: '2026-10-15T09:05:00Z', messages: 3, unread: 2 }],
    });

    for (const said of ['3 unread', '2 unread']) {
      expect(screen.getByText(said).className).not.toMatch(/st-open|ink-open/);
      expect(screen.getByText(said).className).toContain('text-ink-soft');
    }
  });

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

  /** FABRIC-CLOSE1d and CLOSE1c3 (§E5): a submitted quest's row reads submitted with its hold; a step its source's reopen holds has its own pill. */
  it('reads a submitted quest as submitted, and a step its source’s reopen holds by its own pill, never the yes', () => {
    list({ quests: [{ quest: { ...SUBMITTED, title: 'Ship the cold cache' } }, { quest: REOPENED }, { quest: TAKEN }] });

    const submitted = screen.getByRole('button', { name: /Ship the cold cache/ });
    expect(within(submitted).getByText('submitted')).toBeInTheDocument();
    expect(within(submitted).getByText('awaits your yes')).toBeInTheDocument();
    const reopened = screen.getByRole('button', { name: /Verify the tiles in a playtest/ });
    expect(within(reopened).getByText('source reopened')).toBeInTheDocument();
    expect(within(reopened).queryByText('awaits your yes')).toBeNull();
    expect(within(screen.getByRole('button', { name: /Read the media field names/ })).getByText('taken')).toBeInTheDocument();
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
