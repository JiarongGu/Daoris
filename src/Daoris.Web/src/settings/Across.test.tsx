import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { code } from '../test/code';
import { AcrossList, type RepositoryAcross } from './Across';

// READ1 (D107): whether agents read each repository's checkout, as the driver resolved it with what said
// so, and which repositories' sessions may also write into another — set from here as `daoris driver
// across` sets it from a terminal (D50).

const REPOSITORIES: RepositoryAcross[] = [
  { repository: 'engine', workspace: 'aurora', checkout: true, read: false, source: 'repository', writesTo: [] },
  { repository: 'game', workspace: 'aurora', checkout: true, read: true, source: 'default', writesTo: [] },
  { repository: 'plugins', workspace: 'aurora', checkout: true, read: true, source: 'default', writesTo: ['engine'] },
  { repository: 'tools', workspace: 'forge', checkout: false, read: false, source: 'workspace', writesTo: [] },
];

const draw = (onRead = vi.fn(), onWrite = vi.fn(), repositories = REPOSITORIES) => {
  render(
    <Tooltip.Provider>
      <AcrossList
        repositories={repositories}
        workspaceReads={[{ workspace: 'forge', read: false }]}
        onRead={onRead}
        onWrite={onWrite}
      />
    </Tooltip.Provider>,
  );
  return { onRead, onWrite };
};

describe('the reading and writing across card', () => {
  // Its body names commands in backticks, which read as raw marks unless drawn as code (seen on the window).
  it('draws the commands its body names as code, never as backticks', () => {
    draw();

    expect(screen.getByText(code('git status'))).toBeInTheDocument();
    expect(screen.queryByText(/`git status`/)).not.toBeInTheDocument();
  });

  it('says whether each checkout is read and what said so, grouped by its workspace', () => {
    draw();

    const aurora = screen.getByRole('region', { name: 'aurora' });
    expect(within(aurora).getByText(/Read by no agent outside it/)).toBeInTheDocument();
    expect(within(aurora).getByText(/Set for this repository/)).toBeInTheDocument();
    expect(within(aurora).getAllByText(/Read by sessions in aurora and by Ask Daoris/)).toHaveLength(2);
    const forge = screen.getByRole('region', { name: 'forge' });
    expect(within(forge).getByText(/Set for the workspace forge/)).toBeInTheDocument();
    expect(within(forge).getByText(/No checkout on this machine/)).toBeInTheDocument();
  });

  it('chooses only what is SET there, and names what an inherited choice stands on', () => {
    draw();

    const engine = screen.getByRole('radiogroup', { name: "Reading engine's checkout" });
    expect(within(engine).getByRole('radio', { name: 'Off' })).toHaveAttribute('aria-checked', 'true');
    const game = screen.getByRole('radiogroup', { name: "Reading game's checkout" });
    expect(within(game).getByRole('radio', { name: 'Inherit (On)' })).toHaveAttribute('aria-checked', 'true');
    // The workspace said off, so what a repository there inherits is off.
    const tools = screen.getByRole('radiogroup', { name: "Reading tools's checkout" });
    expect(within(tools).getByRole('radio', { name: 'Inherit (Off)' })).toHaveAttribute('aria-checked', 'true');
    const forge = screen.getByRole('radiogroup', { name: 'Reading across in forge' });
    expect(within(forge).getByRole('radio', { name: 'Off' })).toHaveAttribute('aria-checked', 'true');
    const aurora = screen.getByRole('radiogroup', { name: 'Reading across in aurora' });
    expect(within(aurora).getByRole('radio', { name: 'Inherit (On)' })).toHaveAttribute('aria-checked', 'true');
  });

  it('sets a repository\'s reading, a workspace\'s, and hands one back to what stands above it', async () => {
    const { onRead } = draw();
    const user = userEvent.setup();

    await user.click(within(screen.getByRole('radiogroup', { name: "Reading game's checkout" })).getByRole('radio', { name: 'Off' }));
    expect(onRead).toHaveBeenLastCalledWith({ repository: 'game', read: false });

    await user.click(within(screen.getByRole('radiogroup', { name: 'Reading across in aurora' })).getByRole('radio', { name: 'Off' }));
    expect(onRead).toHaveBeenLastCalledWith({ workspace: 'aurora', read: false });

    await user.click(within(screen.getByRole('radiogroup', { name: "Reading engine's checkout" })).getByRole('radio', { name: 'Inherit (On)' }));
    expect(onRead).toHaveBeenLastCalledWith({ repository: 'engine' });
  });

  it('shows each declared relationship with a way to take it back', async () => {
    const { onWrite } = draw();
    const user = userEvent.setup();

    const plugins = screen.getByRole('list', { name: 'plugins writes into' });
    expect(within(plugins).getByText('engine')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Stop plugins writing into engine' }));
    expect(onWrite).toHaveBeenLastCalledWith({ repository: 'plugins', to: 'engine', allow: false });
  });

  it('declares a relationship toward another repository of the same workspace only', async () => {
    const { onWrite } = draw();
    const user = userEvent.setup();

    const trigger = screen.getByRole('combobox', { name: 'let game write into…' });
    trigger.focus();
    await user.keyboard('{Enter}');
    const offered = (await screen.findAllByRole('option')).map((option) => option.textContent);
    // Not itself, and not `tools`, which is another workspace's.
    expect(offered).toEqual(['engine', 'plugins']);
    await user.click(screen.getByRole('option', { name: 'engine' }));
    expect(onWrite).toHaveBeenLastCalledWith({ repository: 'game', to: 'engine', allow: true });
  });

  it('says so when no repository is here', () => {
    render(<AcrossList repositories={[]} workspaceReads={[]} onRead={vi.fn()} onWrite={vi.fn()} />);
    expect(screen.getByText('No repository on this machine yet.')).toBeInTheDocument();
  });

  it('shows a workspace that sets its own reading even with no repository here', () => {
    draw(vi.fn(), vi.fn(), []);
    const forge = screen.getByRole('radiogroup', { name: 'Reading across in forge' });
    expect(within(forge).getByRole('radio', { name: 'Off' })).toHaveAttribute('aria-checked', 'true');
  });
});
