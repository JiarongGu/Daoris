import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { BranchPage, type BranchRead } from './BranchPage';
import type { LandingDoor } from './LandingFlow';
import { SHARED_BRANCH, SHARED_DUE, SHARED_UNITS } from './landingFixtures';
import { GIT_BRANCH_PARTED, GIT_BRANCH_PARTED_PAGES, GIT_BRANCHES_ENGINE } from '../fixtures';

// A branch's page (UX6h3b, the built-in Git design §2.3): its header names it and its kind, says in one line where it stands
// against its line, against origin and its pull request, in the list's words, and goes back to Branches; beneath it, the
// branch beside its line since they parted, a page at a time. A molecule: each state reached by its props.

const branch = (name: string) => GIT_BRANCHES_ENGINE.branches.find((each) => each.name === name)!;
const PHYSICS = 'daoris/engine-physics-step';
const LINE_COMMIT = GIT_BRANCHES_ENGINE.line.commit!;
const drawn = [LINE_COMMIT, branch(PHYSICS).commit];

/** The branch as the list holds it, with its graph read. */
const listed = (name: string, graph: Extract<BranchRead, { state: 'read' }>['graph'] = parted(), title: string | null = null): BranchRead =>
  ({ state: 'read', value: { branch: branch(name), line: 'main', title }, graph });

function parted(more: Partial<Extract<Extract<BranchRead, { state: 'read' }>['graph'], { state: 'read' }>['value']> = {}) {
  return { state: 'read' as const, value: { tips: drawn, commits: GIT_BRANCH_PARTED, more: false, ...more } };
}

/** The page's header, inside its main area: the title's. */
const header = () => screen.getByRole('heading', { level: 1 }).closest('header')!;

const page = (read: BranchRead, name = PHYSICS, more: { onBack?: () => void; onMore?: () => void } = {}) =>
  render(<BranchPage repository="engine" name={name} read={read} onBack={more.onBack ?? (() => {})} onMore={more.onMore} />);

describe("a branch's page", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('names the branch and its kind in its header, and goes back to Branches', async () => {
    const onBack = vi.fn();
    page(listed(PHYSICS, parted(), 'Expose a streaming budget'), PHYSICS, { onBack });
    const head = header();
    expect(within(head).getByRole('heading', { level: 1, name: PHYSICS })).toBeInTheDocument();
    expect(head).toHaveTextContent('session branch');
    // A session's branch says its session's state and title, as the list does.
    expect(head).toHaveTextContent('working');
    expect(head).toHaveTextContent('Expose a streaming budget');

    await userEvent.click(within(head).getByRole('button', { name: 'Back to Branches' }));
    expect(onBack).toHaveBeenCalledTimes(1);
  });

  it('says in one line where it stands against its line, in the words its list row says', () => {
    page(listed(PHYSICS));
    expect(header()).toHaveTextContent('2 commits ahead of main, and 2 behind.');
  });

  it("says a landed branch's standing on origin and its pull request, with its state and when", () => {
    const { rerender } = page(listed('review/q6-asset-cache'), 'review/q6-asset-cache');
    const head = () => header();
    expect(head()).toHaveTextContent('landed');
    expect(head()).toHaveTextContent('In step with origin/review/q6-asset-cache.');
    expect(within(head()).getByRole('link', { name: 'Open the pull request' })).toHaveAttribute('href', 'https://code.example.invalid/engine/pull/12');
    expect(head()).toHaveTextContent('Open: waits for you to merge it on the platform.');
    expect(head()).toHaveTextContent('2h ago');

    rerender(<BranchPage repository="engine" name="review/q2-shader-cache" read={listed('review/q2-shader-cache')} onBack={() => {}} />);
    expect(head()).toHaveTextContent('Not pushed.');
    expect(within(head()).queryByRole('link')).toBeNull();
  });

  it("names yours and origin's by their kinds", () => {
    const { rerender } = page(listed('spike/renderer'), 'spike/renderer');
    expect(header()).toHaveTextContent('yours');
    expect(header()).toHaveTextContent('4 commits ahead of main, and 9 behind.');
    rerender(<BranchPage repository="engine" name="origin/release" read={listed('origin/release')} onBack={() => {}} />);
    expect(header()).toHaveTextContent("origin's");
  });

  it('draws the branch beside its line since they parted, newest first, with a lane each', () => {
    const { container } = page(listed(PHYSICS));
    expect(screen.getByText(/since it left/).closest('p')).toHaveTextContent(
      `The commits on ${PHYSICS} since it left main, beside those on main since then, newest first.`,
    );
    const list = screen.getByRole('list', { name: `${PHYSICS} beside main since they parted` });
    expect(within(list).getAllByRole('listitem').map((row) => row.getAttribute('data-commit'))).toEqual(GIT_BRANCH_PARTED.map((each) => each.commit));
    // The line's lane, the asset cache it merged beside it, and the branch's.
    expect(container.querySelector('svg[data-lanes]')!.getAttribute('data-lanes')).toBe('3');
    // No foot while the walk has no next page.
    expect(screen.queryByRole('button', { name: 'Show more' })).toBeNull();
  });

  it('pages the walk with the load-more foot: the count, Show more, and the focus on the first row it brought', async () => {
    const [first, second] = GIT_BRANCH_PARTED_PAGES;
    const onMore = vi.fn();
    const { rerender } = page(listed(PHYSICS, parted({ commits: first, more: true })), PHYSICS, { onMore });
    expect(screen.getByText('2 commits listed')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Show more' }));
    expect(onMore).toHaveBeenCalledTimes(1);

    rerender(<BranchPage repository="engine" name={PHYSICS} read={listed(PHYSICS, parted({ commits: [...first, ...second] }))} onBack={() => {}} onMore={onMore} />);
    expect(document.activeElement).toBe(document.querySelector(`[data-commit="${second[0]!.commit}"]`));
  });

  it("says the next page's refusal in the foot", () => {
    page(listed(PHYSICS, parted({ commits: GIT_BRANCH_PARTED_PAGES[0], more: true, moreError: 'Git could not answer for this.' })), PHYSICS, { onMore: () => {} });
    expect(screen.getByText('Git could not answer for this.')).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(2);
  });

  it('says a branch at its line\'s own commit has nothing since they parted', () => {
    page(listed(PHYSICS, parted({ commits: [] })));
    expect(screen.getByText(/is at the same commit as/).closest('p')).toHaveTextContent(
      `${PHYSICS} is at the same commit as main: neither holds one the other lacks.`,
    );
    expect(screen.queryByRole('list')).toBeNull();
  });

  it('says its states in its place, the header and its way back always there', () => {
    const { rerender } = page({ state: 'reading' });
    const back = () => screen.getByRole('button', { name: 'Back to Branches' });
    expect(screen.getByRole('heading', { level: 1, name: PHYSICS })).toBeInTheDocument();
    expect(screen.getByText(/Reading the commits on/)).toHaveTextContent(`Reading the commits on ${PHYSICS}…`);
    expect(back()).toBeInTheDocument();

    const again = (read: BranchRead) => rerender(<BranchPage repository="engine" name={PHYSICS} read={read} onBack={() => {}} />);
    again({ state: 'refused', sentence: 'Git could not answer for this.' });
    expect(screen.getByText('Git could not answer for this.')).toBeInTheDocument();
    again({ state: 'noCheckout' });
    expect(screen.getByText('No checkout on this machine')).toBeInTheDocument();
    // The list no longer holds it: said, with the way back.
    again({ state: 'gone' });
    expect(screen.getByText('No such branch here')).toBeInTheDocument();
    expect(screen.getByText(/holds no branch named/)).toHaveTextContent(`engine's checkout holds no branch named ${PHYSICS} now.`);
    expect(back()).toBeInTheDocument();
    // Listed, its graph on its way, refused, or its line with no commit here.
    again(listed(PHYSICS, { state: 'reading' }));
    expect(screen.getByText(/Reading the commits on/)).toBeInTheDocument();
    expect(header()).toHaveTextContent('session branch');
    again(listed(PHYSICS, { state: 'refused', sentence: 'That page of history does not exist.' }));
    expect(screen.getByText('That page of history does not exist.')).toBeInTheDocument();
    again(listed(PHYSICS, { state: 'noLine' }));
    expect(screen.getByText('No history yet')).toBeInTheDocument();
  });

  it('says itself in 中文', async () => {
    await i18n.changeLanguage('zh');
    page(listed(PHYSICS));
    const head = header();
    expect(head).toHaveTextContent('会话分支');
    expect(head).toHaveTextContent('领先 main 2 个提交，也落后 2 个。');
    expect(within(head).getByRole('button', { name: '回到分支' })).toBeInTheDocument();
    expect(screen.getByRole('list', { name: `${PHYSICS} 与 main 分开以来` })).toBeInTheDocument();
  });
});

describe("a landed branch's page: how it landed (FLOWCHART1c; the flowchart design §2.7)", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });
  const LANDED = 'review/q6-asset-cache';
  const shown = (door: LandingDoor) =>
    render(<BranchPage repository="engine" name={LANDED} read={listed(LANDED)} onBack={() => {}} landing={door} />);
  const section = () => screen.getByRole('region', { name: 'How it landed' });

  it('folds it to a line until it is opened, and asks for it only then', async () => {
    const onToggle = vi.fn();
    shown({ open: false, onToggle });
    const fold = within(section()).getByRole('button', { name: /How it landed/ });
    expect(fold).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('list', { name: `How ${LANDED} landed` })).toBeNull();
    await userEvent.click(fold);
    expect(onToggle).toHaveBeenCalledTimes(1);
  });

  it('draws it once read: the tries stacked, the landing and its advances, the push, the pull request, the end, the clean-up', () => {
    shown({ open: true, onToggle: () => {}, landing: { name: LANDED, branch: SHARED_BRANCH, due: SHARED_DUE, units: SHARED_UNITS } });
    const flow = screen.getByRole('list', { name: `How ${LANDED} landed` });
    const names = within(flow).getAllByRole('listitem').map((item) => item.getAttribute('data-flow-node'));
    // In reading order, rank by rank: the push, the pull request and the first advance each hang from the landing, beside each other.
    expect(names).toEqual([
      'due', 'try:0', 'stack:try:1', 'try:4', 'landed', 'advance:0', 'pushed', 'pull-request', 'stack:advance:1', 'end', 'cleanup', 'recovery',
    ]);
    expect(within(flow).getByRole('listitem', { name: /^its quest is held for your yes ×3/ })).toBeInTheDocument();
    expect(within(flow).getByRole('listitem', { name: /^Moved on by session s4 ×3/ })).toBeInTheDocument();
    const pull = within(flow).getByRole('listitem', { name: /^Its pull request/ });
    expect(within(pull).getByRole('link', { name: 'Open the pull request' })).toHaveAttribute('href', 'https://example.test/pull/42');
    expect(within(flow).getByRole('listitem', { name: /^Landed on/ })).toHaveTextContent('made for session s2');
  });

  it('says it is reading, refused, that no landing names the branch, and that the units were not read', () => {
    const { rerender } = shown({ open: true, onToggle: () => {}, reading: true });
    expect(section()).toHaveTextContent('Reading how it landed…');
    const again = (door: LandingDoor) => rerender(<BranchPage repository="engine" name={LANDED} read={listed(LANDED)} onBack={() => {}} landing={door} />);
    again({ open: true, onToggle: () => {}, refusal: 'the driver did not answer' });
    expect(section()).toHaveTextContent('How it landed could not be read: the driver did not answer');
    again({ open: true, onToggle: () => {}, landing: null });
    expect(section()).toHaveTextContent('No landing on this machine names this branch');
    again({ open: true, onToggle: () => {}, landing: { name: LANDED, branch: SHARED_BRANCH, units: null }, unitsUnread: true });
    expect(section()).toHaveTextContent('Where its work stands could not be read');
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    shown({ open: true, onToggle: () => {}, landing: { name: LANDED, branch: SHARED_BRANCH, due: SHARED_DUE, units: SHARED_UNITS } });
    expect(screen.getByRole('region', { name: '它是如何落地的' })).toBeInTheDocument();
    expect(screen.getByRole('list', { name: `${LANDED} 是如何落地的` })).toBeInTheDocument();
  });
});
