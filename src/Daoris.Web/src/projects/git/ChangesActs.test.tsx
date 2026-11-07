import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { GIT_CHANGES_ENGINE, GIT_HUNKS_BUDGET } from '../fixtures';
import { ChangeHunks } from './ChangeHunks';
import { ChangesTab, type ChangesActs } from './RepositoryGit';

// Stage, unstage and Undo in the Changes tab: a file pressed whole, the hunks and lines of
// it, the last act said with Undo beside a done one, a refusal in the driver's sentence, and every press waiting while an act runs.
// A molecule: its acts arrive as a prop, so each state is reached by it.

const BUDGET_UNSTAGED = GIT_CHANGES_ENGINE.unstaged.find((file) => file.path === 'src/budget.cs')!;

const acts = (over: Partial<ChangesActs> = {}): ChangesActs => ({
  busy: false,
  last: null,
  press: vi.fn(),
  undo: vi.fn(),
  Hunks: ({ side, busy, onPress }) => (
    <ChangeHunks read={{ state: 'read', value: GIT_HUNKS_BUDGET }} verb={side === 'unstaged' ? 'stage' : 'unstage'} busy={busy} onPress={onPress} />
  ),
  ...over,
});

const tab = (given: ChangesActs) => render(<ChangesTab repository="engine" read={{ state: 'read', value: GIT_CHANGES_ENGINE }} acts={given} />);
const row = (name: string, path: string) =>
  within(screen.getByRole('list', { name })).getAllByRole('listitem').find((each) => each.querySelector('code')?.textContent === path)!;

describe('stage and unstage in the Changes tab', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('stages a file whole from Not staged, naming the index entry the list showed', async () => {
    const given = acts();
    tab(given);

    await userEvent.click(within(row('Not staged (2)', 'src/budget.cs')).getByRole('button', { name: 'Stage' }));

    expect(given.press).toHaveBeenCalledWith({ verb: 'stage', path: 'src/budget.cs', shown: `${BUDGET_UNSTAGED.oldMode} ${BUDGET_UNSTAGED.oldBlob}` });
  });

  it('unstages a file whole from Staged, and stages an untracked one, which no index holds yet', async () => {
    const given = acts();
    tab(given);

    await userEvent.click(within(row('Staged (2)', 'src/budget.cs')).getByRole('button', { name: 'Unstage' }));
    await userEvent.click(within(row('Untracked (1)', 'scratch/trace.log')).getByRole('button', { name: 'Stage' }));

    const [unstaged, staged] = (given.press as ReturnType<typeof vi.fn>).mock.calls.map(([call]) => call);
    expect(unstaged).toMatchObject({ verb: 'unstage', path: 'src/budget.cs' });
    expect(staged).toEqual({ verb: 'stage', path: 'scratch/trace.log', shown: '' });
  });

  it('shows a file\'s hunks on request and stages one whole, sending the ids it was read with', async () => {
    const given = acts();
    tab(given);
    const budget = row('Not staged (2)', 'src/budget.cs');

    await userEvent.click(within(budget).getByRole('button', { name: 'Show changes' }));
    expect(within(budget).getByText('@@ -10,4 +10,5 @@ class Budget')).toBeInTheDocument();
    await userEvent.click(within(budget).getByRole('button', { name: 'Stage this hunk' }));

    expect(given.press).toHaveBeenCalledWith(expect.objectContaining({
      verb: 'stage', path: 'src/budget.cs', picks: [{ id: 'a1b2c3d4e5f6' }],
      faces: { head: GIT_HUNKS_BUDGET.head, index: GIT_HUNKS_BUDGET.index, source: GIT_HUNKS_BUDGET.source },
    }));
  });

  it('stages only the chosen lines of a hunk, by their place in it', async () => {
    const given = acts();
    tab(given);
    const budget = row('Not staged (2)', 'src/budget.cs');
    await userEvent.click(within(budget).getByRole('button', { name: 'Show changes' }));
    const lines = within(budget).getByRole('button', { name: 'Stage the chosen lines (0)' });
    expect(lines).toBeDisabled();

    await userEvent.click(within(budget).getByRole('checkbox', { name: 'Choose the line: total += cost;' }));
    await userEvent.click(within(budget).getByRole('checkbox', { name: 'Choose the line: total += cost * rate;' }));
    await userEvent.click(within(budget).getByRole('button', { name: 'Stage the chosen lines (2)' }));

    expect(given.press).toHaveBeenCalledWith(expect.objectContaining({ picks: [{ id: 'a1b2c3d4e5f6', lines: [1, 2] }] }));
  });

  it('unstages a hunk of Staged with the opposite words', async () => {
    const given = acts();
    tab(given);
    const budget = row('Staged (2)', 'src/budget.cs');

    await userEvent.click(within(budget).getByRole('button', { name: 'Show changes' }));
    await userEvent.click(within(budget).getByRole('button', { name: 'Unstage this hunk' }));

    expect(given.press).toHaveBeenCalledWith(expect.objectContaining({ verb: 'unstage', picks: [{ id: 'a1b2c3d4e5f6' }] }));
  });

  it('says a binary file has no hunks to choose', async () => {
    const given = acts({
      Hunks: ({ busy, onPress }) => <ChangeHunks read={{ state: 'read', value: { ...GIT_HUNKS_BUDGET, binary: true, hunks: [] } }} verb="stage" busy={busy} onPress={onPress} />,
    });
    tab(given);
    const budget = row('Not staged (2)', 'src/budget.cs');

    await userEvent.click(within(budget).getByRole('button', { name: 'Show changes' }));

    expect(within(budget).getByText('A binary file has no hunks to choose: stage or unstage it whole.')).toBeInTheDocument();
  });

  it('holds every press while an act runs', async () => {
    tab(acts({ busy: true }));
    const budget = row('Not staged (2)', 'src/budget.cs');
    await userEvent.click(within(budget).getByRole('button', { name: 'Show changes' }));

    expect(within(budget).getByRole('button', { name: 'Stage' })).toBeDisabled();
    expect(within(budget).getByRole('button', { name: 'Stage this hunk' })).toBeDisabled();
    expect(within(row('Untracked (1)', 'scratch/trace.log')).getByRole('button', { name: 'Stage' })).toBeDisabled();
  });
});

describe('the last act and Undo in the Changes tab', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says a done stage and offers Undo, which takes the act', async () => {
    const given = acts({ last: { verb: 'stage', path: 'src/budget.cs', act: 'act-1', done: true, said: null } });
    tab(given);

    expect(screen.getByRole('status')).toHaveTextContent('Staged src/budget.cs.');
    await userEvent.click(screen.getByRole('button', { name: 'Undo' }));

    expect(given.undo).toHaveBeenCalledWith('act-1');
  });

  it('offers no Undo beside an undo, and says a refusal in the driver\'s sentence', () => {
    const { rerender } = tab(acts({ last: { verb: 'undo', path: null, act: 'act-2', done: true, said: null } }));
    expect(screen.getByRole('status')).toHaveTextContent('The index is put back as it was before that act.');
    expect(screen.queryByRole('button', { name: 'Undo' })).toBeNull();

    rerender(<ChangesTab repository="engine" read={{ state: 'read', value: GIT_CHANGES_ENGINE }} acts={acts({
      last: { verb: 'unstage', path: 'src/budget.cs', act: null, done: false, said: '`src/budget.cs` changed since act 7 left it, so none was restored: nothing was changed.' },
    })} />);
    expect(screen.getByRole('status')).toHaveTextContent('changed since act 7 left it');
    expect(screen.queryByRole('button', { name: 'Undo' })).toBeNull();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    tab(acts({ last: { verb: 'unstage', path: 'a.txt', act: 'act-3', done: true, said: null } }));

    expect(screen.getByRole('status')).toHaveTextContent('已取消暂存 a.txt。');
    expect(screen.getByRole('button', { name: '撤销' })).toBeInTheDocument();
    expect(within(row('已暂存（2）', 'src/budget.cs')).getByRole('button', { name: '取消暂存' })).toBeInTheDocument();
  });

  it('draws nothing of the acts where there are none', () => {
    render(<ChangesTab repository="engine" read={{ state: 'read', value: GIT_CHANGES_ENGINE }} />);
    expect(screen.queryByRole('button', { name: 'Stage' })).toBeNull();
    expect(screen.queryByRole('status')).toBeNull();
  });
});
