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

const draw = (onSet = vi.fn(), landers: string[] = [], landings: RepositoryLanding[] = LANDINGS) => {
  render(
    <Tooltip.Provider>
      <LandingList
        landings={landings}
        workspaceLandings={[{ workspace: 'aurora', form: 'branch', pattern: 'feature/{quest}-{slug}' }]}
        landers={landers}
        onSet={onSet}
      />
    </Tooltip.Provider>,
  );
  return onSet;
};

async function choose(name: string, option: string) {
  const user = userEvent.setup();
  screen.getByRole('combobox', { name }).focus();
  await user.keyboard('{Enter}');
  await user.click(await screen.findByRole('option', { name: option }));
}

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
    expect(within(group).getAllByRole('radio').map((option) => option.textContent)).toEqual(['Merge', 'Branch']);
  });

  it('sets a branch rule with its pattern, and an empty field means the example', async () => {
    const onSet = draw();
    const user = userEvent.setup();
    const forge = screen.getByRole('region', { name: 'forge' });

    await user.click(within(screen.getByRole('radiogroup', { name: 'How work in tools lands' })).getByRole('radio', { name: 'Branch' }));
    const pattern = screen.getByRole('textbox', { name: 'The branch pattern for tools' });
    expect(pattern).toHaveValue('');
    expect(pattern).toHaveAttribute('placeholder', 'feature/{quest}-{slug}');
    await user.click(within(forge).getAllByRole('button', { name: 'Save' })[1]!);
    expect(onSet).toHaveBeenLastCalledWith({ repository: 'tools', form: 'branch', pattern: 'feature/{quest}-{slug}' });

    await user.type(pattern, 'review/{{session}');
    await user.click(within(forge).getAllByRole('button', { name: 'Save' })[1]!);
    expect(onSet).toHaveBeenLastCalledWith({ repository: 'tools', form: 'branch', pattern: 'review/{session}' });
  });

  /** D88: the tidy rides the rule — the tree and its branch go once a press lands the work. */
  it('sends the tidy with the rule when it is ticked', async () => {
    const onSet = draw();
    const user = userEvent.setup();
    const forge = screen.getByRole('region', { name: 'forge' });

    await user.click(within(forge).getAllByRole('checkbox', { name: 'Clean up once landed' })[1]!);
    await user.click(within(forge).getAllByRole('button', { name: 'Save' })[1]!);

    expect(onSet).toHaveBeenLastCalledWith({ repository: 'tools', form: 'merge', tidy: true });
  });

  it('shows an inherited pattern as the placeholder, never as a value', () => {
    draw();

    const game = screen.getByRole('textbox', { name: 'The branch pattern for game' });
    expect(game).toHaveValue('');
    expect(game).toHaveAttribute('placeholder', 'feature/{quest}-{slug}');
    expect(screen.getByRole('textbox', { name: 'The branch pattern for aurora' })).toHaveValue('feature/{quest}-{slug}');
  });

  // A field's percentage cap is what lets a narrow control column shrink it: with a fixed width alone it
  // spilled left over the row's words at 1205px with the side bar open (seen on the window).
  it('lets the pattern field give way to a narrow row', () => {
    draw();

    const field = screen.getByRole('textbox', { name: 'The branch pattern for game' });
    expect(field).toHaveClass('min-w-0', 'max-w-full');
  });

  /**
   * WSR4 (D100): a branch rule may name the plugin that pushes it and opens the pull request — one of the
   * plugins here that land work. With none installed nothing is offered, and a merge never names one.
   */
  it('offers the plugins here that land work on a branch rule, and sends the one chosen', async () => {
    const onSet = draw(vi.fn(), ['github-pull-request']);
    const user = userEvent.setup();
    const forge = screen.getByRole('region', { name: 'forge' });

    expect(screen.queryByRole('combobox', { name: 'Who pushes the branch for tools' })).toBeNull();
    await user.click(within(screen.getByRole('radiogroup', { name: 'How work in tools lands' })).getByRole('radio', { name: 'Branch' }));
    await choose('Who pushes the branch for tools', 'Plugin github-pull-request');
    await user.click(within(forge).getAllByRole('button', { name: 'Save' })[1]!);

    expect(onSet).toHaveBeenLastCalledWith({ repository: 'tools', form: 'branch', pattern: 'feature/{quest}-{slug}', plugin: 'github-pull-request' });

    await choose('Who pushes the branch for tools', 'You push it');
    await user.click(within(forge).getAllByRole('button', { name: 'Save' })[1]!);
    expect(onSet).toHaveBeenLastCalledWith({ repository: 'tools', form: 'branch', pattern: 'feature/{quest}-{slug}' });
  });

  it('offers no plugin where none here lands work', async () => {
    draw();
    const user = userEvent.setup();

    await user.click(within(screen.getByRole('radiogroup', { name: 'How work in tools lands' })).getByRole('radio', { name: 'Branch' }));

    expect(screen.queryByRole('combobox', { name: 'Who pushes the branch for tools' })).toBeNull();
  });

  it('says which plugin pushes where a rule names one', () => {
    draw(vi.fn(), ['github-pull-request'], [
      { repository: 'game', workspace: 'aurora', form: 'branch', pattern: 'feature/{quest}-{slug}', plugin: 'github-pull-request', source: 'repository' },
    ]);

    expect(screen.getByText(/pushes it and opens the pull request/)).toBeInTheDocument();
    expect(screen.getByText('github-pull-request', { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Who pushes the branch for game' })).toHaveTextContent('Plugin github-pull-request');
  });

  it('does not send what already stands, and clears only where a rule is set', async () => {
    const onSet = draw();
    const user = userEvent.setup();

    // `game` inherits the workspace's branch rule, and pressing Set with it unchanged sends nothing.
    const game = screen.getAllByRole('button', { name: 'Save' })[2]!;
    expect(game).toBeDisabled();
    // A clear on `aurora` (set) and `engine` (set); none on the inherited or the default rows.
    expect(screen.getAllByRole('button', { name: 'Clear' })).toHaveLength(2);

    await user.click(within(screen.getByRole('region', { name: 'aurora' })).getAllByRole('button', { name: 'Clear' })[0]!);
    expect(onSet).toHaveBeenLastCalledWith({ workspace: 'aurora' });
  });

  /**
   * LAND2a (D145): *Accept automatically* — a branch rule's switch, offered on a branch only, sent with the rule, and
   * said as it is set: the plugin pushes and opens pull requests without a press, or with none, nothing leaves here.
   */
  it('offers Accept automatically on a branch rule only, and warns that nothing leaves the machine without a plugin', async () => {
    const onSet = draw();
    const user = userEvent.setup();
    const forge = screen.getByRole('region', { name: 'forge' });

    expect(within(forge).queryByRole('checkbox', { name: 'Accept automatically' })).toBeNull();
    await user.click(within(screen.getByRole('radiogroup', { name: 'How work in tools lands' })).getByRole('radio', { name: 'Branch' }));
    await user.click(within(forge).getByRole('checkbox', { name: 'Accept automatically' }));

    expect(within(forge).getByText(/no plugin opens a pull request, so each done's branch waits here for you to push it/)).toBeInTheDocument();
    await user.click(within(forge).getAllByRole('button', { name: 'Save' })[1]!);
    expect(onSet).toHaveBeenLastCalledWith({ repository: 'tools', form: 'branch', pattern: 'feature/{quest}-{slug}', autoAccept: true });
  });

  it('says the chosen plugin pushes and opens pull requests without asking each time', async () => {
    const onSet = draw(vi.fn(), ['github-pull-request']);
    const user = userEvent.setup();
    const forge = screen.getByRole('region', { name: 'forge' });

    await user.click(within(screen.getByRole('radiogroup', { name: 'How work in tools lands' })).getByRole('radio', { name: 'Branch' }));
    await choose('Who pushes the branch for tools', 'Plugin github-pull-request');
    await user.click(within(forge).getByRole('checkbox', { name: 'Accept automatically' }));

    expect(within(forge).getByText(/pushes it and opens a pull request without asking you each time/)).toBeInTheDocument();
    expect(within(forge).getByText('github-pull-request', { selector: 'code' })).toBeInTheDocument();
    await user.click(within(forge).getAllByRole('button', { name: 'Save' })[1]!);
    expect(onSet).toHaveBeenLastCalledWith(
      { repository: 'tools', form: 'branch', pattern: 'feature/{quest}-{slug}', plugin: 'github-pull-request', autoAccept: true });
  });

  it('shows a rule that accepts automatically, and a merge drops the switch', async () => {
    const onSet = draw(vi.fn(), [], [
      { repository: 'game', workspace: 'aurora', form: 'branch', pattern: 'feature/{quest}-{slug}', autoAccept: true, source: 'repository' },
    ]);
    const user = userEvent.setup();
    const aurora = screen.getByRole('region', { name: 'aurora' });

    expect(within(aurora).getByText(/Accepted automatically when its quest is done/)).toBeInTheDocument();
    expect(within(aurora).getAllByRole('checkbox', { name: 'Accept automatically' })[1]).toBeChecked();
    await user.click(within(screen.getByRole('radiogroup', { name: 'How work in game lands' })).getByRole('radio', { name: 'Merge' }));
    expect(within(aurora).queryAllByRole('checkbox', { name: 'Accept automatically' })).toHaveLength(1);
    await user.click(within(aurora).getAllByRole('button', { name: 'Save' })[1]!);
    expect(onSet).toHaveBeenLastCalledWith({ repository: 'game', form: 'merge' });
  });
});
