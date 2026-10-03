import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { Ask } from '../api';
import i18n from '../i18n';
import type { WorkDoor } from '../work/pausing';
import { ABANDON_ANSWER, ABANDONED_ASK, MIXED_ASK, PAUSABLE_ASK, PAUSED_ASK } from '../work/pausingFixtures';
import { AskPage } from './AskPage';
import { CLOSED, PUBLISHED } from './fixtures';

// An ask's page with this machine's driver (PAUSE1e, D132 §7.1): *Pause…*, *Resume* and *Abandon…* in its header, each where
// it applies and absent where it does not; the pause's ask and the abandon's list under it; *What went* and *What stayed*
// after. Props in and presses out, so every plan the driver could answer is an assertion (components §2).

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const door = (over: Partial<WorkDoor> = {}): WorkDoor => ({
  plan: PAUSABLE_ASK, wired: false, busy: false, onPause: vi.fn(), onResume: vi.fn(), onAbandon: vi.fn(), ...over,
});

const page = (work?: WorkDoor, ask: Ask = { ...PUBLISHED, id: 'a1b2c3', deletable: true }) => {
  const props = {
    ask, receivers: ['engine'], questTitles: {}, onPublish: vi.fn(), onClose: vi.fn(), onDelete: vi.fn(), onOpenQuest: vi.fn(), work,
  };
  const view = render(<AskPage {...props} />);
  return { ...props, view, main: screen.getByRole('main') };
};

const headerActs = () => within(screen.getByRole('main').querySelector('header')!).getAllByRole('button').map((button) => button.textContent);

describe('pausing and abandoning an ask', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('offers Pause… and Abandon… where the plan says they apply, beside Close ask and Delete…', () => {
    page(door());
    expect(headerActs()).toEqual(['Pause…', 'Close ask', 'Abandon…', 'Delete…']);
  });

  it('offers none of the three while the plan is on its way: an act is never guessed', () => {
    page(door({ plan: null }));
    expect(headerActs()).toEqual(['Close ask', 'Delete…']);
  });

  it('asks once before a pause that stops a running session, saying what follows, then pauses', async () => {
    const work = door({ wired: true });
    page(work);

    await userEvent.click(screen.getByRole('button', { name: 'Pause…' }));
    const ask = screen.getByRole('group', { name: 'pause this work' });
    expect(ask).toHaveTextContent('Stops 1 running session now and starts nothing of ask #a1b2c3 on this machine until you resume it.');
    expect(ask).toHaveTextContent('Another machine may still take #5e4f3d');
    expect(screen.queryByRole('button', { name: 'Pause…' })).toBeNull();
    expect(work.onPause).not.toHaveBeenCalled();

    await userEvent.click(within(ask).getByRole('button', { name: 'Pause ask' }));
    expect(work.onPause).toHaveBeenCalledOnce();
  });

  it('applies a pause that stops nothing at once, since nothing is lost', async () => {
    const quiet = { ...PAUSABLE_ASK, sessions: PAUSABLE_ASK.sessions.filter((session) => session.pause !== 'stopped') };
    const work = door({ plan: quiet });
    page(work);

    await userEvent.click(screen.getByRole('button', { name: 'Pause…' }));
    expect(work.onPause).toHaveBeenCalledOnce();
    expect(screen.queryByRole('group', { name: 'pause this work' })).toBeNull();
  });

  it('says paused beside its state, and offers Resume as its loud act and no Pause…', async () => {
    const work = door({ plan: PAUSED_ASK });
    page(work);

    expect(headerActs()).toEqual(['Resume', 'Close ask', 'Abandon…', 'Delete…']);
    expect(screen.getByRole('button', { name: 'Resume' }).className).toContain('bg-accent');
    expect(within(screen.getByRole('main').querySelector('header')!).getByText('paused')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Resume' }));
    expect(work.onResume).toHaveBeenCalledOnce();
  });

  it('lists what the abandon would take and keep, and sends the reason with exactly the pieces it listed', async () => {
    const work = door({ plan: MIXED_ASK });
    const { view } = page(work);
    const user = userEvent.setup();

    await user.click(screen.getByRole('button', { name: 'Abandon…' }));
    const list = screen.getByRole('group', { name: 'abandon this work' });
    expect(within(list).getByRole('list', { name: 'What stays' })).toHaveTextContent('taken on studio-pc');

    // The reader answers again while the list is open; the second press still sends what the first listed.
    view.rerender(
      <Tooltip.Provider>
        <AskPage
          ask={{ ...PUBLISHED, id: 'a1b2c3', deletable: true }} receivers={['engine']} questTitles={{}}
          onPublish={vi.fn()} onClose={vi.fn()} onDelete={vi.fn()} onOpenQuest={vi.fn()}
          work={{ ...work, plan: { ...MIXED_ASK, abandon: { ...MIXED_ASK.abandon, pieces: ['quest:9a8b7c'] } } }}
        />
      </Tooltip.Provider>,
    );
    await user.type(screen.getByRole('textbox', { name: 'why — kept with each decline' }), 'It went the wrong way.');
    await user.click(screen.getByRole('button', { name: 'Abandon ask' }));

    expect(work.onAbandon).toHaveBeenCalledWith('It went the wrong way.', MIXED_ASK.abandon.pieces, expect.any(Function));
  });

  it('shows when it was abandoned, what went and what stayed, from this machine’s record', () => {
    page(door({ plan: ABANDONED_ASK }), { ...CLOSED, id: 'a1b2c3' });

    expect(screen.getByText('Abandoned')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'What went' })).toHaveTextContent('Declined #9a8b7c, #5e4f3d with your reason.');
    expect(screen.getByRole('region', { name: 'What stayed' })).toHaveTextContent('done: finished work keeps its record');
  });

  it('says the second press’s answer at once, before the record catches up', () => {
    page(door({ plan: MIXED_ASK, outcome: { answer: ABANDON_ANSWER, at: '2026-10-03T10:41:00Z' } }));
    expect(screen.getByRole('region', { name: 'What went' })).toHaveTextContent('#5e4f3d was taken on another machine');
  });

  it('says closing leaves its quests as they are, and that Abandon declines them', async () => {
    page(door());
    await userEvent.click(screen.getByRole('button', { name: 'Close ask' }));
    expect(screen.getByText('Closing leaves its quests as they are; Abandon declines them.')).toBeInTheDocument();
  });

  it('offers none of the three in a browser, and names the terminal’s commands instead', () => {
    page(undefined);
    expect(headerActs()).toEqual(['Close ask', 'Delete…']);
    const said = screen.getByText(/a browser offers neither/);
    expect(within(said).getByText('daoris-driver ask --pause a1b2c3').tagName).toBe('CODE');
  });

  it('names its acts in 中文', async () => {
    await i18n.changeLanguage('zh');
    page(door());
    expect(headerActs()).toEqual(['暂缓…', '关闭需求', '放弃…', '删除…']);
  });
});
