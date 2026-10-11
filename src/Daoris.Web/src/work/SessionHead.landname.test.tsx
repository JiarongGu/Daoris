import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import type { Session } from '../api';
import { SessionHead } from './SessionHead';

// LANDNAME1b (D145's LANDNAME1a note): the head's land ask takes a branch name that replaces the rule's whole name, where the
// rule makes a branch; blank keeps the rule's. The plan is asked again with the name by the frame (`onName`), so a refusal shows
// before the press.

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: 'q1', repository: 'reports', adapter: 'claude-code', state: 'completed', kind: 'driven',
  created: '2026-10-09T08:00:00Z', updated: '2026-10-09T09:00:00Z', tree: 'C:/somewhere/reports', ...over,
});
const LANDS = { branch: 'daoris/s-1a2b3c4', tree: 's-1a2b3c4', commits: 2, uncommitted: 0 };
const BRANCH = { session: 's1a2b3c4', form: 'branch', target: 'feature/delta' };
const ask = () => screen.getByRole('group', { name: 'accept daoris/s-1a2b3c4' });

describe("the head's land ask names a branch", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('offers a field whose placeholder is the rule’s name, and a blank press sends no name', async () => {
    const onLand = vi.fn();
    render(<SessionHead session={session()} lands={LANDS} onLand={onLand} onName={vi.fn()} landing={BRANCH} />);
    await userEvent.click(screen.getByRole('button', { name: 'Accept…' }));
    const field = within(ask()).getByRole('textbox', { name: 'Branch name' });
    expect(field).toHaveAttribute('placeholder', 'feature/delta');
    await userEvent.click(within(ask()).getByRole('button', { name: 'Accept' }));
    expect(onLand).toHaveBeenCalledWith(expect.anything(), null, null);
  });

  it('asks the plan again with the name as it is typed, and sends it on the press', async () => {
    const onLand = vi.fn();
    const onName = vi.fn();
    render(<SessionHead session={session()} lands={LANDS} onLand={onLand} onName={onName} landing={BRANCH} />);
    await userEvent.click(screen.getByRole('button', { name: 'Accept…' }));
    await userEvent.type(within(ask()).getByRole('textbox', { name: 'Branch name' }), ' my/name ');
    expect(onName).toHaveBeenLastCalledWith('my/name');
    await userEvent.click(within(ask()).getByRole('button', { name: 'Accept' }));
    expect(onLand).toHaveBeenCalledWith(expect.anything(), null, 'my/name');
  });

  it('keeps the rule’s name in the placeholder once the plan answers with the entered one, and shows its refusal', async () => {
    const { rerender } = render(<SessionHead session={session()} lands={LANDS} onLand={vi.fn()} onName={vi.fn()} landing={BRANCH} />);
    await userEvent.click(screen.getByRole('button', { name: 'Accept…' }));
    await userEvent.type(within(ask()).getByRole('textbox', { name: 'Branch name' }), 'main');
    rerender(<SessionHead session={session()} lands={LANDS} onLand={vi.fn()} onName={vi.fn()}
      landing={{ ...BRANCH, target: 'main', problem: 'a branch named `main` stands, which Daoris did not make.' }} />);
    expect(within(ask()).getByRole('textbox', { name: 'Branch name' })).toHaveAttribute('placeholder', 'feature/delta');
    expect(ask()).toHaveTextContent('which Daoris did not make.');
  });

  it('puts the name down with the ask, so the plan is the rule’s again', async () => {
    const onName = vi.fn();
    render(<SessionHead session={session()} lands={LANDS} onLand={vi.fn()} onName={onName} landing={BRANCH} />);
    await userEvent.click(screen.getByRole('button', { name: 'Accept…' }));
    await userEvent.type(within(ask()).getByRole('textbox', { name: 'Branch name' }), 'x');
    await userEvent.click(within(ask()).getByRole('button', { name: 'Never mind' }));
    expect(onName).toHaveBeenLastCalledWith('');
  });

  it('offers no field where the rule merges', async () => {
    render(<SessionHead session={session()} lands={LANDS} onLand={vi.fn()} onName={vi.fn()} landing={{ ...BRANCH, form: 'merge', target: 'main' }} />);
    await userEvent.click(screen.getByRole('button', { name: 'Accept…' }));
    expect(within(ask()).queryByRole('textbox')).toBeNull();
  });

  it('says its words in Chinese, and the name stays as typed', async () => {
    await i18n.changeLanguage('zh');
    render(<SessionHead session={session()} lands={LANDS} onLand={vi.fn()} onName={vi.fn()} landing={BRANCH} />);
    await userEvent.click(screen.getByRole('button', { name: '采纳…' }));
    expect(within(screen.getByRole('group', { name: '采纳 daoris/s-1a2b3c4' })).getByRole('textbox', { name: '分支名' }))
      .toHaveAttribute('placeholder', 'feature/delta');
  });
});
