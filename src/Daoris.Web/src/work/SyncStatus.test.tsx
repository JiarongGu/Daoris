import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { StatusBar } from './frame';
import { SyncStatus } from './SyncStatus';

/** The provider the application mounts once (`main.tsx`); a tooltip outside one throws. */
const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const minutesAgo = (count: number) => new Date(Date.now() - count * 60_000).toISOString();
const LEVEL = { ahead: 0, behind: [], synced: minutesAgo(2), tried: minutesAgo(2), problem: null };

// Where a circle stands with its remote (SYNC6b), as a molecule: props in, states out. The standing
// is the host's to tell; *Sync now* is the shell's, so a browser is simply this without `onSyncNow`.

describe('SyncStatus', () => {
  it('says a level circle is synced, and nothing more', () => {
    render(<SyncStatus workspace="aurora" standing={LEVEL} conflicts={[]} onOpenQuest={() => {}} />);

    expect(screen.getByRole('button', { name: /sync/i }).textContent).toContain('synced');
  });

  it('counts what waits to go up and what is in conflict, instead of saying synced', () => {
    render(
      <SyncStatus
        workspace="aurora" standing={{ ...LEVEL, ahead: 3 }}
        conflicts={[{ id: 'q1' }, { id: 'q2' }]} onOpenQuest={() => {}}
      />,
    );

    const item = screen.getByRole('button', { name: /sync/i });
    expect(item.textContent).toContain('3');
    expect(item.textContent).toContain('2');
    expect(item.textContent).not.toContain('synced');
  });

  it('says a circle never synced is not synced yet, rather than inventing a time', () => {
    render(
      <SyncStatus
        workspace="aurora" standing={{ ahead: 0, behind: [], synced: null, tried: null, problem: null }}
        conflicts={[]} onOpenQuest={() => {}}
      />,
    );

    expect(screen.getByRole('button', { name: /sync/i }).textContent).toContain('not synced yet');
  });

  /** A wall is said on the bar itself — a glyph alone is not a sentence, and "synced" would be a lie. */
  it('says on the bar that the remote could not be reached, and not that it synced', () => {
    render(
      <SyncStatus
        workspace="aurora" standing={{ ...LEVEL, tried: minutesAgo(1), problem: 'connection refused' }}
        conflicts={[]} onOpenQuest={() => {}}
      />,
    );

    const item = screen.getByRole('button', { name: /sync/i });
    expect(item.textContent).toContain('unreachable');
    expect(item.textContent).not.toContain('synced');
  });

  /**
   * The wall is the host's sentence, verbatim — it names what the machine could not reach — beside
   * when the circle last DID reach it, which a wall does not move.
   */
  it('names the wall verbatim beside when the circle last reached its remote', async () => {
    const wall = 'the remote could not be reached (connection refused)';
    render(
      <SyncStatus
        workspace="aurora" standing={{ ...LEVEL, synced: minutesAgo(90), tried: minutesAgo(1), problem: wall }}
        conflicts={[]} onOpenQuest={() => {}}
      />,
    );
    await open();

    expect(screen.getByText(wall)).toBeTruthy();
    expect(screen.getByText(/Synced 1h ago/)).toBeTruthy();
    // The lead-in says only WHEN: the host's sentence already says what went wrong, and a lead-in
    // that said it too read "did not reach the remote: the remote could not be reached" on the window.
    expect(screen.getByText(/^The last try, 1m ago:$/)).toBeTruthy();
  });

  it('lists each quest in conflict and opens the one chosen', async () => {
    const onOpenQuest = vi.fn();
    render(
      <SyncStatus
        workspace="aurora" standing={LEVEL}
        conflicts={[{ id: '4b7e21aa90c3', title: 'Cut the release branch' }]} onOpenQuest={onOpenQuest}
      />,
    );
    const user = await open();

    expect(screen.getByRole('menuitem', { name: /Cut the release branch/ })).toBeTruthy();
    await user.keyboard('{ArrowDown}{Enter}');
    expect(onOpenQuest).toHaveBeenCalledWith('4b7e21aa90c3');
  });

  it('runs Sync now when the shell offers it, and not twice while it runs', async () => {
    const onSyncNow = vi.fn();
    const { rerender } = render(
      <SyncStatus workspace="aurora" standing={LEVEL} conflicts={[]} onOpenQuest={() => {}} onSyncNow={onSyncNow} />,
    );
    const user = await open();

    expect(screen.getByRole('menuitem', { name: /Sync now/ })).toBeTruthy();
    await user.keyboard('{ArrowDown}{Enter}');
    expect(onSyncNow).toHaveBeenCalledTimes(1);

    rerender(
      <Tooltip.Provider>
        <SyncStatus workspace="aurora" standing={LEVEL} conflicts={[]} onOpenQuest={() => {}} onSyncNow={onSyncNow} syncing />
      </Tooltip.Provider>,
    );
    await open();
    expect(screen.getByRole('menuitem', { name: /Syncing/ }).getAttribute('aria-disabled')).toBe('true');
  });

  /** A browser reads the standing over HTTP, and the pass is the shell's: no Sync now at all. */
  it('offers no Sync now where there is no shell to run it', async () => {
    render(<SyncStatus workspace="aurora" standing={LEVEL} conflicts={[]} onOpenQuest={() => {}} />);
    await open();

    expect(screen.getByRole('menu')).toBeTruthy();
    expect(screen.queryByRole('menuitem', { name: /Sync now/ })).toBeNull();
  });
});

/**
 * Opened from the keyboard with a fresh `userEvent.setup()` — AppMenu's harness, for AppMenu's reason:
 * the shared `userEvent.*` API carries pointer state between tests, and Radix reads it, so the first
 * menu in a file opened and every later one silently did not. (A `defaultOpen` menu does the same
 * under jsdom, which is why the stories use that prop and these tests do not.)
 */
async function open() {
  const user = userEvent.setup();
  screen.getByRole('button', { name: /sync/i }).focus();
  await user.keyboard('{Enter}');
  return user;
}

describe('StatusBar with a sync item', () => {
  /** "Wired" is exactly what the sync item elaborates, so it takes the remote item's place rather than sitting beside it. */
  it('puts the sync item where the remote item was', () => {
    render(
      <StatusBar
        driver="running" sessions={0} workspace="aurora" remote
        sync={<SyncStatus workspace="aurora" standing={LEVEL} conflicts={[]} onOpenQuest={() => {}} />}
      />,
    );

    expect(screen.getByRole('button', { name: /sync/i })).toBeTruthy();
    expect(screen.queryByText('wired')).toBeNull();
  });
});
