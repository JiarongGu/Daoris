import { afterEach, describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import i18n from '../i18n';
import { GIT_BRANCHES_ENGINE, GIT_HISTORY_ENGINE } from './fixtures';
import { BranchesTab, HistoryTab } from './RepositoryGit';

// A repository's Branches and History tabs (UX6h1, D150 §4.2, §4.4): molecules, each state reached by its props. The reads
// are this machine's (D147 §6): a tab says its answer is on its way, that this machine has no checkout, or why Git did not
// answer, in its place, so it never goes blank. History draws its line's commits with their lanes (graphLanes).

const tips = [GIT_HISTORY_ENGINE[0]!.commit];

describe("a repository's Branches tab", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says its branches are on their way', () => {
    render(<BranchesTab repository="engine" read={{ state: 'reading' }} />);
    expect(screen.getByText("Reading engine's branches…")).toBeInTheDocument();
  });

  it('says this machine has no checkout to read them from', () => {
    render(<BranchesTab repository="engine" read={{ state: 'noCheckout' }} />);
    expect(screen.getByText('No checkout on this machine')).toBeInTheDocument();
    expect(screen.getByText("engine's branches are read from its checkout, and this machine has none.")).toBeInTheDocument();
  });

  it('says why Git did not answer, in its place', () => {
    render(<BranchesTab repository="engine" read={{ state: 'refused', sentence: 'Git could not answer for this.' }} />);
    expect(screen.getByText('Git could not answer for this.')).toBeInTheDocument();
  });

  it('says a checkout with no commit holds no branch yet', () => {
    render(<BranchesTab repository="engine" read={{ state: 'read', value: { ...GIT_BRANCHES_ENGINE, branches: [] } }} />);
    expect(screen.getByText('No branches yet')).toBeInTheDocument();
  });

  it('lists its branches, each by its name and its last commit', () => {
    render(<BranchesTab repository="engine" read={{ state: 'read', value: GIT_BRANCHES_ENGINE }} />);
    const rows = within(screen.getByRole('list', { name: "engine's branches" })).getAllByRole('listitem');
    expect(rows.map((row) => row.querySelector('code')?.textContent)).toEqual(['main', 'daoris/engine-physics-step', 'spike/renderer']);
    expect(rows[1]).toHaveTextContent('Fix the physics step on a slow frame');
  });

  it('says its states in 中文', async () => {
    await i18n.changeLanguage('zh');
    const { rerender } = render(<BranchesTab repository="engine" read={{ state: 'noCheckout' }} />);
    expect(screen.getByText('本机没有检出')).toBeInTheDocument();
    rerender(<BranchesTab repository="engine" read={{ state: 'read', value: { ...GIT_BRANCHES_ENGINE, branches: [] } }} />);
    expect(screen.getByText('还没有分支')).toBeInTheDocument();
  });
});

describe("a repository's History tab", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says its history is on its way', () => {
    render(<HistoryTab repository="engine" read={{ state: 'reading' }} />);
    expect(screen.getByText("Reading engine's history…")).toBeInTheDocument();
  });

  it('says this machine has no checkout to read it from', () => {
    render(<HistoryTab repository="engine" read={{ state: 'noCheckout' }} />);
    expect(screen.getByText("engine's history is read from its checkout, and this machine has none.")).toBeInTheDocument();
  });

  it('says why Git did not answer, in its place', () => {
    render(<HistoryTab repository="engine" read={{ state: 'refused', sentence: 'That page of history does not exist.' }} />);
    expect(screen.getByText('That page of history does not exist.')).toBeInTheDocument();
  });

  it('says its line holds no commit here, or that nothing names its line', () => {
    const { rerender } = render(<HistoryTab repository="engine" read={{ state: 'noLine', line: 'main' }} />);
    expect(screen.getByText('No history yet')).toBeInTheDocument();
    expect(screen.getByText(/has no commit in this checkout, so there is no history to show\./).closest('p')).toHaveTextContent(
      "engine's line, main, has no commit in this checkout, so there is no history to show.",
    );
    rerender(<HistoryTab repository="engine" read={{ state: 'noLine', line: null }} />);
    expect(screen.getByText("No branch is named engine's line, so there is no history to show.")).toBeInTheDocument();
  });

  it('lists its line\'s commits newest first, with a lane drawn for each and a dot per commit', () => {
    const { container } = render(<HistoryTab repository="engine" read={{ state: 'read', value: { line: 'main', tips, commits: GIT_HISTORY_ENGINE } }} />);
    const list = screen.getByRole('list', { name: "engine's history on main" });
    const rows = within(list).getAllByRole('listitem');
    expect(rows.map((row) => row.getAttribute('data-commit'))).toEqual(GIT_HISTORY_ENGINE.map((commit) => commit.commit));
    expect(rows[0]).toHaveTextContent('Merge the asset cache');
    expect(rows[0]).toHaveTextContent('c4f1a20');
    expect(rows[0]).toHaveTextContent('Mei Lin');
    // The merged branch has a lane of its own beside the line's: two lanes, five dots, the merge's hollow.
    const graph = container.querySelector('svg[data-lanes]')!;
    expect(graph.getAttribute('data-lanes')).toBe('2');
    expect(graph.querySelectorAll('circle')).toHaveLength(5);
    expect(graph.querySelectorAll('circle[data-merge]')).toHaveLength(1);
  });

  it('says its states in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<HistoryTab repository="engine" read={{ state: 'noLine', line: 'main' }} />);
    expect(screen.getByText('还没有历史')).toBeInTheDocument();
  });
});
