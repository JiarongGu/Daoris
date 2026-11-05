import { describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { show } from '../../test/shellHarness';
import { GitAccessRows, resolveGitAccess, wideningSays } from './GitAccess';

describe('git access rows', () => {
  it('resolves each class from the nearest level, Write being Daoris\'s default where none is set', () => {
    expect(resolveGitAccess([{ classes: [{ class: 'read', value: 'ask' }], source: 'machine' }])).toEqual([
      { class: 'read', value: 'ask', source: 'machine' },
      { class: 'write', value: 'as-today', source: 'default' },
      { class: 'dangerous', value: 'deny', source: 'default' },
    ]);
  });

  it('offers Dangerous allow or deny alone, asks first to allow it, and sends a deny at once (GITAUTH1g)', async () => {
    const onChange = vi.fn();
    show(
      <GitAccessRows
        owner="machine"
        classes={[{ class: 'dangerous', value: 'deny', source: 'default' }]}
        above={resolveGitAccess([])}
        onChange={onChange}
      />,
    );
    const user = userEvent.setup();

    expect(screen.getByText(/Force-pushes, deletes, tags, mirrors and prunes on a git remote: deny\./)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Set for this machine' }));
    expect(screen.queryByRole('radio', { name: 'ask' })).toBeNull();
    await user.click(screen.getByRole('radio', { name: 'allow' }));
    expect(onChange).not.toHaveBeenCalled();
    expect(screen.getByText('Sessions may force-push, delete refs, push tags, mirror and prune for this machine, with no one asked.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Loosen git access' }));
    expect(onChange).toHaveBeenCalledWith({ access: { dangerous: 'allow' } }, expect.anything());
  });

  it('says what a loosening lets happen and nothing for a tightening', () => {
    const t = (key: string) => key;
    expect(wideningSays(t as never, 'engine', 'write', 'as-today', 'ask')).toBe('settings.git.access.confirm.write.ask');
    expect(wideningSays(t as never, 'engine', 'write', 'ask', 'allow')).toBe('settings.git.access.confirm.write.allow');
    expect(wideningSays(t as never, 'engine', 'write', 'allow', 'deny')).toBeNull();
    expect(wideningSays(t as never, 'engine', 'read', 'ask', 'ask')).toBeNull();
  });

  it('asks first when Clear gives the class back to a looser level above, and clears at once under a tighter one (REVIEW2 P3)', async () => {
    const onChange = vi.fn();
    const own = [{ class: 'read', value: 'allow', source: 'default' }, { class: 'write', value: 'deny', source: 'repository' }];
    show(
      <GitAccessRows
        owner="repository"
        name="engine"
        classes={own}
        above={[{ class: 'read', value: 'allow', source: 'default' }, { class: 'write', value: 'allow', source: 'workspace' }]}
        onChange={onChange}
      />,
    );
    const user = userEvent.setup();

    await user.click(screen.getByRole('button', { name: 'Clear' }));
    expect(onChange).not.toHaveBeenCalled();
    expect(screen.getByText('Sessions may push to a git remote without asking you for engine.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Loosen git access' }));
    expect(onChange).toHaveBeenCalledWith({ access: { write: null } }, expect.anything());
  });

  it('clears at once where the level above is as tight or tighter (REVIEW2 P3)', async () => {
    const onChange = vi.fn();
    show(
      <GitAccessRows
        owner="repository"
        name="engine"
        classes={[{ class: 'read', value: 'allow', source: 'default' }, { class: 'write', value: 'allow', source: 'repository' }]}
        above={[{ class: 'read', value: 'allow', source: 'default' }, { class: 'write', value: 'ask', source: 'workspace' }]}
        onChange={onChange}
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Clear' }));
    expect(onChange).toHaveBeenCalledWith({ access: { write: null } }, expect.anything());
  });

  it('sends a tightening at once and asks first for a loosening', async () => {
    const onChange = vi.fn();
    show(
      <GitAccessRows
        owner="machine"
        classes={[{ class: 'read', value: 'allow', source: 'default' }, { class: 'write', value: 'ask', source: 'machine' }]}
        above={resolveGitAccess([])}
        onChange={onChange}
      />,
    );
    const user = userEvent.setup();

    expect(screen.getByText(/Pushing to a git remote: ask\./)).toBeInTheDocument();
    await user.click(screen.getAllByRole('button', { name: 'Set for this machine' })[0]!);
    await user.click(screen.getAllByRole('radio', { name: 'deny' })[0]!);
    expect(onChange).toHaveBeenCalledWith({ access: { read: 'deny' } }, expect.anything());
  });
});
