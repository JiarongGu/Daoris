import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { type MilestoneEntry, readWorkState } from '../work/milestone';
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
