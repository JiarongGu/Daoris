import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import i18n from '../../locales/i18n';
import { GIT_CHANGES_ENGINE, GIT_COMMIT_PLAN_ENGINE } from '../fixtures';
import { CommitBox, type CommitPlanValue } from './CommitBox';
import { type ChangesActs, ChangesTab } from './RepositoryGit';

// Commit and amend in the Changes tab: the message, Amend the last commit, the hooks the git runs,
// who commits, a press that sends the tip and tree it read, and the last act said with a hook's words. A molecule: the plan arrives
// as a prop.

const plan = (over: Partial<CommitPlanValue> = {}): CommitPlanValue => ({ ...GIT_COMMIT_PLAN_ENGINE, ...over });

/** The box with its holder's amend state, as the view keeps it. */
function Held({ value, busy = false, onPress }: { value: CommitPlanValue; busy?: boolean; onPress: (press: unknown, done: () => void) => void }) {
  const [amend, setAmend] = useState(false);
  return <CommitBox read={{ state: 'read', value }} amend={amend} onAmend={setAmend} busy={busy} onPress={onPress} />;
}

describe('the commit box', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('presses Commit with the message typed and the tip and tree it was read with', async () => {
    const onPress = vi.fn();
    render(<Held value={plan()} onPress={onPress} />);
    expect(screen.getByRole('button', { name: 'Commit' })).toBeDisabled();

    await userEvent.type(screen.getByRole('textbox', { name: 'Message' }), 'Round the total');
    await userEvent.click(screen.getByRole('button', { name: 'Commit' }));

    expect(onPress).toHaveBeenCalledWith({ verb: 'commit', message: 'Round the total', head: GIT_COMMIT_PLAN_ENGINE.head, tree: GIT_COMMIT_PLAN_ENGINE.tree }, expect.any(Function));
  });

  it('names the hooks it runs, never skipped, and who commits', () => {
    render(<Held value={plan()} onPress={vi.fn()} />);

    expect(screen.getByText(/Runs these hooks, never skipped/)).toHaveTextContent('pre-commit, commit-msg');
    expect(screen.getByText('Commits as Aurora Dev <aurora@example.com>.')).toBeInTheDocument();
  });

  it('says so where the repository has no hooks and where git has no one to commit as', () => {
    render(<Held value={plan({ hooks: [], identity: null })} onPress={vi.fn()} />);

    expect(screen.getByText('The repository has no hooks to run.')).toBeInTheDocument();
    expect(screen.getByText(/Git has no name and email to commit as/)).toBeInTheDocument();
  });

  it('starts an amend from HEAD\'s message and presses Amend', async () => {
    const onPress = vi.fn();
    render(<Held value={plan()} onPress={onPress} />);

    await userEvent.click(screen.getByRole('checkbox', { name: 'Amend the last commit' }));

    expect(screen.getByRole('textbox', { name: 'Message' })).toHaveValue('Round the budget total');
    await userEvent.click(screen.getByRole('button', { name: 'Amend' }));
    expect(onPress).toHaveBeenCalledWith(expect.objectContaining({ verb: 'amend', message: 'Round the budget total' }), expect.any(Function));
  });

  it('holds Commit where nothing is staged and says why, but not Amend', async () => {
    render(<Held value={plan({ empty: true })} onPress={vi.fn()} />);
    await userEvent.type(screen.getByRole('textbox', { name: 'Message' }), 'x');

    expect(screen.getByRole('button', { name: 'Commit' })).toBeDisabled();
    expect(screen.getByText('Nothing is staged: stage a change first.')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('checkbox', { name: 'Amend the last commit' }));
    expect(screen.getByRole('button', { name: 'Amend' })).toBeEnabled();
  });

  it('holds both where a file in conflict stands in the index, in the driver\'s words', async () => {
    render(<Held value={plan({ tree: null, why: 'a file in conflict stands in the index, so it cannot be committed' })} onPress={vi.fn()} />);
    await userEvent.type(screen.getByRole('textbox', { name: 'Message' }), 'x');

    expect(screen.getByRole('button', { name: 'Commit' })).toBeDisabled();
    expect(screen.getByText(/a file in conflict stands in the index/)).toBeInTheDocument();
  });

  it('offers no amend on a branch with no commit, and holds every control while an act runs', () => {
    const { unmount } = render(<Held value={plan({ head: null, message: null })} onPress={vi.fn()} />);
    expect(screen.getByRole('checkbox', { name: 'Amend the last commit' })).toBeDisabled();
    unmount();

    render(<Held value={plan()} busy onPress={vi.fn()} />);
    expect(screen.getByRole('textbox', { name: 'Message' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Commit' })).toBeDisabled();
  });

  it('clears the message once the act is done', async () => {
    const onPress = vi.fn((_press: unknown, done: () => void) => done());
    render(<Held value={plan()} onPress={onPress} />);
    await userEvent.type(screen.getByRole('textbox', { name: 'Message' }), 'Round the total');

    await userEvent.click(screen.getByRole('button', { name: 'Commit' }));

    expect(screen.getByRole('textbox', { name: 'Message' })).toHaveValue('');
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<Held value={plan()} onPress={vi.fn()} />);

    expect(screen.getByRole('textbox', { name: '提交信息' })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: '修补上一个提交' })).toBeInTheDocument();
    expect(screen.getByText(/将运行这些钩子，绝不跳过/)).toHaveTextContent('pre-commit, commit-msg');
    expect(screen.getByRole('button', { name: '提交' })).toBeInTheDocument();
  });
});

describe('the last commit in the Changes tab', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  const acts = (last: ChangesActs['last']): ChangesActs => ({
    busy: false, last, press: vi.fn(), undo: vi.fn(), Hunks: () => null,
    Commit: ({ busy }) => <CommitBox read={{ state: 'read', value: plan() }} amend={false} onAmend={vi.fn()} busy={busy} onPress={vi.fn()} />,
  });
  const tab = (given: ChangesActs) => render(<ChangesTab repository="engine" read={{ state: 'read', value: GIT_CHANGES_ENGINE }} acts={given} />);

  it('draws the box in the tab and says a done commit with what a hook changed, and offers no Undo', () => {
    tab(acts({ verb: 'commit', path: null, act: 'act-1', done: true, said: 'x', notes: 'A hook changed what was committed: `a.txt` differ from what was staged.' }));

    expect(screen.getByRole('region', { name: 'Commit' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Committed. A hook changed what was committed');
    expect(screen.queryByRole('button', { name: 'Undo' })).toBeNull();
  });

  it('says a hook\'s refusal in its own words', () => {
    tab(acts({ verb: 'commit', path: null, act: 'act-2', done: false, said: 'act 2 (commit) ...', notes: 'Git refused the commit (lint failed).' }));

    expect(screen.getByRole('status')).toHaveTextContent('Git refused the commit (lint failed).');
  });

  it('says an amend done, in 中文 too', async () => {
    await i18n.changeLanguage('zh');
    tab(acts({ verb: 'amend', path: null, act: 'act-3', done: true, said: 'x' }));

    expect(screen.getByRole('status')).toHaveTextContent('已修补上一个提交');
  });

  it('draws no box where the acts bring none', () => {
    tab({ ...acts(null), Commit: undefined });

    expect(screen.queryByRole('region', { name: 'Commit' })).toBeNull();
  });
});
