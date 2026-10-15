import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import type { GoAhead, Session } from '../data/api';
import { STEP_SHOWN } from './reviewFixtures';
import { WaitStrip } from './WaitStrip';

// WAITFOOT1b (D168): what waits on the person, named at the foot above the box with the same presses the head has, so the
// person reading the last message answers it there. Props only: the frame hands it what it hands the head.

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: '7a82cc', repository: 'engine', adapter: 'claude-code', state: 'completed', kind: 'driven',
  created: '2026-09-21T09:46:00Z', updated: '2026-09-21T11:56:00Z', ...over,
});

const parked = session({ state: 'awaiting-person', note: 'I need go-ahead 2 before I write to production.' });
const LANDS = { branch: 'daoris/s-1a2b3c4', tree: 's-1a2b3c4', commits: 2, uncommitted: 0 };
const PLAN = { session: 's1a2b3c4', form: 'merge', target: 'main' };

const goAhead = (number: number, state: GoAhead['state'], act: string): GoAhead => ({
  number, kind: 'write', on: 'production', act, state,
  asked: [{ session: 's1a2b3c4', quest: '7a82cc', at: '2026-09-21T11:00:00Z', why: 'The tile reads its target from it.' }],
});
const GO_AHEADS = [goAhead(1, 'approved', 'the release notes'), goAhead(2, 'asked', 'dashboard configuration')];

const strip = () => screen.getByRole('region', { name: /waiting on you/i });

describe('the strip of what waits on the person, at the foot', () => {
  it('is absent where nothing waits on the person', () => {
    const { container } = render(<WaitStrip session={session({ state: 'working' })} onTop={vi.fn()} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('names a park\'s question and says the box below carries its answer, with no second box', () => {
    render(<WaitStrip session={parked} onResolve={vi.fn()} answerBelow onTop={vi.fn()} />);
    expect(strip()).toHaveTextContent('It asked you a question: answer it in the box below.');
    // The box below is the answer's one door (FIX-LOG 2026-09-29): nothing here takes the words.
    expect(within(strip()).queryByRole('textbox')).toBeNull();
    expect(within(strip()).queryByRole('button', { name: /carry on|Finish|Decline/ })).toBeNull();
  });

  it('answers a go-ahead still open through the head\'s own door, and lists none answered', async () => {
    const onGoAhead = vi.fn();
    render(<WaitStrip session={parked} onResolve={vi.fn()} answerBelow goAheads={GO_AHEADS} onGoAhead={onGoAhead} onTop={vi.fn()} />);

    const items = within(strip()).getAllByRole('listitem');
    expect(items).toHaveLength(1);
    expect(items[0]).toHaveTextContent('dashboard configuration');
    await userEvent.click(within(items[0]!).getByRole('button', { name: 'Approve' }));
    expect(onGoAhead).toHaveBeenCalledWith(2, true, undefined, { done: expect.any(Function), refused: expect.any(Function) });
  });

  it('opens *Accept…* where the frame holds its ask, and holds it while a landing is on its way', async () => {
    const onOpen = vi.fn();
    const props = { session: session(), lands: LANDS, landing: PLAN, onLand: vi.fn(), onTop: vi.fn() };
    const { rerender } = render(<WaitStrip {...props} workAsk={{ open: false, onOpen }} />);

    await userEvent.click(within(strip()).getByRole('button', { name: 'Accept…' }));
    expect(onOpen).toHaveBeenCalledWith(true);

    rerender(<WaitStrip {...props} workAsk={{ open: true, onOpen }} />);
    expect(within(strip()).getByRole('group', { name: 'accept daoris/s-1a2b3c4' })).toBeInTheDocument();

    rerender(<WaitStrip {...props} workAsk={{ open: false, onOpen }} workBusy />);
    expect(within(strip()).getByRole('button', { name: 'Accept…' })).toBeDisabled();
  });

  it('presses the review\'s gate with the same acts the head has', async () => {
    const reviewed = vi.fn();
    render(
      <WaitStrip
        session={session()}
        lands={LANDS}
        landing={{ ...PLAN, review: { state: 'shown', environment: 'local', quest: STEP_SHOWN.id } }}
        onLand={vi.fn()}
        review={{ step: STEP_SHOWN, served: true, acts: { reviewed } }}
        onTop={vi.fn()}
      />,
    );
    await userEvent.click(within(strip()).getByRole('button', { name: 'Reviewed' }));
    expect(reviewed).toHaveBeenCalled();
  });

  it('folds to one line naming them where several wait, and shows them again', async () => {
    render(<WaitStrip session={parked} onResolve={vi.fn()} answerBelow goAheads={GO_AHEADS} onGoAhead={vi.fn()} onTop={vi.fn()} />);
    expect(strip()).toHaveAccessibleName('2 waiting on you');

    await userEvent.click(within(strip()).getByRole('button', { name: 'Fold' }));
    expect(within(strip()).queryByRole('button', { name: 'Approve' })).toBeNull();
    expect(strip()).toHaveTextContent('its question · go-ahead #2');

    await userEvent.click(within(strip()).getByRole('button', { name: 'Show' }));
    expect(within(strip()).getByRole('button', { name: 'Approve' })).toBeInTheDocument();
  });

  it('takes the person to the head\'s waits at the top', async () => {
    const onTop = vi.fn();
    render(<WaitStrip session={parked} onResolve={vi.fn()} onTop={onTop} />);
    expect(strip()).toHaveTextContent('It asked you a question: read it and answer it at the top.');

    await userEvent.click(within(strip()).getByRole('button', { name: 'To the top' }));
    expect(onTop).toHaveBeenCalledTimes(1);
  });

  // WAITFOOT1c (D168 point 4): each line leads to the event in the conversation that raised it, where one was found, and the
  // raising block's *To the wait* leads back to its line.
  it('leads each wait to the event in the conversation that raised it, where one was found', async () => {
    const toEvent = { question: vi.fn(), work: vi.fn(), goAheads: { 2: vi.fn() } };
    render(
      <WaitStrip
        session={parked} onResolve={vi.fn()} answerBelow goAheads={GO_AHEADS} onGoAhead={vi.fn()}
        lands={LANDS} landing={PLAN} onLand={vi.fn()} toEvent={toEvent} onTop={vi.fn()}
      />,
    );

    await userEvent.click(within(screen.getByText(/It asked you a question/).closest('p')!).getByRole('button', { name: 'In the conversation' }));
    expect(toEvent.question).toHaveBeenCalledTimes(1);
    await userEvent.click(within(within(strip()).getByRole('listitem')).getByRole('button', { name: 'In the conversation' }));
    expect(toEvent.goAheads[2]).toHaveBeenCalledTimes(1);
    await userEvent.click(within(within(strip()).getByRole('button', { name: 'Accept…' }).parentElement!).getByRole('button', { name: 'In the conversation' }));
    expect(toEvent.work).toHaveBeenCalledTimes(1);
  });

  it('has no such press where no raising event was found', () => {
    render(<WaitStrip session={parked} onResolve={vi.fn()} answerBelow goAheads={GO_AHEADS} onGoAhead={vi.fn()} onTop={vi.fn()} />);
    expect(within(strip()).queryByRole('button', { name: 'In the conversation' })).toBeNull();
  });

  it('shows the wait a raising block leads to: unfolded, focused and outlined', async () => {
    const props = { session: parked, onResolve: vi.fn(), answerBelow: true, goAheads: GO_AHEADS, onGoAhead: vi.fn(), onTop: vi.fn() };
    const { rerender } = render(<WaitStrip {...props} />);
    await userEvent.click(within(strip()).getByRole('button', { name: 'Fold' }));
    expect(within(strip()).queryByRole('listitem')).toBeNull();

    rerender(<WaitStrip {...props} seek={{ wait: 'goAhead:2', n: 1 }} />);
    const item = within(strip()).getByRole('listitem');
    expect(item).toHaveFocus();
    expect(item.className).toContain('outline-accent');

    rerender(<WaitStrip {...props} seek={{ wait: 'question', n: 2 }} />);
    const line = screen.getByText(/It asked you a question/).closest('p')!;
    expect(line).toHaveFocus();
    expect(line.className).toContain('outline-accent');
    expect(item.className).not.toContain('outline-accent');
  });

  it('shows the same wait again when it is led to again, though its line was folded since', async () => {
    const props = { session: parked, onResolve: vi.fn(), answerBelow: true, goAheads: GO_AHEADS, onGoAhead: vi.fn(), onTop: vi.fn() };
    const { rerender } = render(<WaitStrip {...props} seek={{ wait: 'goAhead:2', n: 1 }} />);
    expect(within(strip()).getByRole('listitem')).toHaveFocus();

    await userEvent.click(within(strip()).getByRole('button', { name: 'Fold' }));
    rerender(<WaitStrip {...props} seek={{ wait: 'goAhead:2', n: 2 }} />);
    expect(within(strip()).getByRole('listitem')).toHaveFocus();
  });

  it('speaks the active catalogue', async () => {
    await i18n.changeLanguage('zh');
    try {
      render(<WaitStrip session={parked} onResolve={vi.fn()} answerBelow onTop={vi.fn()} />);
      expect(screen.getByRole('region', { name: '等你处理' })).toHaveTextContent('在下面的框里回答它');
      expect(screen.getByRole('button', { name: '到顶部' })).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
