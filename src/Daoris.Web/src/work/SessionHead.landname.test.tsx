import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import type { Session } from '../data/api';
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

  describe('the plan is asked once after a pause, not per keystroke', () => {
    afterEach(() => { vi.useRealTimers(); });
    const field = () => within(ask()).getByRole('textbox', { name: 'Branch name' });
    const accept = () => within(ask()).getByRole('button', { name: 'Accept' });

    it('asks once after the pause, and Confirm is held until the plan of that name has answered', () => {
      vi.useFakeTimers();
      const onLand = vi.fn();
      const onName = vi.fn();
      const props = { session: session(), lands: LANDS, onLand, onName };
      const { rerender } = render(<SessionHead {...props} landing={BRANCH} />);
      fireEvent.click(screen.getByRole('button', { name: 'Accept…' }));
      for (const value of ['m', 'my', ' my/name ']) {
        fireEvent.change(field(), { target: { value } });
        act(() => { vi.advanceTimersByTime(100); });
      }
      expect(onName).not.toHaveBeenCalled();
      // Pressed before the pause: held, so a refusal of that exact name is never skipped.
      expect(accept()).toBeDisabled();
      act(() => { vi.advanceTimersByTime(300); });
      expect(onName).toHaveBeenCalledTimes(1);
      expect(onName).toHaveBeenLastCalledWith('my/name');
      // Asked, not yet answered: still held.
      rerender(<SessionHead {...props} landing={BRANCH} planning />);
      expect(accept()).toBeDisabled();
      rerender(<SessionHead {...props} landing={{ ...BRANCH, target: 'my/name' }} />);
      fireEvent.click(accept());
      expect(onLand).toHaveBeenCalledWith(expect.anything(), null, 'my/name');
    });

    it('asks at once when the field loses its focus', () => {
      vi.useFakeTimers();
      const onName = vi.fn();
      render(<SessionHead session={session()} lands={LANDS} onLand={vi.fn()} onName={onName} landing={BRANCH} />);
      fireEvent.click(screen.getByRole('button', { name: 'Accept…' }));
      fireEvent.change(field(), { target: { value: 'a/b' } });
      fireEvent.blur(field());
      expect(onName).toHaveBeenCalledTimes(1);
      expect(onName).toHaveBeenLastCalledWith('a/b');
    });
  });

  it('keeps the rule’s name in the placeholder once the plan answers with the entered one, and shows its refusal', async () => {
    const { rerender } = render(<SessionHead session={session()} lands={LANDS} onLand={vi.fn()} onName={vi.fn()} landing={BRANCH} />);
    await userEvent.click(screen.getByRole('button', { name: 'Accept…' }));
    await userEvent.type(within(ask()).getByRole('textbox', { name: 'Branch name' }), 'main');
    rerender(<SessionHead session={session()} lands={LANDS} onLand={vi.fn()} onName={vi.fn()}
      landing={{ ...BRANCH, target: 'main', problem: 'a branch named `main` stands, which Daoris did not make.' }} />);
    expect(within(ask()).getByRole('textbox', { name: 'Branch name' })).toHaveAttribute('placeholder', 'feature/delta');
    expect(ask()).toHaveTextContent('which Daoris did not make.');
    // STANDSREFUSE1: a press the plan says would be refused is held, its sentence saying why.
    expect(within(ask()).getByRole('button', { name: 'Accept' })).toBeDisabled();
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

  /**
   * TREEFOLLOWWEB1 (D164's TREEFOLLOW1 note): a tree its agent moved onto a branch of its own lands that branch as it stands, which
   * the ask says, naming the branch the head's line names; a name entered has nowhere to go, so no field is offered.
   */
  it('says the session’s own branch lands as it stands, and offers no field for a name', async () => {
    const own = { branch: 'feature/x', tree: 's-1a2b3c4', commits: 2, uncommitted: 0 };
    render(<SessionHead session={session()} lands={own} onLand={vi.fn()} onName={vi.fn()} landing={{ ...BRANCH, target: 'feature/x', own: true }} />);
    expect(screen.getByText('feature/x')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Accept…' }));
    const asked = screen.getByRole('group', { name: 'accept feature/x' });
    expect(asked).toHaveTextContent("Accepting lands feature/x, the session's own branch, as it stands");
    expect(within(asked).queryByRole('textbox')).toBeNull();
  });

  it('says its words in Chinese, and the name stays as typed', async () => {
    await i18n.changeLanguage('zh');
    render(<SessionHead session={session()} lands={LANDS} onLand={vi.fn()} onName={vi.fn()} landing={BRANCH} />);
    await userEvent.click(screen.getByRole('button', { name: '采纳…' }));
    expect(within(screen.getByRole('group', { name: '采纳 daoris/s-1a2b3c4' })).getByRole('textbox', { name: '分支名' }))
      .toHaveAttribute('placeholder', 'feature/delta');
  });
});
