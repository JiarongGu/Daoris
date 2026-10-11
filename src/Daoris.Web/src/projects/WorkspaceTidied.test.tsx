import { afterEach, describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import type { LogRow } from '../settings/Logs';
import { WorkspaceTidied } from './WorkspaceTidied';

// What the look cleaned up on its own, in a workspace's Branches tab (AUTOTIDY1a, D88): a molecule over the log's lines, each
// state reached by its props. The log's own words (`why`, the event) are never shown as they are written.

const hoursAgo = (hours: number) => new Date(Date.now() - hours * 3_600_000).toISOString();

const tidied = (branch: string, hours: number, data: Partial<LogRow['data']> = {}): LogRow => ({
  time: hoursAgo(hours), source: 'driver', level: 'info', event: 'branch.tidied',
  data: { repository: 'billing', workspace: 'work', branch, tree: false, folder: null, ...data },
});

const kept = (why: string, hours: number, data: Partial<LogRow['data']> = {}): LogRow => ({
  time: hoursAgo(hours), source: 'driver', level: 'warn', event: 'branch.kept',
  data: { repository: 'api', workspace: 'work', branch: 'daoris/s-aaaa1111', why, ...data },
});

describe('what was cleaned up on its own', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws nothing where there is nothing, or no reading', () => {
    const { container, rerender } = render(<WorkspaceTidied lines={[]} />);
    expect(container).toBeEmptyDOMElement();
    rerender(<WorkspaceTidied lines={null} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("names a cleaned-up branch, its repository and when, and that its tree's folder was set aside where it had one", () => {
    render(<WorkspaceTidied lines={[
      tidied('daoris/s-1f2e3d4c', 3, { tree: true, folder: 'trees/.tidied/work/billing/s-1' }),
      tidied('daoris/s-5a6b7c8d', 30),
    ]} />);
    expect(screen.getByRole('heading', { name: 'Cleaned up on its own' })).toBeInTheDocument();
    const [first, second] = screen.getAllByRole('listitem');
    expect(first).toHaveTextContent('daoris/s-1f2e3d4c');
    expect(first).toHaveTextContent('billing');
    expect(first).toHaveTextContent('Cleaned up 3h ago');
    expect(first).toHaveTextContent("Its tree's folder was set aside");
    expect(first).not.toHaveTextContent('trees/');
    expect(second).toHaveTextContent('Cleaned up 1d ago');
    expect(second).not.toHaveTextContent('set aside');
  });

  it.each([
    ['unread', 'unreadable'],
    ['refused', 'refused'],
    ['busy', 'in use'],
    ['hidden', 'hidden change'],
    ['nested', 'nested repo'],
    ['linked', 'linked'],
    ['starting', 'starting'],
    ['sessions', 'sessions unknown'],
  ])('words a branch the look left, for %s', (why, word) => {
    render(<WorkspaceTidied lines={[kept(why, 2)]} />);
    const row = screen.getByRole('listitem');
    expect(row).toHaveTextContent('daoris/s-aaaa1111');
    expect(row).toHaveTextContent('api');
    expect(row).toHaveTextContent('Left in place 2h ago');
    expect(row).toHaveTextContent(word);
  });

  it('draws a kept row without a branch where the log names none, and shows no unknown code as written', () => {
    render(<WorkspaceTidied lines={[kept('starting', 1, { branch: null }), kept('some-new-code', 1)]} />);
    const [none, unknown] = screen.getAllByRole('listitem');
    expect(none).not.toHaveTextContent('daoris/');
    expect(unknown).not.toHaveTextContent('some-new-code');
  });

  // The driver logs a kept branch once at each start of the window, so one branch kept for a week is a line a start (AUTOTIDY1a2).
  it('draws the lines of one branch kept for the same reason as one row: the newest time, and since the oldest', () => {
    const lines = Array.from({ length: 7 }, (_, index) => kept('unread', 2 + index * 24, { cause: 'folder' }));
    render(<WorkspaceTidied lines={lines} />);
    const row = screen.getByRole('listitem');
    expect(row).toHaveTextContent('Left in place 2h ago');
    expect(row).toHaveTextContent('since 6d ago');
    expect(row).toHaveTextContent('unreadable');
  });

  it('draws one row where a group is one line, with no since; and keeps apart another branch, repository, why or cause', () => {
    render(<WorkspaceTidied lines={[
      kept('unread', 1, { cause: 'folder' }),
      kept('unread', 2, { cause: 'index' }),
      kept('refused', 3, { cause: undefined }),
      kept('unread', 4, { cause: 'folder', branch: 'daoris/s-bbbb2222' }),
      kept('unread', 5, { cause: 'folder', repository: 'web' }),
    ]} />);
    const rows = screen.getAllByRole('listitem');
    expect(rows).toHaveLength(5);
    rows.forEach((row) => expect(row).not.toHaveTextContent('since'));
  });

  it.each([
    ['toplevel', 'checkout path', '检出位置'],
    ['refs', 'branch list', '分支列表'],
    ['list', 'tree list', '工作树列表'],
    ['folder', 'stray folder', '多出文件夹'],
    ['index', "tree's index", '工作树索引'],
    ['folders', 'folders unread', '文件夹读取'],
    ['pass', 'pass failed', '整理出错'],
    ['judged', 'item unread', '未能判断'],
  ])('words the cause %s after the reason, in English and Chinese', async (cause, en, zh) => {
    const { unmount } = render(<WorkspaceTidied lines={[kept('unread', 2, { cause })]} />);
    expect(screen.getByRole('listitem')).toHaveTextContent(`unreadable · ${en}`);
    unmount();
    await i18n.changeLanguage('zh');
    render(<WorkspaceTidied lines={[kept('unread', 2, { cause })]} />);
    expect(screen.getByRole('listitem')).toHaveTextContent(`读取不了 · ${zh}`);
  });

  it('shows the reason alone where the cause is one this page does not know, or none', () => {
    render(<WorkspaceTidied lines={[kept('unread', 2, { cause: 'some-new-cause' }), kept('unread', 3, { branch: 'daoris/s-cccc3333' })]} />);
    const [unknown, none] = screen.getAllByRole('listitem');
    expect(unknown).toHaveTextContent(/unreadable$/);
    expect(unknown).not.toHaveTextContent('some-new-cause');
    expect(none).toHaveTextContent(/unreadable$/);
  });

  it('shows five, and the rest behind Show N more', async () => {
    const lines = Array.from({ length: 7 }, (_, index) => tidied(`daoris/s-${index}`, index + 1));
    render(<WorkspaceTidied lines={lines} />);
    expect(screen.getAllByRole('listitem')).toHaveLength(5);
    await userEvent.click(screen.getByRole('button', { name: 'Show 2 more' }));
    expect(screen.getAllByRole('listitem')).toHaveLength(7);
    expect(screen.queryByRole('button', { name: /Show/ })).toBeNull();
  });

  it('reads in Chinese, with the glossary\'s words', async () => {
    await i18n.changeLanguage('zh');
    render(<WorkspaceTidied lines={[tidied('daoris/s-1', 3, { tree: true }), kept('busy', 2)]} />);
    expect(screen.getByRole('heading', { name: '自动清理的分支' })).toBeInTheDocument();
    const [done, left] = screen.getAllByRole('listitem');
    expect(done).toHaveTextContent('已清理');
    expect(done).toHaveTextContent('工作树');
    expect(left).toHaveTextContent('已保留');
    expect(left).toHaveTextContent('占用中');
  });
});
