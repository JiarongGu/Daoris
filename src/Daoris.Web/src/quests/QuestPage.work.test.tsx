import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { ContextMenus } from '../menus/ContextMenu';
import { menuActs, rightClick } from '../test/contextMenu';
import type { WorkDoor } from '../work/pausing';
import { ABANDONED_ENTRY, PAUSABLE_QUEST, PAUSED_QUEST } from '../work/pausingFixtures';
import { FAILED, OPEN, PAUSED_ITSELF, PAUSED_WITH_ASK, PAUSED_WITH_QUEST, STOPPED, TAKEN, TAKEN_ELSEWHERE } from './fixtures';
import { QuestPage } from './QuestPage';

// A quest's page with this machine's driver (PAUSE1e, D132 §7.1): *Pause…* and *Abandon…* in its header's ⋯ (UX7c, D152 §7),
// with *Decline…* beside it;
// under *Sitting*, *Resume* for its own pause where *Try again* stands, and for another's pause the sentence and a door to
// whose it is; *What went* and *What stayed* after an abandon.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const PLAN = { ...PAUSABLE_QUEST, id: OPEN.id };

const door = (over: Partial<WorkDoor> = {}): WorkDoor => ({
  plan: PLAN, wired: false, busy: false, onPause: vi.fn(), onResume: vi.fn(), onAbandon: vi.fn(), ...over,
});

const page = (over: Partial<Parameters<typeof QuestPage>[0]> = {}) => {
  const props = {
    quest: OPEN, onRespond: vi.fn(), onDismiss: vi.fn(), onRetry: vi.fn(), onOpenQuest: vi.fn(), onOpenAsk: vi.fn(), ...over,
  };
  render(<QuestPage {...props} />);
  return props;
};

const headerActs = () => within(screen.getByRole('main').querySelector('header')!).getAllByRole('button')
  .map((button) => button.textContent || button.getAttribute('aria-label'));

/** The acts the head keeps in its ⋯ (UX7c), opened from the keyboard as every menu is in jsdom. */
async function foldedActs(): Promise<string[]> {
  const user = userEvent.setup();
  within(screen.getByRole('main').querySelector('header')!).getByRole('button', { name: /More actions|更多操作/ }).focus();
  await user.keyboard('{Enter}');
  await screen.findAllByRole('menuitem');
  return screen.getAllByRole('menuitem').map((item) => item.textContent ?? '');
}

/** One act from the head's ⋯. */
async function folded(name: string) {
  await foldedActs();
  await userEvent.setup().click(screen.getByRole('menuitem', { name }));
}

describe('pausing and abandoning a quest', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('offers Pause… and Abandon… in its ⋯ beside Decline…, where the plan says they apply', async () => {
    page({ work: door() });
    expect(headerActs()).toEqual(['Take', 'Decline…', 'More actions']);
    expect(await foldedActs()).toEqual(['Mark done…', 'Pause…', 'Abandon…', 'Copy quest ID']);
  });

  it('asks once before a pause that stops its running session, then pauses', async () => {
    const work = door();
    page({ quest: TAKEN, work: { ...work, plan: { ...PLAN, id: TAKEN.id } } });

    await folded('Pause…');
    const ask = screen.getByRole('group', { name: 'pause this work' });
    expect(ask).toHaveTextContent('starts nothing of quest #def456 on this machine');
    await userEvent.click(within(ask).getByRole('button', { name: 'Pause quest' }));
    expect(work.onPause).toHaveBeenCalledOnce();
  });

  it('offers Resume under Sitting for its own pause, where Try again would stand, and no Pause…', async () => {
    const work = door({ plan: { ...PAUSED_QUEST, id: OPEN.id } });
    page({ sitting: PAUSED_ITSELF, work });

    const sitting = screen.getByText('Sitting').nextElementSibling as HTMLElement;
    expect(sitting).toHaveTextContent('you paused #abc123');
    expect(within(sitting).queryByRole('button', { name: 'Try again' })).toBeNull();
    expect(headerActs()).not.toContain('Pause…');
    await userEvent.click(within(sitting).getByRole('button', { name: 'Resume' }));
    expect(work.onResume).toHaveBeenCalledOnce();
  });

  it('says its own pause and offers Resume even where the driver has no verdict for it (its session waits on you)', async () => {
    const work = door({ plan: { ...PAUSED_QUEST, id: OPEN.id } });
    page({ sitting: null, work });

    expect(screen.getByText('You paused it on this machine: nothing of its work starts here until you resume it.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Resume' }));
    expect(work.onResume).toHaveBeenCalledOnce();
  });

  it('opens the ask whose pause holds it, where that pause is resumed', async () => {
    const props = page({ sitting: PAUSED_WITH_ASK, work: door() });
    const sitting = screen.getByText('Sitting').nextElementSibling as HTMLElement;
    expect(within(sitting).queryByRole('button', { name: 'Resume' })).toBeNull();
    await userEvent.click(within(sitting).getByRole('button', { name: 'Open ask #a1b2c3' }));
    expect(props.onOpenAsk).toHaveBeenCalledWith('a1b2c3');
  });

  it('opens the quest whose pause holds a question it asked', async () => {
    const props = page({ sitting: PAUSED_WITH_QUEST, work: door() });
    await userEvent.click(screen.getByRole('button', { name: 'Open quest #def456' }));
    expect(props.onOpenQuest).toHaveBeenCalledWith('def456');
  });

  it('keeps Try again for a stop, which a pause comes before', () => {
    page({ sitting: STOPPED, work: door() });
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Resume' })).toBeNull();
  });

  it('lists the abandon, and sends its reason with exactly the pieces listed', async () => {
    const work = door();
    page({ work });
    const user = userEvent.setup();

    await folded('Abandon…');
    expect(screen.queryByRole('menuitem', { name: 'Abandon…' })).toBeNull();
    await user.type(screen.getByRole('textbox', { name: 'why — kept with each decline' }), 'Not needed now.');
    await user.click(screen.getByRole('button', { name: 'Abandon quest' }));
    expect(work.onAbandon).toHaveBeenCalledWith('Not needed now.', PLAN.abandon.pieces, expect.any(Function));
  });

  it('shows what went and what stayed after an abandon', () => {
    page({
      quest: { ...OPEN, status: 'Declined' },
      work: door({ plan: { ...PLAN, abandon: { abandonable: false, pieces: [], closes: null, abandoned: { ...ABANDONED_ENTRY, closed: false } } } }),
    });
    expect(screen.getByText('Abandoned')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'What went' })).toHaveTextContent('Declined #9a8b7c, #5e4f3d with your reason.');
    expect(screen.getByRole('region', { name: 'What stayed' })).toBeInTheDocument();
  });

  it('offers none of the three in a browser, and names the terminal’s commands instead', () => {
    page({});
    expect(headerActs()).toEqual(['Take', 'Decline…', 'More actions']);
    expect(screen.getByText(/a browser offers neither/)).toHaveTextContent('daoris-driver quest abandon abc123');
  });

  it('names its acts in 中文', async () => {
    await i18n.changeLanguage('zh');
    page({ work: door() });
    expect(headerActs()).toEqual(['接下', '谢绝…', '更多操作']);
    expect(await foldedActs()).toEqual(['标为完成…', '暂缓…', '放弃…', '复制委托 ID']);
  });
});

/**
 * CTX1 (D138, design §4): a right-click on a quest's page offers what is done to it: its header's acts, then its body's,
 * then its id, each pressed as its button is, so an act that asks still asks under the header.
 */
describe("a quest's page on a right-click", () => {
  const copy = vi.fn();
  const withMenus = (over: Partial<Parameters<typeof QuestPage>[0]> = {}) => {
    const props = page(over);
    render(<ContextMenus doors={{ copy }} />);
    return props;
  };
  const onPage = () => rightClick(screen.getByText(/World streaming needs a per-frame cap/));

  afterEach(async () => {
    vi.clearAllMocks();
    await i18n.changeLanguage('en');
  });

  it('offers its header’s acts, then its id, named for the quest', async () => {
    withMenus({ work: door() });
    onPage();
    expect(await menuActs('Actions for Expose a streaming budget on the chunk API'))
      .toEqual(['Take', 'Mark done…', 'Pause…', 'Decline…', 'Abandon…', 'Copy quest ID']);
  });

  it('presses each as its button does: Take responds, Decline… asks its reason under the header', async () => {
    const props = withMenus({ work: door() });
    onPage();
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Take' }));
    expect(props.onRespond).toHaveBeenCalledWith('take');

    onPage();
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Decline…' }));
    expect(await screen.findByRole('textbox', { name: /reason/i })).toBeInTheDocument();
  });

  it('offers what its body offers: Try again for a stop, Resume for its own pause, its session’s door', async () => {
    const props = withMenus({
      sitting: STOPPED, work: door(),
      session: { id: 's1a2b3c4', quest: OPEN.id, repository: 'engine', adapter: 'claude-code', state: 'stopped', created: OPEN.filed, updated: OPEN.filed },
      onAttend: vi.fn(),
    });
    onPage();
    const acts = await menuActs();
    expect(acts.slice(-3)).toEqual(['Try again', 'Open in Sessions', 'Copy quest ID']);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Try again' }));
    expect(props.onRetry).toHaveBeenCalledOnce();

    onPage();
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Open in Sessions' }));
    expect(props.onAttend).toHaveBeenCalledWith('s1a2b3c4');
  });

  it('resumes its own pause from the menu, and offers no Pause…', async () => {
    const work = door({ plan: { ...PAUSED_QUEST, id: OPEN.id } });
    withMenus({ sitting: PAUSED_ITSELF, work });
    onPage();
    const acts = await menuActs();
    expect(acts).not.toContain('Pause…');
    await userEvent.click(screen.getByRole('menuitem', { name: 'Resume' }));
    expect(work.onResume).toHaveBeenCalledOnce();
  });

  it('copies its id through the window’s copy', async () => {
    withMenus({});
    onPage();
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Copy quest ID' }));
    expect(copy).toHaveBeenCalledWith('abc123');
  });

  it('a link on the page comes first, and the quest’s acts after it', async () => {
    withMenus({});
    rightClick(screen.getByRole('link', { name: 'https://tickets.example/T-1' }));
    expect(await menuActs()).toEqual(['Open', 'Copy link', 'Take', 'Mark done…', 'Decline…', 'Copy quest ID']);
  });

  it('names them in 中文', async () => {
    await i18n.changeLanguage('zh');
    withMenus({ work: door() });
    onPage();
    expect(await menuActs()).toEqual(['接下', '标为完成…', '暂缓…', '谢绝…', '放弃…', '复制委托 ID']);
  });
});

/**
 * LANG1b (D142 points 1, 4, 5): its driven session's record words Daoris's lines in the reader's language, keeps the
 * agent's words as written, and shows a record from before parts as it was kept, marked *shown as recorded*.
 */
describe("a quest's page words its session's note", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('in the reader’s language, with the agent’s words as written beneath their lead-in', async () => {
    await i18n.changeLanguage('zh');
    page({
      quest: TAKEN,
      session: {
        ...FAILED,
        note: 'The agent’s turn failed with the quest still taken: rate limited',
        noteParts: [
          { code: 'ended.turn-failed-taken', values: {}, text: 'The agent’s turn failed with the quest still taken:' },
          { words: 'rate limited until 16:00', by: 'agent' },
        ],
      },
    });

    const record = screen.getByRole('region', { name: '会话' });
    expect(within(record).getByText('智能体这一轮失败，委托仍已接下：')).toBeInTheDocument();
    expect(within(record).getByText('rate limited until 16:00').closest('blockquote')).not.toBeNull();
    expect(within(record).queryByText('按原文显示')).toBeNull();
  });

  it('shows a record from before parts as it was kept, marked', () => {
    page({ quest: TAKEN, session: FAILED });

    const record = screen.getByRole('region', { name: 'Session' });
    expect(within(record).getByText('the first attempt died')).toBeInTheDocument();
    expect(within(record).getByText('shown as recorded')).toBeInTheDocument();
  });
});

/**
 * CARRY2c (D80's CARRY2b note): a taken quest whose take is another machine's sits, and its page says why under *Sitting*.
 * In 中文 that was the driver's English; the tick now names whose the take is, so the page words it from those facts, and a
 * tick from an older shell, with no facts, still shows the driver's sentence. No Try again: nothing here can carry it on.
 */
describe("a quest's page says a take elsewhere", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('in 中文 from the machine and session the tick names', async () => {
    await i18n.changeLanguage('zh');
    page({ quest: TAKEN, sitting: TAKEN_ELSEWHERE });

    const sitting = screen.getByText('搁置').nextElementSibling as HTMLElement;
    expect(sitting).toHaveTextContent('委托 #def456 已在 alice-laptop 上由会话 alice-laptop/ab12cd34 接下');
    expect(sitting).toHaveTextContent('不会接续会话 s0f1r2s3');
    expect(sitting).not.toHaveTextContent('taken on');
    expect(within(sitting).queryByRole('button', { name: '重试' })).toBeNull();
  });

  it('in the driver’s words where the tick names no facts', async () => {
    await i18n.changeLanguage('zh');
    page({ quest: TAKEN, sitting: { ...TAKEN_ELSEWHERE, takenBy: undefined } });

    const sitting = screen.getByText('搁置').nextElementSibling as HTMLElement;
    expect(sitting).toHaveTextContent('Quest #def456 is taken on alice-laptop, by session alice-laptop/ab12cd34');
  });
});
