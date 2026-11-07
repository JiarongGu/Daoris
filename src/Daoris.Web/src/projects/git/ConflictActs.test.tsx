import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { GIT_CHANGES_ENGINE, GIT_CONFLICT_CACHE } from '../fixtures';
import { ConflictHunks } from './ConflictHunks';
import { ChangesTab, type ChangesActs } from './RepositoryGit';

// Resolve a conflicted file in the Changes tab: the conflicts of a file with ours and theirs
// side by side, a take for each, the file written once all are chosen, Mark resolved beside it, and the answer said with Undo.
// A molecule: its acts arrive as a prop, so each state is reached by it.

const acts = (over: Partial<ChangesActs> = {}): ChangesActs => ({
  busy: false,
  last: null,
  press: vi.fn(),
  undo: vi.fn(),
  Hunks: () => null,
  resolve: vi.fn(),
  mark: vi.fn(),
  Conflict: ({ busy, onPress }) => <ConflictHunks read={{ state: 'read', value: GIT_CONFLICT_CACHE }} busy={busy} onPress={onPress} />,
  ...over,
});

const tab = (given: ChangesActs) => render(<ChangesTab repository="engine" read={{ state: 'read', value: GIT_CHANGES_ENGINE }} acts={given} />);
// The row is the list's first item; the lines of a conflict are lists of their own inside it.
const conflicted = (name = 'Conflicted (1)') => within(screen.getByRole('list', { name })).getAllByRole('listitem')[0]!;

describe('resolving a conflicted file in the Changes tab', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('shows the conflicts under the row with ours and theirs, and writes nothing until each has a take', async () => {
    const given = acts();
    tab(given);
    await userEvent.click(within(conflicted()).getByRole('button', { name: 'Show conflicts' }));

    expect(within(conflicted()).getByText('Conflict 1 of 2')).toBeInTheDocument();
    expect(within(conflicted()).getByText('private const int Size = 64;', { exact: false })).toBeInTheDocument();
    const write = within(conflicted()).getByRole('button', { name: 'Write the file (0 of 2 chosen)' });
    expect(write).toBeDisabled();

    await userEvent.click(within(conflicted()).getAllByRole('button', { name: 'Take ours' })[0]!);
    expect(within(conflicted()).getByRole('button', { name: 'Write the file (1 of 2 chosen)' })).toBeDisabled();
    expect(given.resolve).not.toHaveBeenCalled();
  });

  it('sends a take for every conflict with the stages and the file id it was read with', async () => {
    const given = acts();
    tab(given);
    await userEvent.click(within(conflicted()).getByRole('button', { name: 'Show conflicts' }));

    await userEvent.click(within(conflicted()).getAllByRole('button', { name: 'Take theirs' })[0]!);
    await userEvent.click(within(conflicted()).getAllByRole('button', { name: 'Take both' })[1]!);
    await userEvent.click(within(conflicted()).getByRole('button', { name: 'Write the file (2 of 2 chosen)' }));

    expect(given.resolve).toHaveBeenCalledWith({
      path: 'src/cache.cs',
      picks: [{ id: '3f9a1c7b2d40', take: 'theirs' }, { id: '8c2e5d9a4b17', take: 'both' }],
      faces: { head: GIT_CONFLICT_CACHE.head, base: GIT_CONFLICT_CACHE.base, ours: GIT_CONFLICT_CACHE.ours, theirs: GIT_CONFLICT_CACHE.theirs, worktree: GIT_CONFLICT_CACHE.worktree },
    });
  });

  it('takes a side everywhere in one press and marks the file resolved by its path', async () => {
    const given = acts();
    tab(given);
    await userEvent.click(within(conflicted()).getByRole('button', { name: 'Show conflicts' }));

    await userEvent.click(within(conflicted()).getByRole('button', { name: 'Ours everywhere' }));
    expect(within(conflicted()).getByRole('button', { name: 'Write the file (2 of 2 chosen)' })).toBeEnabled();
    await userEvent.click(within(conflicted()).getByRole('button', { name: 'Mark resolved' }));

    expect(given.mark).toHaveBeenCalledWith('src/cache.cs');
  });

  it('says why a file cannot be taken by hunk, and draws no write for it', async () => {
    const why = '`src/cache.cs` is in conflict with a side that has no file: nothing can be chosen hunk by hunk.';
    tab(acts({ Conflict: ({ busy, onPress }) => <ConflictHunks read={{ state: 'read', value: { ...GIT_CONFLICT_CACHE, why, hunks: [] } }} busy={busy} onPress={onPress} /> }));
    await userEvent.click(within(conflicted()).getByRole('button', { name: 'Show conflicts' }));

    expect(within(conflicted()).getByText(why)).toBeInTheDocument();
    expect(within(conflicted()).queryByRole('button', { name: /Write the file/ })).toBeNull();
  });

  it('holds every press while an act runs', async () => {
    tab(acts({ busy: true }));

    expect(within(conflicted()).getByRole('button', { name: 'Mark resolved' })).toBeDisabled();
    await userEvent.click(within(conflicted()).getByRole('button', { name: 'Show conflicts' }));
    expect(within(conflicted()).getAllByRole('button', { name: 'Take ours' })[0]).toBeDisabled();
    expect(within(conflicted()).getByRole('button', { name: 'Ours everywhere' })).toBeDisabled();
  });

  it('says a resolve and offers Undo, and a mark; an undo of a resolve says the bytes are back', () => {
    const given = acts({ last: { verb: 'resolve', path: 'src/cache.cs', act: 'act-9', done: true, said: null } });
    const { rerender } = tab(given);

    expect(screen.getByRole('status')).toHaveTextContent('Wrote your choices into src/cache.cs.');
    expect(screen.getByRole('button', { name: 'Undo' })).toBeInTheDocument();

    rerender(<ChangesTab repository="engine" read={{ state: 'read', value: GIT_CHANGES_ENGINE }} acts={acts({ last: { verb: 'mark', path: 'src/cache.cs', act: 'act-10', done: true, said: null } })} />);
    expect(screen.getByRole('status')).toHaveTextContent('Marked src/cache.cs resolved.');
    expect(screen.getByRole('button', { name: 'Undo' })).toBeInTheDocument();

    rerender(<ChangesTab repository="engine" read={{ state: 'read', value: GIT_CHANGES_ENGINE }} acts={acts({ last: { verb: 'undoResolve', path: null, act: 'act-11', done: true, said: null } })} />);
    expect(screen.getByRole('status')).toHaveTextContent("The file's bytes are put back");
    expect(screen.queryByRole('button', { name: 'Undo' })).toBeNull();
  });

  it('says a refusal in the driver\'s sentence', () => {
    tab(acts({ last: { verb: 'resolve', path: 'src/cache.cs', act: null, done: false, said: '`src/cache.cs`\'s bytes changed since they were read: nothing was changed.' } }));

    expect(screen.getByRole('status')).toHaveTextContent('bytes changed since they were read');
    expect(screen.queryByRole('button', { name: 'Undo' })).toBeNull();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    tab(acts());

    await userEvent.click(within(conflicted('有冲突（1）')).getByRole('button', { name: '查看冲突' }));
    const item = conflicted('有冲突（1）');
    expect(within(item).getByRole('button', { name: '标记为已解决' })).toBeInTheDocument();
    expect(within(item).getByText('第 1 处冲突，共 2 处')).toBeInTheDocument();
    expect(within(item).getByRole('button', { name: '写入文件（已选 0/2）' })).toBeDisabled();
  });

  it('draws no resolve where the acts carry none (the staging acts only)', () => {
    tab(acts({ Conflict: undefined, resolve: undefined, mark: undefined }));

    expect(screen.queryByRole('button', { name: 'Show conflicts' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Mark resolved' })).toBeNull();
  });
});
