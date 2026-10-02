import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import type { WorkDoor } from '../work/pausing';
import { ABANDONED_ENTRY, PAUSABLE_QUEST, PAUSED_QUEST } from '../work/pausingFixtures';
import { OPEN, PAUSED_ITSELF, PAUSED_WITH_ASK, PAUSED_WITH_QUEST, STOPPED, TAKEN } from './fixtures';
import { QuestPage } from './QuestPage';

// A quest's page with this machine's driver (PAUSE1e, D132 §7.1): *Pause…* and *Abandon…* in its header, beside *Decline…*;
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

const headerActs = () => within(screen.getByRole('main').querySelector('header')!).getAllByRole('button').map((button) => button.textContent);

describe('pausing and abandoning a quest', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('offers Pause… and Abandon… beside Decline…, where the plan says they apply', () => {
    page({ work: door() });
    expect(headerActs()).toEqual(['Take', 'Mark done', 'Pause…', 'Decline…', 'Abandon…']);
  });

  it('asks once before a pause that stops its running session, then pauses', async () => {
    const work = door();
    page({ quest: TAKEN, work: { ...work, plan: { ...PLAN, id: TAKEN.id } } });

    await userEvent.click(screen.getByRole('button', { name: 'Pause…' }));
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

    await user.click(screen.getByRole('button', { name: 'Abandon…' }));
    expect(screen.queryByRole('button', { name: 'Abandon…' })).toBeNull();
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
    expect(headerActs()).toEqual(['Take', 'Mark done', 'Decline…']);
    expect(screen.getByText(/a browser offers neither/)).toHaveTextContent('daoris-driver quest abandon abc123');
  });

  it('names its acts in 中文', async () => {
    await i18n.changeLanguage('zh');
    page({ work: door() });
    expect(headerActs()).toEqual(['接下', '标为完成', '暂缓…', '谢绝…', '放弃…']);
  });
});
