import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { LandingList, type RepositoryLanding } from './Landings';

// WSR1 (D87): how work lands in each workspace's repositories that set none of their own, set from here as `daoris
// driver landing --workspace` sets it from a terminal (D50). Daoris never pushes. Since UX6f (D150 §3.1) a repository's
// own rule is on its page, under Setup: the card names the repositories that set their own, each a door there. The
// rule's control is one, here and on a repository's Setup, so its behaviour is held here on a workspace's row.

const LANDINGS: RepositoryLanding[] = [
  { repository: 'engine', workspace: 'aurora', form: 'merge', source: 'repository' },
  { repository: 'game', workspace: 'aurora', form: 'branch', pattern: 'feature/{quest}-{slug}', source: 'workspace' },
  { repository: 'tools', workspace: 'forge', form: 'merge', source: 'default' },
];

const draw = (onSet = vi.fn(), landers: string[] = [], landings: RepositoryLanding[] = LANDINGS, onOpen = vi.fn()) => {
  render(
    <Tooltip.Provider>
      <LandingList
        landings={landings}
        workspaceLandings={[{ workspace: 'aurora', form: 'branch', pattern: 'feature/{quest}-{slug}' }]}
        landers={landers}
        onSet={onSet}
        onOpen={onOpen}
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

/** forge sets no rule of its own, so its row starts from Daoris's merge. */
const forgeForm = () => screen.getByRole('radiogroup', { name: 'How work in forge lands' });
const forgeSave = () => within(screen.getByRole('region', { name: 'forge' })).getByRole('button', { name: 'Save' });

describe('the landing card', () => {
  it('offers merge and branch only — the form that pushes is a plugin\'s', () => {
    draw();

    expect(within(forgeForm()).getAllByRole('radio').map((option) => option.textContent)).toEqual(['Merge', 'Branch']);
  });

  it('sets a branch rule with its pattern, and an empty field means the example', async () => {
    const onSet = draw();
    const user = userEvent.setup();

    await user.click(within(forgeForm()).getByRole('radio', { name: 'Branch' }));
    const pattern = screen.getByRole('textbox', { name: 'The branch pattern for forge' });
    expect(pattern).toHaveValue('');
    expect(pattern).toHaveAttribute('placeholder', 'feature/{quest}-{slug}');
    await user.click(forgeSave());
    expect(onSet).toHaveBeenLastCalledWith({ workspace: 'forge', form: 'branch', pattern: 'feature/{quest}-{slug}' });

    await user.type(pattern, 'review/{{session}');
    await user.click(forgeSave());
    expect(onSet).toHaveBeenLastCalledWith({ workspace: 'forge', form: 'branch', pattern: 'review/{session}' });
  });

  /** D88: the tidy rides the rule — the tree and its branch go once a press lands the work. */
  it('sends the tidy with the rule when it is ticked', async () => {
    const onSet = draw();
    const user = userEvent.setup();

    await user.click(within(screen.getByRole('region', { name: 'forge' })).getByRole('checkbox', { name: 'Clean up once landed' }));
    await user.click(forgeSave());

    expect(onSet).toHaveBeenLastCalledWith({ workspace: 'forge', form: 'merge', tidy: true });
  });

  it("shows a workspace's own pattern as its value", () => {
    draw();

    expect(screen.getByRole('textbox', { name: 'The branch pattern for aurora' })).toHaveValue('feature/{quest}-{slug}');
  });

  // A field's percentage cap is what lets a narrow control column shrink it: with a fixed width alone it
  // spilled left over the row's words at 1205px with the side bar open (seen on the window).
  it('lets the pattern field give way to a narrow row', () => {
    draw();

    const field = screen.getByRole('textbox', { name: 'The branch pattern for aurora' });
    expect(field).toHaveClass('min-w-0', 'max-w-full');
  });

  /**
   * WSR4 (D100): a branch rule may name the plugin that pushes it and opens the pull request — one of the
   * plugins here that land work. With none installed nothing is offered, and a merge never names one.
   */
  it('offers the plugins here that land work on a branch rule, and sends the one chosen', async () => {
    const onSet = draw(vi.fn(), ['github-pull-request']);
    const user = userEvent.setup();

    expect(screen.queryByRole('combobox', { name: 'Who pushes the branch for forge' })).toBeNull();
    await user.click(within(forgeForm()).getByRole('radio', { name: 'Branch' }));
    await choose('Who pushes the branch for forge', 'Plugin github-pull-request');
    await user.click(forgeSave());

    expect(onSet).toHaveBeenLastCalledWith({ workspace: 'forge', form: 'branch', pattern: 'feature/{quest}-{slug}', plugin: 'github-pull-request' });

    await choose('Who pushes the branch for forge', 'You push it');
    await user.click(forgeSave());
    expect(onSet).toHaveBeenLastCalledWith({ workspace: 'forge', form: 'branch', pattern: 'feature/{quest}-{slug}' });
  });

  it('offers no plugin where none here lands work', async () => {
    draw();
    const user = userEvent.setup();

    await user.click(within(forgeForm()).getByRole('radio', { name: 'Branch' }));

    expect(screen.queryByRole('combobox', { name: 'Who pushes the branch for forge' })).toBeNull();
  });

  it('does not send what already stands, and clears only where a rule is set', async () => {
    const onSet = draw();
    const user = userEvent.setup();

    // forge stands on Daoris's merge, and pressing Save with it unchanged sends nothing.
    expect(forgeSave()).toBeDisabled();
    // A clear on aurora, which sets its own; none on forge.
    expect(screen.getAllByRole('button', { name: 'Clear' })).toHaveLength(1);

    await user.click(within(screen.getByRole('region', { name: 'aurora' })).getByRole('button', { name: 'Clear' }));
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
    await user.click(within(forgeForm()).getByRole('radio', { name: 'Branch' }));
    await user.click(within(forge).getByRole('checkbox', { name: 'Accept automatically' }));

    expect(within(forge).getByText(/no plugin opens a pull request, so each done's branch waits here for you to push it/)).toBeInTheDocument();
    await user.click(forgeSave());
    expect(onSet).toHaveBeenLastCalledWith({ workspace: 'forge', form: 'branch', pattern: 'feature/{quest}-{slug}', autoAccept: true });
  });

  it('says the chosen plugin pushes and opens pull requests without asking each time', async () => {
    const onSet = draw(vi.fn(), ['github-pull-request']);
    const user = userEvent.setup();
    const forge = screen.getByRole('region', { name: 'forge' });

    await user.click(within(forgeForm()).getByRole('radio', { name: 'Branch' }));
    await choose('Who pushes the branch for forge', 'Plugin github-pull-request');
    await user.click(within(forge).getByRole('checkbox', { name: 'Accept automatically' }));

    expect(within(forge).getByText(/pushes it and opens a pull request without asking you each time/)).toBeInTheDocument();
    expect(within(forge).getByText('github-pull-request', { selector: 'code' })).toBeInTheDocument();
    await user.click(forgeSave());
    expect(onSet).toHaveBeenLastCalledWith(
      { workspace: 'forge', form: 'branch', pattern: 'feature/{quest}-{slug}', plugin: 'github-pull-request', autoAccept: true });
  });

  it('a merge drops the switch', async () => {
    const onSet = draw();
    const user = userEvent.setup();
    const aurora = screen.getByRole('region', { name: 'aurora' });

    await user.click(within(aurora).getByRole('checkbox', { name: 'Accept automatically' }));
    await user.click(within(screen.getByRole('radiogroup', { name: 'How work in aurora lands' })).getByRole('radio', { name: 'Merge' }));
    expect(within(aurora).queryByRole('checkbox', { name: 'Accept automatically' })).toBeNull();
    await user.click(within(aurora).getByRole('button', { name: 'Save' }));
    expect(onSet).toHaveBeenLastCalledWith({ workspace: 'aurora', form: 'merge' });
  });

  // UX6f: a repository's own rule has one home, its Setup.
  it('keeps no row per repository, and names those that set their own, each a door to its Setup', async () => {
    const onOpen = vi.fn();
    draw(vi.fn(), [], LANDINGS, onOpen);

    expect(screen.queryByRole('radiogroup', { name: 'How work in engine lands' })).toBeNull();
    expect(screen.queryByRole('radiogroup', { name: 'How work in game lands' })).toBeNull();
    const aurora = screen.getByRole('region', { name: 'aurora' });
    expect(within(aurora).getAllByRole('button', { name: /'s setup$/ }).map((door) => door.textContent)).toEqual(['engine']);
    await userEvent.click(within(aurora).getByRole('button', { name: "Open engine's setup" }));
    expect(onOpen).toHaveBeenLastCalledWith('engine');
    expect(within(screen.getByRole('region', { name: 'forge' })).getByText(/No repository here sets its own/)).toBeInTheDocument();
  });
});
