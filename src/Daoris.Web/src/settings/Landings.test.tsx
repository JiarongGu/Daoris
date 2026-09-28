import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { LandingList, type RepositoryLanding } from './Landings';

// WSR1 (D87): how work lands — each repository's rule as the driver chose it, with what said so, and
// set from here as `daoris driver landing` sets it from a terminal (D50). Daoris never pushes.

const LANDINGS: RepositoryLanding[] = [
  { repository: 'engine', workspace: 'aurora', form: 'merge', source: 'repository' },
  { repository: 'game', workspace: 'aurora', form: 'branch', pattern: 'feature/{quest}-{slug}', source: 'workspace' },
  { repository: 'tools', workspace: 'forge', form: 'merge', source: 'default' },
];

const draw = (onSet = vi.fn()) => {
  render(
    <Tooltip.Provider>
      <LandingList
        landings={LANDINGS}
        workspaceLandings={[{ workspace: 'aurora', form: 'branch', pattern: 'feature/{quest}-{slug}' }]}
        onSet={onSet}
      />
    </Tooltip.Provider>,
  );
  return onSet;
};

describe('the landing card', () => {
  it('says each repository\'s rule and what said so, grouped by its workspace', () => {
    draw();

    const aurora = screen.getByRole('region', { name: 'aurora' });
    expect(within(aurora).getByText(/Merged into its line, set for this repository/)).toBeInTheDocument();
    expect(within(aurora).getByText(/set for the workspace aurora/)).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'forge' })).getByText(/the default/)).toBeInTheDocument();
  });

  it('offers merge and branch only — the form that pushes is a plugin\'s', () => {
    draw();

    const group = screen.getByRole('radiogroup', { name: 'How work in tools lands' });
    expect(within(group).getAllByRole('radio').map((option) => option.textContent)).toEqual(['merge', 'branch']);
  });

  it('sets a branch rule with its pattern, and an empty field means the example', async () => {
    const onSet = draw();
    const user = userEvent.setup();
    const forge = screen.getByRole('region', { name: 'forge' });

    await user.click(within(screen.getByRole('radiogroup', { name: 'How work in tools lands' })).getByRole('radio', { name: 'branch' }));
    const pattern = screen.getByRole('textbox', { name: 'The branch pattern for tools' });
    expect(pattern).toHaveValue('');
    expect(pattern).toHaveAttribute('placeholder', 'feature/{quest}-{slug}');
    await user.click(within(forge).getAllByRole('button', { name: 'Set' })[1]!);
    expect(onSet).toHaveBeenLastCalledWith({ repository: 'tools', form: 'branch', pattern: 'feature/{quest}-{slug}' });

    await user.type(pattern, 'review/{{session}');
    await user.click(within(forge).getAllByRole('button', { name: 'Set' })[1]!);
    expect(onSet).toHaveBeenLastCalledWith({ repository: 'tools', form: 'branch', pattern: 'review/{session}' });
  });

  it('shows an inherited pattern as the placeholder, never as a value', () => {
    draw();

    const game = screen.getByRole('textbox', { name: 'The branch pattern for game' });
    expect(game).toHaveValue('');
    expect(game).toHaveAttribute('placeholder', 'feature/{quest}-{slug}');
    expect(screen.getByRole('textbox', { name: 'The branch pattern for aurora' })).toHaveValue('feature/{quest}-{slug}');
  });

  it('does not send what already stands, and clears only where a rule is set', async () => {
    const onSet = draw();
    const user = userEvent.setup();

    // `game` inherits the workspace's branch rule, and pressing Set with it unchanged sends nothing.
    const game = screen.getAllByRole('button', { name: 'Set' })[2]!;
    expect(game).toBeDisabled();
    // A clear on `aurora` (set) and `engine` (set); none on the inherited or the default rows.
    expect(screen.getAllByRole('button', { name: 'Clear' })).toHaveLength(2);

    await user.click(within(screen.getByRole('region', { name: 'aurora' })).getAllByRole('button', { name: 'Clear' })[0]!);
    expect(onSet).toHaveBeenLastCalledWith({ workspace: 'aurora' });
  });
});
