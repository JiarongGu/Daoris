import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { ConfiguredAccess } from './ConfiguredAccess';
import type { HelpGrantAgent, HelpGrantChoice, HelpGrantId } from './wired';

// HELPMODE1c1 (D183 point 1; the modes design §4.1, §5): restricted mode's configured access, drawn from props for the owner's
// review before any grant is built. Each grant says its reach, its default and its state, its operations each a box of its own
// or one group, and its door; a grant the agent cannot be held to says so with why, its boxes disabled; a choice the
// conversation in view did not start with is marked as taking its next start.

const CLAUDE: HelpGrantAgent = { name: 'Claude Code', rules: true };
const CODEX: HelpGrantAgent = { name: 'Codex', rules: false };

const CHOSEN: HelpGrantChoice[] = [
  { id: 'housekeeping', chosen: ['inspect', 'clean-up'] },
  { id: 'browser', chosen: ['reads', 'click', 'type'] },
  { id: 'checkouts', chosen: ['engine'] },
  { id: 'settings', chosen: [] },
];

/** HELPMODE1c4: the checkouts the driver serves, each by its repository's name; `game` turned off. */
const SERVED = { checkouts: ['engine', 'game'] };

const ALL: HelpGrantId[] = ['housekeeping', 'browser', 'checkouts', 'settings', 'shell'];

function show({ agent = CLAUDE, grants = CHOSEN, opened, built = SERVED, onChoose = vi.fn(), onManage = vi.fn() }: {
  agent?: HelpGrantAgent; grants?: HelpGrantChoice[]; opened?: HelpGrantId[]; built?: Partial<Record<HelpGrantId, readonly string[]>>;
  onChoose?: () => void; onManage?: (grant: HelpGrantId, plugin: string | null) => void;
} = {}) {
  return render(
    <Tooltip.Provider>
      <ConfiguredAccess agent={agent} grants={grants} opened={opened} built={built} onChoose={onChoose} onManage={onManage} />
    </Tooltip.Provider>,
  );
}

/** A grant's row, found by its name. */
const row = (id: HelpGrantId) => screen.getByRole('listitem', { name: i18n.t(`help.grants.name.${id}`) });
const box = (id: HelpGrantId, operation: string) =>
  within(row(id)).getByRole('checkbox', { name: id === 'checkouts' ? operation : i18n.t(`help.grants.op.${id}.${operation}`) });

describe.each(['en', 'zh'])('configured access in %s', (language) => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('draws the five grants in the table’s order under its title, each with its reach, its default and its state', async () => {
    await i18n.changeLanguage(language);
    show();

    const section = screen.getByRole('region', { name: i18n.t('help.grants.title') });
    const names = within(section).getAllByRole('listitem').map((item) => item.getAttribute('aria-label'));
    expect(names).toEqual(ALL.map((id) => i18n.t(`help.grants.name.${id}`)));
    for (const id of ALL) expect(within(row(id)).getByText(i18n.t(`help.grants.reach.${id}`))).toBeInTheDocument();

    expect(within(row('housekeeping')).getByText(i18n.t('help.grants.state.on'))).toBeInTheDocument();
    expect(within(row('housekeeping')).getByText(i18n.t('help.grants.default.off'))).toBeInTheDocument();
    expect(within(row('browser')).getByText(i18n.t('help.grants.default.approval'))).toBeInTheDocument();
    expect(within(row('checkouts')).getByText(i18n.t('help.grants.default.inherited'))).toBeInTheDocument();
    expect(within(row('settings')).getByText(i18n.t('help.grants.state.off'))).toBeInTheDocument();
    expect(within(row('shell')).getByText(i18n.t('help.grants.state.later'))).toBeInTheDocument();
    // A sentence's backticks are code, never shown as backticks.
    expect(within(row('shell')).getByText('cd', { selector: 'code' })).toBeInTheDocument();
    expect(row('shell').textContent).not.toContain('`');
  });

  it('opens a grant’s operations from its door, each chosen on its own, and hands each choice up', async () => {
    await i18n.changeLanguage(language);
    const onChoose = vi.fn();
    show({ onChoose });

    // Closed, the row shows no box; its door says what it opens and whether it is open.
    expect(within(row('housekeeping')).queryAllByRole('checkbox')).toEqual([]);
    const door = within(row('housekeeping')).getByRole('button', { name: (name) => name.startsWith(i18n.t('help.grants.door.operations')) });
    expect(door).toHaveAttribute('aria-expanded', 'false');
    await userEvent.click(door);
    expect(door).toHaveAttribute('aria-expanded', 'true');

    expect(box('housekeeping', 'inspect')).toBeChecked();
    expect(box('housekeeping', 'clean-up')).toBeChecked();
    expect(box('housekeeping', 'discard-branch')).not.toBeChecked();
    expect(box('housekeeping', 'drop-recovery')).not.toBeChecked();
    expect(within(row('housekeeping')).getByText(i18n.t('help.grants.never.housekeeping'))).toBeInTheDocument();

    await userEvent.click(box('housekeeping', 'discard-branch'));
    expect(onChoose).toHaveBeenCalledWith('housekeeping', 'discard-branch', true);
    await userEvent.click(box('housekeeping', 'inspect'));
    expect(onChoose).toHaveBeenCalledWith('housekeeping', 'inspect', false);
  });

  // HELPMODE1c3 (the modes design §4.1's decided note): the browser's grant is its plugin's approval, made on that plugin's tab, the
  // one place its record is changed; this row shows that record's state read only and leads there, with no second set of boxes.
  it('draws the browser read only from its plugin’s approval, its door leading to the plugin’s tab, with no boxes', async () => {
    await i18n.changeLanguage(language);
    const onManage = vi.fn();
    show({ grants: [{ id: 'browser', chosen: [], plugin: 'in-app-browser' }], opened: ['browser'], onManage });

    expect(within(row('browser')).getByText(i18n.t('help.grants.state.needs-approval'))).toBeInTheDocument();
    expect(within(row('browser')).getByText(i18n.t('help.grants.signedIn'))).toBeInTheDocument();
    expect(within(row('browser')).getByText(i18n.t('help.grants.never.browser'))).toBeInTheDocument();
    expect(within(row('browser')).getByText(i18n.t('help.grants.onPlugin'))).toBeInTheDocument();
    expect(within(row('browser')).queryAllByRole('checkbox')).toEqual([]);

    const door = within(row('browser')).getByRole('button', { name: (name) => name.startsWith(i18n.t('help.grants.door.manage')) });
    expect(door).not.toHaveAttribute('aria-expanded');
    await userEvent.click(door);
    expect(onManage).toHaveBeenCalledWith('browser', 'in-app-browser');
    expect(within(row('browser')).queryAllByRole('checkbox')).toEqual([]);
  });

  it('says where no plugin here offers Ask Daoris the browser, its door leading to Plugins', async () => {
    await i18n.changeLanguage(language);
    const onManage = vi.fn();
    show({ grants: [{ id: 'browser', chosen: [] }], onManage });

    expect(within(row('browser')).getByText(i18n.t('help.grants.noPlugin'))).toBeInTheDocument();
    await userEvent.click(within(row('browser')).getByRole('button', { name: (name) => name.startsWith(i18n.t('help.grants.door.manage')) }));
    expect(onManage).toHaveBeenCalledWith('browser', null);
  });

  it('says a browser changed since it was approved apart from one never approved', async () => {
    await i18n.changeLanguage(language);
    show({ grants: [{ id: 'browser', chosen: ['reads', 'click'], changed: true }] });

    expect(within(row('browser')).getByText(i18n.t('help.grants.state.changed'))).toBeInTheDocument();
    expect(within(row('browser')).getByText(i18n.t('help.grants.why.changed'))).toBeInTheDocument();
    expect(within(row('browser')).queryByText(i18n.t('help.grants.state.needs-approval'))).toBeNull();
  });

  // HELPMODE1c4: the checkouts the driver serves, one box each by its repository's name as written, those not turned off checked; a
  // press hands the repository up. With none served, it says why and draws no box.
  it('draws a box for each checkout the driver serves, by its repository’s name, and lists the five ordinary settings', async () => {
    await i18n.changeLanguage(language);
    const onChoose = vi.fn();
    show({ opened: ['checkouts', 'settings'], onChoose });

    expect(within(row('checkouts')).getAllByRole('checkbox')).toEqual([box('checkouts', 'engine'), box('checkouts', 'game')]);
    expect(box('checkouts', 'engine')).toBeChecked();
    expect(box('checkouts', 'game')).not.toBeChecked();
    expect(within(row('checkouts')).getByText(i18n.t('help.grants.state.on'))).toBeInTheDocument();
    await userEvent.click(box('checkouts', 'game'));
    expect(onChoose).toHaveBeenCalledWith('checkouts', 'game', true);
    // Its git commands are code, never shown as backticks.
    expect(row('checkouts').querySelectorAll('code').length).toBeGreaterThan(0);
    expect(row('checkouts').textContent).not.toContain('`');
    expect(within(row('settings')).getAllByRole('checkbox')).toEqual(
      ['notify', 'language', 'cap', 'cooloff'].map((setting) => box('settings', setting)),
    );
    expect(within(row('settings')).getByText(i18n.t('help.grants.never.settings'))).toBeInTheDocument();
  });

  it('says no checkout here may be read where the driver serves none, with no box', async () => {
    await i18n.changeLanguage(language);
    show({ grants: [{ id: 'checkouts', chosen: [] }], built: { checkouts: [] }, opened: ['checkouts'] });

    expect(within(row('checkouts')).queryAllByRole('checkbox')).toEqual([]);
    expect(within(row('checkouts')).getByText(i18n.t('help.grants.none.checkouts'))).toBeInTheDocument();
    expect(within(row('checkouts')).getByText(i18n.t('help.grants.state.off'))).toBeInTheDocument();
  });

  it('offers the confined shell no door and no box', async () => {
    await i18n.changeLanguage(language);
    show({ opened: ALL });

    expect(within(row('shell')).queryAllByRole('button')).toEqual([]);
    expect(within(row('shell')).queryAllByRole('checkbox')).toEqual([]);
  });

  it('says on Codex that the browser is unavailable until mediated, with why, and holds the rest Daoris serves', async () => {
    await i18n.changeLanguage(language);
    show({ agent: CODEX, opened: ALL });

    expect(within(row('browser')).getByText(i18n.t('help.grants.state.unavailable'))).toBeInTheDocument();
    expect(within(row('browser')).queryByText(i18n.t('help.grants.state.on'))).toBeNull();
    expect(within(row('browser')).getByText(i18n.t('help.grants.why.browser', { agent: 'Codex' }))).toBeInTheDocument();

    // Daoris holds its typed tools itself, so they hold on Codex too: housekeeping, the chosen settings, and since HELPMODE1c4 the
    // checkouts' read, which no longer waits on a rule Codex does not take.
    expect(within(row('checkouts')).getByText(i18n.t('help.grants.state.on'))).toBeInTheDocument();
    expect(within(row('checkouts')).queryByText(i18n.t('help.grants.state.unavailable'))).toBeNull();
    for (const each of within(row('checkouts')).getAllByRole('checkbox')) expect(each).not.toBeDisabled();
    expect(within(row('housekeeping')).getByText(i18n.t('help.grants.state.on'))).toBeInTheDocument();
    expect(box('housekeeping', 'drop-recovery')).not.toBeDisabled();
    expect(box('settings', 'notify')).not.toBeDisabled();
  });

  it('holds both on Claude, saying no grant unavailable', async () => {
    await i18n.changeLanguage(language);
    show({ opened: ALL });

    expect(screen.queryByText(i18n.t('help.grants.state.unavailable'))).toBeNull();
    expect(within(row('browser')).getByText(i18n.t('help.grants.state.on'))).toBeInTheDocument();
  });

  it('marks a choice a running conversation did not start with as taking its next turn, and only that one', async () => {
    await i18n.changeLanguage(language);
    show({ grants: [{ id: 'housekeeping', chosen: ['inspect'], pending: true }, { id: 'settings', chosen: ['notify'] }] });

    expect(within(row('housekeeping')).getByText(i18n.t('help.grants.pending'))).toBeInTheDocument();
    expect(within(row('settings')).queryByText(i18n.t('help.grants.pending'))).toBeNull();
  });
});
