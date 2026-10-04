import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { LineList, type RepositoryLine } from './Lines';

// WSR2: the line each workspace gives the repositories in it that set none of their own, set from here as
// `daoris driver line --workspace` sets it from a terminal (D50). Since UX6f (D150 §3.1) a repository's own line is on
// its page, under Setup: the card names the repositories that set their own, each a door there.

const LINES: RepositoryLine[] = [
  { repository: 'engine', workspace: 'aurora', branch: 'release', source: 'repository' },
  { repository: 'game', workspace: 'aurora', branch: 'develop', source: 'workspace' },
  { repository: 'tools', workspace: 'forge', branch: 'main', source: 'checkout' },
  { repository: 'remote-only', workspace: 'forge', source: 'none' },
];

const draw = (onSet = vi.fn(), onOpen = vi.fn(), lines = LINES) => {
  render(
    <Tooltip.Provider>
      <LineList lines={lines} workspaceLines={[{ workspace: 'aurora', branch: 'develop' }]} onSet={onSet} onOpen={onOpen} />
    </Tooltip.Provider>,
  );
  return { onSet, onOpen };
};

describe('the lines card', () => {
  it("fills a workspace's field only with what is SET there, and offers a clear only where something is", () => {
    draw();

    expect(screen.getByRole('textbox', { name: /aurora/ })).toHaveValue('develop');
    expect(screen.getByRole('textbox', { name: /forge/ })).toHaveValue('');
    expect(screen.getAllByRole('button', { name: 'Clear' })).toHaveLength(1);
  });

  it("sets a workspace's line, and clears one", async () => {
    const { onSet } = draw();
    const user = userEvent.setup();

    const aurora = screen.getByRole('textbox', { name: /aurora/ });
    await user.clear(aurora);
    await user.type(aurora, 'trunk{Enter}');
    expect(onSet).toHaveBeenLastCalledWith({ workspace: 'aurora', branch: 'trunk' });

    await user.click(within(screen.getByRole('region', { name: 'aurora' })).getByRole('button', { name: 'Clear' }));
    expect(onSet).toHaveBeenLastCalledWith({ workspace: 'aurora', branch: undefined });
  });

  it('does not send what is already set, or nothing at all', async () => {
    const { onSet } = draw();

    await userEvent.setup().type(screen.getByRole('textbox', { name: /aurora/ }), '{Enter}');
    expect(onSet).not.toHaveBeenCalled();
  });

  // UX6f: a repository's own line has one home, its Setup.
  it('keeps no row per repository, and names those that set their own, each a door to its Setup', async () => {
    const { onOpen } = draw();

    expect(screen.queryByRole('textbox', { name: /engine/ })).toBeNull();
    expect(screen.queryByRole('textbox', { name: /game/ })).toBeNull();
    const aurora = screen.getByRole('region', { name: 'aurora' });
    expect(within(aurora).getAllByRole('button', { name: /'s setup$/ }).map((door) => door.textContent)).toEqual(['engine']);
    await userEvent.click(within(aurora).getByRole('button', { name: "Open engine's setup" }));
    expect(onOpen).toHaveBeenLastCalledWith('engine');

    // forge's repositories take git's guess or nothing: none sets its own.
    const forge = screen.getByRole('region', { name: 'forge' });
    expect(within(forge).getByText(/No repository here sets its own/)).toBeInTheDocument();
    await userEvent.click(within(forge).getByRole('button', { name: 'Open Repositories' }));
    expect(onOpen).toHaveBeenLastCalledWith(null);
  });

  it('names a machine with no repository to set a line for', () => {
    render(<Tooltip.Provider><LineList lines={[]} workspaceLines={[]} onSet={vi.fn()} /></Tooltip.Provider>);

    expect(screen.getByText(/No repository/)).toBeInTheDocument();
  });
});
