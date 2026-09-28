import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { LineList, type RepositoryLine } from './Lines';

// WSR2: the line each repository's work grows from and lands on — shown as the driver resolved it,
// with what said so, and set from here as `daoris driver line` sets it from a terminal (D50).

const LINES: RepositoryLine[] = [
  { repository: 'engine', workspace: 'aurora', branch: 'release', source: 'repository' },
  { repository: 'game', workspace: 'aurora', branch: 'develop', source: 'workspace' },
  { repository: 'tools', workspace: 'forge', branch: 'main', source: 'checkout' },
  { repository: 'remote-only', workspace: 'forge', source: 'none' },
];

const draw = (onSet = vi.fn(), lines = LINES) => {
  render(
    <Tooltip.Provider>
      <LineList lines={lines} workspaceLines={[{ workspace: 'aurora', branch: 'develop' }]} onSet={onSet} />
    </Tooltip.Provider>,
  );
  return onSet;
};

describe('the lines card', () => {
  it('says each repository\'s line and what said so, grouped by its workspace', () => {
    draw();

    const aurora = screen.getByRole('region', { name: 'aurora' });
    expect(within(aurora).getByText(/set for this repository/)).toBeInTheDocument();
    expect(within(aurora).getByText(/set for the workspace aurora/)).toBeInTheDocument();
    expect(within(aurora).getByText('release', { selector: 'code' })).toBeInTheDocument();
    const forge = screen.getByRole('region', { name: 'forge' });
    expect(within(forge).getByText(/the checkout's own/)).toBeInTheDocument();
    expect(within(forge).getByText(/none is set/)).toBeInTheDocument();
  });

  it('fills a field only with what is SET there, and offers a clear only where something is', () => {
    draw();

    expect(screen.getByRole('textbox', { name: /engine/ })).toHaveValue('release');
    // Inherited, so the field is empty and the inherited line is the placeholder.
    expect(screen.getByRole('textbox', { name: /game/ })).toHaveValue('');
    expect(screen.getByRole('textbox', { name: /game/ })).toHaveAttribute('placeholder', 'develop');
    expect(screen.getByRole('textbox', { name: /aurora/ })).toHaveValue('develop');
    expect(screen.getAllByRole('button', { name: 'Clear' })).toHaveLength(2);
  });

  it('sets a repository\'s line, a workspace\'s, and clears one', async () => {
    const onSet = draw();
    const user = userEvent.setup();

    await user.type(screen.getByRole('textbox', { name: /game/ }), 'feature/x{Enter}');
    expect(onSet).toHaveBeenLastCalledWith({ repository: 'game', branch: 'feature/x' });

    const aurora = screen.getByRole('textbox', { name: /aurora/ });
    await user.clear(aurora);
    await user.type(aurora, 'trunk{Enter}');
    expect(onSet).toHaveBeenLastCalledWith({ workspace: 'aurora', branch: 'trunk' });

    await user.click(within(screen.getByRole('region', { name: 'aurora' })).getAllByRole('button', { name: 'Clear' })[0]!);
    expect(onSet).toHaveBeenLastCalledWith({ workspace: 'aurora', branch: undefined });
  });

  it('does not send what is already set, or nothing at all', async () => {
    const onSet = draw();

    await userEvent.setup().type(screen.getByRole('textbox', { name: /engine/ }), '{Enter}');
    expect(onSet).not.toHaveBeenCalled();
  });

  it('names a machine with no repository to set a line for', () => {
    render(<Tooltip.Provider><LineList lines={[]} workspaceLines={[]} onSet={vi.fn()} /></Tooltip.Provider>);

    expect(screen.getByText(/No repository/)).toBeInTheDocument();
  });
});
