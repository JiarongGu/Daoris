import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { GitDeletedRef } from '../../bridge/repositories/git';
import { DeletedRefs } from './DeletedRefs';

// ONEWAY1r: the git page lists a repository's deletes from the journal, each with its Undo. A molecule: the rows and the acts arrive as props.

const row = (over: Partial<GitDeletedRef> = {}): GitDeletedRef => ({
  act: 'd1', kind: 'branch', name: 'spike/renderer', commit: 'a'.repeat(40), at: new Date().toISOString(), press: 'daoris', recovery: 'refs/daoris/discarded/spike', closed: null,
  ...over,
});

afterEach(async () => { await i18n.changeLanguage('en'); });

describe('DeletedRefs', () => {
  it('draws nothing for a repository with no delete', () => {
    const { container } = render(<DeletedRefs rows={[]} acts={{ undo: vi.fn() }} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('lists each delete with who pressed it, its commit and its kept commits, and Undo undoes it by its act', async () => {
    const undo = vi.fn(async () => ({ refused: false, outcome: 'done', said: '' }));
    render(<DeletedRefs rows={[row(), row({ act: 'd2', name: 'v1', kind: 'tag', press: 'terminal', recovery: null })]} acts={{ undo }} />);

    const list = screen.getByRole('list');
    const [first, second] = within(list).getAllByRole('listitem');
    expect(first).toHaveTextContent('spike/renderer');
    expect(first).toHaveTextContent('At aaaaaaaaaaaa, deleted by Daoris. Its commits are kept for recovery.');
    expect(second).toHaveTextContent('deleted by you in the terminal.');
    expect(second).not.toHaveTextContent('kept for recovery');

    await userEvent.click(screen.getByRole('button', { name: 'Undo the delete of spike/renderer' }));
    expect(undo).toHaveBeenCalledWith('d1');
    expect(await screen.findByRole('status')).toHaveTextContent('Undone: it is back.');
    expect(screen.queryByRole('button', { name: 'Undo the delete of spike/renderer' })).toBeNull();
  });

  it('says why an Undo is shut, and offers none, in either language', async () => {
    const rows = [row({ closed: 'name-taken' }), row({ act: 'd2', closed: 'commit-gone' }), row({ act: 'd3', closed: 'undone' })];
    render(<DeletedRefs rows={rows} acts={{ undo: vi.fn() }} />);
    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.getByText('Undo is closed: a branch or tag named spike/renderer stands again.')).toBeInTheDocument();
    expect(screen.getByText('Undo is closed: its commit is no longer in the repository.')).toBeInTheDocument();
  });

  it('says the driver’s sentence where an Undo is refused, and speaks 中文', async () => {
    await i18n.changeLanguage('zh');
    const undo = vi.fn(async () => ({ refused: true, said: 'a branch named `x` already exists' }));
    render(<DeletedRefs rows={[row()]} acts={{ undo }} />);
    expect(screen.getByRole('heading')).toHaveTextContent('已删除的分支和标签');
    await userEvent.click(screen.getByRole('button', { name: '撤销删除 spike/renderer' }));
    expect(await screen.findByRole('status')).toHaveTextContent('a branch named `x` already exists');
  });
});
