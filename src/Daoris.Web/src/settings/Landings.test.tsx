import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { AcceptingNote, type Accepting, LandingField, type LandingRule } from './Landings';

// WSR1 (D87): how work lands, set as `daoris driver landing` sets it from a terminal (D50). Daoris never pushes. The
// rule's control is one, on a workspace's Setup and a repository's (UX6f, UX6g; D150 §4.2, §4.3), so its behaviour is
// held here on two rows as either page draws them: aurora, which sets its own branch rule, and forge, which stands on
// Daoris's merge. Each row is a region, as each page's section is.

const MERGE: LandingRule = { form: 'merge' };

/** One row: the field, and beneath it what accepting automatically gives, as both Setups draw it. */
function Row({ name, set, landers, onSet }: { name: string; set?: LandingRule; landers: string[]; onSet: (change: Record<string, unknown>) => void }) {
  const [accepting, setAccepting] = useState<Accepting>(null);
  return (
    <section aria-label={name}>
      <LandingField name={name} set={set} inherited={MERGE} landers={landers} onSave={(rule) => onSet({ workspace: name, ...rule })} onAccepting={setAccepting} />
      <AcceptingNote accepting={accepting} />
    </section>
  );
}

const draw = (onSet = vi.fn(), landers: string[] = []) => {
  render(
    <Tooltip.Provider>
      <Row name="aurora" set={{ form: 'branch', pattern: 'feature/{quest}-{slug}' }} landers={landers} onSet={onSet} />
      <Row name="forge" landers={landers} onSet={onSet} />
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

describe("the landing rule's control", () => {
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
});
