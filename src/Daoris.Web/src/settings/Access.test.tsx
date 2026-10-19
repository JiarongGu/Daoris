import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../locales/i18n';
import { code } from '../test/code';
import type { Answered } from '../work/InlineConfirm';
import {
  type AccessOwner, AccessRows, accessSummary, accessToast, isFull, type ResolvedPause, resolveAccess, TODAY,
} from './Access';

// AUTONOMY1d (D184 points 1-3, 5): the access setup a repository's sessions start under, shown and set where it is set up, the same
// rows a level up for a workspace and this machine. Full access is one switch, confirmed once; each pause shows what it means, its
// value and where that was set, and changes at this level or goes back to the level above. The words are the person's: a pause is
// never named by its file key.

const t = i18n.t.bind(i18n);

/** The engine's setup: its permissions from its workspace, routing from this machine, landing Daoris's, its go-aheads its own. */
const MIXED: ResolvedPause[] = [
  { pause: 'permissions', value: 'asking', source: 'workspace' },
  { pause: 'routing', value: 'intake', source: 'machine' },
  { pause: 'landing', value: 'person', source: 'default' },
  { pause: 'goAheads', value: 'never', source: 'repository' },
  { pause: 'questions', value: 'claude-code', source: 'default' },
];

const OPEN = (source: string): ResolvedPause[] => [
  { pause: 'permissions', value: 'unrestricted', source },
  { pause: 'routing', value: 'intake', source },
  { pause: 'landing', value: 'rule', source },
  { pause: 'goAheads', value: 'never', source },
  { pause: 'questions', value: 'claude-code', source },
];

const DEFAULTS: ResolvedPause[] = resolveAccess([]);

const draw = (pauses: ResolvedPause[], { owner = 'repository' as AccessOwner, aboveFull = false, onChange = vi.fn() } = {}) => {
  render(
    <Tooltip.Provider>
      <AccessRows
        owner={owner}
        name={owner === 'workspace' ? 'aurora' : owner === 'repository' ? 'engine' : undefined}
        workspace="aurora"
        pauses={pauses}
        aboveFull={aboveFull}
        setHere={owner === 'repository' ? 'Set for this repository' : owner === 'workspace' ? 'Set for this workspace' : 'Set for this machine'}
        onChange={onChange}
      />
    </Tooltip.Provider>,
  );
  return onChange;
};

/** One row, found by its label, which leads it: the switch's own name, kept for a reader alone, comes after. */
const row = (label: string) => screen.getAllByText(label, { selector: 'span' })[0]!.closest('.\\@container') as HTMLElement;

afterEach(async () => { await i18n.changeLanguage('en'); });

describe('how an access setup resolves on the page', () => {
  it('takes each pause from the nearest level that sets it, full access opening every one, else today', () => {
    expect(resolveAccess([
      { setting: { full: false, pauses: [{ pause: 'landing', value: 'rule' }] }, source: 'workspace' },
      { setting: { full: true, pauses: [] }, source: 'machine' },
    ])).toEqual([
      { pause: 'permissions', value: 'unrestricted', source: 'machine' },
      { pause: 'routing', value: 'intake', source: 'machine' },
      { pause: 'landing', value: 'rule', source: 'workspace' },
      { pause: 'goAheads', value: 'never', source: 'machine' },
      { pause: 'questions', value: 'claude-code', source: 'machine' },
    ]);
    expect(DEFAULTS).toEqual([
      { pause: 'permissions', value: 'auto', source: 'default' },
      { pause: 'routing', value: 'person', source: 'default' },
      { pause: 'landing', value: 'person', source: 'default' },
      { pause: 'goAheads', value: 'named', source: 'default' },
      { pause: 'questions', value: 'claude-code', source: 'default' },
    ]);
  });

  it('is full only where every pause it knows is open, and a pause it does not know is never read as open', () => {
    expect(isFull(OPEN('workspace'))).toBe(true);
    expect(isFull(MIXED)).toBe(false);
    expect(isFull([...OPEN('workspace'), { pause: 'later', value: 'whatever', source: 'workspace' }])).toBe(false);
    expect(isFull([])).toBe(false);
  });

  it("says a folded line: full access, or each pause's value, marked where it came from above", () => {
    expect(accessSummary(t, OPEN('repository'), 'repository')).toBe('full access');
    expect(accessSummary(t, OPEN('workspace'), 'repository')).toBe("full access (the workspace's default)");
    expect(accessSummary(t, DEFAULTS, 'repository')).toBe(
      "permissions in auto mode, you accept each route, lands on your Accept…, go-aheads before named acts (Daoris's default)");
    expect(accessSummary(t, MIXED, 'repository')).toBe(
      "permissions in asking mode (the workspace's default), the intake routes (this machine's default), lands on your Accept… "
      + "(Daoris's default), no go-aheads");
    // At the workspace, its own values are unmarked.
    expect(accessSummary(t, OPEN('workspace'), 'workspace')).toBe('full access');
  });

  it('says what each change did, once the driver took it', () => {
    expect(accessToast(t, 'engine', { access: 'full' }, false)).toBe('Sessions in engine now start with full access.');
    expect(accessToast(t, 'engine', { access: null }, false)).toBe('engine takes its sessions\' access from what stands above it again.');
    expect(accessToast(t, 'engine', { access: { ...TODAY } }, false)).toBe('Sessions in engine now stop at each pause as today.');
    expect(accessToast(t, 'engine', { access: { permissions: 'unrestricted' } }, false)).toBe('Permission prompts for engine: None.');
    expect(accessToast(t, 'engine', { access: { landing: null } }, false)).toBe('Landing for engine comes from what stands above it again.');
    expect(accessToast(t, 'this machine', { access: 'full' }, true)).toBe('Sessions on this machine now start with full access.');
  });
});

describe("a level's access rows", () => {
  it('shows each pause with what it means, its value and where it came from', () => {
    draw(MIXED);

    // Asking refuses what the rules do not allow, since no prompt reaches the person yet (AUTONOMY1b; AUTONOMY1b3 is the row).
    expect(row('Permission prompts')).toHaveTextContent(
      'The agent runs in its asking mode: it uses what its rules allow, and any other tool call is refused, since no prompt reaches '
      + 'you yet. From the workspace aurora.');
    expect(row('Intake routing')).toHaveTextContent(
      'An ask its declarations do not settle goes where its intake routed it; you may route it again. From this machine.');
    expect(row('Landing')).toHaveTextContent("Finished work waits for your Accept… before it lands, as today. Daoris's default: nothing is set.");
    expect(row('Go-aheads')).toHaveTextContent('No go-ahead is asked, save one the quest or your words demand. Set for this repository.');
    // Each row's hint is its terminal twin, and each pause's own meaning is on its glyph.
    expect(within(row('Permission prompts')).getByText(code('daoris-driver access engine permissions=asking|auto|unrestricted')))
      .toBeInTheDocument();
    expect(within(row('Full access')).getByText(code('daoris-driver access engine full|--clear'))).toBeInTheDocument();
    expect(within(row('Landing')).getByRole('note')).toHaveAccessibleName(
      "Whether a quest's finished work waits for your Accept… or lands by the repository's landing rule, pushed as the rule says.");
  });

  it('says a landing rule still leaves a merge waiting for Accept… (ACCESSWORDS1)', () => {
    draw(resolveAccess([{ setting: { full: false, pauses: [{ pause: 'landing', value: 'rule' }] }, source: 'workspace' }]), { owner: 'machine' });
    expect(screen.getByText(/Finished work lands by the landing rule, with no press\. A merge rule still waits for your Accept…/)).toBeInTheDocument();
  });

  it('says questions work as in Claude Code in every setup, with nothing to change', () => {
    draw(MIXED);

    const questions = row('Questions');
    expect(questions).toHaveTextContent('As in Claude Code: a session asks only what it cannot decide');
    expect(within(questions).queryByRole('button')).toBeNull();
    expect(within(questions).queryByRole('radio')).toBeNull();
  });

  it('changes a pause from above at this level, and clears one of its own back to the level above', async () => {
    const onChange = draw(MIXED);

    const permissions = row('Permission prompts');
    expect(within(permissions).queryByRole('button', { name: 'Clear' })).toBeNull();
    await userEvent.click(within(permissions).getByRole('button', { name: 'Set for this repository' }));
    expect(within(permissions).getByRole('radiogroup', { name: 'Permission prompts for engine' })).toBeInTheDocument();
    expect(within(permissions).getByRole('radio', { name: 'Asking mode' })).toHaveAttribute('aria-checked', 'true');
    await userEvent.click(within(permissions).getByRole('radio', { name: 'None' }));
    expect(onChange).toHaveBeenLastCalledWith({ access: { permissions: 'unrestricted' } });

    // Its own carries its value's choice and Clear, which takes the pause back to the level above.
    const goAheads = row('Go-aheads');
    expect(within(goAheads).getByRole('radio', { name: 'Never' })).toHaveAttribute('aria-checked', 'true');
    await userEvent.click(within(goAheads).getByRole('button', { name: 'Clear' }));
    expect(onChange).toHaveBeenLastCalledWith({ access: { goAheads: null } });
  });

  it('asks once before full access, saying what it turns off and what stays, and sends it only on the second press', async () => {
    const onChange = draw(MIXED);

    const full = row('Full access');
    expect(full).toHaveTextContent('Off: sessions stop at each pause as it stands below.');
    expect(full).toHaveTextContent(
      'Full access turns off permission prompts, an ask waiting for you to accept its route, Accept… before landing, and go-aheads.');
    expect(full).toHaveTextContent('It never turns off your own deny rules, nor a go-ahead the ask itself demands.');
    const box = within(full).getByRole('checkbox', { name: 'Full access' });
    expect(box).not.toBeChecked();

    await userEvent.click(box);
    expect(onChange).not.toHaveBeenCalled();
    const ask = within(full).getByRole('group', { name: 'Turn on full access for engine' });
    expect(ask).toHaveTextContent('Your own deny rules still hold, and a go-ahead the ask itself demands is still asked.');
    await userEvent.click(within(ask).getByRole('button', { name: 'Never mind' }));
    expect(within(full).queryByRole('group')).toBeNull();
    expect(onChange).not.toHaveBeenCalled();

    await userEvent.click(box);
    await userEvent.click(within(full).getByRole('button', { name: 'Enable full access' }));
    expect(onChange).toHaveBeenLastCalledWith({ access: 'full' }, expect.anything());
    // It waits for the driver's answer, and a refusal is said inside it, whole.
    act(() => (onChange.mock.calls[0]![1] as Answered).refused('an access setup is set for a repository, a workspace or this machine.'));
    expect(within(full).getByRole('alert')).toHaveTextContent('an access setup is set for a repository, a workspace or this machine.');
    act(() => (onChange.mock.calls[0]![1] as Answered).done());
  });

  it('wears the warning colour while on, and turning it off clears the level where the level above is not full', async () => {
    const onChange = draw(OPEN('repository'));

    const full = row('Full access');
    expect(within(full).getByRole('checkbox', { name: 'Full access' })).toBeChecked();
    const on = within(full).getByText(/^On: sessions start with every pause below off\./);
    expect(on).toHaveTextContent('On: sessions start with every pause below off. Set for this repository.');
    expect(on).toHaveClass('border-warn');
    await userEvent.click(within(full).getByRole('checkbox', { name: 'Full access' }));
    expect(onChange).toHaveBeenLastCalledWith({ access: null });
  });

  it("turns full access from above off by setting today's pauses here, since clearing would leave it full", async () => {
    const onChange = draw(OPEN('workspace'), { aboveFull: true });

    expect(row('Full access')).toHaveTextContent('On: sessions start with every pause below off. From the workspace aurora.');
    await userEvent.click(within(row('Full access')).getByRole('checkbox', { name: 'Full access' }));
    expect(onChange).toHaveBeenLastCalledWith({ access: { permissions: 'auto', routing: 'person', landing: 'person', goAheads: 'named' } });
  });

  it("says a workspace's and this machine's own values as theirs, each twin at its level", () => {
    draw(resolveAccess([{ setting: { full: false, pauses: [{ pause: 'routing', value: 'intake' }] }, source: 'workspace' }]), { owner: 'workspace' });
    expect(row('Intake routing')).toHaveTextContent('Set for this workspace.');
    expect(within(row('Intake routing')).getByText(code('daoris-driver access --workspace aurora routing=person|intake'))).toBeInTheDocument();
    expect(within(row('Intake routing')).getByRole('button', { name: 'Clear' })).toBeInTheDocument();
  });

  it("says this machine's own values as its own, and offers to set one for it", () => {
    draw(resolveAccess([{ setting: { full: false, pauses: [{ pause: 'landing', value: 'rule' }] }, source: 'machine' }]), { owner: 'machine' });
    expect(row('Landing')).toHaveTextContent('Set for this machine.');
    expect(within(row('Landing')).getByText(code('daoris-driver access landing=person|rule'))).toBeInTheDocument();
    expect(within(row('Permission prompts')).getByRole('button', { name: 'Set for this machine' })).toBeInTheDocument();
  });

  it('reads in Chinese', async () => {
    await i18n.changeLanguage('zh');
    draw(MIXED);

    expect(within(row('完全访问')).getByRole('checkbox', { name: '完全访问' })).not.toBeChecked();
    expect(screen.getByText('权限提示', { selector: 'span' })).toBeInTheDocument();
    expect(screen.getByText('放行', { selector: 'span' })).toBeInTheDocument();
    expect(row('落地')).toHaveTextContent('完成的工作等你采纳…之后才落地，和现在一样。Daoris 的默认：哪里都没有设定。');
    expect(accessSummary(t, MIXED, 'repository')).toBe(
      '权限用询问模式（工作区的默认），受理直接分派（本机的默认），等你采纳…才落地（Daoris 的默认），不请你放行');
  });
});
