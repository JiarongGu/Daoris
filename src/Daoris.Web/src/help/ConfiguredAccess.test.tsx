import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
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
  { id: 'checkouts', chosen: ['read'] },
  { id: 'settings', chosen: [] },
];

const ALL: HelpGrantId[] = ['housekeeping', 'browser', 'checkouts', 'settings', 'shell'];

function show({ agent = CLAUDE, grants = CHOSEN, opened, onChoose = vi.fn() }: {
  agent?: HelpGrantAgent; grants?: HelpGrantChoice[]; opened?: HelpGrantId[]; onChoose?: () => void;
} = {}) {
  return render(
    <Tooltip.Provider>
      <ConfiguredAccess agent={agent} grants={grants} opened={opened} onChoose={onChoose} />
    </Tooltip.Provider>,
  );
}

/** A grant's row, found by its name. */
const row = (id: HelpGrantId) => screen.getByRole('listitem', { name: i18n.t(`help.grants.name.${id}`) });
const box = (id: HelpGrantId, operation: string) =>
  within(row(id)).getByRole('checkbox', { name: i18n.t(`help.grants.op.${id}.${operation}`) });

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

  it('asks approval of the browser’s reads together and of each write on its own, saying signed-in pages may be affected', async () => {
    await i18n.changeLanguage(language);
    show({ grants: [{ id: 'browser', chosen: [] }], opened: ['browser'] });

    expect(within(row('browser')).getByText(i18n.t('help.grants.state.needs-approval'))).toBeInTheDocument();
    expect(within(row('browser')).getByText(i18n.t('help.grants.signedIn'))).toBeInTheDocument();
    expect(within(row('browser')).getByText(i18n.t('help.grants.never.browser'))).toBeInTheDocument();
    expect(within(row('browser')).getAllByRole('checkbox')).toHaveLength(10);
    for (const write of ['click', 'type', 'press', 'fill', 'select', 'check', 'drag', 'upload', 'dialog']) {
      expect(box('browser', write)).not.toBeDisabled();
    }
  });

  it('says a browser changed since it was approved apart from one never approved', async () => {
    await i18n.changeLanguage(language);
    show({ grants: [{ id: 'browser', chosen: ['reads', 'click'], changed: true }] });

    expect(within(row('browser')).getByText(i18n.t('help.grants.state.changed'))).toBeInTheDocument();
    expect(within(row('browser')).getByText(i18n.t('help.grants.why.changed'))).toBeInTheDocument();
    expect(within(row('browser')).queryByText(i18n.t('help.grants.state.needs-approval'))).toBeNull();
  });

  it('reads the checkouts as one group, and lists the five ordinary settings with what stays out', async () => {
    await i18n.changeLanguage(language);
    show({ opened: ['checkouts', 'settings'] });

    expect(within(row('checkouts')).getAllByRole('checkbox')).toEqual([box('checkouts', 'read')]);
    // Its git commands are code, never shown as backticks.
    expect(row('checkouts').querySelectorAll('code').length).toBeGreaterThan(0);
    expect(row('checkouts').textContent).not.toContain('`');
    expect(within(row('settings')).getAllByRole('checkbox')).toEqual(
      ['notify', 'language', 'timeout', 'cap', 'cooloff'].map((setting) => box('settings', setting)),
    );
    expect(within(row('settings')).getByText(i18n.t('help.grants.never.settings'))).toBeInTheDocument();
  });

  it('offers the confined shell no door and no box', async () => {
    await i18n.changeLanguage(language);
    show({ opened: ALL });

    expect(within(row('shell')).queryAllByRole('button')).toEqual([]);
    expect(within(row('shell')).queryAllByRole('checkbox')).toEqual([]);
  });

  it('says on Codex that the browser’s writes and the checkouts’ scoped reads cannot be held, with why, their boxes disabled', async () => {
    await i18n.changeLanguage(language);
    show({ agent: CODEX, opened: ALL });

    for (const id of ['browser', 'checkouts'] as const) {
      expect(within(row(id)).getByText(i18n.t('help.grants.state.unavailable'))).toBeInTheDocument();
      expect(within(row(id)).queryByText(i18n.t('help.grants.state.on'))).toBeNull();
      for (const each of within(row(id)).getAllByRole('checkbox')) {
        expect(each).toBeDisabled();
        // Its sentence reads as disabled too, not only its box.
        expect(each.closest('label')!.className).toContain('cursor-not-allowed');
      }
    }
    expect(within(row('browser')).getByText(i18n.t('help.grants.why.browser', { agent: 'Codex' }))).toBeInTheDocument();
    expect(within(row('checkouts')).getByText(i18n.t('help.grants.why.checkouts', { agent: 'Codex' }))).toBeInTheDocument();

    // Daoris holds its typed tools itself, so they hold on Codex too.
    expect(within(row('housekeeping')).getByText(i18n.t('help.grants.state.on'))).toBeInTheDocument();
    expect(box('housekeeping', 'drop-recovery')).not.toBeDisabled();
  });

  it('holds both on Claude, saying no grant unavailable', async () => {
    await i18n.changeLanguage(language);
    show({ opened: ALL });

    expect(screen.queryByText(i18n.t('help.grants.state.unavailable'))).toBeNull();
    expect(within(row('browser')).getByText(i18n.t('help.grants.state.on'))).toBeInTheDocument();
  });

  it('marks a choice the conversation in view did not start with as taking its next start, and only that one', async () => {
    await i18n.changeLanguage(language);
    show({ grants: [{ id: 'housekeeping', chosen: ['inspect'], pending: true }, { id: 'settings', chosen: ['notify'] }] });

    expect(within(row('housekeeping')).getByText(i18n.t('help.grants.pending'))).toBeInTheDocument();
    expect(within(row('settings')).queryByText(i18n.t('help.grants.pending'))).toBeNull();
  });
});
