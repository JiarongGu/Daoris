import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { SyncSection, type LinePull, type RebaseBranch, type SyncPlan, type SyncRepository } from './Sync';
import type { LandedBranch } from './Sweep';

// WSR6 (D109): after a pull request merges — each line pulled by a fast-forward, the branches still at work
// replayed onto it, the landed branches whose work reached it deleted. Listed first, done by one press, and the
// list is asked for, never fetched on its own.

const pull = (extra: Partial<LinePull> & Pick<LinePull, 'kind'>): LinePull => ({
  repository: 'engine', workspace: 'aurora', line: 'main', commits: 0, moves: false, ...extra,
});

const rebase = (extra: Partial<RebaseBranch> & Pick<RebaseBranch, 'branch' | 'kind'>): RebaseBranch => ({
  repository: 'engine', workspace: 'aurora', landed: false, onto: 'main', commits: 0, replays: false, ...extra,
});

const landed = (extra: Partial<LandedBranch> & Pick<LandedBranch, 'branch' | 'kind'>): LandedBranch => ({
  repository: 'engine', workspace: 'aurora', files: [], commits: 1, removable: false, ...extra,
});

const PLAN: SyncPlan = {
  lines: [
    pull({ kind: 'fast-forward', commits: 1, moves: true }),
    pull({ repository: 'game', kind: 'dirty' }),
  ],
  rebases: [
    rebase({ branch: 'daoris/s-step', kind: 'replay', commits: 1, cutBy: 'record', grewFrom: 'daoris/s-parent', replays: true }),
    rebase({ branch: 'feature/q2-second', kind: 'replay', landed: true, commits: 1, cutBy: 'record', replays: true }),
    rebase({ branch: 'daoris/s-busy', kind: 'in-use' }),
    rebase({ branch: 'feature/q3-open', kind: 'pushed', landed: true }),
    rebase({ branch: 'daoris/s-waits', kind: 'grew-from-unlanded', grewFrom: 'feature/q3-open' }),
  ],
  deletes: [
    landed({ branch: 'feature/q1-first', kind: 'on-line', where: 'origin/main', removable: true }),
  ],
};

const draw = (props: Partial<Parameters<typeof SyncSection>[0]> = {}) => {
  const onSync = vi.fn();
  const onLook = vi.fn();
  render(
    <Tooltip.Provider>
      <SyncSection plan={PLAN} onLook={onLook} onSync={onSync} {...props} />
    </Tooltip.Provider>,
  );
  return { onSync, onLook };
};

describe('bringing repositories up to date', () => {
  it('is asked for: before a look it lists nothing, and says the look fetches while nothing of yours moves', async () => {
    const { onLook } = draw({ plan: undefined });

    expect(screen.queryByRole('listitem')).toBeNull();
    expect(screen.getByText(/never pushes/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Look for updates' }));
    expect(onLook).toHaveBeenCalledWith([]);
  });

  describe('which repositories it takes (D112)', () => {
    const SCOPE: SyncRepository[] = [
      { repository: 'engine', workspace: 'aurora', holds: true },
      { repository: 'alpha', workspace: 'aurora', holds: false },
      { repository: 'beta', workspace: 'aurora', holds: false },
    ];

    it('says before a look how many repositories hold Daoris\'s branches, and lists the rest apart, collapsed', () => {
      draw({ plan: undefined, scope: SCOPE });

      expect(screen.getByText(/A look fetches the 1 repository that holds a branch of Daoris's/)).toBeInTheDocument();
      const summary = screen.getByText("2 other repositories with a checkout here hold no branch of Daoris's");
      expect(summary.closest('details')).not.toHaveAttribute('open');
    });

    it('looks at the ticked ones beside the default, and at every one when all are ticked', async () => {
      const { onLook } = draw({ plan: undefined, scope: SCOPE });

      await userEvent.click(screen.getByText("2 other repositories with a checkout here hold no branch of Daoris's"));
      expect(screen.getByRole('button', { name: 'Include and look (0)' })).toBeDisabled();
      await userEvent.click(screen.getByRole('checkbox', { name: 'beta' }));
      await userEvent.click(screen.getByRole('button', { name: 'Include and look (1)' }));
      expect(onLook).toHaveBeenLastCalledWith(['beta']);

      await userEvent.click(screen.getByRole('checkbox', { name: 'All 2' }));
      await userEvent.click(screen.getByRole('button', { name: 'Include and look (2)' }));
      expect(onLook).toHaveBeenLastCalledWith('all');
    });

    it('keeps what was included when it looks again, and lists apart only what the look left', async () => {
      const { onLook } = draw({
        plan: { ...PLAN, looked: [SCOPE[0], SCOPE[2]], apart: [SCOPE[1]] }, scope: SCOPE, included: ['beta'],
      });

      expect(screen.getByText("1 other repository with a checkout here holds no branch of Daoris's")).toBeInTheDocument();
      await userEvent.click(screen.getByRole('button', { name: 'Look again' }));
      expect(onLook).toHaveBeenLastCalledWith(['beta']);

      await userEvent.click(screen.getByText("1 other repository with a checkout here holds no branch of Daoris's"));
      await userEvent.click(screen.getByRole('checkbox', { name: 'alpha' }));
      await userEvent.click(screen.getByRole('button', { name: 'Include and look (1)' }));
      // Ticking each by name asks for those by name: only the box for all of them asks for every one.
      expect(onLook).toHaveBeenLastCalledWith(['beta', 'alpha']);
    });

    it('says so when no repository holds a branch of Daoris\'s', () => {
      draw({ plan: { lines: [], rebases: [], deletes: [], looked: [], apart: [SCOPE[1]] } });

      expect(screen.getByText(/No repository here holds a branch of Daoris's, so there was nothing/)).toBeInTheDocument();
      expect(screen.queryByText('Everything here is up to date.')).toBeNull();
    });
  });

  it('lists each line, each branch and each landed branch with what the press would do', () => {
    draw();

    const row = (name: string) => screen.getByRole('listitem', { name });
    const engine = within(screen.getByRole('group', { name: 'engine' }));
    expect(within(engine.getByRole('listitem', { name: 'main' })).getByText('moves')).toBeInTheDocument();
    expect(within(engine.getByRole('listitem', { name: 'main' })).getByText(/Fast-forwards 1 commit to/)).toBeInTheDocument();
    expect(within(screen.getByRole('group', { name: 'game' })).getByText(/uncommitted work, so it stays/)).toBeInTheDocument();
    expect(within(row('daoris/s-step')).getByText(/Replays 1 commit of its own onto/)).toBeInTheDocument();
    expect(within(row('daoris/s-step')).getByText('daoris/s-parent', { selector: 'code' })).toBeInTheDocument();
    expect(within(row('feature/q2-second')).getByText("landed")).toBeInTheDocument();
    expect(within(row('daoris/s-busy')).getByText('stays')).toBeInTheDocument();
    expect(within(row('feature/q3-open')).getByText(/force push/)).toBeInTheDocument();
    expect(within(row('daoris/s-waits')).getByText(/has not reached the line yet/)).toBeInTheDocument();
    expect(within(row('feature/q1-first')).getByText('goes')).toBeInTheDocument();
  });

  it('does only what it listed, on one press', async () => {
    const { onSync } = draw();

    await userEvent.click(screen.getByRole('button', { name: 'Bring up to date (4)' }));

    expect(onSync).toHaveBeenCalledWith(['engine:main', 'engine:daoris/s-step', 'engine:feature/q2-second', 'engine:feature/q1-first']);
  });

  it('says so when everything is up to date, and has nothing to press', () => {
    draw({ plan: { lines: [pull({ kind: 'up-to-date' })], rebases: [rebase({ branch: 'daoris/s-done', kind: 'up-to-date' })], deletes: [] } });

    expect(screen.getByText('Everything here is up to date.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Bring up to date/ })).toBeDisabled();
  });

  it('names a fetch that did not happen, in git\'s words', () => {
    draw({ plan: { lines: [pull({ kind: 'up-to-date', fetch: 'there is no `origin` remote here' })], rebases: [], deletes: [] } });

    const note = within(screen.getByRole('note', { name: 'Not fetched' }));
    expect(note.getByText('origin', { selector: 'code' })).toBeInTheDocument();
    expect(within(screen.getByRole('listitem', { name: 'main' })).getByText('not fetched')).toBeInTheDocument();
  });

  /**
   * WSR7: every fetch failed on the owner's workspace (the git on the path could not reach its SSH remotes), and the
   * look said so only at the end of each row. It is said once, first: how many, grouped by git's reason, when each
   * last heard from origin, and what that git needs — with a short mark on each row.
   */
  it('says once, before the rows, what was not fetched, by reason, since when, and what git needs to reach origin', () => {
    const unreadable = 'fatal: Could not read from remote repository.';
    const yesterday = new Date(Date.now() - 26 * 3_600_000).toISOString();
    draw({
      plan: {
        lines: [
          pull({ kind: 'up-to-date', fetch: unreadable, lastFetch: yesterday, reach: 'ssh' }),
          pull({ repository: 'game', kind: 'up-to-date', fetch: unreadable, reach: 'ssh' }),
          pull({ repository: 'tools', kind: 'fast-forward', commits: 1, moves: true }),
        ],
        rebases: [],
        deletes: [],
      },
    });

    const note = screen.getByRole('note', { name: 'Not fetched' });
    const said = within(note);
    expect(said.getByText(/^2 of 3 repositories were not fetched, so each is judged against what origin said/)).toBeInTheDocument();
    expect(said.getByText(unreadable)).toBeInTheDocument();
    expect(said.getByText('engine: last fetched 1d ago · game: never fetched')).toBeInTheDocument();
    expect(note.textContent).toMatch(/needs a key its own ssh reads, or core\.sshCommand/);
    expect(note.textContent).not.toMatch(/credential helper/);
    // Said once, before the rows: the rows carry a short mark, not the sentence.
    expect(note.compareDocumentPosition(screen.getByRole('group', { name: 'engine' })) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(screen.getAllByText(unreadable)).toHaveLength(1);
    expect(screen.getAllByText('not fetched')).toHaveLength(2);
  });

  it('says what the git on the path needs for an origin over HTTPS, and nothing where every fetch landed', () => {
    const { unmount } = render(
      <Tooltip.Provider>
        <SyncSection
          plan={{ lines: [pull({ kind: 'up-to-date', fetch: 'fatal: could not read Username', reach: 'https' })], rebases: [], deletes: [] }}
          onLook={vi.fn()} onSync={vi.fn()} />
      </Tooltip.Provider>,
    );
    expect(screen.getByRole('note', { name: 'Not fetched' }).textContent).toMatch(/credential helper that answers without asking/);
    unmount();

    draw();
    expect(screen.queryByRole('note', { name: 'Not fetched' })).toBeNull();
  });

  it('holds both presses while one is under way', () => {
    draw({ bringing: true });

    expect(screen.getByRole('button', { name: /Bring/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Look again' })).toBeDisabled();
  });

  describe('while it works, and when the page stops waiting (WSR7)', () => {
    it('says how many repositories a look is fetching, and its button reads as busy', () => {
      draw({ plan: undefined, looking: true, lookingAt: 7 });

      expect(screen.getByRole('status').textContent).toBe('Looking at 7 repositories: fetching each from origin, a few at a time.');
      const button = screen.getByRole('button', { name: 'Looking…' });
      expect(button).toBeDisabled();
      expect(button).toHaveAttribute('aria-busy', 'true');
    });

    it('still says it is looking where it does not know how many', () => {
      draw({ plan: undefined, looking: true });

      expect(screen.getByRole('status').textContent).toMatch(/^Looking: fetching each repository/);
    });

    it('holds the last answer, dimmed, while it looks again', () => {
      draw({ looking: true, lookingAt: 2 });

      expect(screen.getByRole('group', { name: 'engine' })).toHaveClass('opacity-60');
    });

    it('says a press is under way on its own button', () => {
      draw({ bringing: true });

      expect(screen.getByRole('status').textContent).toMatch(/judged again right before it moves/);
      expect(screen.getByRole('button', { name: 'Bringing up to date…' })).toHaveAttribute('aria-busy', 'true');
    });

    it('says in the section that the page stopped waiting, for a look and for a press', () => {
      const { rerender } = render(
        <Tooltip.Provider>
          <SyncSection plan={undefined} stopped="look" onLook={vi.fn()} onSync={vi.fn()} />
        </Tooltip.Provider>,
      );
      expect(screen.getByRole('alert').textContent).toMatch(/stopped waiting before the look answered/);

      rerender(
        <Tooltip.Provider>
          <SyncSection plan={PLAN} stopped="press" onLook={vi.fn()} onSync={vi.fn()} />
        </Tooltip.Provider>,
      );
      expect(screen.getByRole('alert').textContent).toMatch(/stopped waiting before the press answered/);

      // A look under way is the answer to it, so the sentence goes while it runs.
      rerender(
        <Tooltip.Provider>
          <SyncSection plan={PLAN} stopped="look" looking onLook={vi.fn()} onSync={vi.fn()} />
        </Tooltip.Provider>,
      );
      expect(screen.queryByRole('alert')).toBeNull();
    });
  });
});
