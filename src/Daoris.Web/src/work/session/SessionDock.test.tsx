import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { GoAhead, Session } from '../../data/api';
import type { RuleProposal } from '../../settings/repository/AgentRules';
import { said } from '../../test/said';
import { JOINT_HOLDING, JOINT_RETURNED, WAITING_PART } from '../../workflow/parts/partFixtures';
import type { WorkflowRun } from '../../workflow/run';
import { bellCount, needsListed, placeCounts } from '../attention/attention';
import type { PushedAttention, StepWaitRow } from '../attention/attentionPushed';
import { SessionDock } from './SessionDock';

// Every wait the person settles is a card in the dock, oldest first under its count, answered by its own presses; what its run
// waits on stands below them, uncounted; a teammate's waits are one line with no press.

afterEach(async () => { await i18n.changeLanguage('en'); });

const AT = '2026-09-21T11:00:00Z';
const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: '7a82cc', repository: 'engine', door: 'claude-code', state: 'awaiting-person', kind: 'driven',
  created: '2026-09-21T09:46:00Z', updated: '2026-09-21T11:56:00Z', ...over,
});
const cardsOf = (container: HTMLElement) => [...container.querySelectorAll<HTMLElement>('[data-region="dock"] > article')];
const order = (container: HTMLElement) => cardsOf(container).map((card) => card.dataset.wait);
const card = (container: HTMLElement, id: string) => cardsOf(container).find((each) => each.dataset.wait === id)!;
const answered = { done: expect.any(Function), refused: expect.any(Function) };

const ASKED: GoAhead[] = [
  {
    number: 1, kind: 'write', on: 'production', act: 'dashboard configuration', state: 'asked',
    asked: [{ session: 's1a2b3c4', quest: '7a82cc', at: AT, why: 'The tile reads its target from it.' }],
  },
  {
    number: 2, kind: 'release', on: 'production', act: 'comparison report', state: 'asked',
    asked: [{ session: 's1a2b3c4', quest: '7a82cc', at: '2026-09-21T11:10:00Z', why: 'Ship it.' }],
  },
];
const RULE: RuleProposal = {
  id: 'r1', state: 'waiting', action: 'add', scope: 'repository', name: 'engine', list: 'allow', rule: 'Bash(git branch -D:*)',
  why: 'Auto mode refused the branch delete you asked for.', session: 's1a2b3c4', proposed: AT,
};
const HELD = {
  session: 's1a2b3c4', request: '900', call: 'tc-1', title: 'npm test', kind: 'execute', since: '2026-09-21T11:58:00Z', until: '2026-09-21T12:13:00Z',
  choices: [{ id: 'allow', name: 'Allow', kind: 'allow_once' }, { id: 'reject', name: 'Reject', kind: 'reject_once' }],
};
const SIGN: StepWaitRow = { run: 'q7', node: 'sign', why: 'approval', role: 'approver', holder: 'person', presses: ['approve', 'reject', 'skip'] };

describe('SessionDock, a park', () => {
  it('starts each parked session\'s decline empty: a reason written for one never carries to another', () => {
    const { rerender } = render(<SessionDock session={session({ note: 'needs a person' })} onResolve={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: 'Decline…' }));
    fireEvent.change(screen.getByLabelText(/the reason/), { target: { value: 'the chunk API is being replaced' } });

    rerender(<SessionDock session={session({ id: 's9f8e7d6', note: 'needs a person' })} onResolve={vi.fn()} />);
    expect(screen.queryByLabelText(/the reason/)).toBeNull();
    expect(screen.getByRole('button', { name: 'Decline…' })).toBeInTheDocument();
  });

  it('gives a parked intake its ask as the answer, not a parked session\'s moves', () => {
    const onAnswerAsk = vi.fn();
    render(
      <SessionDock
        session={session({
          id: 'i9n8t7k6', quest: null, kind: 'chat', repository: 'ask #0fda18', ask: '0fda18', tree: 'C:/somewhere/data/intake/default',
          note: 'published nothing: it asks you rather than guess.',
          noteParts: [{ words: 'published nothing: it asks you rather than guess.', by: 'before' }],
        })}
        onResolve={() => {}}
        onAnswerAsk={onAnswerAsk}
      />,
    );

    expect(screen.getByText('Its intake asked you; the answer is on its ask.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Finish' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Decline…' })).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Answer ask #0fda18' }));
    expect(onAnswerAsk).toHaveBeenCalledWith('0fda18');
  });

  it('words a park\'s lead-in in the reader\'s language and keeps the agent\'s question beneath it as written', async () => {
    await i18n.changeLanguage('zh');
    const { container } = render(
      <SessionDock
        session={session({
          note: 'It stopped with its quest still taken, to ask you:\nWhich branch should the release land on?',
          noteParts: [
            { code: 'ended.parked-asked', values: {}, text: 'It stopped with its quest still taken, to ask you:' },
            { words: 'Which branch should the release land on?\n\n1. main\n2. release/1.0', by: 'agent' },
          ],
        })}
        onResolve={vi.fn()}
        onAnswerSession={vi.fn()}
      />,
    );

    const park = card(container, 'question');
    expect(within(park).getByText('它停了下来，委托仍已接下，想问你：')).toBeInTheDocument();
    expect(within(park).queryByText(/with its quest still taken/)).toBeNull();
    const question = within(park).getByText(/Which branch should the release land on\?/);
    expect(question.closest('blockquote')!.textContent).toBe('Which branch should the release land on?\n\n1. main\n2. release/1.0');
  });
});

describe('SessionDock, the go-aheads a park asked', () => {
  const PARK: Partial<Session> = { note: 'I need the two go-aheads to finish.' };

  it('draws each as its own card after the park\'s, with Approve and Refuse, the park keeping Finish', () => {
    const { container } = render(<SessionDock session={session(PARK)} onResolve={vi.fn()} goAheads={ASKED} onGoAhead={vi.fn()} />);

    expect(order(container)).toEqual(['question', 'goAhead:1', 'goAhead:2']);
    expect(card(container, 'goAhead:1')).toHaveAccessibleName('write on production · dashboard configuration');
    expect(card(container, 'goAhead:2')).toHaveAccessibleName('release on production · comparison report');
    for (const id of ['goAhead:1', 'goAhead:2']) {
      expect(within(card(container, id)).getByRole('button', { name: 'Approve' })).toBeInTheDocument();
      expect(within(card(container, id)).getByRole('button', { name: 'Refuse' })).toBeInTheDocument();
    }
    expect(within(card(container, 'question')).getByRole('button', { name: 'Finish' })).toBeInTheDocument();
  });

  it('says which go-ahead, and the yes or the no', () => {
    const onGoAhead = vi.fn();
    const { container } = render(<SessionDock session={session(PARK)} goAheads={ASKED} onGoAhead={onGoAhead} />);

    fireEvent.click(within(card(container, 'goAhead:2')).getByRole('button', { name: 'Approve' }));
    fireEvent.click(within(card(container, 'goAhead:1')).getByRole('button', { name: 'Refuse' }));
    expect(onGoAhead.mock.calls).toEqual([[2, true, undefined, answered], [1, false, undefined, answered]]);
  });

  it('keeps the one still waiting answerable beneath an answered park', () => {
    const some: GoAhead[] = [{ ...ASKED[0]!, state: 'approved', answer: { approved: true, at: '2026-09-21T11:58:00Z' } }, ASKED[1]!];
    const { container } = render(<SessionDock session={session({ ...PARK, said: said('carry on.') })} goAheads={some} onGoAhead={vi.fn()} />);

    expect(order(container)).toEqual(['goAhead:2']);
    expect(screen.getAllByRole('button', { name: 'Approve' })).toHaveLength(1);
  });

  it('draws nothing where nothing can answer them', () => {
    const { container } = render(<SessionDock session={session(PARK)} goAheads={ASKED} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('draws no go-ahead for a park that asked none, or a session no longer parked', () => {
    const { container, rerender } = render(<SessionDock session={session(PARK)} goAheads={[]} onGoAhead={vi.fn()} />);
    expect(order(container)).toEqual([]);

    rerender(<SessionDock session={session({ state: 'working' })} goAheads={ASKED} onGoAhead={vi.fn()} />);
    expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull();
  });

  it('shows the question its agent asked in place of its last message where the box below answers the park', () => {
    const question = {
      id: 'q5t10n00', question: 'Which target should the tile show?', recommend: 1, why: null, by: null, at: '2026-09-21T11:20:00Z',
      choices: [{ label: '1,200', does: 'Write 1,200.' }, { label: '1,350', does: 'Write 1,350.' }],
    };
    render(<SessionDock session={session(PARK)} onResolve={vi.fn()} parkBox question={question} />);

    expect(screen.getByText('Which target should the tile show?')).toBeInTheDocument();
    expect(screen.getByText('If you choose this: Write 1,350.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '1,200' })).toBeNull();
    expect(screen.queryByRole('radio')).toBeNull();
    expect(screen.queryByText('I need the two go-aheads to finish.')).toBeNull();
    expect(screen.getByRole('button', { name: 'Finish' })).toBeInTheDocument();
  });

  it('says its presses in the active catalog', async () => {
    await i18n.changeLanguage('zh');
    render(<SessionDock session={session(PARK)} goAheads={ASKED} onGoAhead={vi.fn()} />);
    expect(screen.getAllByRole('button', { name: '放行' })).toHaveLength(2);
  });
});

describe('SessionDock, the way from each card to the conversation', () => {
  const LANDS = { branch: 'daoris/s-1a2b3c4', tree: 's-1a2b3c4', commits: 2, uncommitted: 0 };
  const PLAN = { session: 's1a2b3c4', form: 'merge', target: 'main' };
  const props = {
    session: session({ note: 'Which of the two should I keep?' }), onResolve: vi.fn(), goAheads: ASKED, onGoAhead: vi.fn(),
    lands: LANDS, landing: PLAN, onReviewRun: vi.fn(),
  };

  it('leads the park\'s card, each go-ahead and the work to the event that raised each, once', async () => {
    const toEvent = { question: vi.fn(), work: vi.fn(), goAheads: { 2: vi.fn() } };
    const { container } = render(<SessionDock {...props} toEvent={toEvent} />);
    const leads = (id: string) => within(card(container, id)).queryAllByRole('button', { name: 'In the conversation' });

    expect(leads('goAhead:1')).toEqual([]);
    expect([leads('question').length, leads('goAhead:2').length, leads('work').length]).toEqual([1, 1, 1]);
    await userEvent.click(leads('question')[0]!);
    await userEvent.click(leads('goAhead:2')[0]!);
    await userEvent.click(leads('work')[0]!);
    expect([toEvent.question, toEvent.goAheads[2], toEvent.work].map((each) => each.mock.calls.length)).toEqual([1, 1, 1]);
  });

  it('has no such press where nothing was found', () => {
    render(<SessionDock {...props} />);
    expect(screen.queryByRole('button', { name: 'In the conversation' })).toBeNull();
  });

  it('gives the focus to the card a raising block leads to', () => {
    const { container, rerender } = render(<SessionDock {...props} />);
    expect(order(container)).toEqual(['question', 'goAhead:1', 'goAhead:2', 'work']);
    expect(card(container, 'work')).toContainElement(screen.getByRole('button', { name: 'Open outputs' }));

    rerender(<SessionDock {...props} seek={{ wait: 'work', n: 1 }} />);
    expect(card(container, 'work')).toHaveFocus();
    rerender(<SessionDock {...props} seek={{ wait: 'question', n: 2 }} />);
    expect(card(container, 'question')).toHaveFocus();
  });
});

describe('SessionDock, the rules a session proposed', () => {
  it('draws its own open ones, answers Decline through the rules\' door, and leads each to its call', async () => {
    const onAnswer = vi.fn();
    const toEvent = { rules: { r1: vi.fn() } };
    const { container } = render(
      <SessionDock
        session={session({ state: 'completed' })} toEvent={toEvent}
        rules={{ proposals: [RULE, { ...RULE, id: 'r2', session: 'o7h3r000' }, { ...RULE, id: 'r3', state: 'applied' }], onAnswer }}
      />,
    );

    expect(order(container)).toEqual(['rule:r1']);
    const items = within(card(container, 'rule:r1')).getAllByRole('listitem');
    expect(items).toHaveLength(1);
    expect(items[0]).toHaveTextContent('allow Bash(git branch -D:*) for repository engine');
    await userEvent.click(within(items[0]!).getByRole('button', { name: 'Decline' }));
    expect(onAnswer).toHaveBeenCalledWith(RULE, false, answered);
    await userEvent.click(within(card(container, 'rule:r1')).getByRole('button', { name: 'In the conversation' }));
    expect(toEvent.rules.r1).toHaveBeenCalledTimes(1);
  });

  it('draws none where it proposed none still open', () => {
    const { container } = render(<SessionDock session={session({ state: 'working' })} rules={{ proposals: [{ ...RULE, state: 'declined' }], onAnswer: vi.fn() }} />);
    expect(container).toBeEmptyDOMElement();
  });
});

describe('SessionDock, the tool calls a session holds for the person', () => {
  it('draws its own with a press per choice, answered through the held calls\' door', async () => {
    const onAnswer = vi.fn();
    const { container } = render(
      <SessionDock session={session({ state: 'working' })} permissions={{ waits: [HELD, { ...HELD, session: 'o7h3r000', request: '1' }], onAnswer }} />,
    );

    expect(order(container)).toEqual(['permission:900']);
    const items = within(card(container, 'permission:900')).getAllByRole('listitem');
    expect(items).toHaveLength(1);
    await userEvent.click(within(items[0]!).getByRole('button', { name: 'Refuse' }));
    expect(onAnswer).toHaveBeenCalledWith(HELD, 'reject', answered);
  });

  it('draws none where it holds none, and none where nothing can answer one', () => {
    const { container, unmount } = render(<SessionDock session={session({ state: 'working' })} permissions={{ waits: [{ ...HELD, session: 'o7h3r000' }], onAnswer: vi.fn() }} />);
    expect(container).toBeEmptyDOMElement();
    unmount();
    expect(render(<SessionDock session={session({ state: 'working' })} permissions={{ waits: [HELD] }} />).container).toBeEmptyDOMElement();
  });
});

describe('SessionDock, every wait in one place', () => {
  it('holds every wait as its card with its own press, the run\'s step and the park\'s Finish among them, oldest first under a count', () => {
    const { container } = render(
      <SessionDock
        session={session({ quest: 'q7', note: 'Which table holds the totals?' })}
        onResolve={vi.fn()}
        permissions={{ waits: [HELD], onAnswer: vi.fn(), busy: false }}
        runWaits={{ waits: [{ run: 'q7', node: 'sign', why: 'approval', role: 'approver', since: '2026-09-21T11:05:00Z' }], steps: [SIGN], onWord: vi.fn() }}
      />,
    );

    expect(screen.getByText('Waiting on you 3')).toBeInTheDocument();
    const step = within(card(container, 'step:q7/sign')).getByRole('group', { name: 'sign: waiting on approver: you' });
    expect(within(step).getByRole('button', { name: 'Approve…' })).toBeInTheDocument();
    expect(within(card(container, 'question')).getByRole('button', { name: /^Finish/ })).toBeInTheDocument();
    expect(within(card(container, 'permission:900')).getByRole('button', { name: 'Allow once' })).toBeInTheDocument();
    expect(order(container)).toEqual(['step:q7/sign', 'question', 'permission:900']);
  });

  it('draws a teammate\'s waits as a line with no press', () => {
    const { container } = render(<SessionDock session={session({ id: 'teammate-machine/s1a2b3c4', note: 'Which table holds the totals?' })} />);
    expect(container).toHaveTextContent('Waits on its owner: its question');
    expect(within(container).queryAllByRole('button')).toEqual([]);
    expect(order(container)).toEqual([]);
  });

  it('counts the park and a rule it proposed as two, as the bell and What needs you count them, in Chinese too', async () => {
    const pushed = (kind: PushedAttention['kind'], id: string): PushedAttention => ({ kind, id, since: AT, session: 's1a2b3c4' });
    const rows = [pushed('parked', 's1a2b3c4'), pushed('rule', 'r1')];
    const places = Object.values(placeCounts(rows)).reduce((sum, each) => sum + each, 0);
    expect({ listed: needsListed(rows).length, bell: bellCount([], rows), places }).toEqual({ listed: 2, bell: 2, places: 2 });
    const dock = <SessionDock session={session({ note: 'Which table?' })} onResolve={vi.fn()} rules={{ proposals: [RULE], onAnswer: vi.fn() }} />;
    const { unmount } = render(dock);
    expect(screen.getByText('Waiting on you 2')).toBeInTheDocument();
    unmount();

    await i18n.changeLanguage('zh');
    render(dock);
    expect(screen.getByText(`等你处理 ${bellCount([], rows)}`)).toBeInTheDocument();
  });
});

describe('SessionDock, what its run\'s parts wait on', () => {
  const stepping = (over: Partial<Session> = {}) => session({ quest: 'q9', repository: 'settings', state: 'awaiting-step', ...over });
  const parts = (runs: readonly WorkflowRun[], onQuest = vi.fn()) => ({ waits: [], steps: [], parts: { runs, onQuest } });

  it('says what the session\'s part waits on, uncounted, with the other part\'s quest', () => {
    const onQuest = vi.fn();
    const { container } = render(<SessionDock session={stepping()} runWaits={parts([WAITING_PART], onQuest)} />);

    expect(screen.queryByText(/^Waiting on you/)).toBeNull();
    expect(container).toHaveTextContent(/waits on app’s stage-ended/);
    fireEvent.click(screen.getByRole('button', { name: 'Open quest #q7' }));
    expect(onQuest).toHaveBeenCalledWith('q7');
  });

  it('draws a joint check\'s parts held and waited for, and the part its failure went back to', () => {
    const { unmount } = render(<SessionDock session={stepping({ quest: 'q7', repository: 'engine' })} runWaits={parts([JOINT_HOLDING])} />);
    const holding = screen.getByRole('list', { name: 'Tested together' });
    expect(holding).toHaveTextContent(/holds app’s stage-ended/);
    expect(holding).toHaveTextContent(/waits for config’s config-written/);
    unmount();

    render(<SessionDock session={stepping({ quest: 'q7', repository: 'engine' })} runWaits={parts([JOINT_RETURNED])} />);
    expect(screen.getByRole('list', { name: 'Tested together' })).toHaveTextContent(/failed on config, sent back to it/);
  });

  it('counts only the person\'s waits beside them', () => {
    const { container } = render(
      <SessionDock session={stepping({ state: 'awaiting-person', note: 'Which stage?' })} onResolve={vi.fn()} runWaits={parts([WAITING_PART])} />,
    );
    expect(screen.getByText('Waiting on you 1')).toBeInTheDocument();
    expect(container).toHaveTextContent(/waits on app’s stage-ended/);
  });

  it('draws nothing where its run waits on no part', () => {
    const { container } = render(<SessionDock session={stepping()} runWaits={parts([{ ...WAITING_PART, graph: { ...WAITING_PART.graph!, partWaits: null } }])} />);
    expect(container).toBeEmptyDOMElement();
  });
});
