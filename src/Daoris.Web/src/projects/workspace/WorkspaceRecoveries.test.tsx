import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { type MilestoneEntry, readWorkState } from '../../work/milestone';
import { WorkspaceRecoveries } from './WorkspaceRecoveries';

// What Daoris keeps for recovery that no unit names, in a workspace's Branches tab (RECOVERYVIEW1, CLEANUP1): a molecule over
// `WORK_STATE.recovery`, each state reached by its props. A folder says its date, a recovery ref its holder and date or, where
// nothing holds it, the command that deletes it.

const recovery = (over: Partial<MilestoneEntry>): MilestoneEntry => ({
  unit: '', kind: 'recovery-ref', resource: 'refs/daoris/discarded/daoris/s-gone', role: 'recovery', disposition: 'retained',
  detail: 'no branch or tag of yours holds its commit, so Daoris keeps it until you delete it: `git update-ref -d refs/daoris/discarded/daoris/s-gone` in `billing`',
  until: null, repository: 'billing', ...over,
});

const folder = recovery({
  kind: 'moved-aside', resource: 'trees/.tidied/work/billing/s-1-20261017T090000000Z', detail: 'moved aside 2026-10-17 09:00 UTC; deleted once it is left untouched until 2026-10-24 09:00 UTC',
  until: '2026-10-24T09:00:00Z',
});
const held = recovery({
  resource: 'refs/daoris/discarded/daoris/s-held', until: '2026-11-16T09:00:00Z',
  detail: "a branch or a tag of yours holds its commit, so the look's tidy removes it once it is 30 days old, from 2026-11-16 09:00 UTC",
});
const unheld = recovery({});

describe('what is kept for recovery', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws nothing where there is none, or where an older shell answered none', () => {
    const { container, rerender } = render(<WorkspaceRecoveries recoveries={[]} repositories={['billing']} />);
    expect(container).toBeEmptyDOMElement();
    rerender(<WorkspaceRecoveries recoveries={readWorkState({ units: [] }).recovery} repositories={['billing']} />);
    expect(container).toBeEmptyDOMElement();
    rerender(<WorkspaceRecoveries recoveries={undefined} repositories={['billing']} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('reads an answer with no recovery field, or a malformed one, as none', () => {
    expect(readWorkState({ units: [], unread: [] }).recovery).toEqual([]);
    expect(readWorkState({ recovery: 'nope' }).recovery).toEqual([]);
    expect(readWorkState({ recovery: [folder] }).recovery).toEqual([folder]);
  });

  it('says a folder set aside with its date, naming the folder and never its path, and a held ref with its holder and date', () => {
    render(<WorkspaceRecoveries recoveries={[folder, held]} repositories={['billing']} />);
    expect(screen.getByRole('heading', { name: 'Kept for recovery' })).toBeInTheDocument();
    const [first, second] = screen.getAllByRole('listitem');
    expect(first).toHaveTextContent('folder set aside');
    expect(first).toHaveTextContent('s-1-20261017T090000000Z');
    expect(first).not.toHaveTextContent('trees/');
    expect(first).toHaveTextContent(/kept until [A-Za-z]+ \d+/);
    expect(first).toHaveTextContent('billing');
    expect(second).toHaveTextContent('recovery ref');
    expect(second).toHaveTextContent('daoris/s-held');
    expect(second).toHaveTextContent(/a branch or tag of yours holds its commit.*removes it from [A-Za-z]+ \d+/);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('says a ref nothing holds with its delete command, which a press copies', async () => {
    const written = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText: written }, configurable: true });
    render(<WorkspaceRecoveries recoveries={[unheld]} repositories={['billing']} />);
    const row = screen.getByRole('listitem');
    expect(row).toHaveTextContent('nothing of yours holds its commit');
    expect(screen.getByText('git update-ref -d refs/daoris/discarded/daoris/s-gone')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Copy the command' }));
    expect(written).toHaveBeenCalledWith('git update-ref -d refs/daoris/discarded/daoris/s-gone');
  });

  it("lists only its workspace's repositories, in any case, and passes an unread ref's clause through", () => {
    const other = recovery({ repository: 'inventory', resource: 'refs/daoris/discarded/daoris/s-other' });
    const unread = recovery({ resource: 'refs/daoris/discarded/daoris/s-unread', detail: 'kept by its own rule: whether a branch or a tag of yours holds its commit was not read, so it is kept, and the end never takes it', repository: 'Billing' });
    render(<WorkspaceRecoveries recoveries={[other, unread]} repositories={['billing']} />);
    const rows = screen.getAllByRole('listitem');
    expect(rows).toHaveLength(1);
    expect(rows[0]).toHaveTextContent('daoris/s-unread');
    expect(rows[0]).toHaveTextContent('was not read');
  });

  // RECOVERYDROP1: the person's press to delete the refs nothing holds, behind a confirmation that says plainly what may be lost.
  describe('the delete of the ones nothing holds', () => {
    const COMMIT = 'a'.repeat(40);
    const shown = recovery({ commit: COMMIT });
    const other = recovery({ resource: 'refs/daoris/discarded/daoris/s-two', commit: 'b'.repeat(40) });
    const answered = { dropped: [{ repository: 'billing', ref: shown.resource, commit: COMMIT }], skipped: [], refused: [] };

    it('is offered only where a ref nothing holds names its commit and the holder gave a press', () => {
      const { rerender } = render(<WorkspaceRecoveries recoveries={[shown]} repositories={['billing']} />);
      expect(screen.queryByRole('button', { name: 'Delete the ones nothing holds' })).toBeNull();
      rerender(<WorkspaceRecoveries recoveries={[unheld]} repositories={['billing']} onDrop={vi.fn()} />);
      expect(screen.queryByRole('button', { name: 'Delete the ones nothing holds' })).toBeNull();
      rerender(<WorkspaceRecoveries recoveries={[folder, held]} repositories={['billing']} onDrop={vi.fn()} />);
      expect(screen.queryByRole('button', { name: 'Delete the ones nothing holds' })).toBeNull();
      rerender(<WorkspaceRecoveries recoveries={[held, shown]} repositories={['billing']} onDrop={vi.fn()} />);
      expect(screen.getByRole('button', { name: 'Delete the ones nothing holds' })).toBeInTheDocument();
    });

    it('lists the refs and says their commits may be lost, and a remote may still have the work, before anything is deleted', async () => {
      const onDrop = vi.fn().mockResolvedValue(answered);
      render(<WorkspaceRecoveries recoveries={[shown, other]} repositories={['billing']} onDrop={onDrop} />);
      await userEvent.click(screen.getByRole('button', { name: 'Delete the ones nothing holds' }));
      const ask = screen.getByRole('group', { name: 'delete the recovery refs nothing holds' });
      expect(ask).toHaveTextContent('Deletes 2 recovery refs that no branch or tag of yours holds');
      expect(ask).toHaveTextContent('daoris/s-gone');
      expect(ask).toHaveTextContent('daoris/s-two');
      expect(ask).toHaveTextContent("may then be lost from this machine, once git's garbage collection runs");
      expect(ask).toHaveTextContent('A git remote may still have the work');
      await userEvent.click(screen.getByRole('button', { name: 'Never mind' }));
      expect(onDrop).not.toHaveBeenCalled();
      expect(screen.queryByRole('group', { name: 'delete the recovery refs nothing holds' })).toBeNull();
    });

    it('sends each repository its refs with the commits shown, and says what was deleted and what was kept and why', async () => {
      const onDrop = vi.fn().mockResolvedValue({
        dropped: [{ repository: 'billing', ref: shown.resource, commit: COMMIT }],
        skipped: [{ repository: 'billing', ref: other.resource, why: 'it moved since you were shown it, so it is kept: look at the list again.' }],
        refused: [],
      });
      render(<WorkspaceRecoveries recoveries={[shown, other]} repositories={['billing']} onDrop={onDrop} />);
      await userEvent.click(screen.getByRole('button', { name: 'Delete the ones nothing holds' }));
      await userEvent.click(screen.getByRole('button', { name: 'Delete them' }));
      expect(onDrop).toHaveBeenCalledWith([{ repository: 'billing', refs: [
        { ref: shown.resource, commit: COMMIT }, { ref: other.resource, commit: 'b'.repeat(40) },
      ] }]);
      const said = await screen.findByRole('status');
      expect(said).toHaveTextContent('Deleted 1 recovery ref');
      expect(said).toHaveTextContent('Kept refs/daoris/discarded/daoris/s-two: it moved since you were shown it');
    });

    it('keeps the ask open and says the driver\'s sentence where the whole press was refused', async () => {
      const onDrop = vi.fn().mockResolvedValue({ dropped: [], skipped: [], refused: ['this process does not hold the home, so it deletes nothing.'] });
      render(<WorkspaceRecoveries recoveries={[shown]} repositories={['billing']} onDrop={onDrop} />);
      await userEvent.click(screen.getByRole('button', { name: 'Delete the ones nothing holds' }));
      await userEvent.click(screen.getByRole('button', { name: 'Delete them' }));
      expect(await screen.findByText('this process does not hold the home, so it deletes nothing.')).toBeInTheDocument();
      expect(screen.getByRole('group', { name: 'delete the recovery refs nothing holds' })).toBeInTheDocument();
    });

    it('says it all in Chinese', async () => {
      await i18n.changeLanguage('zh');
      const onDrop = vi.fn().mockResolvedValue(answered);
      render(<WorkspaceRecoveries recoveries={[shown]} repositories={['billing']} onDrop={onDrop} />);
      await userEvent.click(screen.getByRole('button', { name: '删除无人持有的那些' }));
      const ask = screen.getByRole('group', { name: '删除无人持有的找回引用' });
      expect(ask).toHaveTextContent('你的分支和标签都不持有它们的提交');
      expect(ask).toHaveTextContent('垃圾回收');
      expect(ask).toHaveTextContent('远程仓库');
      await userEvent.click(screen.getByRole('button', { name: '确认删除' }));
      expect(await screen.findByRole('status')).toHaveTextContent('已删除 1 个找回引用');
    });
  });

  it('says it all in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<WorkspaceRecoveries recoveries={[folder, held, unheld]} repositories={['billing']} />);
    expect(screen.getByRole('heading', { name: '为找回保留的内容' })).toBeInTheDocument();
    const [first, second, third] = screen.getAllByRole('listitem');
    expect(first).toHaveTextContent('移开的文件夹');
    expect(first).toHaveTextContent(/保留到/);
    expect(second).toHaveTextContent('找回引用');
    expect(second).toHaveTextContent(/持有它的提交/);
    expect(third).toHaveTextContent('你的分支和标签都不持有它的提交');
    expect(screen.getByRole('button', { name: '复制命令' })).toBeInTheDocument();
    expect(third).toHaveTextContent('git update-ref -d refs/daoris/discarded/daoris/s-gone');
  });
});
