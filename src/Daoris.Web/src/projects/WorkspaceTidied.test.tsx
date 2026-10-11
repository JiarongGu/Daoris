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
