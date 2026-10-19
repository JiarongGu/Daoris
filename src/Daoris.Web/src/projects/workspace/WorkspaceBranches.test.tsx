import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { InclusionContext, WorkspaceBranches } from './WorkspaceBranches';

// The branch list in a workspace's Branches tab (UX6h2b2, the built-in Git design §2.2, D112): a molecule, each state reached
// by its props. Each repository holding a branch of Daoris's is a group headed by its door, folded after five; the others
// with a checkout here are folded apart, each drawn once the person includes it. A group's list is its holder's, asked for
// only where the group shows.

/** A group's list as its holder would draw it, and the groups it was asked for. */
function holder() {
  const asked = new Set<string>();
  const group = (repository: string, included: boolean) => {
    asked.add(repository);
    return <p>{`${repository}'s list${included ? ', included' : ''}`}</p>;
  };
  return { asked, group };
}

/** The groups shown, by their headings, in order. */
const groups = () => screen.queryAllByRole('heading', { level: 2 }).map((heading) => heading.textContent);

const read = (held: string[], apart: string[] = []) => ({ state: 'read' as const, value: { held, apart } });

describe("a workspace's Branches tab's list", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it("says its repositories' branches are on their way, or why they did not come, in its place", () => {
    const { group } = holder();
    const { rerender } = render(<WorkspaceBranches workspace="work" read={{ state: 'reading' }} group={group} onOpen={() => {}} />);
    expect(screen.getByText("Reading the branches of work's repositories…")).toBeInTheDocument();

    rerender(<WorkspaceBranches workspace="work" read={{ state: 'refused', sentence: 'the driver is still coming up.' }} group={group} onOpen={() => {}} />);
    expect(screen.getByText('the driver is still coming up.')).toBeInTheDocument();
  });

  it('says no repository of it has a checkout here, and draws no group', () => {
    const { asked, group } = holder();
    render(<WorkspaceBranches workspace="work" read={read([])} group={group} onOpen={() => {}} />);
    expect(screen.getByText('No checkout on this machine')).toBeInTheDocument();
    expect(screen.getByText("None of work's repositories has a checkout on this machine, so there are no branches to list.")).toBeInTheDocument();
    expect(asked.size).toBe(0);
  });

  it("draws a group for each repository holding Daoris's branches, in the driver's order, each heading the door to its page", async () => {
    const { group } = holder();
    const onOpen = vi.fn();
    render(<WorkspaceBranches workspace="work" read={read(['billing', 'api'])} group={group} onOpen={onOpen} />);
    expect(groups()).toEqual(['billing', 'api']);
    expect(screen.getByText("billing's list")).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Open api' }));
    expect(onOpen).toHaveBeenCalledWith('api');
    // Nothing apart: no fold.
    expect(screen.queryByRole('group', { name: 'Repositories not listed' })).toBeNull();
  });

  it('folds the groups after five, asks for no hidden one, and brings the rest by its press with the focus on the first', async () => {
    const { asked, group } = holder();
    const held = ['api', 'billing', 'gateway', 'portal', 'reports', 'search', 'workers'];
    render(<WorkspaceBranches workspace="work" read={read(held)} group={group} onOpen={() => {}} />);
    expect(groups()).toEqual(held.slice(0, 5));
    expect([...asked]).toEqual(held.slice(0, 5));

    await userEvent.click(screen.getByRole('button', { name: 'Show 2 more' }));
    expect(groups()).toEqual(held);
    expect(screen.queryByRole('button', { name: /^Show \d+ more$/ })).toBeNull();
    expect(screen.getByRole('button', { name: 'Open search' })).toHaveFocus();
  });

  it('folds the repositories apart, and draws each one the person includes after the others, the focus on the first', async () => {
    const { asked, group } = holder();
    render(<WorkspaceBranches workspace="work" read={read(['api'], ['game', 'tools', 'site'])} group={group} onOpen={() => {}} />);
    expect(groups()).toEqual(['api']);
    expect([...asked]).toEqual(['api']);

    await userEvent.click(screen.getByText("3 repositories with a checkout here hold no branch of Daoris's, and are not listed"));
    const apart = within(screen.getByRole('group', { name: 'Repositories not listed' }));
    expect(apart.getByRole('button', { name: 'Include (0)' })).toBeDisabled();
    await userEvent.click(apart.getByLabelText('tools'));
    await userEvent.click(apart.getByRole('button', { name: 'Include (1)' }));

    expect(groups()).toEqual(['api', 'tools']);
    expect(screen.getByText("tools's list, included")).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Open tools' })).toHaveFocus();
    expect(screen.getByText("2 repositories with a checkout here hold no branch of Daoris's, and are not listed")).toBeInTheDocument();

    // Every one left, by the box for all of them (the fold stays open): drawn in the driver's order, and the fold is gone.
    const left = within(screen.getByRole('group', { name: 'Repositories not listed' }));
    expect(left.queryByLabelText('tools')).toBeNull();
    await userEvent.click(left.getByLabelText('All 2'));
    await userEvent.click(left.getByRole('button', { name: 'Include (2)' }));
    expect(groups()).toEqual(['api', 'game', 'tools', 'site']);
    expect(screen.queryByRole('group', { name: 'Repositories not listed' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Open game' })).toHaveFocus();
  });

  /**
   * UX6h2b3: the fold is the one place a workspace's repositories apart are included, so its include is held by the Branches
   * part and handed up, where *Updates*' next look reads it. A held set draws its groups, and a press hands the ticked ones up.
   */
  it('hands what it includes to the page that holds it, and draws the groups that page says are included', async () => {
    const { group } = holder();
    const include = vi.fn();
    const { rerender } = render(
      <InclusionContext.Provider value={{ included: [], include }}>
        <WorkspaceBranches workspace="work" read={read(['api'], ['game', 'tools'])} group={group} onOpen={() => {}} />
      </InclusionContext.Provider>,
    );
    await userEvent.click(screen.getByText("2 repositories with a checkout here hold no branch of Daoris's, and are not listed"));
    const apart = within(screen.getByRole('group', { name: 'Repositories not listed' }));
    await userEvent.click(apart.getByLabelText('tools'));
    await userEvent.click(apart.getByRole('button', { name: 'Include (1)' }));
    expect(include).toHaveBeenCalledWith(['tools']);
    // Nothing is drawn until the holder says so: the set is the holder's.
    expect(groups()).toEqual(['api']);

    rerender(
      <InclusionContext.Provider value={{ included: ['tools'], include }}>
        <WorkspaceBranches workspace="work" read={read(['api'], ['game', 'tools'])} group={group} onOpen={() => {}} />
      </InclusionContext.Provider>,
    );
    expect(groups()).toEqual(['api', 'tools']);
    expect(screen.getByText("tools's list, included")).toBeInTheDocument();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    const { group } = holder();
    const { rerender } = render(<WorkspaceBranches workspace="work" read={{ state: 'reading' }} group={group} onOpen={() => {}} />);
    expect(screen.getByText('正在读取 work 各仓库的分支…')).toBeInTheDocument();

    rerender(<WorkspaceBranches workspace="work" read={read(['api'], ['game'])} group={group} onOpen={() => {}} />);
    expect(screen.getByRole('button', { name: '打开 api' })).toBeInTheDocument();
    await userEvent.click(screen.getByText('1 个有检出的仓库不持有 Daoris 的分支，没有列出'));
    const apart = within(screen.getByRole('group', { name: '未列出的仓库' }));
    await userEvent.click(apart.getByLabelText('全部 1 个'));
    expect(apart.getByRole('button', { name: '纳入（1）' })).toBeEnabled();
  });
});
