import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { Ask } from '../api';
import i18n from '../i18n';
import type { Consideration } from '../signals';
import type { WorkDoor } from '../work/pausing';
import { LISTED_ASK, LISTED_CONSIDERED, PAUSED_LISTED_ASK, PAUSED_LISTED_CONSIDERED } from '../work/pausingFixtures';
import { AskPage } from './AskPage';
import { PUBLISHED } from './fixtures';

// An ask's page shows its work (PAUSE1h, D132 §7.1): each quest of `WORK_PLAN` with its state and why it sits, the questions
// its sessions asked of other repositories under the quest whose session asked them, and those sessions as doors into
// Sessions, so the person sees what a pause or an abandon reaches before pressing. Props in, presses out (components §2).

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const door = (over: Partial<WorkDoor> = {}): WorkDoor => ({
  plan: LISTED_ASK, wired: false, busy: false, onPause: vi.fn(), onResume: vi.fn(), onAbandon: vi.fn(), ...over,
});

const ASK: Ask = { ...PUBLISHED, id: 'a1b2c3', quests: ['9a8b7c', '5e4f3d', '2d3e4f', '3c2b1a'] };

const page = ({ work = door(), considered = LISTED_CONSIDERED, attend = true, titles = { '9a8b7c': 'Cap chunk hydration per frame' } }: {
  work?: WorkDoor | undefined; considered?: Consideration[]; attend?: boolean; titles?: Record<string, string>;
} = {}) => {
  const props = {
    ask: ASK, receivers: ['engine'], questTitles: titles, onPublish: vi.fn(), onClose: vi.fn(), onDelete: vi.fn(),
    onOpenQuest: vi.fn(), onAttend: attend ? vi.fn() : undefined, work, considered,
  };
  render(<AskPage {...props} />);
  return props;
};

const section = () => screen.getByRole('region', { name: 'Became' });
/** A quest's own entry in the work: the list item whose first line names it. */
const entry = (id: string) => within(section()).getByText(`#${id}`).closest('li')!;

describe("an ask's page lists its work", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('lists each quest it asked with its state and route, and says that this is what a pause or an abandon reaches', () => {
    page();
    expect(section()).toHaveTextContent("Its work as this machine's driver reads it, which is what a pause or an abandon reaches.");
    const top = within(section()).getAllByRole('list')[0];
    const quests = within(top).getAllByRole('listitem').filter((item) => item.parentElement === top);
    expect(quests.map((item) => item.querySelector('p')!.textContent)).toEqual([
      'taken#9a8b7cCap chunk hydration per frame→ engine',
      'taken#5e4f3dExpose the streaming budget to the HUD→ game',
      'open#2d3e4fDocument the budget in the HUD guide→ game',
      'taken#3c2b1aTune the frame-time histogram buckets→ engine',
    ]);
  });

  it('says why each sits: the driver’s sentence, a wait as its question below, its own pause', () => {
    page();
    // The driver's sentence, its backticks set as code.
    expect(entry('2d3e4f')).toHaveTextContent('game is held by the person.');
    expect(entry('9a8b7c')).toHaveTextContent('Waits on the answer to its question below, and carries on by itself in the same tree once it comes.');
    expect(entry('3c2b1a')).toHaveTextContent('Paused on its own: its own page resumes it, and resuming this ask does not.');
    // A quest whose session runs has no sentence of the driver's: its session's state says where it is.
    expect(within(entry('5e4f3d')).queryByText(/held|paused|waits/i)).toBeNull();
  });

  it('lists the questions its sessions asked under the quest whose session asked them, each with why it sits', () => {
    page();
    const asked = within(entry('9a8b7c')).getByRole('list', { name: 'Asked of other repositories (2)' });
    expect(within(asked).getAllByRole('listitem').map((item) => item.querySelector('p')!.textContent)).toEqual([
      'taken#0c1d2eProfile the cold-cache path→ lantern',
      'done#7f6e5dWhich chunk sizes does the HUD assume?→ game',
    ]);
    expect(entry('0c1d2e')).toHaveTextContent('Taken on studio-pc: a pause here does not reach it.');
  });

  it('offers each session as a door into Sessions, a teammate’s saying where it runs, and never the intake', async () => {
    const { onAttend } = page();
    const sessions = within(entry('9a8b7c')).getAllByRole('button', { name: /^Open session/ })
      .filter((button) => button.closest('li') === entry('9a8b7c'));
    expect(sessions.map((button) => button.textContent)).toEqual(['f41led00', 's1a2b3c4']);
    expect(within(entry('9a8b7c')).getByText('failed')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Open session w0rk1ng0 in Sessions' }));
    expect(onAttend).toHaveBeenCalledWith('w0rk1ng0');
    expect(entry('0c1d2e')).toHaveTextContent('runs on studio-pc');
    expect(within(section()).queryByText(/i9n8t7k6/)).toBeNull();
  });

  it('names its sessions where Sessions is not, and opens a quest’s page only where the page holds it', async () => {
    const { onOpenQuest } = page({ attend: false });
    expect(within(section()).queryAllByRole('button', { name: /^Open session/ })).toEqual([]);
    expect(within(entry('5e4f3d')).getByText('w0rk1ng0')).toBeInTheDocument();

    await userEvent.click(within(section()).getByRole('button', { name: 'Cap chunk hydration per frame' }));
    expect(onOpenQuest).toHaveBeenCalledWith('9a8b7c');
    expect(within(section()).queryByRole('button', { name: 'Expose the streaming budget to the HUD' })).toBeNull();
  });

  it('says this ask’s pause briefly on each quest it holds, and a quest’s own pause and one taken elsewhere as they are', () => {
    page({ work: door({ plan: PAUSED_LISTED_ASK }), considered: PAUSED_LISTED_CONSIDERED });
    for (const id of ['9a8b7c', '5e4f3d', '2d3e4f']) expect(entry(id)).toHaveTextContent('Paused with this ask, until you resume it.');
    expect(entry('3c2b1a')).toHaveTextContent('Paused on its own');
    expect(entry('0c1d2e')).toHaveTextContent('Taken on studio-pc: a pause here does not reach it.');
    expect(within(entry('5e4f3d')).getByText('stopped')).toBeInTheDocument();
  });

  it('lists the quests it became, as before, while the plan is on its way and in a browser', () => {
    page({ work: door({ plan: null }) });
    expect(section()).not.toHaveTextContent('what a pause or an abandon reaches');
    expect(within(section()).getByRole('button', { name: /Cap chunk hydration per frame/ })).toBeInTheDocument();
    expect(within(section()).getByText('#5e4f3d')).toBeInTheDocument();
  });

  it('says its work in 中文', async () => {
    await i18n.changeLanguage('zh');
    page({ work: door({ plan: PAUSED_LISTED_ASK }), considered: PAUSED_LISTED_CONSIDERED });
    const zh = screen.getByRole('region', { name: '已成为的委托' });
    expect(zh).toHaveTextContent('本机驱动读到的它的工作，也就是暂缓或放弃所涉及的范围。');
    expect(within(zh).getByRole('list', { name: '向其他仓库提出（2）' })).toBeInTheDocument();
    expect(zh).toHaveTextContent('随此需求暂缓，直到你恢复它。');
    expect(zh).toHaveTextContent('已在 studio-pc 上接下：本机的暂缓不涉及它。');
    expect(within(zh).getByRole('button', { name: '在「会话」中打开会话 w0rk1ng0' })).toBeInTheDocument();
  });
});
