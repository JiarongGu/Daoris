import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { Session } from '../../data/api';
import { DETAIL, GATES } from '../gates/opinionFixtures';
import { STEP_REVIEWED, STEP_SHOWN } from '../gates/reviewFixtures';
import type { Answered } from '../InlineConfirm';
import { SessionDock } from './SessionDock';

// The work's card: what a session left to land with its one door to the review, a line holding it by content with its discard,
// and the review's gate standing where that door would.

afterEach(async () => { await i18n.changeLanguage('en'); });

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: '7a82cc', repository: 'engine', door: 'claude-code', state: 'completed', kind: 'driven',
  created: '2026-09-21T09:46:00Z', updated: '2026-09-21T11:56:00Z', ...over,
});

describe('SessionDock, the landing a session left', () => {
  const LANDS = { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', commits: 1, uncommitted: 0 };
  const PLAN = { session: 's1a2b3c4', form: 'branch', target: 'feature/delta-release', plugin: 'azure-devops-pull-request' };

  it('keeps the plain review where the review says nothing is beyond the line', () => {
    const onReview = vi.fn();
    const onReviewRun = vi.fn();
    render(<SessionDock session={session({ state: 'failed' })} lands={LANDS} landing={PLAN} onReviewRun={onReviewRun} onReview={onReview} behind={{ line: 'master', commits: 2, ahead: 0 }} />);

    fireEvent.click(screen.getByRole('button', { name: 'Open outputs' }));
    expect(onReview).toHaveBeenCalledOnce();
    expect(onReviewRun).not.toHaveBeenCalled();
  });

  it('opens the run’s review where the review has commits beyond the line, or uncommitted work', () => {
    const onReviewRun = vi.fn();
    const { rerender } = render(
      <SessionDock session={session({ state: 'failed' })} lands={LANDS} landing={PLAN} onReviewRun={onReviewRun} behind={{ line: 'master', commits: 0, ahead: 1 }} />,
    );
    fireEvent.click(screen.getByRole('button', { name: 'Open outputs' }));
    expect(onReviewRun).toHaveBeenCalledOnce();

    rerender(
      <SessionDock session={session({ state: 'failed' })} lands={{ ...LANDS, uncommitted: 2 }} landing={PLAN} onReviewRun={onReviewRun} behind={{ line: 'master', commits: 0, ahead: 0 }} />,
    );
    fireEvent.click(screen.getByRole('button', { name: 'Open outputs' }));
    expect(onReviewRun).toHaveBeenCalledTimes(2);
  });

  it('says a failed session’s commits to land, naming its branch and tree, with one door to the review and no Accept', () => {
    const onReviewRun = vi.fn();
    render(<SessionDock session={session({ state: 'failed' })} lands={LANDS} landing={PLAN} onReviewRun={onReviewRun} onReview={vi.fn()} />);

    expect(screen.getByText('Its work')).toBeInTheDocument();
    expect(screen.getByText('1 commit no branch of yours holds')).toBeInTheDocument();
    expect(screen.getByText('daoris/s-4e6837ed')).toBeInTheDocument();
    expect(screen.getByText('in its tree s-4e6837ed')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Accept…' })).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Open outputs' }));
    expect(onReviewRun).toHaveBeenCalledOnce();
  });

  it('says the work with no door where nothing can press it', () => {
    render(<SessionDock session={session({ state: 'failed' })} lands={LANDS} />);
    expect(screen.getByText('1 commit no branch of yours holds')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Open outputs' })).toBeNull();
  });

  it('shows a session its Accept carried on as carried on, and says it in Chinese too', async () => {
    render(
      <SessionDock
        session={session()} lands={{ ...LANDS, uncommitted: 2 }} landing={{ ...PLAN, carried: { carries: 1, awaiting: true } }} onReviewRun={vi.fn()}
      />,
    );

    expect(screen.getByText('Accepted: its session was carried on to clear its tree, and its work lands once the tree is clean. This is not finished work yet.'))
      .toBeInTheDocument();
    await act(async () => { await i18n.changeLanguage('zh'); });
    expect(screen.getByText('已采纳：它的会话已被继续，去把它的工作树处理干净，工作树干净后即会落地它的工作。它还不是已完成的工作。')).toBeInTheDocument();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<SessionDock session={session({ state: 'failed' })} lands={LANDS} landing={PLAN} onReviewRun={vi.fn()} onReview={vi.fn()} />);
    expect(screen.getByText('它的工作')).toBeInTheDocument();
    expect(screen.getByText('1 个提交不在你的任何分支上')).toBeInTheDocument();
    expect(screen.getByText('在它的工作树 s-4e6837ed 中')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '打开产出' })).toBeInTheDocument();
  });

  it('draws the second opinion where a level asks one, and opens the run’s review beside a dispute it answers', () => {
    const onReviewRun = vi.fn();
    render(<SessionDock session={session()} lands={LANDS} landing={{ ...PLAN, opinion: GATES.disputed }} opinion={{ detail: DETAIL }} onReviewRun={onReviewRun} />);

    expect(screen.getByRole('region', { name: 'Second opinion' })).toHaveTextContent('1 finding by Codex (OpenAI) is disputed');
    fireEvent.click(screen.getByRole('button', { name: 'Open outputs' }));
    expect(onReviewRun).toHaveBeenCalledOnce();
  });

  it('opens no run’s review while another agent still reads the work, nor while the rule requires one none could be had for', () => {
    const onReviewRun = vi.fn();
    const { rerender } = render(<SessionDock session={session()} lands={LANDS} landing={{ ...PLAN, opinion: GATES.reading }} onReviewRun={onReviewRun} />);
    expect(screen.getByRole('region', { name: 'Second opinion' })).toHaveTextContent('being read');
    expect(screen.queryByRole('button', { name: 'Open outputs' })).toBeNull();

    rerender(<SessionDock session={session()} lands={LANDS} landing={{ ...PLAN, opinion: GATES.unavailableRequired }} onReviewRun={onReviewRun} />);
    expect(screen.queryByRole('button', { name: 'Open outputs' })).toBeNull();

    rerender(<SessionDock session={session()} lands={LANDS} landing={{ ...PLAN, opinion: GATES.unavailable }} onReviewRun={onReviewRun} />);
    expect(screen.getByRole('region', { name: 'Second opinion' })).toHaveTextContent('The rule does not require one, so nothing waits for it.');
    expect(screen.getByRole('button', { name: 'Open outputs' })).toBeInTheDocument();
  });
});

describe('SessionDock, a session whose work the line holds by content', () => {
  const KEPT = 'refs/daoris/discarded/daoris/s-4e6837ed';
  const DISCARDS = {
    branch: 'daoris/s-4e6837ed',
    tree: 's-4e6837ed',
    says: 'Its work is on `main` by content (a squash merge).',
    keeps: KEPT,
    keptAt: `Its commits stay at \`${KEPT}\` until you delete that ref; \`git branch daoris/s-4e6837ed ${KEPT}\` brings the branch back.`,
  };

  it('says where its work is, offers no Accept…, and discards its tree unforced on the second press', () => {
    const onDiscardTree = vi.fn();
    const { container } = render(<SessionDock session={session()} discards={DISCARDS} onDiscardTree={onDiscardTree} onReview={vi.fn()} />);

    expect(container).toHaveTextContent('Its work is on main by content (a squash merge).');
    expect(screen.getByText('daoris/s-4e6837ed')).toBeInTheDocument();
    expect(screen.getByText('in its tree s-4e6837ed')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Accept…' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Open outputs' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Discard branch…' }));
    expect(onDiscardTree).not.toHaveBeenCalled();
    const ask = screen.getByRole('group', { name: 'discard daoris/s-4e6837ed' });
    expect(ask).toHaveTextContent(`Its commits stay at ${KEPT} until you delete that ref; git branch daoris/s-4e6837ed ${KEPT} brings the branch back.`);
    fireEvent.click(within(ask).getByRole('button', { name: 'Discard branch' }));
    expect(onDiscardTree).toHaveBeenCalledOnce();
    act(() => (onDiscardTree.mock.calls[0]![0] as Answered).done());
    expect(screen.queryByRole('group', { name: 'discard daoris/s-4e6837ed' })).toBeNull();
  });

  it('says a refusal inside the ask, and offers no discard where it would not go or nothing can press it', () => {
    const onDiscardTree = vi.fn();
    const { container, rerender } = render(<SessionDock session={session({ state: 'failed' })} discards={DISCARDS} onDiscardTree={onDiscardTree} />);
    fireEvent.click(screen.getByRole('button', { name: 'Discard branch…' }));
    const ask = screen.getByRole('group', { name: 'discard daoris/s-4e6837ed' });
    fireEvent.click(within(ask).getByRole('button', { name: 'Discard branch' }));
    act(() => (onDiscardTree.mock.calls[0]![0] as Answered).refused('the tree at s-4e6837ed has uncommitted work — 1 path(s).'));
    expect(within(ask).getByRole('alert')).toHaveTextContent('the tree at s-4e6837ed has uncommitted work — 1 path(s).');

    rerender(
      <SessionDock
        session={session({ id: 's9f8e7d6', state: 'failed' })}
        discards={{
          ...DISCARDS,
          says: 'Its work is on `main` by content (a squash merge). Its tree stays: it holds 1 ignored path(s) your checkout does not have: local.db, which a discard would destroy.',
          keeps: null,
          keptAt: null,
        }}
        onDiscardTree={onDiscardTree}
      />,
    );
    expect(container).toHaveTextContent('Its tree stays: it holds 1 ignored path(s) your checkout does not have: local.db');
    expect(screen.queryByRole('button', { name: 'Discard branch…' })).toBeNull();

    rerender(<SessionDock session={session()} discards={DISCARDS} />);
    expect(container).toHaveTextContent('Its work is on main by content (a squash merge).');
    expect(screen.queryByRole('button', { name: 'Discard branch…' })).toBeNull();
  });

  it('says it in Chinese, the driver’s sentence as it is', async () => {
    await i18n.changeLanguage('zh');
    const { container } = render(<SessionDock session={session()} discards={DISCARDS} onDiscardTree={vi.fn()} onReview={vi.fn()} />);
    expect(container).toHaveTextContent('Its work is on main by content (a squash merge).');
    expect(screen.getByText('在它的工作树 s-4e6837ed 中')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '采纳…' })).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: '丢弃分支…' }));
    const ask = screen.getByRole('group', { name: '丢弃 daoris/s-4e6837ed' });
    expect(within(ask).getByRole('button', { name: '确认丢弃分支' })).toBeInTheDocument();
  });
});

describe('SessionDock, the review\'s gate on the work', () => {
  const reports = (over: Partial<Session> = {}) => session({ quest: 'q1', repository: 'reports', tree: 'C:/somewhere/reports', ...over });
  const LANDS = { branch: 'daoris/s-1a2b3c4', tree: 's-1a2b3c4', commits: 2, uncommitted: 0 };
  const PLAN = { session: 's1a2b3c4', form: 'merge', target: 'main' };
  const gate = () => screen.getByRole('region', { name: /Review in|Review before/ });

  it.each(['kind-paths', 'cannot-start', 'unread'])('shows the workflow %s hold and offers no door to the review', (state) => {
    render(<SessionDock session={reports()} lands={LANDS} onReviewRun={vi.fn()}
      landing={{ ...PLAN, workflow: { state, holds: true, says: 'Holds: keep this workflow in the terminal.' } }} />);
    expect(screen.queryByRole('button', { name: 'Open outputs' })).toBeNull();
    expect(screen.getByRole('region', { name: 'Workflow before it lands' })).toHaveTextContent('keep this workflow');
  });

  it('withdraws the door to the run’s review when a workflow hold arrives', () => {
    const { rerender } = render(<SessionDock session={reports()} lands={LANDS} onReviewRun={vi.fn()} landing={PLAN} />);
    expect(screen.getByRole('button', { name: 'Open outputs' })).toBeInTheDocument();
    rerender(<SessionDock session={reports()} lands={LANDS} onReviewRun={vi.fn()}
      landing={{ ...PLAN, workflow: { state: 'unread', holds: true, says: 'Binding unread.' } }} />);
    expect(screen.queryByRole('button', { name: 'Open outputs' })).toBeNull();
  });

  it.each(['follows', 'kept'])('offers the door when the workflow %s lets go', (state) => {
    render(<SessionDock session={reports()} lands={LANDS} onReviewRun={vi.fn()}
      landing={{ ...PLAN, workflow: { state, holds: false, says: 'Follows the bound workflow.' } }} />);
    expect(screen.getByRole('button', { name: 'Open outputs' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Workflow before it lands' })).toBeNull();
  });

  it('translates the workflow hold chrome and preserves the driver words', async () => {
    await i18n.changeLanguage('zh');
    render(<SessionDock session={reports()} lands={LANDS} onReviewRun={vi.fn()}
      landing={{ ...PLAN, workflow: { state: 'unread', holds: true, says: 'Binding unread.' } }} />);
    expect(screen.getByRole('region', { name: '落地前的工作流' })).toHaveTextContent('Binding unread.');
    expect(screen.queryByRole('button', { name: /接受/ })).toBeNull();
  });

  it('stands where Accept… would while the gate holds, with the set-up shown and its verdict', async () => {
    const reviewed = vi.fn();
    const user = userEvent.setup();
    render(
      <SessionDock
        session={reports()} lands={LANDS} onReviewRun={vi.fn()}
        landing={{ ...PLAN, review: { state: 'shown', environment: 'local', level: 'set-up-step', quest: 'q2', says: 'Waits for your review in `local`: …' } }}
        review={{ step: STEP_SHOWN, served: true, acts: { reviewed, skip: vi.fn() } }}
      />,
    );

    expect(screen.queryByRole('button', { name: 'Open outputs' })).toBeNull();
    expect(gate()).toHaveTextContent('Review in local');
    expect(gate()).toHaveTextContent('Set-up step #q2 showed it at a1b2c3d4.');
    await user.click(within(gate()).getByRole('button', { name: 'Reviewed' }));
    expect(reviewed).toHaveBeenCalledWith({ machine: 'desk', sequence: 7 }, expect.any(Object));
  });

  it('offers Set it up and Skip… where nothing shows the work yet', async () => {
    const setUp = vi.fn();
    const user = userEvent.setup();
    render(
      <SessionDock
        session={reports()} lands={LANDS} onReviewRun={vi.fn()}
        landing={{ ...PLAN, review: { state: 'not-shown', environment: 'local', level: 'repository' } }}
        review={{ acts: { setUp, skip: vi.fn() } }}
      />,
    );

    await user.click(within(gate()).getByRole('button', { name: 'Set it up in local' }));
    expect(setUp).toHaveBeenCalledWith('local', expect.any(Object));
    expect(within(gate()).getByRole('button', { name: 'Skip the review for this work…' })).toBeInTheDocument();
  });

  it('says newer work since a review, with the door to the step', async () => {
    const open = vi.fn();
    const user = userEvent.setup();
    render(
      <SessionDock
        session={reports()} lands={LANDS} onReviewRun={vi.fn()}
        landing={{ ...PLAN, review: { state: 'not-held', environment: 'local', quest: 'q2' } }}
        review={{ step: STEP_REVIEWED, acts: { skip: vi.fn(), open } }}
      />,
    );

    expect(gate()).toHaveTextContent('newer work since');
    await user.click(within(gate()).getByRole('button', { name: 'Open #q2' }));
    expect(open).toHaveBeenCalled();
  });

  it('says an unread gate in its own words, with the driver\'s sentence beneath', () => {
    render(
      <SessionDock
        session={reports()} lands={LANDS} onReviewRun={vi.fn()}
        landing={{ ...PLAN, review: { state: 'unread', says: 'Whether this work waits for your review could not be read: connection refused.' } }}
      />,
    );

    expect(gate()).toHaveTextContent('Review before it lands');
    expect(gate()).toHaveTextContent('The driver says: Whether this work waits');
    expect(screen.queryByRole('button', { name: 'Open outputs' })).toBeNull();
  });

  it('offers the door to the run’s review where nothing waits for a review', () => {
    render(<SessionDock session={reports()} lands={LANDS} onReviewRun={vi.fn()} landing={PLAN} />);
    expect(screen.getByRole('button', { name: 'Open outputs' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: /Review in/ })).toBeNull();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(
      <SessionDock
        session={reports()} lands={LANDS} onReviewRun={vi.fn()}
        landing={{ ...PLAN, review: { state: 'shown', environment: 'local', quest: 'q2' } }}
        review={{ step: STEP_SHOWN, acts: { reviewed: vi.fn() } }}
      />,
    );
    expect(screen.getByRole('region', { name: /在 local 中审阅/ })).toHaveTextContent('已展示');
    expect(screen.getByRole('button', { name: '已审阅' })).toBeInTheDocument();
  });
});
