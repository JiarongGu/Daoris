import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as renderPlain, screen, within, type RenderOptions } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { ReactElement, ReactNode } from 'react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { GIT_BRANCHES_ENGINE, GIT_HISTORY_ENGINE, GIT_HISTORY_PAGES } from '../fixtures';
import { BranchesTab, HistoryTab, type HistoryRead } from './RepositoryGit';

// A repository's Branches and History tabs (UX6h1, D150 §4.2, §4.4): molecules, each state reached by its props. The reads
// are this machine's (D147 §6): a tab says its answer is on its way, that this machine has no checkout, or why Git did not
// answer, in its place, so it never goes blank. History draws its line's commits with their lanes (graphLanes).

// The page's tooltips live under the app's provider; the list's ⋯ (UX6h2b1) carries one, so a case renders under its own.
const render = (ui: ReactElement, options?: RenderOptions) =>
  renderPlain(ui, { wrapper: ({ children }: { children: ReactNode }) => <Tooltip.Provider>{children}</Tooltip.Provider>, ...options });

const tips =[GIT_HISTORY_ENGINE[0]!.commit];

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

  it("lists its branches by kind: its line at the head, then sessions', landed and yours, and none only origin holds", () => {
    render(<BranchesTab repository="engine" read={read()} />);
    const list = screen.getByRole('region', { name: "engine's branches" });
    expect(within(list).getAllByRole('heading').map((heading) => heading.textContent)).toEqual(['Line', "Sessions' (2)", 'Landed (3)', 'Yours (7)']);
    expect(names("Sessions' (2)")).toEqual(['daoris/engine-physics-step', 'daoris/engine-audio-mix']);
    expect(names('Landed (3)')).toEqual(['review/q6-asset-cache', 'review/q4-input-map', 'review/q2-shader-cache']);
    // Each row is its name, its last commit and when, whole.
    expect(row("Sessions' (2)", 'daoris/engine-physics-step')).toHaveTextContent('Fix the physics step on a slow frame');
    expect(row("Sessions' (2)", 'daoris/engine-physics-step')).toHaveTextContent('1h ago');
    // Origin's are on the wire, and shown from the list's ⋯ (UX6h2b): not here.
    expect(list).not.toHaveTextContent('origin/release');
  });

  it('heads the list with its line: what set it, how it stands to origin, and when it was last fetched', () => {
    const { rerender } = render(<BranchesTab repository="engine" read={read()} />);
    const head = () => screen.getByRole('group', { name: 'Line' });
    expect(head()).toHaveTextContent('main');
    expect(head()).toHaveTextContent('Merge the asset cache');
    expect(head()).toHaveTextContent('main, set for the workspace default. In step with origin/main. Fetched 3h ago.');

    const line = (more: Partial<typeof GIT_BRANCHES_ENGINE.line>, fetch = GIT_BRANCHES_ENGINE.fetch) =>
      rerender(<BranchesTab repository="engine" read={read({ line: { ...GIT_BRANCHES_ENGINE.line, ...more }, fetch })} />);
    line({ ahead: 2 });
    expect(head()).toHaveTextContent('2 commits ahead of origin/main.');
    line({ behind: 1 });
    expect(head()).toHaveTextContent('1 commit behind origin/main.');
    line({ ahead: 1, behind: 3 });
    expect(head()).toHaveTextContent('1 commit ahead of origin/main, and 3 behind.');
    line({ ahead: null, behind: null, origin: 'd'.repeat(40) });
    expect(head()).toHaveTextContent('Differs from origin/main, and git could not count how.');
    line({ origin: null, ahead: null, behind: null });
    expect(head()).toHaveTextContent('No origin/main here.');
    line({ commit: null, ahead: null, behind: null });
    expect(head()).toHaveTextContent('This checkout holds no main.');
    // Git's own FETCH_HEAD time, or never fetched here, never a guess (the built-in Git design §2.2).
    line({}, null);
    expect(head()).toHaveTextContent('Never fetched here.');
    line({}, { at: new Date(Date.now() - 3 * 3_600_000).toISOString(), heard: false });
    expect(head()).toHaveTextContent('A fetch was tried 3h ago and heard nothing.');
  });

  it("names a session's branch by its session's title and state, with how far it stands from its line", () => {
    const titles = { 'daoris/engine-physics-step': 'Fix the slow frame' };
    const { rerender } = render(<BranchesTab repository="engine" read={read()} titles={titles} />);
    const named = row("Sessions' (2)", 'daoris/engine-physics-step');
    expect(named).toHaveTextContent('Fix the slow frame');
    expect(within(named).getByText('working')).toBeInTheDocument();
    expect(named).toHaveTextContent('2 commits ahead of main, and 2 behind.');
    // One no record names says so, and is still a session's by its namespace (D88).
    expect(row("Sessions' (2)", 'daoris/engine-audio-mix')).toHaveTextContent('No session record names it. 3 commits ahead of main.');

    // Where the records could not be read, the list says so once, beside the sessions'.
    rerender(<BranchesTab repository="engine" read={read({ sessionsUnread: true })} titles={titles} />);
    expect(screen.getByText('The session records could not be read, so no branch here is named by its session.')).toBeInTheDocument();
    expect(row("Sessions' (2)", 'daoris/engine-audio-mix')).not.toHaveTextContent('No session record names it.');
  });

  it('says whether each landed branch is pushed, and its pull request with its state and when', () => {
    const { rerender } = render(<BranchesTab repository="engine" read={read()} />);
    const open = row('Landed (3)', 'review/q6-asset-cache');
    expect(open).toHaveTextContent('In step with origin/review/q6-asset-cache.');
    expect(within(open).getByRole('link', { name: 'Open the pull request' })).toHaveAttribute('href', 'https://code.example.invalid/engine/pull/12');
    expect(open).toHaveTextContent('Open: waits for you to merge it on the platform.');
    expect(within(open).getByText('2h ago')).toBeInTheDocument();
    const merged = row('Landed (3)', 'review/q4-input-map');
    expect(merged).toHaveTextContent('Pushed, and gone from origin since.');
    expect(merged).toHaveTextContent('Merged on the platform.');
    expect(within(merged).getByText('1d ago')).toBeInTheDocument();
    const kept = row('Landed (3)', 'review/q2-shader-cache');
    expect(kept).toHaveTextContent('Not pushed.');
    expect(within(kept).queryByRole('link')).toBeNull();

    // Each other standing, and the plugin's other answers: abandoned, no state, and an ask that failed since.
    const landed = GIT_BRANCHES_ENGINE.branches[3]!;
    const as = (more: Partial<NonNullable<typeof landed.landed>>) => rerender(
      <BranchesTab repository="engine" read={read({ branches: [{ ...landed, landed: { ...landed.landed!, ...more } }] })} />,
    );
    const shown = () => row('Landed (1)', 'review/q6-asset-cache');
    as({ origin: 'ahead', originAhead: 2, originBehind: 0 });
    expect(shown()).toHaveTextContent('2 commits ahead of origin/review/q6-asset-cache.');
    as({ origin: 'behind', originAhead: 0, originBehind: 1 });
    expect(shown()).toHaveTextContent('1 commit behind origin/review/q6-asset-cache.');
    as({ origin: 'diverged', originAhead: 2, originBehind: 5 });
    expect(shown()).toHaveTextContent('2 commits ahead of origin/review/q6-asset-cache, and 5 behind.');
    as({ origin: 'unknown' });
    expect(shown()).toHaveTextContent('Differs from origin/review/q6-asset-cache, and git could not count how.');
    as({ pullRequestState: { ...landed.landed!.pullRequestState!, state: 'abandoned' } });
    expect(shown()).toHaveTextContent('Abandoned on the platform.');
    as({ pullRequestState: { ...landed.landed!.pullRequestState!, state: 'unknown' } });
    expect(shown()).toHaveTextContent('Waits for you to merge it on the platform; azure last answered no state for it.');
    as({ pullRequestAskFailed: { code: 'timeout', plugin: 'azure', at: new Date(Date.now() - 60 * 60_000).toISOString() } });
    expect(shown()).toHaveTextContent('Could not read azure: timeout.');
    expect(within(shown()).getByText('1h ago')).toBeInTheDocument();
  });

  it('folds yours after five, and the press shows the rest with the focus on the first it brought', async () => {
    render(<BranchesTab repository="engine" read={read()} />);
    expect(names('Yours (7)')).toEqual(['spike/renderer', 'try/ecs', 'notes/frame-budget', 'spike/vulkan', 'fix/readme']);
    expect(row('Yours (7)', 'spike/renderer')).toHaveTextContent('4 commits ahead of main, and 9 behind.');

    await userEvent.click(screen.getByRole('button', { name: 'Show 2 more' }));
    expect(names('Yours (7)')).toHaveLength(7);
    expect(screen.queryByRole('button', { name: /^Show \d+ more$/ })).toBeNull();
    expect(row('Yours (7)', 'experiment/jobs')).toHaveFocus();
  });

  it("keeps origin's branches behind the list's ⋯: drawn only where some exist, shown by its checked item, hidden again", async () => {
    const { rerender } = render(<BranchesTab repository="engine" read={read()} />);
    const more = screen.getByRole('button', { name: "Show origin's branches" });
    expect(screen.queryByRole('list', { name: /^Origin's/ })).toBeNull();

    await opened(more);
    const item = screen.getByRole('menuitemcheckbox', { name: "Show origin's branches (1)" });
    expect(item).toHaveAttribute('aria-checked', 'false');
    await userEvent.click(item);
    expect(names("Origin's (1)")).toEqual(['origin/release']);
    expect(row("Origin's (1)", 'origin/release')).toHaveTextContent(/ago/);
    expect(more).toHaveFocus();

    await opened(more);
    expect(screen.getByRole('menuitemcheckbox', { name: "Show origin's branches (1)" })).toHaveAttribute('aria-checked', 'true');
    await userEvent.click(screen.getByRole('menuitemcheckbox', { name: "Show origin's branches (1)" }));
    expect(screen.queryByRole('list', { name: /^Origin's/ })).toBeNull();

    // None only origin holds: no ⋯.
    rerender(<BranchesTab repository="engine" read={read({ branches: GIT_BRANCHES_ENGINE.branches.filter((branch) => branch.kind !== 'origin') })} />);
    expect(screen.queryByRole('button', { name: "Show origin's branches" })).toBeNull();
  });

  // UX6h3b (the built-in Git design §2.3): a branch's name opens its page; the line's page is the repository's history.
  it("opens a branch's page from its name, origin's once shown, and History from the line's own row", async () => {
    const onOpen = vi.fn();
    const onOpenHistory = vi.fn();
    render(<BranchesTab repository="engine" read={read()} onOpen={onOpen} onOpenHistory={onOpenHistory} />);

    await userEvent.click(screen.getByRole('button', { name: 'Open daoris/engine-physics-step' }));
    expect(onOpen).toHaveBeenLastCalledWith('daoris/engine-physics-step');
    await userEvent.click(screen.getByRole('button', { name: 'Open review/q4-input-map' }));
    expect(onOpen).toHaveBeenLastCalledWith('review/q4-input-map');
    await userEvent.click(screen.getByRole('button', { name: 'Open spike/renderer' }));
    expect(onOpen).toHaveBeenLastCalledWith('spike/renderer');
    // The name is still the row's whole name, its press a ghost button around it.
    expect(names('Yours (7)')[0]).toBe('spike/renderer');

    await opened(screen.getByRole('button', { name: "Show origin's branches" }));
    await userEvent.click(screen.getByRole('menuitemcheckbox', { name: "Show origin's branches (1)" }));
    await userEvent.click(screen.getByRole('button', { name: 'Open origin/release' }));
    expect(onOpen).toHaveBeenLastCalledWith('origin/release');

    await userEvent.click(within(screen.getByRole('group', { name: 'Line' })).getByRole('button', { name: "Open main's history" }));
    expect(onOpenHistory).toHaveBeenCalledTimes(1);
    expect(onOpen).toHaveBeenCalledTimes(4);
  });

  it('offers no press where none is handed, where a branch has no commit, or where its line has none here', () => {
    const { rerender } = render(<BranchesTab repository="engine" read={read()} />);
    expect(screen.queryAllByRole('button', { name: /^Open / })).toEqual([]);

    // A branch with no commit of its own has nothing to draw; the others keep theirs.
    const [line, physics, ...rest] = GIT_BRANCHES_ENGINE.branches;
    const pressed = { onOpen: vi.fn(), onOpenHistory: vi.fn() };
    rerender(<BranchesTab repository="engine" read={read({ branches: [line!, { ...physics!, commit: '' }, ...rest] })} {...pressed} />);
    expect(screen.queryByRole('button', { name: 'Open daoris/engine-physics-step' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Open daoris/engine-audio-mix' })).toBeInTheDocument();

    // A line with no commit here: no branch can be drawn beside it, so no row but the line's own opens.
    rerender(<BranchesTab repository="engine" read={read({ line: { ...GIT_BRANCHES_ENGINE.line, commit: null } })} {...pressed} />);
    expect(screen.queryAllByRole('button', { name: /^Open / }).map((button) => button.getAttribute('aria-label'))).toEqual(["Open main's history"]);
  });

  it("says origin's branches in 中文", async () => {
    await i18n.changeLanguage('zh');
    render(<BranchesTab repository="engine" read={read()} />);
    await opened(screen.getByRole('button', { name: '显示 origin 的分支' }));
    await userEvent.click(screen.getByRole('menuitemcheckbox', { name: '显示 origin 的分支（1）' }));
    expect(names('origin 的分支（1）')).toEqual(['origin/release']);
  });

  it('says its states in 中文', async () => {
    await i18n.changeLanguage('zh');
    const { rerender } = render(<BranchesTab repository="engine" read={{ state: 'noCheckout' }} />);
    expect(screen.getByText('本机没有检出')).toBeInTheDocument();
    rerender(<BranchesTab repository="engine" read={{ state: 'read', value: { ...GIT_BRANCHES_ENGINE, branches: [] } }} />);
    expect(screen.getByText('还没有分支')).toBeInTheDocument();
    rerender(<BranchesTab repository="engine" read={read({ sessionsUnread: true })} />);
    const list = screen.getByRole('region', { name: 'engine 的分支' });
    expect(within(list).getAllByRole('heading').map((heading) => heading.textContent)).toEqual(['主线', '会话分支（2）', '已落地（3）', '你的（7）']);
    expect(screen.getByRole('group', { name: '主线' })).toHaveTextContent('与 origin/main 一致。上次获取：3 小时前。');
    expect(screen.getByText('无法读取会话记录，所以这里没有分支以它的会话命名。')).toBeInTheDocument();
    expect(row('已落地（3）', 'review/q2-shader-cache')).toHaveTextContent('未推送。');
    expect(screen.getByRole('button', { name: '再显示 2 个' })).toBeInTheDocument();
    rerender(<BranchesTab repository="engine" read={read()} onOpen={() => {}} onOpenHistory={() => {}} />);
    expect(screen.getByRole('button', { name: '打开 daoris/engine-physics-step' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '打开 main 的历史' })).toBeInTheDocument();
  });
});

/** A menu opened from its trigger by the keyboard, as the doors' tests open one (a jsdom pointer does not open Radix's). */
async function opened(trigger: HTMLElement) {
  trigger.focus();
  await userEvent.keyboard('{Enter}');
  await screen.findByRole('menuitemcheckbox');
}

/** The engine's list as read, with what a case changes. */
function read(more: Partial<typeof GIT_BRANCHES_ENGINE> & { sessionsUnread?: boolean } = {}) {
  return { state: 'read' as const, value: { ...GIT_BRANCHES_ENGINE, ...more } };
}

/** The names a kind's list shows, in its order. */
const names = (kind: string) =>
  within(screen.getByRole('list', { name: kind })).getAllByRole('listitem').map((each) => each.querySelector('code')?.textContent);

/** A kind's row, found by its branch's name. */
const row = (kind: string, name: string) =>
  within(screen.getByRole('list', { name: kind })).getAllByRole('listitem').find((each) => each.querySelector('code')?.textContent === name)!;

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
    const { container } = render(<HistoryTab repository="engine" read={{ state: 'read', value: { line: 'main', tips, commits: GIT_HISTORY_ENGINE, more: false } }} />);
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

  it('lets its rows give way to the tab\'s width, so a long subject truncates and the id, author and time stay in view', () => {
    // jsdom lays nothing out: the class is what the window needed. Without it the graph, a grid item, grew to its longest
    // subject and pushed the row's last columns past the tab (the installed window, 2026-10-11; FIX-LOG).
    render(<HistoryTab repository="engine" read={{ state: 'read', value: { line: 'main', tips, commits: GIT_HISTORY_ENGINE, more: false } }} />);
    expect(screen.getByRole('list', { name: "engine's history on main" }).parentElement).toHaveClass('min-w-0');
  });

  it('says its states in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<HistoryTab repository="engine" read={{ state: 'noLine', line: 'main' }} />);
    expect(screen.getByText('还没有历史')).toBeInTheDocument();
  });
});

// The graph's pages (UX6h3a, the built-in Git design §2.3; UXLOADMORE1): the count of what is listed and *Show more* while the
// graph answers `more`, with no total, as Ask Daoris's history foots its own.
describe("a repository's History tab, a page at a time", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  const [first, second] = GIT_HISTORY_PAGES;
  const paged = (value: Partial<Extract<HistoryRead, { state: 'read' }>['value']> = {}): HistoryRead =>
    ({ state: 'read', value: { line: 'main', tips, commits: first!, more: true, ...value } });

  it('counts what is listed, with no total, and offers Show more while the graph has another page', () => {
    render(<HistoryTab repository="engine" read={paged()} onMore={() => {}} />);
    expect(screen.getByText('3 commits listed')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Show more' })).toBeEnabled();
  });

  it('says one commit in the singular', () => {
    render(<HistoryTab repository="engine" read={paged({ commits: first!.slice(0, 1) })} onMore={() => {}} />);
    expect(screen.getByText('1 commit listed')).toBeInTheDocument();
  });

  it('keeps the foot only while a next page exists', () => {
    render(<HistoryTab repository="engine" read={paged({ commits: [...first!, ...second!], more: false })} onMore={() => {}} />);
    expect(screen.queryByRole('button', { name: 'Show more' })).toBeNull();
    expect(screen.queryByText(/commits listed/)).toBeNull();
  });

  it('has its status from the first and empty, and says the wait in it', () => {
    const { rerender } = render(<HistoryTab repository="engine" read={paged()} onMore={() => {}} />);
    const status = screen.getByRole('status');
    expect(status).toBeEmptyDOMElement();
    rerender(<HistoryTab repository="engine" read={paged({ loadingMore: true })} onMore={() => {}} />);
    expect(screen.getByRole('status')).toBe(status);
    expect(status).toHaveTextContent('Reading more commits…');
  });

  it('says why the next page did not come, in the foot, and keeps the commits it has', () => {
    render(<HistoryTab repository="engine" read={paged({ moreError: 'Git could not answer for this.' })} onMore={() => {}} />);
    expect(within(screen.getByRole('button', { name: 'Show more' }).closest('div')!.parentElement!).getByText('Git could not answer for this.')).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(first!.length);
  });

  it('asks for the next page on a press', async () => {
    const onMore = vi.fn();
    render(<HistoryTab repository="engine" read={paged()} onMore={onMore} />);
    await userEvent.click(screen.getByRole('button', { name: 'Show more' }));
    expect(onMore).toHaveBeenCalledTimes(1);
  });

  it('asks nothing more while it reads', async () => {
    const onMore = vi.fn();
    render(<HistoryTab repository="engine" read={paged({ loadingMore: true })} onMore={onMore} />);
    await userEvent.click(screen.getByRole('button', { name: 'Show more' }));
    expect(onMore).not.toHaveBeenCalled();
  });

  it('brings the focus to the first commit the press brought', async () => {
    const { rerender } = render(<HistoryTab repository="engine" read={paged()} onMore={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Show more' }));
    rerender(<HistoryTab repository="engine" read={paged({ commits: [...first!, ...second!], more: false })} onMore={() => {}} />);
    expect(document.activeElement).toBe(document.querySelector(`[data-commit="${second![0]!.commit}"]`));
  });

  it('leaves the focus where the reader went meanwhile', async () => {
    const { rerender } = render(<><input aria-label="elsewhere" /><HistoryTab repository="engine" read={paged()} onMore={() => {}} /></>);
    await userEvent.click(screen.getByRole('button', { name: 'Show more' }));
    screen.getByLabelText('elsewhere').focus();
    rerender(<><input aria-label="elsewhere" /><HistoryTab repository="engine" read={paged({ commits: [...first!, ...second!], more: false })} onMore={() => {}} /></>);
    expect(document.activeElement).toBe(screen.getByLabelText('elsewhere'));
  });

  it('says its foot in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<HistoryTab repository="engine" read={paged()} onMore={() => {}} />);
    expect(screen.getByText('已列出 3 个提交')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '显示更多' })).toBeInTheDocument();
  });
});

// *All branches* (UX6h3c, the built-in Git design §2.3): the ⋯ in History's head, one checked item, the page's state; ticked,
// the head says the graph is the repository's over its branches, and past the read's limit how many it drew.
describe("a repository's History tab, over all its branches", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  const read: HistoryRead = { state: 'read', value: { line: 'main', tips, commits: GIT_HISTORY_ENGINE, more: false } };

  it('offers *All branches* from a ⋯ in its head, ticked while shown, and tells its holder when it is pressed', async () => {
    const onToggle = vi.fn();
    const { rerender } = render(<HistoryTab repository="engine" read={read} all={{ on: false, onToggle }} />);
    expect(screen.getByText(/newest first\.$/)).toHaveTextContent("engine's line, main, newest first.");
    await opened(screen.getByRole('button', { name: 'History options' }));
    const item = screen.getByRole('menuitemcheckbox', { name: 'All branches' });
    expect(item).toHaveAttribute('aria-checked', 'false');
    await userEvent.click(item);
    expect(onToggle).toHaveBeenCalledTimes(1);

    rerender(<HistoryTab repository="engine" read={read} all={{ on: true, onToggle }} />);
    expect(screen.getByText(/every local branch and origin's/)).toBeInTheDocument();
    expect(screen.getByRole('list', { name: "engine's history over all its branches" })).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    await opened(screen.getByRole('button', { name: 'History options' }));
    expect(screen.getByRole('menuitemcheckbox', { name: 'All branches' })).toHaveAttribute('aria-checked', 'true');
  });

  it('draws no ⋯ where it is not handed', () => {
    render(<HistoryTab repository="engine" read={read} />);
    expect(screen.queryByRole('button', { name: 'History options' })).toBeNull();
  });

  it('keeps the ⋯ while ticked and the graph is on its way or refused, so it can be unticked', () => {
    const all = { on: true, onToggle: () => {} };
    const { rerender } = render(<HistoryTab repository="engine" read={{ state: 'reading' }} all={all} />);
    expect(screen.getByRole('button', { name: 'History options' })).toBeInTheDocument();
    rerender(<HistoryTab repository="engine" read={{ state: 'refused', sentence: 'Git could not answer for this.' }} all={all} />);
    expect(screen.getByRole('button', { name: 'History options' })).toBeInTheDocument();
  });

  it('says past the limit that it drew the first of the branches', () => {
    render(<HistoryTab repository="engine" read={read} all={{ on: true, onToggle: () => {}, limit: { shown: 256, total: 300 } }} />);
    expect(screen.getByText(/the first 256 of 300 branches/)).toBeInTheDocument();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<HistoryTab repository="engine" read={read} all={{ on: true, onToggle: () => {}, limit: { shown: 256, total: 300 } }} />);
    expect(screen.getByText(/300 个分支中的前 256 个/)).toBeInTheDocument();
    await opened(screen.getByRole('button', { name: '历史选项' }));
    expect(screen.getByRole('menuitemcheckbox', { name: '所有分支' })).toBeInTheDocument();
  });
});
