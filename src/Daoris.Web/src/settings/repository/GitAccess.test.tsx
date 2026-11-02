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
    ]);
  });

  it('says what a loosening lets happen and nothing for a tightening', () => {
    const t = (key: string) => key;
    expect(wideningSays(t as never, 'engine', 'write', 'as-today', 'ask')).toBe('settings.git.access.confirm.write.ask');
    expect(wideningSays(t as never, 'engine', 'write', 'ask', 'allow')).toBe('settings.git.access.confirm.write.allow');
    expect(wideningSays(t as never, 'engine', 'write', 'allow', 'deny')).toBeNull();
    expect(wideningSays(t as never, 'engine', 'read', 'ask', 'ask')).toBeNull();
  });

  it('sends a tightening at once and asks first for a loosening', async () => {
    const onChange = vi.fn();
    show(
      <GitAccessRows
        owner="machine"
        classes={[{ class: 'read', value: 'allow', source: 'default' }, { class: 'write', value: 'ask', source: 'machine' }]}
        onChange={onChange}
      />,
    );
    const user = userEvent.setup();

    expect(screen.getByText(/Pushing to a remote: ask\./)).toBeInTheDocument();
    await user.click(screen.getAllByRole('button', { name: 'Set for this machine' })[0]!);
    await user.click(screen.getAllByRole('radio', { name: 'deny' })[0]!);
    expect(onChange).toHaveBeenCalledWith({ access: { read: 'deny' } }, expect.anything());
  });
});
