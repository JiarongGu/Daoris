import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { GoAhead, Session } from '../../data/api';
import type { RuleProposal } from '../../settings/repository/AgentRules';
import type { HelperAskRow, HelperWaits } from './HelperAsks';
import type { PermissionWait } from './PermissionAsks';
import { STEP_SHOWN } from '../gates/reviewFixtures';
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

  // WAITFOOT2c (D168): the strip sits directly above the box that takes the park's answer, and the box is the door, so no
  // line says "answer it in the box below" over it; a park with nothing else waiting draws nothing.
  it('draws no line for a park\'s question where its box is right below', () => {
    const { container } = render(<WaitStrip session={parked} onResolve={vi.fn()} answerBelow onTop={vi.fn()} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('names a park\'s question at the top where no box below takes it', () => {
    render(<WaitStrip session={parked} onResolve={vi.fn()} onTop={vi.fn()} />);
    expect(strip()).toHaveTextContent(/It asked you a question/);
    expect(within(strip()).queryByRole('textbox')).toBeNull();
  });

  it('leaves the park\'s question line out while the go-aheads beside it stay', () => {
    render(<WaitStrip session={parked} onResolve={vi.fn()} answerBelow goAheads={GO_AHEADS} onGoAhead={vi.fn()} onTop={vi.fn()} />);
    expect(strip()).not.toHaveTextContent('answer it in the box below');
    expect(strip()).toHaveTextContent('dashboard configuration');
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
        session={parked} onResolve={vi.fn()} goAheads={GO_AHEADS} onGoAhead={vi.fn()}
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
    const props = { session: parked, onResolve: vi.fn(), goAheads: GO_AHEADS, onGoAhead: vi.fn(), onTop: vi.fn() };
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

  // PROPOSEHERE1b (D168 point 2, D74): a rule the session proposed is a wait, answered through the head's own door.
  const RULE: RuleProposal = {
    id: 'r1', state: 'waiting', action: 'add', scope: 'repository', name: 'engine', list: 'allow', rule: 'Bash(git branch -D:*)',
    why: 'Auto mode refused the branch delete you asked for.', session: 's1a2b3c4', proposed: '2026-09-21T11:00:00Z',
  };

  it('answers a rule the session proposed through the head\'s own door, and shows none of another session\'s', async () => {
    const onAnswer = vi.fn((_proposal: RuleProposal, _accept: boolean, told: { done: () => void }) => told.done());
    const onOpen = vi.fn();
    const rules = { proposals: [RULE, { ...RULE, id: 'r2', session: 'o7h3r000' }], onAnswer };
    const { rerender } = render(<WaitStrip session={session()} rules={rules} ruleAsk={{ open: null, onOpen }} onTop={vi.fn()} />);
    expect(strip()).toHaveAccessibleName('Waiting on you');
    const items = within(strip()).getAllByRole('listitem');
    expect(items).toHaveLength(1);
    expect(items[0]).toHaveTextContent('allow Bash(git branch -D:*) for repository engine');

    await userEvent.click(within(items[0]!).getByRole('button', { name: 'Decline' }));
    expect(onAnswer).toHaveBeenCalledWith(RULE, false, { done: expect.any(Function), refused: expect.any(Function) });
    // *Allow…* is open in one place at a time: the frame's (D168 point 3).
    await userEvent.click(within(items[0]!).getByRole('button', { name: 'Allow…' }));
    expect(onOpen).toHaveBeenCalledWith('r1');
    rerender(<WaitStrip session={session()} rules={rules} ruleAsk={{ open: 'r1', onOpen }} onTop={vi.fn()} />);
    expect(within(strip()).getByRole('group', { name: 'Allow…' })).toBeInTheDocument();
  });

  it('folds naming a rule it proposed, and shows the one a raising block leads to', async () => {
    const props = { session: parked, onResolve: vi.fn(), answerBelow: true, rules: { proposals: [RULE], onAnswer: vi.fn() }, onTop: vi.fn() };
    const { rerender } = render(<WaitStrip {...props} toEvent={{ rules: { r1: vi.fn() } }} />);
    await userEvent.click(within(strip()).getByRole('button', { name: 'Fold' }));
    expect(strip()).toHaveTextContent('its question · a rule it proposed');

    rerender(<WaitStrip {...props} toEvent={{ rules: { r1: vi.fn() } }} seek={{ wait: 'rule:r1', n: 1 }} />);
    const item = within(strip()).getByRole('listitem');
    expect(item).toHaveFocus();
    expect(item.className).toContain('outline-accent');
  });

  it('has no rule\'s line where nothing can answer it', () => {
    const { container } = render(<WaitStrip session={session()} rules={{ proposals: [RULE] }} onTop={vi.fn()} />);
    expect(container).toBeEmptyDOMElement();
  });

  // MIXAGENT2c (D168 points 1 to 3, D163 point 3): an agent's ask for a helper that waits on the person's word, with the head's own
  // yes and no, and none of the asks that wait on nothing of theirs.
  const ASK: HelperAskRow = {
    id: 'a1', question: 'Which test covers the loader?', agent: 'codex-acp', askedBy: 'agent', at: '2026-09-21T11:00:00Z',
    state: 'waiting', code: null, helper: null, startedOn: null,
  };
  const helpers = (over: Partial<HelperWaits> = {}): HelperWaits => ({
    asks: [ASK, { ...ASK, id: 'a2', question: 'Waits on a slot.', code: 'cap' }, { ...ASK, id: 'a3', question: 'Mine.', askedBy: 'person', state: null }],
    nameOf: (agent) => (agent === 'codex-acp' ? 'Codex' : agent),
    onApprove: vi.fn(),
    onDecline: vi.fn(),
    ...over,
  });

  it('answers an agent’s ask for a helper through the head’s own yes and no, and shows none that waits on nothing of the person’s', async () => {
    const given = helpers();
    render(<WaitStrip session={session({ state: 'working' })} helpers={given} onTop={vi.fn()} />);

    expect(strip()).toHaveAccessibleName('Waiting on you');
    expect(within(strip()).getByText('It asked for another agent’s help. Nothing is asked until you let it.')).toBeInTheDocument();
    const items = within(strip()).getAllByRole('listitem');
    expect(items).toHaveLength(1);
    expect(items[0]).toHaveTextContent('Which test covers the loader?');

    await userEvent.click(within(items[0]!).getByRole('button', { name: 'Let it ask' }));
    expect(given.onApprove).toHaveBeenCalledWith('a1', { done: expect.any(Function), refused: expect.any(Function) });
    const [, told] = vi.mocked(given.onApprove!).mock.calls[0]!;
    told.done();
    await userEvent.click(within(items[0]!).getByRole('button', { name: 'Not now' }));
    expect(given.onDecline).toHaveBeenCalledWith('a1', { done: expect.any(Function), refused: expect.any(Function) });
  });

  it('folds naming a helper it asked for, and has none where nothing can answer it', async () => {
    render(<WaitStrip session={parked} onResolve={vi.fn()} answerBelow goAheads={GO_AHEADS} onGoAhead={vi.fn()} helpers={helpers()} onTop={vi.fn()} />);
    await userEvent.click(within(strip()).getByRole('button', { name: 'Fold' }));
    expect(strip()).toHaveTextContent('its question · go-ahead #2 · a helper it asked for');

    const { container } = render(<WaitStrip session={session({ state: 'working' })} helpers={helpers({ onApprove: undefined })} onTop={vi.fn()} />);
    expect(container).toBeEmptyDOMElement();
  });

  // AUTONOMY1b3c (D184 point 3): a tool call the session holds for the person's answer under asking, with the head's own press per
  // choice the agent offered, and none another session holds.
  const HELD: PermissionWait = {
    session: 's1a2b3c4', request: '900', call: 'tc-1', title: 'npm test', kind: 'execute', since: '2026-09-21T11:00:00Z', until: '2026-09-21T11:15:00Z',
    choices: [{ id: 'allow', name: 'Allow', kind: 'allow_once' }, { id: 'reject', name: 'Reject', kind: 'reject_once' }],
  };

  it('answers a tool call it holds for the person with the option they pressed, and shows none of another session', async () => {
    const onAnswer = vi.fn();
    render(
      <WaitStrip
        session={session({ state: 'working' })}
        permissions={{ waits: [HELD, { ...HELD, session: 'o7h3r000', request: '1', title: 'rm -rf build' }], onAnswer }}
        onTop={vi.fn()}
      />,
    );

    expect(within(strip()).getByText(/Its rules do not allow these by themselves/)).toBeInTheDocument();
    const items = within(strip()).getAllByRole('listitem');
    expect(items).toHaveLength(1);
    expect(items[0]).toHaveTextContent('npm test');
    await userEvent.click(within(items[0]!).getByRole('button', { name: 'Allow once' }));
    expect(onAnswer).toHaveBeenCalledWith(HELD, 'allow', { done: expect.any(Function), refused: expect.any(Function) });
  });

  it('folds naming a tool call to answer, and has none where nothing can answer it', async () => {
    render(<WaitStrip session={session({ state: 'working' })} permissions={{ waits: [HELD], onAnswer: vi.fn() }} helpers={helpers()} onTop={vi.fn()} />);
    await userEvent.click(within(strip()).getByRole('button', { name: 'Fold' }));
    expect(strip()).toHaveTextContent('a tool call to answer · a helper it asked for');

    const { container } = render(<WaitStrip session={session({ state: 'working' })} permissions={{ waits: [HELD] }} onTop={vi.fn()} />);
    expect(container).toBeEmptyDOMElement();
  });

  /**
   * WAITFOOT2a (D168's WAITFOOT2 note): the owner found each wait drawn two and three times at once, so while the head's waits
   * are on screen the strip stands aside; never from under an ask open in it, nor from under the person's focus.
   */
  it('stands aside while the head\'s waits are on screen', () => {
    const props = { session: parked, onResolve: vi.fn(), answerBelow: true, goAheads: GO_AHEADS, onGoAhead: vi.fn(), onTop: vi.fn() };
    const { rerender } = render(<WaitStrip {...props} aside />);
    expect(screen.queryByRole('region', { name: /waiting on you/i })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull();

    rerender(<WaitStrip {...props} aside={false} />);
    expect(within(strip()).getByRole('button', { name: 'Approve' })).toBeInTheDocument();
  });

  it('stays while its own ask is open, though the head\'s waits came on screen', () => {
    const props = { session: session(), lands: LANDS, landing: PLAN, onLand: vi.fn(), onTop: vi.fn() };
    const { rerender } = render(<WaitStrip {...props} workAsk={{ open: true, onOpen: vi.fn() }} aside />);
    const ask = within(strip()).getByRole('group', { name: 'accept daoris/s-1a2b3c4' });
    expect(ask).toBeInTheDocument();

    // Put down with the focus in it, which says no blur as it goes: the strip reads the focus again.
    within(ask).getAllByRole('button')[0]!.focus();
    expect(ask).toContainElement(document.activeElement as HTMLElement);
    rerender(<WaitStrip {...props} workAsk={{ open: false, onOpen: vi.fn() }} aside />);
    expect(screen.queryByRole('region', { name: /waiting on you/i })).toBeNull();

    rerender(<WaitStrip {...props} rules={{ proposals: [RULE], onAnswer: vi.fn() }} ruleAsk={{ open: 'r1', onOpen: vi.fn() }} aside />);
    expect(within(strip()).getByRole('group', { name: 'Allow…' })).toBeInTheDocument();
  });

  it('stays while the person is in it, and stands aside once they leave', async () => {
    const props = { session: parked, onResolve: vi.fn(), answerBelow: true, goAheads: GO_AHEADS, onGoAhead: vi.fn(), onTop: vi.fn() };
    const { rerender } = render(<><button type="button">elsewhere</button><WaitStrip {...props} seek={{ wait: 'goAhead:2', n: 1 }} /></>);
    expect(within(strip()).getByRole('listitem')).toHaveFocus();

    rerender(<><button type="button">elsewhere</button><WaitStrip {...props} seek={{ wait: 'goAhead:2', n: 1 }} aside /></>);
    expect(within(strip()).getByRole('listitem')).toHaveFocus();

    await userEvent.click(screen.getByRole('button', { name: 'elsewhere' }));
    expect(screen.queryByRole('region', { name: /waiting on you/i })).toBeNull();
  });

  it('speaks the active catalogue', async () => {
    await i18n.changeLanguage('zh');
    try {
      render(<WaitStrip session={parked} onResolve={vi.fn()} onTop={vi.fn()} />);
      expect(screen.getByRole('region', { name: '等你处理' })).toHaveTextContent('到顶部阅读并回答它');
      expect(screen.getByRole('button', { name: '到顶部' })).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
