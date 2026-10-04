import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { code } from '../test/code';
import { AcrossList, type RepositoryAcross } from './Across';

// READ1 (D107): whether agents read the checkouts of each workspace's repositories that set none of their own, as the
// driver resolved it, set from here as `daoris driver across --workspace` sets it from a terminal (D50). Since UX6f
// (D150 §3.1) a repository's own reading and what its sessions also write into are on its page, under Setup: the card
// names the repositories that set either, each a door there.

const REPOSITORIES: RepositoryAcross[] = [
  { repository: 'engine', workspace: 'aurora', checkout: true, read: false, source: 'repository', writesTo: [] },
  { repository: 'game', workspace: 'aurora', checkout: true, read: true, source: 'default', writesTo: [] },
  { repository: 'plugins', workspace: 'aurora', checkout: true, read: true, source: 'default', writesTo: ['engine'] },
  { repository: 'tools', workspace: 'forge', checkout: false, read: false, source: 'workspace', writesTo: [] },
];

const draw = (onRead = vi.fn(), onOpen: ((repository: string | null) => void) | undefined = vi.fn(), repositories = REPOSITORIES) => {
  render(
    <Tooltip.Provider>
      <AcrossList
        repositories={repositories}
        workspaceReads={[{ workspace: 'forge', read: false }]}
        onRead={onRead}
        onOpen={onOpen}
      />
    </Tooltip.Provider>,
  );
  return { onRead, onOpen };
};

describe('the reading across card', () => {
  // Its body names commands in backticks, which read as raw marks unless drawn as code (seen on the window).
  it('draws the commands its body names as code, never as backticks', () => {
    draw();

    expect(screen.getByText(code('git status'))).toBeInTheDocument();
    expect(screen.queryByText(/`git status`/)).not.toBeInTheDocument();
  });

  it("chooses each workspace's reading as it is SET, and names what an inherited one stands on", () => {
    draw();

    const forge = screen.getByRole('radiogroup', { name: 'Reading across in forge' });
    expect(within(forge).getByRole('radio', { name: 'Off' })).toHaveAttribute('aria-checked', 'true');
    const aurora = screen.getByRole('radiogroup', { name: 'Reading across in aurora' });
    expect(within(aurora).getByRole('radio', { name: 'Inherit (On)' })).toHaveAttribute('aria-checked', 'true');
  });

  it("sets a workspace's reading, and hands it back to Daoris's", async () => {
    const { onRead } = draw();
    const user = userEvent.setup();

    await user.click(within(screen.getByRole('radiogroup', { name: 'Reading across in aurora' })).getByRole('radio', { name: 'Off' }));
    expect(onRead).toHaveBeenLastCalledWith({ workspace: 'aurora', read: false });

    await user.click(within(screen.getByRole('radiogroup', { name: 'Reading across in forge' })).getByRole('radio', { name: 'Inherit (On)' }));
    expect(onRead).toHaveBeenLastCalledWith({ workspace: 'forge' });
  });

  // UX6f: a repository's own reading and its relationships have one home, its Setup.
  it('keeps no row per repository, and names those that set their own reading or a relationship, each a door to its Setup', async () => {
    const { onOpen } = draw();

    expect(screen.queryByRole('radiogroup', { name: "Reading engine's checkout" })).toBeNull();
    expect(screen.queryByRole('combobox', { name: /write into/ })).toBeNull();
    const aurora = screen.getByRole('region', { name: 'aurora' });
    expect(within(aurora).getByText('Set for a repository of its own, on its page under Setup:')).toBeInTheDocument();
    // engine set its own reading, plugins declared a relationship; game set neither.
    expect(within(aurora).getAllByRole('button', { name: /^Open .*'s setup$/ }).map((door) => door.textContent)).toEqual(['engine', 'plugins']);
    await userEvent.click(within(aurora).getByRole('button', { name: "Open plugins's setup" }));
    expect(onOpen).toHaveBeenLastCalledWith('plugins');
  });

  it('says so where no repository sets its own, with a door to Repositories at Setup', async () => {
    const { onOpen } = draw();

    const forge = screen.getByRole('region', { name: 'forge' });
    expect(within(forge).getByText(/No repository here sets its own/)).toBeInTheDocument();
    await userEvent.click(within(forge).getByRole('button', { name: 'Open Repositories' }));
    expect(onOpen).toHaveBeenLastCalledWith(null);
  });

  it('says so when no repository is here', () => {
    render(<AcrossList repositories={[]} workspaceReads={[]} onRead={vi.fn()} />);
    expect(screen.getByText('No repository on this machine yet.')).toBeInTheDocument();
  });

  it('shows a workspace that sets its own reading even with no repository here', () => {
    draw(vi.fn(), vi.fn(), []);
    const forge = screen.getByRole('radiogroup', { name: 'Reading across in forge' });
    expect(within(forge).getByRole('radio', { name: 'Off' })).toHaveAttribute('aria-checked', 'true');
  });
});
